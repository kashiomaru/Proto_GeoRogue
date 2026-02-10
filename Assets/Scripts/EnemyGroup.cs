using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// 1種類の敵データ（1つの EnemyData）に対応する1グループを管理するクラス。
/// 座標・回転・HP・空間マップ・キューを保持し、移動Job・リスポーン・描画・弾衝突用のAPIを提供する。
/// MonoBehaviour ではなく通常の C# クラス。EnemyManager が new / Dispose で管理する。
/// </summary>
public class EnemyGroup
{
    private readonly int _maxCount;
    private readonly int _spawnCount; // 実際に出現させる数（1～_maxCount）
    private readonly float _speed;
    private readonly int _maxHp;
    private readonly float _flashDuration;
    private readonly int _damageAmount;
    private readonly float _damageRadius;
    private readonly float _collisionRadius;
    private readonly float _cellSize;
    private readonly float _respawnDistance;
    private readonly float _respawnMinRadius;
    private readonly float _respawnMaxRadius;
    private readonly Mesh _mesh;
    private readonly Material _material;
    private readonly Vector3 _scale;
    private readonly BulletData _bulletData;
    private readonly int _gemDropAmount;
    /// <summary>1発あたりの拡散用 Y 軸回転（CountPerShot 分）。初期化時に計算。Job に渡すため NativeArray。</summary>
    private NativeArray<quaternion> _bulletSpreadRotations;
    /// <summary>発射リクエストを Job から溜め、メインスレッドでドレインして SpawnEnemyBullet に渡す。</summary>
    private NativeQueue<EnemyBulletSpawnRequest> _bulletSpawnQueue;

    /// <summary>描画用。コンストラクタで一度だけ初期化。matProps は RenderManager が毎フレーム設定する。</summary>
    private RenderParams _rpEnemy;

    private NativeArray<float3> _positions;
    /// <summary>敵の向き（前方ベクトル、正規化）。描画・発射方向に使用。</summary>
    private NativeArray<float3> _directions;
    private NativeArray<bool> _active;
    private NativeArray<int> _hp;
    private NativeArray<float> _fireTimers;
    private NativeArray<float> _flashTimers;
    private NativeArray<Matrix4x4> _matrices;
    private NativeArray<Vector4> _emissionColors;
    private NativeReference<int> _drawCounter;
    /// <summary>DrawMatrixJob 用。敵は要素別スケールを使わないため長さ0の配列を渡す。</summary>
    private NativeArray<float> _emptyScaleMultipliers;
    private NativeParallelMultiHashMap<int, int> _spatialMap;
    private List<Vector3> _deadPositions;
    private NativeQueue<BulletDamageInfo> _damageQueue;

    private DrawMatrixJob _cachedMatrixJob;
    private EnemyEmissionJob _cachedEmissionJob;
    private EnemyMoveAndHashJob _cachedMoveJob;
    private EnemyRespawnJob _cachedRespawnJob;
    private EnemyGroupInitJob _cachedGroupInitJob;
    private EnemyBulletFireJob _cachedBulletFireJob;

    private readonly BulletManager _bulletManager;
    private readonly int _enemyBulletGroupId = -1;

    /// <summary>実際に出現させる敵の数。</summary>
    public int SpawnCount => _spawnCount;
    /// <summary>このグループ用の敵弾 BulletGroup ID（GameManager の当たり判定などで使用）。</summary>
    public int EnemyBulletGroupId => _enemyBulletGroupId;
    /// <summary>空間マップ（弾衝突Job用）。</summary>
    public NativeParallelMultiHashMap<int, int> SpatialMap => _spatialMap;
    /// <summary>敵座標（弾衝突Job用）。</summary>
    public NativeArray<float3> Positions => _positions;
    /// <summary>ターゲットのアクティブフラグ（弾衝突Job用）。</summary>
    public NativeArray<bool> Active => _active;
    /// <summary>敵HP（弾衝突Job用）。</summary>
    public NativeArray<int> EnemyHp => _hp;
    /// <summary>弾との当たり判定に使う半径（弾衝突Job用）。</summary>
    public float CollisionRadius => _collisionRadius;
    /// <summary>セルサイズ（弾衝突Job用）。</summary>
    public float CellSize => _cellSize;
    /// <summary>表示用メッシュ。</summary>
    public Mesh Mesh => _mesh;
    /// <summary>表示用マテリアル。</summary>
    public Material Material => _material;
    /// <summary>表示用スケール。</summary>
    public Vector3 Scale => _scale;

