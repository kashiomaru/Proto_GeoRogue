using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// 弾 1 種類分のバッファ・移動・描画用 Matrix4x4 をまとめる。
/// Job で詰めた Matrix4x4 を RenderManager に直接渡す。
/// </summary>
public class BulletGroup
{
    // --- 弾プール用（旧 BulletPool の内容）---
    private NativeArray<float3> _positions;
    private NativeArray<float3> _directions;
    private NativeArray<float3> _velocities;
    private NativeArray<bool> _active;
    private NativeArray<float> _lifeTime;
    private int _indexHead;
    private bool _disposed;

    private BulletInitActiveJob _cachedInitActiveJob;
    private BulletMoveJob _cachedMoveJob;
    private BulletCollectHitsCircleJob _cachedCollectHitsJob;

    /// <summary>最大弾数</summary>
    private int _maxCount;
    public int MaxCount => _disposed ? 0 : _maxCount;

    public NativeArray<float3> Positions => _positions;
    public NativeArray<float3> Directions => _directions;
    public NativeArray<float3> Velocities => _velocities;
    public NativeArray<bool> Active => _active;
    public NativeArray<float> LifeTime => _lifeTime;

    // --- 描画用 ---
    private NativeArray<Matrix4x4> _matrices;
    private NativeReference<int> _matrixCounter;
    private int _damage;
    private float _scale;
    /// <summary>毎フレームの new を避けるため RunMatrixJob で再利用するスケール。</summary>
    private Vector3 _cachedScale;
    private DrawMatrixJob _matrixJob;
    private readonly Mesh _mesh;
    private readonly Material _material;
    private readonly RenderParams _renderParams;

    public int Damage => _damage;
    /// <summary>描画用。RunMatrixJob 後に RenderManager に渡す。</summary>
    public NativeArray<Matrix4x4> Matrices => _matrices;
    /// <summary>描画数。RunMatrixJob 後に _matrixCounter.Value を参照する。</summary>
    public int DrawCount => _matrixCounter.IsCreated ? _matrixCounter.Value : 0;
    /// <summary>描画用メッシュ。未設定の場合は null。</summary>
    public Mesh Mesh => _mesh;
    /// <summary>描画用マテリアル。未設定の場合は null。</summary>
    public Material Material => _material;
    /// <summary>HasRenderParams が true のときのみ有効。描画用 RenderParams。</summary>
    public RenderParams RenderParams => _renderParams;

    /// <summary>
    /// 最大弾数・描画スケール・メッシュ・マテリアルを指定して構築する。
    /// </summary>
    /// <param name="maxCount">最大弾数</param>
    /// <param name="damage">弾ごとのダメージ</param>
    /// <param name="scale">描画スケール</param>
    /// <param name="mesh">描画用メッシュ</param>
    /// <param name="material">描画用マテリアル</param>
    public BulletGroup(int maxCount, int damage, float scale, Mesh mesh, Material material)
    {
        Debug.Assert(mesh != null, "[BulletGroup] mesh が未設定です。インスペクターで指定してください。");
        Debug.Assert(material != null, "[BulletGroup] material が未設定です。インスペクターで指定してください。");

        _maxCount = maxCount;
        _disposed = false;

        _positions = new NativeArray<float3>(maxCount, Allocator.Persistent);
        _directions = new NativeArray<float3>(maxCount, Allocator.Persistent);
        _velocities = new NativeArray<float3>(maxCount, Allocator.Persistent);
        _active = new NativeArray<bool>(maxCount, Allocator.Persistent);
        _lifeTime = new NativeArray<float>(maxCount, Allocator.Persistent);

        _cachedInitActiveJob.active = _active;
        _cachedInitActiveJob.Schedule(maxCount, 64).Complete();

        _cachedMoveJob.bulletPositions = _positions;
        _cachedMoveJob.bulletVelocities = _velocities;
        _cachedMoveJob.bulletActive = _active;
        _cachedMoveJob.bulletLifeTime = _lifeTime;

        _indexHead = 0;

        _matrices = new NativeArray<Matrix4x4>(maxCount, Allocator.Persistent);
        _matrixCounter = new NativeReference<int>(0, Allocator.Persistent);
        _damage = damage;
        _scale = scale;
        _cachedScale = new Vector3(_scale, _scale, _scale);
        _mesh = mesh;
        _material = material;
        if (material != null)
        {
            _renderParams = new RenderParams(material)
            {
                shadowCastingMode = ShadowCastingMode.On,
                receiveShadows = true
            };
        }
        else
        {
            _renderParams = default;
        }

        _matrixJob = new DrawMatrixJob
        {
            positions = _positions,
            directions = _directions,
            activeFlags = _active,
            matrices = _matrices,
            counter = _matrixCounter,
            scale = _cachedScale
        };
    }

