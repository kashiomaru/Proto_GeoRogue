using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// 1グループ内でプレイヤーに最も近い敵の座標と距離の2乗を求める Job。
/// グループごとに1本スケジュールし、結果を resultPositions[groupIndex] / resultDistancesSq[groupIndex] に書き込む。
/// アクティブな敵が1体もいない場合は resultDistancesSq に float.MaxValue を書き込む。
/// </summary>
[BurstCompile]
public struct FindClosestEnemyJob : IJob
{
    public int groupIndex;
    public float3 playerPos;
    [ReadOnly] public NativeArray<float3> positions;
    [ReadOnly] public NativeArray<bool> active;
    public int count;

    public NativeArray<float3> resultPositions;
    public NativeArray<float> resultDistancesSq;

    public void Execute()
    {
        float bestSq = float.MaxValue;
        float3 bestPos = default;

        for (int i = 0; i < count; i++)
        {
            if (!active[i])
                continue;

            float3 p = positions[i];
            float sq = math.distancesq(p, playerPos);
            if (sq < bestSq)
            {
                bestSq = sq;
                bestPos = p;
            }
        }

        resultPositions[groupIndex] = bestPos;
        resultDistancesSq[groupIndex] = bestSq;
    }
}
