using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>敵グループの active / directions / fireTimers / flashTimers を一括で初期化する Job。</summary>
[BurstCompile]
public struct EnemyGroupInitJob : IJobParallelFor
{
    public NativeArray<bool> active;
    public NativeArray<float3> directions;
    public NativeArray<float> fireTimers;
    public NativeArray<float> flashTimers;

    public void Execute(int index)
    {
        active[index] = false;
        directions[index] = new float3(0f, 0f, 1f);
        fireTimers[index] = 0f;
        flashTimers[index] = 0f;
    }
}
