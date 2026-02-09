using UnityEngine;

/// <summary>
/// 発射モード「Straight（直線型）」：プレイヤー向きに同じ方向で発射し、弾は横並びに一定の隙間で並ぶ。
/// 配置・位置の計算はこのステートで行い、Player.SpawnPlayerBullet で 1 発ずつ生成する。
/// </summary>
public class StraightFiringState : IState<Player>
{
    /// <summary>隣り合う弾の中心間隔に加える隙間（接しない程度）。</summary>
    private const float GapBetweenBullets = 0.1f;

    public void OnEnter(Player context) { }

    public void OnUpdate(Player context)
    {
        if (context.TryConsumeFireInterval() == false)
        {
            return;
        }

        int countPerShot = context.GetBulletCountPerShot();
        Vector3 forward = context.CachedTransform.forward;
        Vector3 right = context.CachedTransform.right;
        right.y = 0f;
        if (right.sqrMagnitude < 0.0001f)
        {
            right = Vector3.right;
        }
        else
        {
            right.Normalize();
        }

        // 隣り合う弾の中心間隔 = 弾のスケール + 一定の隙間（接しない程度）
        float spacing = context.BulletData.Scale + GapBetweenBullets;
        Vector3 basePos = context.CachedTransform.position;

        for (int i = 0; i < countPerShot; i++)
        {
            float offset = (i - (countPerShot - 1) * 0.5f) * spacing;
            Vector3 spawnPos = basePos + right * offset;
            context.SpawnPlayerBullet(spawnPos, forward);
        }
    }

    public void OnExit(Player context) { }
}
