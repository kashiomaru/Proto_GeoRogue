using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;

/// <summary>ジェムの active / flying フラグを一括で false に初期化する Job。</summary>
[BurstCompile]
public struct GemInitFlagsJob : IJobParallelFor
{
    public NativeArray<bool> active;
    public NativeArray<bool> flying;

    public void Execute(int index)
    {
        active[index] = false;
        flying[index] = false;
    }
}
