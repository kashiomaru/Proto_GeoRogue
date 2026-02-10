using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>敵弾 1 発分のスポーンリクエスト。Job からキューに積み、メインスレッドで BulletManager.SpawnEnemyBullet に渡す。</summary>
public struct EnemyBulletSpawnRequest
{
    public float3 position;
    public float3 direction;
    public float speed;
    public int damage;
    public float lifeTime;

    public EnemyBulletSpawnRequest(float3 pos, float3 dir, float spd, int dmg, float life)
    {
        position = pos;
        direction = dir;
        speed = spd;
        damage = dmg;
        lifeTime = life;
    }
}

/// <summary>
/// 弾を撃つ敵について発射タイマーを進め、間隔が来たら発射リクエストをキューに Enqueue する Job。
/// 実際の弾生成はメインスレッドでキューをドレインして BulletManager.SpawnEnemyBullet を呼ぶ。
/// </summary>
[BurstCompile]
public struct EnemyBulletFireJob : IJobParallelFor
{
    public float interval;
    public float deltaTime;
    public float speed;
    public int damage;
    public float lifeTime;
    public int countPerShot;

    public NativeArray<float3> positions;
    public NativeArray<float3> directions;
    [ReadOnly] public NativeArray<bool> active;
    public NativeArray<float> fireTimers;
    [ReadOnly] public NativeArray<quaternion> spreadRotations;

    public NativeQueue<EnemyBulletSpawnRequest>.ParallelWriter spawnQueue;

    public void Execute(int i)
    {
        if (!active[i])
            return;

        fireTimers[i] += deltaTime;
        if (fireTimers[i] < interval)
            return;

        fireTimers[i] = 0f;

        float3 pos = positions[i];
        float3 baseDir = directions[i];
        if (math.lengthsq(baseDir) < 0.0001f)
            baseDir = new float3(0f, 0f, 1f);

        for (int j = 0; j < countPerShot; j++)
        {
            float3 dir = math.rotate(spreadRotations[j], baseDir);
            if (math.lengthsq(dir) > 0.0001f)
                dir = math.normalize(dir);
            spawnQueue.Enqueue(new EnemyBulletSpawnRequest(pos, dir, speed, damage, lifeTime));
        }
    }
}
