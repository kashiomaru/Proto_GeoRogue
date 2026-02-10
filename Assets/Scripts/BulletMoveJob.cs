using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// 弾の移動とライフタイム減算を行う Job。カーブ値がある場合は進行方向を毎フレーム回転させる。1フレームに1回だけスケジュールする。
/// </summary>
[BurstCompile]
public struct BulletMoveJob : IJobParallelFor
{
    public float deltaTime;
    /// <summary>弾の進行方向を回転させる速度（度/秒）。0で直進。</summary>
    public float curveDegreesPerSecond;

    public NativeArray<float3> bulletPositions;
    public NativeArray<float3> bulletVelocities;
    public NativeArray<bool> bulletActive;
    public NativeArray<float> bulletLifeTime;

    public void Execute(int index)
    {
        if (bulletActive[index] == false)
        {
            return;
        }

        float life = bulletLifeTime[index] - deltaTime;
        bulletLifeTime[index] = life;

        if (life <= 0)
        {
            bulletActive[index] = false;
            return;
        }

        float3 velocity = bulletVelocities[index];
        if (math.abs(curveDegreesPerSecond) > 1e-6f)
        {
            float angleRad = math.radians(curveDegreesPerSecond * deltaTime);
            velocity = math.rotate(quaternion.Euler(0f, angleRad, 0f), velocity);
            bulletVelocities[index] = velocity;
        }

        float3 pos = bulletPositions[index];
        pos += velocity * deltaTime;
        bulletPositions[index] = pos;
    }
}
