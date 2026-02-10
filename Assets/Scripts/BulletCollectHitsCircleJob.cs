using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>指定円（中心・半径の2乗）と当たった弾のダメージを NativeQueue に集め、該当弾を無効化する Job。クリティカル判定あり。</summary>
[BurstCompile]
public struct BulletCollectHitsCircleJob : IJobParallelFor
{
    public float3 center;
    public float radiusSq;

    [ReadOnly] public NativeArray<float3> positions;
    public int damage;
    public float criticalChance;
    public float criticalMultiplier;
    public uint seed;

    public NativeArray<bool> active;
    public NativeQueue<HitDamageInfo>.ParallelWriter damageOut;

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
        int finalDamage = damage;
        bool isCritical = false;
        if (criticalChance > 1e-6f)
        {
            var rng = Random.CreateFromIndex(seed + (uint)index);
            if (rng.NextFloat() < criticalChance)
            {
                finalDamage = (int)(damage * criticalMultiplier);
                isCritical = true;
            }
        }
        damageOut.Enqueue(new HitDamageInfo { damage = finalDamage, isCritical = isCritical });
        active[index] = false;
    }
}
