using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct EnemyMoveAndHashJob : IJobParallelFor
{
    public float deltaTime;
    public float3 target;
    public float speed;
    public float cellSize;
    public float damageRadius;

    [WriteOnly] public NativeParallelMultiHashMap<int, int>.ParallelWriter spatialMap;
    public NativeArray<float3> positions;
    public NativeArray<quaternion> rotations; // プレイヤー方向を向く回転（描画用）
    public NativeArray<bool> activeFlags;

    public NativeQueue<int>.ParallelWriter damageQueue;

    public void Execute(int index)
    {
        if (activeFlags[index] == false)
        {
            positions[index] = new float3(0, -1000, 0);
            rotations[index] = quaternion.identity;
            return;
        }

        float3 pos = positions[index];
        float3 dir = math.normalize(target - pos);
        pos += dir * speed * deltaTime;

        positions[index] = pos;

        // プレイヤー方向を向く回転（XZ 平面のみ）
        float3 toPlayer = target - pos;
        toPlayer.y = 0f;
        rotations[index] = math.lengthsq(toPlayer) > 0.0001f
            ? quaternion.LookRotationSafe(math.normalize(toPlayer), math.up())
            : quaternion.identity;

        float distSq = math.distancesq(pos, target);
        if (distSq < damageRadius * damageRadius)
        {
            damageQueue.Enqueue(1);
        }

        // --- 空間ハッシュ登録 ---
        // 座標をグリッド整数座標に変換
        int2 gridCoords = new int2((int)math.floor(pos.x / cellSize), (int)math.floor(pos.z / cellSize));
        int hash = (int)math.hash(gridCoords);
        spatialMap.Add(hash, index);
    }
}
