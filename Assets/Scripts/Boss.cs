using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;

/// <summary>
/// プレイヤーに近づき、接触範囲内でダメージを与えるボス。挙動の基本形。
/// </summary>
public class Boss : BossBase
{
    protected override void UpdateBehavior(float3 targetPos, NativeQueue<int> playerDamageQueue)
    {
        float3 pos = transform.position;
        // プレイヤーがブーストで浮いていても、ボスは地面（XZ平面）上のみ移動する
        float3 targetFlat = targetPos;
        targetFlat.y = pos.y;
        float distSq = math.distancesq(pos, targetFlat);
        float damageRadiusSq = damageRadius * damageRadius;

        LookAtPlayer(targetPos);

        if (distSq <= damageRadiusSq)
        {
            if (playerDamageQueue.IsCreated)
                playerDamageQueue.Enqueue(damageAmount);
        }
        else
        {
            float3 dir = targetFlat - pos;
            dir.y = 0f;
            if (math.lengthsq(dir) > 0.0001f)
            {
                dir = math.normalize(dir);
                float3 newPos = pos + dir * speed * Time.deltaTime;
                newPos.y = pos.y;
                transform.position = newPos;
            }
        }
    }
}
