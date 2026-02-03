using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// 弾と1つの敵グループとの当たり判定のみを行う Job。敵グループごとに1本スケジュールする。
/// 弾の移動は BulletMoveJob で事前に完了している前提。
/// </summary>
[BurstCompile]
public struct BulletCollideJob : IJobParallelFor
{
    public float cellSize;
    /// <summary>弾との当たり判定に使う敵の半径（このグループ共通）。</summary>
    public float enemyCollisionRadius;

    [ReadOnly] public NativeParallelMultiHashMap<int, int> spatialMap;
    [ReadOnly] public NativeArray<float3> enemyPositions;
    [ReadOnly] public NativeArray<float3> bulletPositions;

    public NativeArray<bool> bulletActive;

    [NativeDisableParallelForRestriction]
    public NativeArray<bool> enemyActive;
    [NativeDisableParallelForRestriction]
    public NativeArray<float> enemyHp;

    public float bulletDamage;

    public NativeQueue<float3>.ParallelWriter deadEnemyPositions;
    public NativeQueue<EnemyDamageInfo>.ParallelWriter enemyDamageQueue;
    public NativeQueue<int>.ParallelWriter enemyFlashQueue;

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

                if (spatialMap.TryGetFirstValue(hash, out int enemyIndex, out var iterator))
                {
                    do
                    {
                        if (enemyActive[enemyIndex] == false)
                        {
                            continue;
                        }

                        float3 enemyPos = enemyPositions[enemyIndex];
                        float distSq = math.distancesq(pos, enemyPos);
                        float radiusSq = enemyCollisionRadius * enemyCollisionRadius;

                        if (distSq < radiusSq)
                        {
                            if (enemyActive[enemyIndex])
                            {
                                float currentHp = enemyHp[enemyIndex];
                                currentHp -= bulletDamage;
                                enemyHp[enemyIndex] = currentHp;

                                enemyDamageQueue.Enqueue(new EnemyDamageInfo(enemyPos, bulletDamage));
                                enemyFlashQueue.Enqueue(enemyIndex);

                                if (currentHp <= 0f)
                                {
                                    enemyActive[enemyIndex] = false;
                                    deadEnemyPositions.Enqueue(enemyPos);
                                }
                            }
                            bulletActive[index] = false;
                            return;
                        }

                    } while (spatialMap.TryGetNextValue(out enemyIndex, ref iterator));
                }
            }
        }
    }
}
