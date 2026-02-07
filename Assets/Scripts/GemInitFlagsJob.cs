using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>ジェムの active / flying フラグと directions を一括で初期化する Job。</summary>
[BurstCompile]
public struct GemInitFlagsJob : IJobParallelFor
{
    public NativeArray<bool> active;
    public NativeArray<bool> flying;
    public NativeArray<float3> directions;

    public void Execute(int index)
    {
        active[index] = false;
        flying[index] = false;
        directions[index] = new float3(0f, 0f, 1f);
    }
}
