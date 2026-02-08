using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>指定円（中心・半径の2乗）と当たった弾のダメージを NativeQueue に集め、該当弾を無効化する Job。</summary>
[BurstCompile]
public struct BulletCollectHitsCircleJob : IJobParallelFor
{
    public float3 center;
    public float radiusSq;

    [ReadOnly] public NativeArray<float3> positions;
    public float damage;

    public NativeArray<bool> active;
    public NativeQueue<float>.ParallelWriter damageOut;

    public void Execute(int index)
    {
        if (active[index] == false)
        {
            return;
        }
        float distSq = math.distancesq(positions[index], center);
        if (distSq >= radiusSq)
        {
            return;
        }
        damageOut.Enqueue(damage);
        active[index] = false;
    }
}
