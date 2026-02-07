using UnityEngine;
using Unity.Collections;
using Unity.Jobs;

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
    private BulletMatrixJob _matrixJob;

    /// <summary>内部の BulletPool。Spawn / ScheduleMoveJob / Positions / Active 等はここから参照する。</summary>
    public BulletPool Pool => _pool;

    /// <summary>描画用。RunMatrixJob 後に RenderManager に渡す。</summary>
    public NativeArray<Matrix4x4> Matrices => _matrices;
    /// <summary>描画数。RunMatrixJob 後に _drawCount[0] を参照する。</summary>
    public int DrawCount => _drawCount.IsCreated ? _drawCount[0] : 0;

    /// <summary>
    /// 初期化。最大弾数・ダメージ配列の有無・描画スケールを指定する。
    /// </summary>
    public void Initialize(int maxCount, bool useDamageArray = false, float scale = 0.5f)
    {
        _pool = new BulletPool();
        _pool.Initialize(maxCount, useDamageArray);

        _matrices = new NativeArray<Matrix4x4>(maxCount, Allocator.Persistent);
        _drawCount = new NativeArray<int>(1, Allocator.Persistent);
        _matrixCounter = new NativeReference<int>(0, Allocator.Persistent);
        _scale = scale;

        _matrixJob = new BulletMatrixJob
        {
            positions = _pool.Positions,
            directions = _pool.Directions,
            activeFlags = _pool.Active,
            matrices = _matrices,
            counter = _matrixCounter,
            scale = _scale
        };
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
        _matrixJob.scale = _scale;
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
