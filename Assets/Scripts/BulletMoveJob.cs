using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// 弾の移動とライフタイム減算のみを行う Job。1フレームに1回だけスケジュールする。
/// </summary>
[BurstCompile]
public struct BulletMoveJob : IJobParallelFor
{
    public float deltaTime;

    public NativeArray<float3> bulletPositions;
    [ReadOnly] public NativeArray<float3> bulletVelocities;
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

        float3 pos = bulletPositions[index];
        float3 velocity = bulletVelocities[index];
        pos += velocity * deltaTime;
        bulletPositions[index] = pos;
    }
}
