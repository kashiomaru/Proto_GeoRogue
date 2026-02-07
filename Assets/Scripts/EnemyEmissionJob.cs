using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;

/// <summary>
/// アクティブな敵の Emission を詰め、フラッシュタイマーを deltaTime 分減算する。
/// DrawMatrixJob と同じ順でスロットを割り当てるため、matrices と emissionColors の並びが一致する。
/// IJobParallelFor + NativeReference + Interlocked。
/// </summary>
[BurstCompile]
public struct EnemyEmissionJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<bool> activeFlags;

    public NativeArray<float> flashTimers;
    public float deltaTime;

    [NativeDisableParallelForRestriction]
    public NativeArray<Vector4> emissionColors;

    [NativeDisableParallelForRestriction]
    public NativeReference<int> counter;

    public float flashIntensity;

    public unsafe void Execute(int index)
    {
        if (!activeFlags[index])
            return;

        if (flashTimers[index] > 0f)
            flashTimers[index] -= deltaTime;

        int w = Interlocked.Increment(ref UnsafeUtility.AsRef<int>(NativeReferenceUnsafeUtility.GetUnsafePtr(counter))) - 1;
        emissionColors[w] = flashTimers[index] > 0f
            ? new Vector4(flashIntensity, flashIntensity, flashIntensity, 1f)
            : Vector4.zero;
    }
}
