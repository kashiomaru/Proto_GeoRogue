using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// アクティブな敵の Matrix4x4 と Emission を詰めて出力する。フラッシュタイマーも deltaTime 分減算する。
/// matrix と emission は同じ writeIndex で対応させるため、1 本の Job で両方を出力している。
/// IJobParallelFor + NativeReference + Interlocked で並列に詰める。
/// </summary>
[BurstCompile]
public struct EnemyDrawMatrixJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<float3> positions;
    [ReadOnly] public NativeArray<quaternion> rotations;
    [ReadOnly] public NativeArray<bool> activeFlags;

    public NativeArray<float> flashTimers;
    public float deltaTime;

    [NativeDisableParallelForRestriction]
    public NativeArray<Matrix4x4> matrices;
    [NativeDisableParallelForRestriction]
    public NativeArray<Vector4> emissionColors;

    [NativeDisableParallelForRestriction]
    public NativeReference<int> counter;

    public float3 scale;
    public float flashIntensity;

    public unsafe void Execute(int index)
    {
        if (!activeFlags[index])
            return;

        if (flashTimers[index] > 0f)
            flashTimers[index] -= deltaTime;

        int w = Interlocked.Increment(ref UnsafeUtility.AsRef<int>(NativeReferenceUnsafeUtility.GetUnsafePtr(counter))) - 1;
        matrices[w] = Matrix4x4.TRS((Vector3)positions[index], (Quaternion)rotations[index], (Vector3)scale);
        emissionColors[w] = flashTimers[index] > 0f
            ? new Vector4(flashIntensity, flashIntensity, flashIntensity, 1f)
            : Vector4.zero;
    }
}
