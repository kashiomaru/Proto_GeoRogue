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
    public float targetCellSize;
    /// <summary>弾との当たり判定に使うターゲットの半径の二乗（このグループ共通）。</summary>
    public float targetCollisionRadiusSq;

    [ReadOnly] public NativeParallelMultiHashMap<int, int> targetSpatialMap;
    [ReadOnly] public NativeArray<float3> targetPositions;
    [ReadOnly] public NativeArray<float3> bulletPositions;
    [ReadOnly] public NativeArray<bool> targetActive;

    public NativeArray<bool> bulletActive;

    public int bulletDamage;

    public NativeQueue<BulletDamageInfo>.ParallelWriter targetDamageQueue;

    public void Execute(int index)
    {
        if (bulletActive[index] == false)
        {
            return;
        }

        float3 pos = bulletPositions[index];

        int2 gridCoords = new int2((int)math.floor(pos.x / targetCellSize), (int)math.floor(pos.z / targetCellSize));

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                int2 neighborCoords = gridCoords + new int2(x, y);
                int hash = (int)math.hash(neighborCoords);

                if (targetSpatialMap.TryGetFirstValue(hash, out int targetIndex, out var iterator))
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
                            targetDamageQueue.Enqueue(new BulletDamageInfo(enemyPos, bulletDamage, targetIndex));
                            bulletActive[index] = false;
                            return;
                        }

                    } while (targetSpatialMap.TryGetNextValue(out targetIndex, ref iterator));
                }
            }
        }
    }
}
