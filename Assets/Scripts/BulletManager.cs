using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using System.Collections.Generic;

/// <summary>弾が敵に与えたダメージ情報を保持する構造体。</summary>
public struct BulletDamageInfo
{
    public float3 position;
    public float damage;
    public int index;

    public BulletDamageInfo(float3 pos, float dmg, int idx)
    {
        position = pos;
        damage = dmg;
        index = idx;
    }
}

/// <summary>
/// プレイヤー弾と敵弾の 2 つの BulletPool を保持し、発射・移動スケジュール・当たり判定・描画をまとめて行う。
/// </summary>
public class BulletManager : InitializeMonobehaviour
{
    [Header("Settings")]
    [SerializeField] private int maxBullets = 1000;

    [Header("Params")]
    [SerializeField] private Transform playerTransform;
    [Tooltip("敵弾とプレイヤーの当たり判定に使うプレイヤー側の半径")]
    [SerializeField] private float playerCollisionRadius = 1f;

    [Header("Player Shot")]
    [SerializeField] private float bulletDamage = 1.0f;

    [Header("Player Bullet Settings")]
    [SerializeField] private Mesh playerBulletMesh;
    [SerializeField] private Material playerBulletMaterial;

    [Header("Enemy Bullet Settings")]
    [SerializeField] private Mesh enemyBulletMesh;
    [SerializeField] private Material enemyBulletMaterial;

