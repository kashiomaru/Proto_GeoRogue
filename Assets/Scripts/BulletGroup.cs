using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// 弾 1 種類分の BulletPool と描画用 Matrix4x4 バッファをまとめる。
/// Job で詰めた Matrix4x4 を RenderManager に直接渡す。
/// </summary>
public class BulletGroup
{
    private BulletPool _pool;
    private NativeArray<Matrix4x4> _matrices;
    private NativeArray<int> _drawCount;
    private NativeReference<int> _matrixCounter;
    private float _scale;
    private DrawMatrixJob _matrixJob;

    /// <summary>最大弾数</summary>
    public int MaxCount => _pool != null ? _pool.MaxCount : 0;

    /// <summary>描画用。RunMatrixJob 後に RenderManager に渡す。</summary>
    public NativeArray<Matrix4x4> Matrices => _matrices;
    /// <summary>描画数。RunMatrixJob 後に _drawCount[0] を参照する。</summary>
    public int DrawCount => _drawCount.IsCreated ? _drawCount[0] : 0;

    /// <summary>
    /// 初期化。最大弾数・描画スケールを指定する。
    /// </summary>
    public void Initialize(int maxCount, float scale = 0.5f)
    {
        _pool = new BulletPool();
        _pool.Initialize(maxCount);

        _matrices = new NativeArray<Matrix4x4>(maxCount, Allocator.Persistent);
        _drawCount = new NativeArray<int>(1, Allocator.Persistent);
        _matrixCounter = new NativeReference<int>(0, Allocator.Persistent);
        _scale = scale;

        _matrixJob = new DrawMatrixJob
        {
            positions = _pool.Positions,
            directions = _pool.Directions,
            activeFlags = _pool.Active,
            matrices = _matrices,
            counter = _matrixCounter,
            scale = new Vector3(_scale, _scale, _scale)
        };
    }

    /// <summary>弾を 1 発生成する。</summary>
    public void Spawn(Vector3 position, Vector3 direction, float speed, float lifeTime, float damage = 0f)
    {
        _pool?.Spawn(position, direction, speed, lifeTime, damage);
    }

    /// <summary>弾の移動 Job をスケジュールする。</summary>
    public JobHandle ScheduleMoveJob(float deltaTime, JobHandle dependency)
    {
        return _pool != null ? _pool.ScheduleMoveJob(deltaTime, dependency) : dependency;
    }

    /// <summary>指定円（中心・半径）と当たった弾を収集し、該当弾を無効化する。ヒットした弾のダメージを damagesOut に追加する。</summary>
    public void CollectHitsAgainstCircle(float3 center, float radius, NativeList<float> damagesOut)
    {
        _pool?.CollectHitsAgainstCircle(center, radius, damagesOut);
    }

    /// <summary>BulletCollideJob にこのグループの弾データ（Positions / Active）を設定する。</summary>
    public void SetCollideJobBulletData(ref BulletCollideJob job)
    {
        if (_pool == null) return;
        job.bulletPositions = _pool.Positions;
        job.bulletActive = _pool.Active;
    }

    /// <summary>
    /// 描画用にアクティブな弾を Matrix4x4 に詰める。RenderManager に渡す前に呼ぶ。
    /// </summary>
    public void RunMatrixJob()
    {
        if (_pool == null || !_matrices.IsCreated)
            return;
        _matrixJob.positions = _pool.Positions;
        _matrixJob.directions = _pool.Directions;
        _matrixJob.activeFlags = _pool.Active;
        _matrixJob.matrices = _matrices;
        _matrixJob.counter = _matrixCounter;
        _matrixJob.scale = new Vector3(_scale, _scale, _scale);
        _matrixCounter.Value = 0;
        _matrixJob.Schedule(_matrices.Length, 64).Complete();
        _drawCount[0] = _matrixCounter.Value;
    }

    /// <summary>
    /// すべての弾を無効化する。
    /// </summary>
    public void Reset()
    {
        _pool?.Reset();
    }

    /// <summary>
    /// ネイティブ配列を解放する。以降このグループは使用しないこと。
    /// </summary>
    public void Dispose()
    {
        _pool?.Dispose();
        _pool = null;
        if (_matrices.IsCreated)
            _matrices.Dispose();
        if (_drawCount.IsCreated)
            _drawCount.Dispose();
        if (_matrixCounter.IsCreated)
            _matrixCounter.Dispose();
    }
}
