using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;

/// <summary>
/// プレイヤーに近づきつつ、一定間隔でプレイヤー方向に弾を発射するボス。
/// BossBase の bulletData をインスペクタで設定し、弾グループが作成されている場合に発射する。
/// </summary>
public class BossShooter : BossBase
{
    protected float _fireTimer;

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

    public override void ProcessFiring(float3 playerPos)
    {
        if (bulletData == null || _bulletHandler == null)
            return;

        _fireTimer += Time.deltaTime;
        float interval = bulletData.FireInterval;
        if (_fireTimer < interval)
            return;

        _fireTimer = 0f;

        Vector3 spawnPos = transform.position;
        int countPerShot = bulletData.CountPerShot;
        float spreadAngle = bulletData.SpreadAngle;
        float speed = bulletData.Speed;
        float lifeTime = bulletData.LifeTime;
        float directionRotation = bulletData.DirectionRotation;

        Vector3 toPlayer = (Vector3)playerPos - spawnPos;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.0001f)
            toPlayer = transform.forward;
        else
            toPlayer.Normalize();

        for (int i = 0; i < countPerShot; i++)
        {
            float spreadDeg = (countPerShot > 1)
                ? (-spreadAngle * (countPerShot - 1) * 0.5f + spreadAngle * i)
                : 0f;
            Vector3 dir = Quaternion.AngleAxis(spreadDeg, Vector3.up) * toPlayer;
            _bulletHandler.Spawn(spawnPos, dir, speed, lifeTime, directionRotation);
        }
    }
}
