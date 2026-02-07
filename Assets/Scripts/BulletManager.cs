using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using System.Collections.Generic;

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
    [SerializeField] private float playerBulletScale = 0.5f;
    [SerializeField] private float enemyBulletScale = 0.5f;

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

    private BulletGroup _playerBullets;
    private BulletGroup _enemyBullets;
    private List<BulletGroup> _bulletGroups;

    /// <summary>敵グループループ内で再利用する当たり判定 Job。グループごとに参照だけ差し替える。</summary>
    private BulletCollideJob _cachedCollideJob;
    /// <summary>CollectHitsAgainstCircle の結果（ヒットした弾のダメージ）を入れるバッファ。敵弾vsプレイヤー・プレイヤー弾vsボスで共用。</summary>
    private NativeList<float> _collectedHitDamages;

    public float BulletDamage => bulletDamage;

    protected override void InitializeInternal()
    {
        _collectedHitDamages = new NativeList<float>(maxBullets, Allocator.Persistent);

        _bulletGroups = new List<BulletGroup>();

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
            group.Dispose();
        }
        _bulletGroups.Clear();
        _bulletGroups = null;
        _playerBullets = null;
        _enemyBullets = null;

        _collectedHitDamages.Dispose();
    }

    public void InitializePlayerBullets()
    {
        _playerBullets = new BulletGroup();
        _playerBullets.Initialize(maxBullets, scale: playerBulletScale);
        _bulletGroups.Add(_playerBullets);
    }

    public void InitializeEnemyBullets()
    {
        _enemyBullets = new BulletGroup();
        _enemyBullets.Initialize(maxBullets, scale: enemyBulletScale);
        _bulletGroups.Add(_enemyBullets);
    }

    /// <summary>
    /// プレイヤー弾を 1 発生成する。Player.HandlePlayerShooting から呼ぶ。
    /// </summary>
    public void SpawnPlayerBullet(Vector3 position, Vector3 direction, float speed, float lifeTime = 2f)
    {
        if (IsInitialized == false || _playerBullets == null)
        {
            return;
        }
        _playerBullets.Spawn(position, direction, speed, lifeTime, damage: bulletDamage);
    }

    /// <summary>
    /// 敵・ボス弾を 1 発生成する。敵グループやボスの Update から呼ぶ。
    /// </summary>
    public void SpawnEnemyBullet(Vector3 position, Vector3 direction, float speed, float damage, float lifeTime)
    {
        if (IsInitialized == false || _enemyBullets == null)
        {
            return;
        }
        _enemyBullets.Spawn(position, direction, speed, lifeTime, damage);
    }

    /// <summary>
    /// プレイヤー弾の移動・敵との衝突 Job と敵弾の移動 Job をスケジュール。敵の移動 Job 完了後に実行するため dependency を渡す。
    /// </summary>
    public JobHandle ScheduleMoveAndCollideJob(float deltaTime, JobHandle dependency, EnemyManager enemyManager)
    {
        JobHandle dep = _playerBullets.ScheduleMoveJob(deltaTime, dependency);

        var groups = enemyManager != null ? enemyManager.GetGroups() : null;
        if (groups != null && groups.Count > 0)
        {
            _playerBullets.SetCollideJobBulletData(ref _cachedCollideJob);
            _cachedCollideJob.bulletDamage = bulletDamage;

            foreach (var g in groups)
            {
                _cachedCollideJob.cellSize = g.CellSize;
                _cachedCollideJob.enemyCollisionRadius = g.CollisionRadius;
                _cachedCollideJob.spatialMap = g.SpatialMap;
                _cachedCollideJob.enemyPositions = g.EnemyPositions;
                _cachedCollideJob.enemyActive = g.EnemyActive;
                _cachedCollideJob.enemyHp = g.EnemyHp;
                _cachedCollideJob.deadEnemyPositions = g.GetDeadEnemyPositionsWriter();
                _cachedCollideJob.enemyDamageQueue = g.GetEnemyDamageQueueWriter();
                _cachedCollideJob.enemyFlashQueue = g.GetEnemyFlashQueueWriter();
                dep = _cachedCollideJob.Schedule(_playerBullets.MaxCount, 64, dep);
            }
        }

        dep = _enemyBullets.ScheduleMoveJob(deltaTime, dep);
        return dep;
    }

    /// <summary>
    /// 弾の座標を Matrix に詰め、RenderManager で描画する。Job 完了後に GameManager から呼ぶ。
    /// </summary>
    public void RenderBullets()
    {
        if (IsInitialized == false || renderManager == null)
        {
            return;
        }
        _playerBullets.RunMatrixJob();
        renderManager.RenderBullets(_rpPlayerBullet, playerBulletMesh, _playerBullets.Matrices, _playerBullets.DrawCount);

        _enemyBullets.RunMatrixJob();
        renderManager.RenderBullets(_rpEnemyBullet, enemyBulletMesh, _enemyBullets.Matrices, _enemyBullets.DrawCount);
    }

    /// <summary>
    /// 弾をすべてリセット（プレイヤー弾・敵弾とも）。ResetGameState 時などに呼ぶ。
    /// </summary>
    public void ResetBullets()
    {
        _playerBullets?.Reset();
        _enemyBullets?.Reset();
    }

    /// <summary>
    /// 敵弾とプレイヤーの当たり判定。ヒットした弾は無効化し、プレイヤーにダメージを通知する。Job 完了後に GameManager から呼ぶ。
    /// </summary>
    public void CheckEnemyBulletVsPlayer()
    {
        if (IsInitialized == false || playerTransform == null || _enemyBullets == null)
        {
            return;
        }
        _enemyBullets.CollectHitsAgainstCircle((float3)playerTransform.position, playerCollisionRadius, _collectedHitDamages);
        for (int i = 0; i < _collectedHitDamages.Length; i++)
        {
            gameManager?.AddPlayerDamage(Mathf.RoundToInt(_collectedHitDamages[i]));
        }
    }

    /// <summary>
    /// ボスとプレイヤー弾の当たり判定。ヒットした弾は無効化する。
    /// </summary>
    public void CheckBossBulletCollision(EnemyManager enemyManager)
    {
        if (enemyManager == null || _playerBullets == null)
        {
            return;
        }
        BossBase boss = enemyManager.GetCurrentBossComponent();
        if (boss == null || boss.IsDead)
        {
            return;
        }
        _playerBullets.CollectHitsAgainstCircle((float3)boss.Position, boss.CollisionRadius, _collectedHitDamages);
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
