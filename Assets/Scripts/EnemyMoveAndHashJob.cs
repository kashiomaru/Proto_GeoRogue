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
    /// <summary>プレイヤーに接触したときに与えるダメージ。</summary>
    public int damageAmount;

    [WriteOnly] public NativeParallelMultiHashMap<int, int>.ParallelWriter spatialMap;
    public NativeArray<float3> positions;
    /// <summary>敵の向き（前方ベクトル、正規化）。プレイヤー方向を向く。</summary>
    public NativeArray<float3> directions;
    public NativeArray<bool> activeFlags;

    public NativeQueue<int>.ParallelWriter damageQueue;

    public void Execute(int index)
    {
        if (activeFlags[index] == false)
        {
            positions[index] = new float3(0, -1000, 0);
            directions[index] = new float3(0f, 0f, 1f);
            return;
        }

        float3 pos = positions[index];
        // プレイヤーがブーストで浮いていても、敵は地面（XZ平面）上のみ移動する
        float3 targetFlat = target;
        targetFlat.y = pos.y;
        float3 dir = targetFlat - pos;
        dir.y = 0f;
        if (math.lengthsq(dir) > 0.0001f)
        {
            dir = math.normalize(dir);
            pos += dir * speed * deltaTime;
        }
        pos.y = 0f;
        positions[index] = pos;

        // プレイヤー方向を向く（XZ 平面のみ）
        float3 toPlayer = target - pos;
        toPlayer.y = 0f;
        directions[index] = math.lengthsq(toPlayer) > 0.0001f
            ? math.normalize(toPlayer)
            : new float3(0f, 0f, 1f);

        // 接触ダメージはXZ平面の距離のみで判定（プレイヤーが浮いているときは当たらない）
        float3 targetXZ = target;
        targetXZ.y = pos.y;
        float distSq = math.distancesq(pos, targetXZ);
        if (distSq < damageRadius * damageRadius)
        {
            damageQueue.Enqueue(damageAmount);
        }

        // --- 空間ハッシュ登録 ---
        // 座標をグリッド整数座標に変換
        int2 gridCoords = new int2((int)math.floor(pos.x / cellSize), (int)math.floor(pos.z / cellSize));
        int hash = (int)math.hash(gridCoords);
        spatialMap.Add(hash, index);
    }
}
