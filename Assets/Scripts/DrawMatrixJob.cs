using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;

/// <summary>
/// アクティブな要素の座標（と任意の方向）を詰めた Matrix4x4 配列を出力する。
/// ジェム・弾など描画用に RenderManager へ渡すバッファを埋める。
/// directions がゼロベクトルの場合は identity 回転、それ以外は LookRotation を使用。
/// IJobParallelFor + NativeReference + Interlocked で並列に詰める。
/// </summary>
[BurstCompile]
public struct DrawMatrixJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<float3> positions;
    [ReadOnly] public NativeArray<float3> directions;
    [ReadOnly] public NativeArray<bool> activeFlags;

    [NativeDisableParallelForRestriction]
    public NativeArray<Matrix4x4> matrices;

    /// <summary>書き込みインデックス用。Schedule 前に 0 にリセットすること。Interlocked で排他制御。</summary>
    [NativeDisableParallelForRestriction]
    public NativeReference<int> counter;

    /// <summary>描画スケール（XYZ 別指定可）。</summary>
    public Vector3 scale;

    public unsafe void Execute(int index)
    {
        if (!activeFlags[index])
            return;

        Assert.IsFalse(scale == Vector3.zero, "DrawMatrixJob.scale must not be zero.");
        Vector3 dir = (Vector3)directions[index];
        Assert.IsTrue(dir.sqrMagnitude > 0.0001f, "Direction must not be zero.");
        int writeIndex = Interlocked.Increment(ref UnsafeUtility.AsRef<int>(NativeReferenceUnsafeUtility.GetUnsafePtr(counter))) - 1;
        Quaternion rot = Quaternion.LookRotation(dir);
        matrices[writeIndex] = Matrix4x4.TRS((Vector3)positions[index], rot, scale);
    }
}
