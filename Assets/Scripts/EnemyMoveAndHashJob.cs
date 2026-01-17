using UnityEngine.Jobs;
using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;

[BurstCompile]
public struct EnemyMoveAndHashJob : IJobParallelForTransform
{
    public float deltaTime;
    public float3 target;
    public float speed;
    public float cellSize;
    public float damageRadius; // プレイヤーとの当たり判定半径

    [WriteOnly] public NativeParallelMultiHashMap<int, int>.ParallelWriter spatialMap;
    public NativeArray<float3> positions;
    public NativeArray<bool> activeFlags;
    
    // プレイヤーへのダメージを記録するキュー（並列書き込み用）
    public NativeQueue<int>.ParallelWriter damageQueue;

    public void Execute(int index, TransformAccess transform)
    {
        if (!activeFlags[index])
        {
            // 死んでる敵は地獄へ送る（当たり判定除外）
            transform.position = new float3(0, -1000, 0);
            return; 
        }

        float3 pos = transform.position;
        float3 dir = math.normalize(target - pos);
        pos += dir * speed * deltaTime;
        
        // 分離（Boids）は省略していますが、前回のコードのSeparationを入れるとより良いです

        transform.position = pos;
        positions[index] = pos;

        // --- プレイヤーとの当たり判定 ---
        float distSq = math.distancesq(pos, target);
        if (distSq < damageRadius * damageRadius)
        {
            // プレイヤーにダメージを与える（ダメージ量は1とする）
            damageQueue.Enqueue(1);
        }

        // --- 空間ハッシュ登録 ---
        // 座標をグリッド整数座標に変換
        int2 gridCoords = new int2((int)math.floor(pos.x / cellSize), (int)math.floor(pos.z / cellSize));
        // ハッシュ値を生成（単純なビット演算でもOKですが、math.hashが便利）
        int hash = (int)math.hash(gridCoords);
        
        spatialMap.Add(hash, index);
    }
}
