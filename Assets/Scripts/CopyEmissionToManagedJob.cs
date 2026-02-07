using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

/// <summary>NativeArray の Emission を managed 配列へコピーする。SetVectorArray 用。</summary>
[BurstCompile]
public unsafe struct CopyEmissionToManagedJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<Vector4> emissionColors;

    [NativeDisableUnsafePtrRestriction]
    public Vector4* targetColors;

    public void Execute(int index)
    {
        targetColors[index] = emissionColors[index];
    }
}
