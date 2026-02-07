using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;

/// <summary>
/// プレイヤーに近づき、接触範囲内でダメージを与えるボス。挙動の基本形。
/// </summary>
public class Boss : BossBase
{
    protected override void UpdateBehavior(float deltaTime, NativeQueue<int> playerDamageQueue)
    {
        float3 pos = transform.position;
        float3 target = (float3)getPlayerPosition();
        float distSq = math.distancesq(pos, target);
        float damageRadiusSq = damageRadius * damageRadius;

        LookAtPlayer();

        if (distSq <= damageRadiusSq)
        {
            if (playerDamageQueue.IsCreated)
                playerDamageQueue.Enqueue(damageAmount);
        }
        else
        {
            float3 dir = math.normalize(target - pos);
            transform.position = pos + dir * speed * deltaTime;
        }
    }
}