    /// <summary>
    /// 敵データと共通パラメータでグループを生成する。バッファを確保し、全敵を非アクティブで初期化する。
    /// 配置は次フレーム以降の ProcessRespawn に任せる（プレイヤー周りのドーナツ状に配置される）。
    /// </summary>
    /// <param name="data">敵データ（null 不可）。</param>
    /// <param name="maxCount">このグループの最大数（配列サイズ）。</param>
    /// <param name="flashDuration">ヒットフラッシュ表示時間。</param>
    /// <param name="respawnDistance">リスポーン判定距離。</param>
    /// <param name="respawnMinRadius">リスポーン最小半径。</param>
    /// <param name="respawnMaxRadius">リスポーン最大半径。</param>
    /// <param name="bulletManager">弾グループ追加用（null の場合は弾発射・当たり判定を行わない）。</param>
    public EnemyGroup(
        EnemyData data,
        int maxCount,
        float flashDuration,
        float respawnDistance,
        float respawnMinRadius,
        float respawnMaxRadius,
        BulletManager bulletManager)
    {
        Assert.IsNotNull(data, "EnemyData must not be null.");
        Assert.IsNotNull(bulletManager, "BulletManager must not be null.");

        _maxCount = Mathf.Max(1, maxCount);
        _spawnCount = data.SpawnCount > 0 ? Mathf.Clamp(data.SpawnCount, 1, _maxCount) : Mathf.Clamp(10, 1, _maxCount);
        _speed = data.Speed;
        _maxHp = data.MaxHp;
        _flashDuration = flashDuration;
        _damageAmount = data.DamageAmount;
        _damageRadius = data.DamageRadius;
        _collisionRadius = data.CollisionRadius;
        _respawnDistance = respawnDistance;
        _respawnMinRadius = respawnMinRadius;
        _respawnMaxRadius = respawnMaxRadius;
        _mesh = data.Mesh;
        _material = data.Material;
        _scale = data.Scale;
        _bulletData = data.BulletData;
        _gemDropAmount = data.GemDropAmount;
        _bulletManager = bulletManager;

        // 弾の拡散方向用 Y 軸回転を事前計算（countPerShot / spreadAngle はグループ固定のため）。Job に渡すため NativeArray。
        if (_bulletData != null)
        {
            int n = _bulletData.CountPerShot;
            _bulletSpreadRotations = new NativeArray<quaternion>(n, Allocator.Persistent);
            float spreadAngle = _bulletData.SpreadAngle;
            for (int j = 0; j < n; j++)
            {
                float angleDeg = n > 1
                    ? -spreadAngle * (n - 1) * 0.5f + (spreadAngle * j)
                    : 0f;
                _bulletSpreadRotations[j] = quaternion.RotateY(math.radians(angleDeg));
            }
            _bulletSpawnQueue = new NativeQueue<EnemyBulletSpawnRequest>(Allocator.Persistent);

            _enemyBulletGroupId = _bulletManager.AddBulletGroup(_bulletData.Damage, _bulletData.Scale, _bulletData.Mesh, _bulletData.Material);
        }

        // 空間分割のセルサイズは当たり半径から算出（R < 2*cellSize を満たす）
        _cellSize = Mathf.Max(0.5f, _collisionRadius * 0.51f);

        if (_material != null)
        {
            _rpEnemy = new RenderParams(_material)
            {
                shadowCastingMode = ShadowCastingMode.On,
                receiveShadows = true
            };
        }

        _positions = new NativeArray<float3>(_maxCount, Allocator.Persistent);
        _directions = new NativeArray<float3>(_maxCount, Allocator.Persistent);
        _active = new NativeArray<bool>(_maxCount, Allocator.Persistent);
        _hp = new NativeArray<int>(_maxCount, Allocator.Persistent);
        _fireTimers = new NativeArray<float>(_maxCount, Allocator.Persistent);
        _flashTimers = new NativeArray<float>(_maxCount, Allocator.Persistent);
        _matrices = new NativeArray<Matrix4x4>(_maxCount, Allocator.Persistent);
        _emissionColors = new NativeArray<Vector4>(_maxCount, Allocator.Persistent);
        _drawCounter = new NativeReference<int>(0, Allocator.Persistent);
        _emptyScaleMultipliers = new NativeArray<float>(0, Allocator.Persistent);

        _spatialMap = new NativeParallelMultiHashMap<int, int>(_maxCount, Allocator.Persistent);
        _deadPositions = new List<Vector3>();
        _damageQueue = new NativeQueue<BulletDamageInfo>(Allocator.Persistent);

        SetupCachedJobs();

        _cachedGroupInitJob.Schedule(_maxCount, 64).Complete();
    }

