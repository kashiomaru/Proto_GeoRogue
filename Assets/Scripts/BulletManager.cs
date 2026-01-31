using UnityEngine;
using UnityEngine.Jobs;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

public class BulletManager : InitializeMonobehaviour
{
    [Header("Settings")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private int maxBullets = 1000;

    [Header("Params")]
    [SerializeField] private Transform playerTransform;

    [Header("MultiShot Settings")]
    [SerializeField] private float multiShotSpreadAngle = 10f;

    [Header("Combat")]
    [SerializeField] private float bulletDamage = 1.0f;

    [Header("References")]
    [SerializeField] private Player player;
    [SerializeField] private DamageTextManager damageTextManager;

    private TransformAccessArray _bulletTransforms;
    private NativeArray<float3> _bulletPositions;
    private NativeArray<float3> _bulletDirections;
    private NativeArray<float3> _bulletVelocities;
    private NativeArray<bool> _bulletActive;
    private NativeArray<float> _bulletLifeTime;

    private float _timer;
    private int _bulletIndexHead;

    public float BulletDamage => bulletDamage;

    protected override void InitializeInternal()
    {
        _bulletTransforms = new TransformAccessArray(maxBullets);
        _bulletPositions = new NativeArray<float3>(maxBullets, Allocator.Persistent);
        _bulletDirections = new NativeArray<float3>(maxBullets, Allocator.Persistent);
        _bulletVelocities = new NativeArray<float3>(maxBullets, Allocator.Persistent);
        _bulletActive = new NativeArray<bool>(maxBullets, Allocator.Persistent);
        _bulletLifeTime = new NativeArray<float>(maxBullets, Allocator.Persistent);

        for (int i = 0; i < maxBullets; i++)
        {
            var obj = Instantiate(bulletPrefab, new Vector3(0, -100, 0), Quaternion.identity, transform);
            if (obj.TryGetComponent<Collider>(out var col))
            {
                col.enabled = false;
            }
            _bulletTransforms.Add(obj.transform);
            _bulletActive[i] = false;
        }
    }

    protected override void FinalizeInternal()
    {
        if (_bulletTransforms.isCreated)
        {
            _bulletTransforms.Dispose();
        }
        if (_bulletPositions.IsCreated)
        {
            _bulletPositions.Dispose();
        }
        if (_bulletDirections.IsCreated)
        {
            _bulletDirections.Dispose();
        }
        if (_bulletVelocities.IsCreated)
        {
            _bulletVelocities.Dispose();
        }
        if (_bulletActive.IsCreated)
        {
            _bulletActive.Dispose();
        }
        if (_bulletLifeTime.IsCreated)
        {
            _bulletLifeTime.Dispose();
        }
    }

    /// <summary>
    /// 発射処理（プレイ中に GameManager の Update から呼ぶ）
    /// </summary>
    public void HandleShooting()
    {
        if (player == null)
        {
            return;
        }
        float fireRate = player.GetFireRate();
        int bulletCountPerShot = player.GetBulletCountPerShot();
        float bulletSpeed = player.GetBulletSpeed();

        _timer += Time.deltaTime;
        if (_timer >= fireRate)
        {
            _timer = 0f;
            Vector3 baseDir = playerTransform.forward;

            for (int i = 0; i < bulletCountPerShot; i++)
            {
                int id = _bulletIndexHead;
                _bulletIndexHead = (_bulletIndexHead + 1) % maxBullets;

                float angle = 0f;
                if (bulletCountPerShot > 1)
                {
                    angle = -multiShotSpreadAngle * (bulletCountPerShot - 1) * 0.5f + (multiShotSpreadAngle * i);
                }
                Quaternion rot = Quaternion.AngleAxis(angle, Vector3.up);
                Vector3 finalDir = rot * baseDir;

                _bulletActive[id] = true;
                _bulletLifeTime[id] = 2.0f;
                _bulletPositions[id] = (float3)playerTransform.position;
                _bulletDirections[id] = (float3)finalDir;
                _bulletVelocities[id] = (float3)(finalDir.normalized * bulletSpeed);
            }
        }
    }

    /// <summary>
    /// 弾の移動・通常敵との衝突 Job をスケジュール。敵の移動 Job 完了後に実行するため dependency を渡す。
    /// </summary>
    public JobHandle ScheduleMoveAndCollideJob(float deltaTime, JobHandle dependency, EnemyManager enemyManager)
    {
        if (enemyManager == null)
        {
            return default;
        }
        float bulletSpeed = player != null ? player.GetBulletSpeed() : 20f;
        var bulletJob = new BulletMoveAndCollideJob
        {
            deltaTime = deltaTime,
            speed = bulletSpeed,
            cellSize = enemyManager.CellSize,
            spatialMap = enemyManager.SpatialMap,
            enemyPositions = enemyManager.EnemyPositions,
            bulletPositions = _bulletPositions,
            bulletDirections = _bulletDirections,
            bulletVelocities = _bulletVelocities,
            bulletActive = _bulletActive,
            bulletLifeTime = _bulletLifeTime,
            enemyActive = enemyManager.EnemyActive,
            enemyHp = enemyManager.EnemyHp,
            bulletDamage = bulletDamage,
            deadEnemyPositions = enemyManager.GetDeadEnemyPositionsWriter(),
            enemyDamageQueue = enemyManager.GetEnemyDamageQueueWriter(),
            enemyFlashQueue = enemyManager.GetEnemyFlashQueueWriter()
        };
        return bulletJob.Schedule(_bulletTransforms, dependency);
    }

    /// <summary>
    /// 弾をすべてリセット（画面外へ）。ResetGameState 時などに呼ぶ。
    /// </summary>
    public void ResetBullets()
    {
        for (int i = 0; i < maxBullets; i++)
        {
            _bulletActive[i] = false;
            _bulletPositions[i] = new float3(0, -100, 0);
            if (_bulletTransforms.isCreated && i < _bulletTransforms.length)
            {
                _bulletTransforms[i].position = new Vector3(0, -100, 0);
            }
        }
        _bulletIndexHead = 0;
        _timer = 0f;
    }

    /// <summary>
    /// ボスと弾の当たり判定。ヒットした弾は無効化する。
    /// </summary>
    public void CheckBossBulletCollision(EnemyManager enemyManager)
    {
        if (enemyManager == null)
        {
            return;
        }
        GameObject bossObject = enemyManager.GetCurrentBoss();
        if (bossObject == null)
        {
            return;
        }
        Boss boss = bossObject.GetComponent<Boss>();
        if (boss == null || boss.IsDead)
        {
            return;
        }

        float3 bossPos = (float3)boss.Position;
        float bossRadiusSq = boss.CollisionRadius * boss.CollisionRadius;

        for (int i = 0; i < maxBullets; i++)
        {
            if (_bulletActive[i] == false)
            {
                continue;
            }
            float3 bulletPos = _bulletPositions[i];
            float distSq = math.distancesq(bulletPos, bossPos);
            if (distSq >= bossRadiusSq)
            {
                continue;
            }

            float actualDamage = boss.TakeDamage(bulletDamage);
            if (actualDamage > 0)
            {
                damageTextManager?.ShowDamage(boss.GetDamageTextPosition(), (int)actualDamage);
            }

            _bulletActive[i] = false;
            if (_bulletTransforms.isCreated && i < _bulletTransforms.length)
            {
                _bulletTransforms[i].position = new Vector3(0, -100, 0);
            }
            _bulletPositions[i] = new float3(0, -100, 0);

            if (boss.IsDead)
            {
                break;
            }
        }
    }
}