    /// <summary>弾を 1 発生成する。</summary>
    /// <param name="position">発射位置</param>
    /// <param name="direction">飛ばす方向（正規化されていなくても内部で正規化する）</param>
    /// <param name="speed">速度</param>
    /// <param name="lifeTime">生存時間（秒）</param>
    /// <param name="damage">弾ごとのダメージ</param>
    public void Spawn(Vector3 position, Vector3 direction, float speed, float lifeTime)
    {
        if (_disposed || !_positions.IsCreated)
        {
            return;
        }
        Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        int id = _indexHead;
        _indexHead = (_indexHead + 1) % _maxCount;

        _active[id] = true;
        _positions[id] = (float3)position;
        _directions[id] = (float3)dir;
        _velocities[id] = (float3)(dir * speed);
        _lifeTime[id] = lifeTime;
    }

    /// <summary>
    /// 弾の移動 Job をスケジュールする。
    /// </summary>
    public JobHandle ScheduleMoveJob(JobHandle dependency)
    {
        if (_disposed || !_positions.IsCreated)
        {
            return dependency;
        }
        _cachedMoveJob.deltaTime = Time.deltaTime;
        return _cachedMoveJob.Schedule(_maxCount, 64, dependency);
    }

    /// <summary>
    /// 指定円（中心・半径）と当たった弾を収集し、該当弾を無効化する。
    /// ヒットした弾のダメージを damageQueueOut に Enqueue する。呼び出し側で NativeQueue を用意すること。
    /// </summary>
    public void CollectHitsAgainstCircle(float3 center, float radius, NativeQueue<int> damageQueueOut)
    {
        if (_disposed || !_positions.IsCreated || !damageQueueOut.IsCreated)
        {
            return;
        }

        _cachedCollectHitsJob.center = center;
        _cachedCollectHitsJob.radiusSq = radius * radius;
        _cachedCollectHitsJob.positions = _positions;
        _cachedCollectHitsJob.damage = _damage;
        _cachedCollectHitsJob.active = _active;
        _cachedCollectHitsJob.damageOut = damageQueueOut.AsParallelWriter();
        _cachedCollectHitsJob.Schedule(_maxCount, 64).Complete();
    }

    /// <summary>BulletCollideJob にこのグループの弾データ（Positions / Active）を設定する。</summary>
    public void SetCollideJobBulletData(ref BulletCollideJob job)
    {
        if (_disposed || !_positions.IsCreated)
        {
            return;
        }
        job.bulletPositions = _positions;
        job.bulletActive = _active;
    }

    /// <summary>
    /// 描画用にアクティブな弾を Matrix4x4 に詰める。RenderManager に渡す前に呼ぶ。
    /// </summary>
    public void RunMatrixJob()
    {
        if (_disposed || !_matrices.IsCreated)
        {
            return;
        }
        _matrixJob.positions = _positions;
        _matrixJob.directions = _directions;
        _matrixJob.activeFlags = _active;
        _matrixJob.matrices = _matrices;
        _matrixJob.counter = _matrixCounter;
        _matrixJob.scale = _cachedScale;
        _matrixCounter.Value = 0;
        _matrixJob.Schedule(_matrices.Length, 64).Complete();
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
        _cachedInitActiveJob.Schedule(_maxCount, 64).Complete();
        _indexHead = 0;
    }

    /// <summary>
    /// ネイティブ配列を解放する。以降このグループは使用しないこと。
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
        if (_matrices.IsCreated) _matrices.Dispose();
        if (_matrixCounter.IsCreated) _matrixCounter.Dispose();
        _disposed = true;
    }
}
