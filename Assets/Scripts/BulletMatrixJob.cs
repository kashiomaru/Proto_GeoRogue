using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// アクティブな弾の座標・方向を詰めた Matrix4x4 配列と描画数を出力する。
/// 描画用に RenderManager へ直接渡すためのバッファを埋める。
/// IJobParallelFor + NativeReference + Interlocked で並列に詰める。
/// </summary>
[BurstCompile]
public struct BulletMatrixJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<float3> positions;
    [ReadOnly] public NativeArray<float3> directions;
    [ReadOnly] public NativeArray<bool> activeFlags;

    // 書き込み用（最大サイズで確保済みであること）
    [NativeDisableParallelForRestriction]
    public NativeArray<Matrix4x4> matrices;

    /// <summary>書き込みインデックス用。Schedule 前に 0 にリセットすること。Interlocked で排他制御。</summary>
    [NativeDisableParallelForRestriction]
    public NativeReference<int> counter;

    public float scale;

    public unsafe void Execute(int index)
    {
        if (!activeFlags[index])
            return;

        int writeIndex = Interlocked.Increment(ref UnsafeUtility.AsRef<int>(NativeReferenceUnsafeUtility.GetUnsafePtr(counter))) - 1;
        Vector3 scaleVec = new Vector3(scale, scale, scale);
        Vector3 dir = (Vector3)directions[index];
        Quaternion rot = dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir) : Quaternion.identity;
        matrices[writeIndex] = Matrix4x4.TRS((Vector3)positions[index], rot, scaleVec);
    }
}
