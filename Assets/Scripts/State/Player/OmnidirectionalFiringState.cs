using UnityEngine;

/// <summary>
/// 発射モード「Omnidirectional（全方向等分割）」：360度を弾数で等分割した方向に、プレイヤー位置から発射する。
/// 配置・方向の計算はこのステートで行い、Player.SpawnPlayerBullet で 1 発ずつ生成する。
/// </summary>
public class OmnidirectionalFiringState : IState<Player>
{
    public void OnEnter(Player context) { }

    public void OnUpdate(Player context)
    {
        if (context.TryConsumeFireInterval() == false)
        {
            return;
        }

        int countPerShot = context.GetBulletCountPerShot();
        Vector3 basePos = context.CachedTransform.position;
        float angleStep = 360f / countPerShot;

        for (int i = 0; i < countPerShot; i++)
        {
            // 0度をプレイヤーの forward に合わせ、そこから等分割（Y軸回転）
            float angleDeg = i * angleStep;
            Vector3 direction = Quaternion.AngleAxis(angleDeg, Vector3.up) * Vector3.forward;
            // ワールド方向に変換（プレイヤーの向きを基準にする場合は baseRot * direction）
            Vector3 worldDir = context.CachedTransform.rotation * direction;
            context.SpawnPlayerBullet(basePos, worldDir);
        }
    }

    public void OnExit(Player context) { }
}
