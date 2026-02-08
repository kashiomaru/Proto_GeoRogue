using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// 弾と1つのターゲットグループとの当たり判定のみを行う Job。ターゲットグループごとに1本スケジュールする。
/// 弾の移動は BulletMoveJob で事前に完了している前提。
/// </summary>
[BurstCompile]
public struct BulletCollideJob : IJobParallelFor
{
    public float cellSize;
    /// <summary>弾との当たり判定に使うターゲットの半径の二乗（このグループ共通）。</summary>
    public float targetCollisionRadiusSq;

    [ReadOnly] public NativeParallelMultiHashMap<int, int> spatialMap;
    [ReadOnly] public NativeArray<float3> targetPositions;
    [ReadOnly] public NativeArray<float3> bulletPositions;
    [ReadOnly] public NativeArray<bool> targetActive;

    public NativeArray<bool> bulletActive;

    public int bulletDamage;

    public NativeQueue<BulletDamageInfo>.ParallelWriter damageQueue;

    public void Execute(int index)
    {
        if (bulletActive[index] == false)
        {
            return;
        }

        float3 pos = bulletPositions[index];

        int2 gridCoords = new int2((int)math.floor(pos.x / cellSize), (int)math.floor(pos.z / cellSize));

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                int2 neighborCoords = gridCoords + new int2(x, y);
                int hash = (int)math.hash(neighborCoords);

                if (spatialMap.TryGetFirstValue(hash, out int targetIndex, out var iterator))
                {
                    do
                    {
                        if (targetActive[targetIndex] == false)
                        {
                            continue;
                        }

                        float3 enemyPos = targetPositions[targetIndex];
                        float distSq = math.distancesq(pos, enemyPos);

                        if (distSq < targetCollisionRadiusSq)
                        {
                            damageQueue.Enqueue(new BulletDamageInfo(enemyPos, bulletDamage, targetIndex));
                            bulletActive[index] = false;
                            return;
                        }

                    } while (spatialMap.TryGetNextValue(out targetIndex, ref iterator));
                }
            }
        }
    }
}
