using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// 弾 1 種類分のバッファと移動を担当する共通クラス。
/// プレイヤー弾用・敵弾用の 2 つを BulletManager が持ち、当たり判定や描画は BulletManager 側で行う。
/// </summary>
public class BulletPool
{
    private NativeArray<float3> _positions;
    private NativeArray<float3> _directions;
    private NativeArray<float3> _velocities;
    private NativeArray<bool> _active;
    private NativeArray<float> _lifeTime;
    private NativeArray<float> _damage;
    private int _indexHead;
    private bool _disposed;

    private BulletInitActiveJob _cachedInitActiveJob;
    private BulletMoveJob _cachedMoveJob;
    private BulletCollectHitsCircleJob _cachedCollectHitsJob;

    /// <summary>最大弾数</summary>
    public int MaxCount { get; private set; }

    public NativeArray<float3> Positions => _positions;
    public NativeArray<float3> Directions => _directions;
    public NativeArray<float3> Velocities => _velocities;
    public NativeArray<bool> Active => _active;
    public NativeArray<float> LifeTime => _lifeTime;
    /// <summary>弾ごとのダメージ配列</summary>
    public NativeArray<float> Damage => _damage;

    /// <summary>
    /// 初期化。Dispose 後は再初期化しないこと。
    /// </summary>
    /// <param name="maxCount">最大弾数</param>
    public void Initialize(int maxCount)
    {
        if (_positions.IsCreated)
        {
            Dispose();
        }
        MaxCount = maxCount;

        _positions = new NativeArray<float3>(maxCount, Allocator.Persistent);
        _directions = new NativeArray<float3>(maxCount, Allocator.Persistent);
        _velocities = new NativeArray<float3>(maxCount, Allocator.Persistent);
        _active = new NativeArray<bool>(maxCount, Allocator.Persistent);
        _lifeTime = new NativeArray<float>(maxCount, Allocator.Persistent);
        _damage = new NativeArray<float>(maxCount, Allocator.Persistent);

        _cachedInitActiveJob.active = _active;
        _cachedInitActiveJob.Schedule(maxCount, 64).Complete();

        _cachedMoveJob.bulletPositions = _positions;
        _cachedMoveJob.bulletVelocities = _velocities;
        _cachedMoveJob.bulletActive = _active;
        _cachedMoveJob.bulletLifeTime = _lifeTime;

        _indexHead = 0;
        _disposed = false;
    }

    /// <summary>
    /// 弾を 1 発生成する。
    /// </summary>
    /// <param name="position">発射位置</param>
    /// <param name="direction">飛ばす方向（正規化されていなくても内部で正規化する）</param>
    /// <param name="speed">速度</param>
    /// <param name="lifeTime">生存時間（秒）</param>
    /// <param name="damage">弾ごとのダメージ</param>
    public void Spawn(Vector3 position, Vector3 direction, float speed, float lifeTime, float damage = 0f)
    {
        if (_disposed || !_positions.IsCreated)
        {
            return;
        }
        Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        int id = _indexHead;
        _indexHead = (_indexHead + 1) % MaxCount;

        _active[id] = true;
        _positions[id] = (float3)position;
        _directions[id] = (float3)dir;
        _velocities[id] = (float3)(dir * speed);
        _lifeTime[id] = lifeTime;
        _damage[id] = damage;
    }

    /// <summary>
    /// 指定インデックスの弾のアクティブ状態を設定する。当たり判定で弾を無効化するときに使用。
    /// </summary>
    public void SetActive(int index, bool value)
    {
        if (_disposed || !_active.IsCreated || index < 0 || index >= MaxCount)
        {
            return;
        }
        _active[index] = value;
    }

    /// <summary>
    /// 指定円（中心・半径）と当たった弾を収集し、該当弾を無効化する。
    /// ヒットした弾のダメージを damagesOut に追加する。呼び出し側で NativeList を用意し、Capacity は当該プールの MaxCount 以上にすること。
    /// </summary>
    public void CollectHitsAgainstCircle(float3 center, float radius, NativeList<float> damagesOut)
    {
        if (_disposed || !_positions.IsCreated || !damagesOut.IsCreated)
        {
            return;
        }
        damagesOut.Clear();
        if (damagesOut.Capacity < MaxCount)
        {
            damagesOut.Capacity = MaxCount;
        }

        _cachedCollectHitsJob.center = center;
        _cachedCollectHitsJob.radiusSq = radius * radius;
        _cachedCollectHitsJob.positions = _positions;
        _cachedCollectHitsJob.damage = _damage;
        _cachedCollectHitsJob.active = _active;
        _cachedCollectHitsJob.damageOut = damagesOut.AsParallelWriter();
        _cachedCollectHitsJob.Schedule(MaxCount, 64).Complete();
    }

    /// <summary>
    /// 弾の移動 Job をスケジュールする。
    /// </summary>
    public JobHandle ScheduleMoveJob(float deltaTime, JobHandle dependency)
    {
        if (_disposed || !_positions.IsCreated)
        {
            return dependency;
        }
        _cachedMoveJob.deltaTime = deltaTime;
        return _cachedMoveJob.Schedule(MaxCount, 64, dependency);
    }

    /// <summary>
    /// すべての弾を無効化する。
    /// </summary>
    public void Reset()
    {
        if (_disposed || !_active.IsCreated)
        {
            return;
        }
        _cachedInitActiveJob.Schedule(MaxCount, 64).Complete();
        _indexHead = 0;
    }

    /// <summary>
    /// ネイティブ配列を解放する。以降このプールは使用しないこと。
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        if (_positions.IsCreated) _positions.Dispose();
        if (_directions.IsCreated) _directions.Dispose();
        if (_velocities.IsCreated) _velocities.Dispose();
        if (_active.IsCreated) _active.Dispose();
        if (_lifeTime.IsCreated) _lifeTime.Dispose();
        if (_damage.IsCreated) _damage.Dispose();
        _disposed = true;
    }
}
