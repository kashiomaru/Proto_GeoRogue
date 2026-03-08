using UnityEngine;

/// <summary>
/// Scene View でプレイヤー・敵・ボス・弾のコリジョンサイズ（円）を Gizmos で表示する。
/// 空の GameObject にアタッチし、Player / EnemyManager / BulletManager をインスペクターで指定する。
/// </summary>
public class CollisionGizmoDrawer : MonoBehaviour
{
    [Header("Collision Gizmo")]
    [Tooltip("ON のとき Scene View にコリジョン円を描画する。")]
    [SerializeField] private bool drawCollisionGizmos = true;

    [Header("References")]
    [SerializeField] private Player player;
    [SerializeField] private EnemyManager enemyManager;
    [SerializeField] private BulletManager bulletManager;

    [Header("Gizmo Colors")]
    [SerializeField] private Color playerColor = new Color(0.2f, 0.9f, 0.2f, 0.8f);
    [SerializeField] private Color bossColor = new Color(0.9f, 0.2f, 0.2f, 0.8f);
    [SerializeField] private Color enemyColor = new Color(0.9f, 0.9f, 0.2f, 0.8f);
    [SerializeField] private Color bulletColor = new Color(0.2f, 0.8f, 0.9f, 0.6f);

    /// <summary>弾の Gizmo 半径は描画スケールの何倍で表示するか（当たり判定は点のため見た目目安）。</summary>
    [SerializeField] private float bulletRadiusScale = 0.5f;

    private void OnDrawGizmos()
    {
        if (!drawCollisionGizmos) return;
        DrawPlayer();
        if (Application.isPlaying)
        {
            DrawBoss();
            DrawEnemies();
            DrawBullets();
        }
    }

    private void DrawPlayer()
    {
        if (player == null) return;
        Gizmos.color = playerColor;
        Gizmos.DrawWireSphere(player.CachedTransform.position, player.CollisionRadius);
    }

    private void DrawBoss()
    {
        if (enemyManager == null) return;
        var boss = enemyManager.GetCurrentBossComponent();
        if (boss == null) return;
        Gizmos.color = bossColor;
        Gizmos.DrawWireSphere(boss.Position, boss.CollisionRadius);
    }

    private void DrawEnemies()
    {
        if (enemyManager == null) return;
        var groups = enemyManager.GetGroups();
        if (groups == null) return;

        Gizmos.color = enemyColor;
        foreach (var group in groups)
        {
            var positions = group.Positions;
            var active = group.Active;
            if (!positions.IsCreated || !active.IsCreated) continue;

            int n = positions.Length;
            float radius = group.CollisionRadius;
            for (int i = 0; i < n; i++)
            {
                if (active[i])
                    Gizmos.DrawWireSphere(positions[i], radius);
            }
        }
    }

    private void DrawBullets()
    {
        if (bulletManager == null) return;
        bulletManager.DrawBulletGizmos(bulletColor, bulletRadiusScale);
    }
}
