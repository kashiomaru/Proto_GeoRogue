using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct GemMagnetJob : IJobParallelFor
{
    public float deltaTime;
    public float3 playerPos;
    public float magnetDistSq;
    public float moveSpeed;

    public NativeArray<float3> positions;
    public NativeArray<bool> activeFlags;
    public NativeArray<bool> flyingFlags;
    
    // 回収されたジェムを記録するキュー（並列書き込み用）
    // 各ジェムごとに1を追加して、メインスレッドでカウントする
    public NativeQueue<int>.ParallelWriter collectedGemQueue;

    public void Execute(int index)
    {
        if (activeFlags[index] == false)
        {
            return;
        }

        float3 currentPos = positions[index];
        float distSq = math.distancesq(currentPos, playerPos);

        // 既に吸い寄せ中、または磁石範囲内なら
        bool isFlying = flyingFlags[index];
        
        if (isFlying || distSq < magnetDistSq)
        {
            // 吸い寄せモードON
            flyingFlags[index] = true;

            // プレイヤーに向かって移動
            float3 dir = math.normalize(playerPos - currentPos);
            // 加速させると気持ちいいが、まずは等速で
            float3 newPos = currentPos + (dir * moveSpeed * deltaTime);
            positions[index] = newPos;

            // プレイヤーに到達（回収）
            if (math.distancesq(newPos, playerPos) < 1.0f) // 半径1.0以内
            {
                // まだアクティブな場合のみ処理（重複防止）
                if (activeFlags[index])
                {
                    activeFlags[index] = false; // 消滅
                    flyingFlags[index] = false;
                    
                    // 回収されたジェムをキューに追加（メインスレッドでカウントするため）
                    collectedGemQueue.Enqueue(1);
                }
            }
        }
        // まだ吸い寄せられていないなら、その場に留まる（何もしない）
    }
}
