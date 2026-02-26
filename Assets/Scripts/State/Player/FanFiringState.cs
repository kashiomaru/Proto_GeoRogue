using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 発射モード「Fan（扇形）」：プレイヤー前方を基準に、SpreadAngle で扇形に広がって発射する。
/// 配置・方向の計算はこのステートで行い、Player.SpawnPlayerBullet で 1 発ずつ生成する。
/// </summary>
public class FanFiringState : IState<Player>
{
    private readonly List<Vector3> _cachedShotDirections = new List<Vector3>();
    private int _lastBulletCountPerShot = -1;

    public void OnEnter(Player context) { }

    public void OnUpdate(Player context)
    {
        if (context.TryConsumeFireInterval() == false)
        {
            return;
        }

        int countPerShot = context.GetBulletCountPerShot();
        Vector3 baseDir = context.CachedTransform.forward;
        Vector3 basePos = context.CachedTransform.position;

        if (countPerShot != _lastBulletCountPerShot)
        {
            _cachedShotDirections.Clear();
            float spreadAngle = context.BulletData.SpreadAngle;
            for (int i = 0; i < countPerShot; i++)
            {
                float angle = countPerShot > 1
                    ? -spreadAngle * (countPerShot - 1) * 0.5f + (spreadAngle * i)
                    : 0f;
                Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward;
                _cachedShotDirections.Add(dir);
            }
            _lastBulletCountPerShot = countPerShot;
        }

        Quaternion baseRot = Quaternion.LookRotation(baseDir);
        for (int i = 0; i < countPerShot; i++)
        {
            Vector3 finalDir = baseRot * _cachedShotDirections[i];
            context.SpawnPlayerBullet(basePos, finalDir);
        }
    }

    public void OnExit(Player context) { }
}