    /// <summary>キャッシュした Job に Native コンテナなどを紐づける。コンストラクタの後半で一度だけ呼ぶ。</summary>
    private void SetupCachedJobs()
    {
        _cachedGroupInitJob.active = _active;
        _cachedGroupInitJob.directions = _directions;
        _cachedGroupInitJob.fireTimers = _fireTimers;
        _cachedGroupInitJob.flashTimers = _flashTimers;

        _cachedMatrixJob.positions = _positions;
        _cachedMatrixJob.directions = _directions;
        _cachedMatrixJob.activeFlags = _active;
        _cachedMatrixJob.matrices = _matrices;
        _cachedMatrixJob.counter = _drawCounter;
        _cachedMatrixJob.scale = _scale;
        _cachedMatrixJob.scaleMultipliers = _emptyScaleMultipliers;

        _cachedEmissionJob.activeFlags = _active;
        _cachedEmissionJob.flashTimers = _flashTimers;
        _cachedEmissionJob.emissionColors = _emissionColors;
        _cachedEmissionJob.counter = _drawCounter;

        _cachedMoveJob.speed = _speed;
        _cachedMoveJob.cellSize = _cellSize;
        _cachedMoveJob.damageAmount = _damageAmount;
        _cachedMoveJob.damageRadius = _damageRadius;
        _cachedMoveJob.positions = _positions;
        _cachedMoveJob.directions = _directions;
        _cachedMoveJob.activeFlags = _active;

        _cachedRespawnJob.respawnMinRadius = _respawnMinRadius;
        _cachedRespawnJob.respawnMaxRadius = _respawnMaxRadius;
        _cachedRespawnJob.maxHp = _maxHp;
        _cachedRespawnJob.positions = _positions;
        _cachedRespawnJob.directions = _directions;
        _cachedRespawnJob.active = _active;
        _cachedRespawnJob.hp = _hp;
        _cachedRespawnJob.fireTimers = _fireTimers;
        _cachedRespawnJob.flashTimers = _flashTimers;

        if (_bulletData != null)
        {
            _cachedBulletFireJob.interval = _bulletData.FireInterval;
            _cachedBulletFireJob.speed = _bulletData.Speed;
            _cachedBulletFireJob.damage = _bulletData.Damage;
            _cachedBulletFireJob.lifeTime = _bulletData.LifeTime;
            _cachedBulletFireJob.countPerShot = _bulletData.CountPerShot;
            _cachedBulletFireJob.positions = _positions;
            _cachedBulletFireJob.directions = _directions;
            _cachedBulletFireJob.active = _active;
            _cachedBulletFireJob.fireTimers = _fireTimers;
            _cachedBulletFireJob.spreadRotations = _bulletSpreadRotations;
        }
    }

    /// <summary>確保したバッファを破棄する。二重呼び出し防止は呼び出し側で行う。</summary>
    public void Dispose()
    {
        _bulletManager.RemoveBulletGroup(_enemyBulletGroupId);

        if (_positions.IsCreated) _positions.Dispose();
        if (_directions.IsCreated) _directions.Dispose();
        if (_active.IsCreated) _active.Dispose();
        if (_hp.IsCreated) _hp.Dispose();
        if (_fireTimers.IsCreated) _fireTimers.Dispose();
        if (_flashTimers.IsCreated) _flashTimers.Dispose();
        if (_matrices.IsCreated) _matrices.Dispose();
        if (_emissionColors.IsCreated) _emissionColors.Dispose();
        if (_drawCounter.IsCreated) _drawCounter.Dispose();
        if (_emptyScaleMultipliers.IsCreated) _emptyScaleMultipliers.Dispose();
        if (_spatialMap.IsCreated) _spatialMap.Dispose();
        if (_damageQueue.IsCreated) _damageQueue.Dispose();
        if (_bulletSpreadRotations.IsCreated) _bulletSpreadRotations.Dispose();
        if (_bulletSpawnQueue.IsCreated) _bulletSpawnQueue.Dispose();
    }

    /// <summary>このグループの敵移動Jobをスケジュールする。複数グループ時は前のグループの Job を dependsOn に渡すこと（同一 playerDamageQueue への書き込み競合を防ぐ）。</summary>
    public JobHandle ScheduleEnemyMoveJob(float3 playerPos, NativeQueue<int>.ParallelWriter playerDamageQueue, JobHandle dependsOn = default)
    {
        Assert.IsTrue(_spatialMap.IsCreated, "SpatialMap must be created in EnemyGroup constructor.");

        _spatialMap.Clear();

        _cachedMoveJob.deltaTime = Time.deltaTime;
        _cachedMoveJob.target = playerPos;
        _cachedMoveJob.spatialMap = _spatialMap.AsParallelWriter();
        _cachedMoveJob.damageQueue = playerDamageQueue;
        return _cachedMoveJob.Schedule(_spawnCount, 64, dependsOn);
    }

