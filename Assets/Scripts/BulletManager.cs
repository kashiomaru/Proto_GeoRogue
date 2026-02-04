using UnityEngine;
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
    [SerializeField] private int maxPlayerBullets = 1000;
    [SerializeField] private int maxEnemyBullets = 500;

    [Header("Params")]
    [SerializeField] private Transform playerTransform;
    [Tooltip("敵弾とプレイヤーの当たり判定に使うプレイヤー側の半径")]
    [SerializeField] private float playerCollisionRadius = 1f;

    [Header("Player Shot")]
    [SerializeField] private float multiShotSpreadAngle = 10f;
    [SerializeField] private float bulletDamage = 1.0f;

    [Header("References")]
    [SerializeField] private Player player;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private DamageTextManager damageTextManager;
    [SerializeField] private RenderManager renderManager;

    private BulletPool _playerBullets;
    private BulletPool _enemyBullets;
    private float _playerShotTimer;

    // 描画用リスト（RenderManager に渡す）
    private List<Vector3> _playerBulletPositionList;
    private List<Quaternion> _playerBulletRotationList;
    private List<bool> _playerBulletActiveList;
    private List<Vector3> _enemyBulletPositionList;
    private List<Quaternion> _enemyBulletRotationList;
    private List<bool> _enemyBulletActiveList;

    public float BulletDamage => bulletDamage;

    protected override void InitializeInternal()
    {
        _playerBullets = new BulletPool();
        _playerBullets.Initialize(maxPlayerBullets, useDamageArray: false);

        _enemyBullets = new BulletPool();
        _enemyBullets.Initialize(maxEnemyBullets, useDamageArray: true);

        _playerBulletPositionList = new List<Vector3>(maxPlayerBullets);
        _playerBulletRotationList = new List<Quaternion>(maxPlayerBullets);
        _playerBulletActiveList = new List<bool>(maxPlayerBullets);
        for (int i = 0; i < maxPlayerBullets; i++)
        {
            _playerBulletPositionList.Add(default);
            _playerBulletRotationList.Add(default);
            _playerBulletActiveList.Add(false);
        }
        _enemyBulletPositionList = new List<Vector3>(maxEnemyBullets);
        _enemyBulletRotationList = new List<Quaternion>(maxEnemyBullets);
        _enemyBulletActiveList = new List<bool>(maxEnemyBullets);
        for (int i = 0; i < maxEnemyBullets; i++)
        {
            _enemyBulletPositionList.Add(default);
            _enemyBulletRotationList.Add(default);
            _enemyBulletActiveList.Add(false);
        }
    }

    protected override void FinalizeInternal()
    {
        _playerBullets?.Dispose();
        _enemyBullets?.Dispose();
    }

    /// <summary>
    /// プレイヤー弾の発射処理（プレイ中に GameManager の Update から呼ぶ）
    /// </summary>
    public void HandlePlayerShooting()
    {
        if (IsInitialized == false || player == null || _playerBullets == null)
        {
            return;
        }
        float fireRate = player.GetFireRate();
        int bulletCountPerShot = player.GetBulletCountPerShot();
        float bulletSpeed = player.GetBulletSpeed();

        _playerShotTimer += Time.deltaTime;
        if (_playerShotTimer >= fireRate)
        {
            _playerShotTimer = 0f;
            Vector3 baseDir = playerTransform.forward;

            for (int i = 0; i < bulletCountPerShot; i++)
            {
                float angle = 0f;
                if (bulletCountPerShot > 1)
                {
                    angle = -multiShotSpreadAngle * (bulletCountPerShot - 1) * 0.5f + (multiShotSpreadAngle * i);
                }
                Quaternion rot = Quaternion.AngleAxis(angle, Vector3.up);
                Vector3 finalDir = rot * baseDir;

                _playerBullets.Spawn(
                    playerTransform.position,
                    finalDir,
                    bulletSpeed,
                    2.0f,
                    damage: 0f
                );
            }
        }
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
            foreach (var g in groups)
            {
                var collideJob = new BulletCollideJob
                {
                    cellSize = g.CellSize,
                    enemyCollisionRadius = g.CollisionRadius,
                    spatialMap = g.SpatialMap,
                    enemyPositions = g.EnemyPositions,
                    bulletPositions = _playerBullets.Positions,
                    bulletActive = _playerBullets.Active,
                    enemyActive = g.EnemyActive,
                    enemyHp = g.EnemyHp,
                    bulletDamage = bulletDamage,
                    deadEnemyPositions = g.GetDeadEnemyPositionsWriter(),
                    enemyDamageQueue = g.GetEnemyDamageQueueWriter(),
                    enemyFlashQueue = g.GetEnemyFlashQueueWriter()
                };
                dep = collideJob.Schedule(_playerBullets.MaxCount, 64, dep);
            }
        }

        dep = _enemyBullets.ScheduleMoveJob(deltaTime, dep);
        return dep;
    }

    /// <summary>
    /// 弾の座標をリストにコピーし、RenderManager で描画する。Job 完了後に GameManager から呼ぶ。
    /// </summary>
    public void RenderBullets()
    {
        if (IsInitialized == false || renderManager == null)
        {
            return;
        }
        _playerBullets.CopyToRenderLists(_playerBulletPositionList, _playerBulletRotationList, _playerBulletActiveList);
        renderManager.RenderPlayerBullets(_playerBulletPositionList, _playerBulletRotationList, _playerBulletActiveList);

        _enemyBullets.CopyToRenderLists(_enemyBulletPositionList, _enemyBulletRotationList, _enemyBulletActiveList);
        renderManager.RenderEnemyBullets(_enemyBulletPositionList, _enemyBulletRotationList, _enemyBulletActiveList);
    }

    /// <summary>
    /// 弾をすべてリセット（プレイヤー弾・敵弾とも）。ResetGameState 時などに呼ぶ。
    /// </summary>
    public void ResetBullets()
    {
        _playerBullets?.Reset();
        _enemyBullets?.Reset();
        _playerShotTimer = 0f;
    }

    /// <summary>
    /// 敵弾とプレイヤーの当たり判定。ヒットした弾は無効化し、プレイヤーにダメージを通知する。Job 完了後に GameManager から呼ぶ。
    /// </summary>
    public void CheckEnemyBulletVsPlayer()
    {
        if (IsInitialized == false || playerTransform == null || player == null || player.IsDead || _enemyBullets == null)
        {
            return;
        }
        float3 playerPos = (float3)playerTransform.position;
        float playerRadiusSq = playerCollisionRadius * playerCollisionRadius;
        int maxCount = _enemyBullets.MaxCount;

        for (int i = 0; i < maxCount; i++)
        {
            if (_enemyBullets.Active[i] == false)
            {
                continue;
            }
            float3 bulletPos = _enemyBullets.Positions[i];
            float distSq = math.distancesq(bulletPos, playerPos);
            if (distSq >= playerRadiusSq)
            {
                continue;
            }

            float damage = _enemyBullets.Damage[i];
            gameManager?.AddPlayerDamage(Mathf.RoundToInt(damage));
            _enemyBullets.SetActive(i, false);
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

        float3 bossPos = (float3)boss.Position;
        float bossRadiusSq = boss.CollisionRadius * boss.CollisionRadius;
        int maxCount = _playerBullets.MaxCount;

        for (int i = 0; i < maxCount; i++)
        {
            if (_playerBullets.Active[i] == false)
            {
                continue;
            }
            float3 bulletPos = _playerBullets.Positions[i];
            float distSq = math.distancesq(bulletPos, bossPos);
            if (distSq >= bossRadiusSq)
            {
                continue;
            }

            float actualDamage = boss.TakeDamage(bulletDamage);
            if (actualDamage > 0)
            {
                damageTextManager?.ShowDamage(boss.GetDamageTextPosition(), (int)actualDamage, boss.CollisionRadius);
            }

            _playerBullets.SetActive(i, false);

            if (boss.IsDead)
            {
                break;
            }
        }
    }
}
