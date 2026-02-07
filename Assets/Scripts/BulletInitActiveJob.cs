using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;

/// <summary>弾の active フラグを一括で false に初期化する Job。</summary>
[BurstCompile]
public struct BulletInitActiveJob : IJobParallelFor
{
    public NativeArray<bool> active;

    public void Execute(int index)
    {
        active[index] = false;
    }
}