    /// <summary>死んだ敵の位置をキューから取り出しジェム生成に渡す。</summary>
    public void ProcessDeadEnemies(GemManager gemManager)
    {
        foreach (var position in _deadPositions)
        {
            gemManager?.SpawnGem(position, _gemDropAmount);
        }
        _deadPositions.Clear();
    }

    /// <summary>ダメージキューを処理し、HP減算・フラッシュ・死亡処理・ダメージ表示を行う。</summary>
    public void ProcessEnemyDamage(DamageTextManager damageTextManager)
    {
        while (_damageQueue.TryDequeue(out BulletDamageInfo damageInfo))
        {
            int idx = damageInfo.index;
            if (idx < 0 || idx >= _spawnCount || _active[idx] == false)
                continue;

            int currentHp = _hp[idx];
            currentHp -= damageInfo.damage;
            _hp[idx] = currentHp;

            if (_flashTimers.IsCreated)
                _flashTimers[idx] = _flashDuration;

            if (currentHp <= 0)
            {
                _active[idx] = false;
                _deadPositions.Add(damageInfo.position);
            }

            if (damageInfo.damage > 0)
                damageTextManager?.ShowDamage(damageInfo.position, damageInfo.damage, _collisionRadius, damageInfo.isCritical);
        }
    }

    /// <summary>プレイヤー位置を元にリスポーン処理を行う。seed は毎フレーム変えると配置が変わる（例: Time.frameCount）。</summary>
    public void ProcessRespawn(float3 playerPos, uint seed)
    {
        _cachedRespawnJob.playerPos = playerPos;
        _cachedRespawnJob.deleteDistSq = _respawnDistance * _respawnDistance;
        _cachedRespawnJob.fireIntervalMax = _bulletData != null ? _bulletData.FireInterval : 0f;
        _cachedRespawnJob.seed = seed;
        _cachedRespawnJob.Schedule(_spawnCount, 64).Complete();
    }

    /// <summary>
    /// 弾を撃つ敵について発射タイマーを進め、間隔が来たら BulletManager に弾を生成させる。BulletManager の Job 完了後に GameManager から呼ぶ。
    /// 発射判定とリクエスト出力は Job で行い、メインスレッドでキューをドレインして SpawnBullet を呼ぶ。弾は発射元の向き（Forward）で飛ばす。
    /// </summary>
    public void ProcessFiring(BulletManager bulletManager)
    {
        if (_bulletData == null || bulletManager == null || _enemyBulletGroupId < 0 || !_bulletSpawnQueue.IsCreated)
        {
            return;
        }

        _cachedBulletFireJob.deltaTime = Time.deltaTime;
        _cachedBulletFireJob.spawnQueue = _bulletSpawnQueue.AsParallelWriter();

        _cachedBulletFireJob.Schedule(_spawnCount, 64).Complete();

        while (_bulletSpawnQueue.TryDequeue(out EnemyBulletSpawnRequest req))
        {
            float dirRot = (_bulletData != null) ? _bulletData.DirectionRotation : 0f;
            bulletManager.SpawnBullet(_enemyBulletGroupId, (Vector3)req.position, (Vector3)req.direction, req.speed, req.lifeTime, dirRot);
        }
    }

    /// <summary>このグループの敵を RenderManager で描画する（DrawMatrixJob + EnemyEmissionJob + RenderEnemies）。</summary>
    public void Render(RenderManager renderManager)
    {
        if (renderManager == null) return;

        _drawCounter.Value = 0;
        _cachedMatrixJob.Schedule(_spawnCount, 64).Complete();

        _drawCounter.Value = 0;
        _cachedEmissionJob.deltaTime = Time.deltaTime;
        _cachedEmissionJob.flashIntensity = renderManager.FlashIntensity;
        _cachedEmissionJob.Schedule(_spawnCount, 64).Complete();

        int drawCount = _drawCounter.Value;
        if (drawCount > 0)
            renderManager.RenderEnemies(_rpEnemy, _mesh, _matrices, _emissionColors, drawCount);
    }

    /// <summary>このグループの敵をすべて非表示（非アクティブ）にする。</summary>
    public void ClearAllEnemies()
    {
        _cachedGroupInitJob.Schedule(_spawnCount, 64).Complete();
    }

    /// <summary>このグループの敵をすべて非アクティブにし、キューとフラッシュをクリアする。配置は次フレーム以降の ProcessRespawn に任せる。</summary>
    public void ResetEnemies()
    {
        _cachedGroupInitJob.Schedule(_maxCount, 64).Complete();

        _deadPositions.Clear();
        while (_damageQueue.TryDequeue(out _)) { }
    }

    /// <summary>敵ダメージ情報キュー（弾衝突Job用）。</summary>
    public NativeQueue<BulletDamageInfo>.ParallelWriter GetEnemyDamageQueueWriter() => _damageQueue.AsParallelWriter();
}