    [Header("References")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private DamageTextManager damageTextManager;
    [SerializeField] private RenderManager renderManager;

    private RenderParams _rpPlayerBullet;
    private RenderParams _rpEnemyBullet;

    private Dictionary<int, BulletGroup> _bulletGroups;
    /// <summary>ProcessMovement などで使う、グループの登録順（プレイヤー→敵）。</summary>
    private List<int> _bulletGroupIdsInOrder;
    private int _bulletGroupIdCounter = 0;
    private int _enemyBulletGroupId;

    /// <summary>敵グループループ内で再利用する当たり判定 Job。グループごとに参照だけ差し替える。</summary>
    private BulletCollideJob _cachedCollideJob;
    /// <summary>CollectHitsAgainstCircle の結果（ヒットした弾のダメージ）を入れるバッファ。敵弾vsプレイヤー・プレイヤー弾vsボスで共用。</summary>
    private NativeList<float> _collectedHitDamages;

    public float BulletDamage => bulletDamage;

    protected override void InitializeInternal()
    {
        _collectedHitDamages = new NativeList<float>(maxBullets, Allocator.Persistent);

        _bulletGroups = new Dictionary<int, BulletGroup>();
        _bulletGroupIdsInOrder = new List<int>();

        if (playerBulletMaterial != null)
        {
            _rpPlayerBullet = new RenderParams(playerBulletMaterial)
            {
                shadowCastingMode = ShadowCastingMode.On,
                receiveShadows = true
            };
        }
        if (enemyBulletMaterial != null)
        {
            _rpEnemyBullet = new RenderParams(enemyBulletMaterial)
            {
                shadowCastingMode = ShadowCastingMode.On,
                receiveShadows = true
            };
        }
    }

    protected override void FinalizeInternal()
    {
        foreach (var group in _bulletGroups)
        {
            group.Value.Dispose();
        }
        _bulletGroups.Clear();
        _bulletGroups = null;

        _collectedHitDamages.Dispose();
    }

    public int AddBulletGroup(float scale)
    {
        var groupId = _bulletGroupIdCounter++;
        var group = new BulletGroup();
        group.Initialize(maxBullets, scale: scale);
        _bulletGroups.Add(groupId, group);
        _bulletGroupIdsInOrder.Add(groupId);
        return groupId;
    }

    public void RemoveBulletGroup(int groupId)
    {
        if (_bulletGroups.TryGetValue(groupId, out var group))
        {
            group.Dispose();
            _bulletGroups.Remove(groupId);
            _bulletGroupIdsInOrder.Remove(groupId);
        }
    }

    public void InitializeEnemyBullets(float scale)
    {
        _enemyBulletGroupId = AddBulletGroup(scale);
    }

    public void SpawnBullet(int bulletGroupId, Vector3 position, Vector3 direction, float speed, float lifeTime = 2f)
    {
        if (IsInitialized == false)
        {
            return;
        }

        if (_bulletGroups.TryGetValue(bulletGroupId, out var group) == false)
        {
            return;
        }

        group.Spawn(position, direction, speed, lifeTime, damage: bulletDamage);
    }

    /// <summary>
    /// 敵・ボス弾を 1 発生成する。敵グループやボスの Update から呼ぶ。
    /// </summary>
    public void SpawnEnemyBullet(Vector3 position, Vector3 direction, float speed, float damage, float lifeTime)
    {
        if (IsInitialized == false || !_bulletGroups.TryGetValue(_enemyBulletGroupId, out var group))
        {
            return;
        }
        _bulletGroups[_enemyBulletGroupId].Spawn(position, direction, speed, lifeTime, damage);
    }

    /// <summary>
    /// 弾の移動 Job をスケジュールし完了まで待機する。プレイヤー弾の移動 → 敵弾の移動の順に依存させる。
    /// </summary>
    public void ProcessMovement()
    {
        JobHandle dep = default;
        foreach (var id in _bulletGroupIdsInOrder)
        {
            dep = _bulletGroups[id].ScheduleMoveJob(dep);
        }
        dep.Complete();
    }

    /// <summary>
    /// 指定した弾グループとターゲットグループの当たり判定 Job をスケジュールする。GameManager.CheckPlayerBulletVsEnemy から呼ばれる。
    /// </summary>
    public JobHandle ProcessDamage(
        int bulletGroupId,
        float targetCellSize,
        float targetCollisionRadiusSq,
        NativeParallelMultiHashMap<int, int> targetSpatialMap,
        NativeArray<float3> targetPositions,
        NativeArray<bool> targetActive,
        NativeQueue<BulletDamageInfo>.ParallelWriter targetDamageQueue,
        JobHandle dependency = default)
    {
        if (IsInitialized == false)
        {
            return dependency;
        }

        if (_bulletGroups.TryGetValue(bulletGroupId, out var bulletGroup) == false)
        {
            return dependency;
        }

        _cachedCollideJob.bulletPositions = bulletGroup.Positions;
        _cachedCollideJob.bulletActive = bulletGroup.Active;
        _cachedCollideJob.bulletDamage = bulletDamage;

        _cachedCollideJob.cellSize = targetCellSize;
        _cachedCollideJob.targetCollisionRadiusSq = targetCollisionRadiusSq;
        _cachedCollideJob.spatialMap = targetSpatialMap;
        _cachedCollideJob.targetPositions = targetPositions;
        _cachedCollideJob.targetActive = targetActive;
        _cachedCollideJob.damageQueue = targetDamageQueue;

        return _cachedCollideJob.Schedule(bulletGroup.MaxCount, 64, dependency);
    }

    void LateUpdate()
    {
        // 参照はインスペクターで必ず指定すること
        Debug.Assert(renderManager != null, "[BulletManager] renderManager が未設定です。インスペクターで指定してください。");
        Debug.Assert(gameManager != null, "[BulletManager] gameManager が未設定です。インスペクターで指定してください。");

        // 描画は LateUpdate で行う（Update で完了した Job の結果を描画）
        if (IsInitialized == false) return;
        if (gameManager.IsPlaying == false) return;
        RenderBullets();
    }

    /// <summary>
    /// 弾の座標を Matrix に詰め、RenderManager で描画する。LateUpdate から呼ぶ。
    /// </summary>
    public void RenderBullets()
    {
        if (IsInitialized == false || renderManager == null)
        {
            return;
        }

        foreach (var group in _bulletGroups)
        {
            group.Value.RunMatrixJob();

            // TODO: グループにマテリアルをもたせるようにする
            if (group.Key == 0)
            {
                renderManager.RenderBullets(_rpPlayerBullet, playerBulletMesh, group.Value.Matrices, group.Value.DrawCount);
            }
            else
            {
                renderManager.RenderBullets(_rpEnemyBullet, enemyBulletMesh, group.Value.Matrices, group.Value.DrawCount);
            }
        }
    }

    /// <summary>
    /// 弾をすべてリセット（プレイヤー弾・敵弾とも）。ResetGameState 時などに呼ぶ。
    /// </summary>
    public void ResetBullets()
    {
        foreach (var group in _bulletGroups)
        {
            group.Value.Reset();
        }
    }

    /// <summary>
    /// 敵弾とプレイヤーの当たり判定。ヒットした弾は無効化し、プレイヤーにダメージを通知する。Job 完了後に GameManager から呼ぶ。
    /// </summary>
    public void CheckEnemyBulletVsPlayer()
    {
        if (IsInitialized == false || playerTransform == null || !_bulletGroups.TryGetValue(_enemyBulletGroupId, out var group))
        {
            return;
        }
        group.CollectHitsAgainstCircle((float3)playerTransform.position, playerCollisionRadius, _collectedHitDamages);
        for (int i = 0; i < _collectedHitDamages.Length; i++)
        {
            gameManager?.AddPlayerDamage(Mathf.RoundToInt(_collectedHitDamages[i]));
        }
    }

    /// <summary>
    /// ボスとプレイヤー弾の当たり判定。ヒットした弾は無効化する。
    /// </summary>
    public void CheckBossBulletCollision(EnemyManager enemyManager, int bulletGroupId)
    {
        Debug.Assert(enemyManager != null, "[BulletManager] enemyManager が未設定です。インスペクターで指定してください。");

        if (_bulletGroups.TryGetValue(bulletGroupId, out var group) == false)
        {
            return;
        }

        BossBase boss = enemyManager.GetCurrentBossComponent();
        if (boss == null || boss.IsDead)
        {
            return;
        }

        group.CollectHitsAgainstCircle((float3)boss.Position, boss.CollisionRadius, _collectedHitDamages);
        
        for (int i = 0; i < _collectedHitDamages.Length; i++)
        {
            float actualDamage = boss.TakeDamage(_collectedHitDamages[i]);
            if (actualDamage > 0)
            {
                damageTextManager?.ShowDamage(boss.GetDamageTextPosition(), (int)actualDamage, boss.CollisionRadius);
            }
            if (boss.IsDead)
            {
                break;
            }
        }
    }
}
