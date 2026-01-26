using UnityEngine;

/// <summary>
/// Bossモードのステート
/// ボス戦の状態
/// </summary>
public class BossGameState : GameStateBase
{
    public override void OnEnter(GameManager context)
    {
        // ボスを生成（プレイヤーの位置と方向を渡す）
        if (context.EnemyManager != null && context.PlayerTransform != null)
        {
            Vector3 playerPosition = context.PlayerTransform.position;
            Vector3 playerForward = context.PlayerTransform.forward;
            
            context.EnemyManager.SpawnBoss(playerPosition, playerForward);
        }
        
        // カメラをインデックス0から1に切り替え
        if (context.CameraManager != null)
        {
            // ボスのTransformをLookAtConstraintのターゲットに設定
            GameObject boss = context.EnemyManager?.GetCurrentBoss();
            if (boss != null)
            {
                context.CameraManager.SwitchCamera(1);
                context.CameraManager.SetBossLookAtTarget(boss.transform);
            }
        }
        
        // UIのタイマーを非表示
        if (context.UIManager != null)
        {
            context.UIManager.HideCountdownTimer();
        }
    }
    
    public override void OnUpdate(GameManager context)
    {
        // ボスと弾の当たり判定
        if (context.EnemyManager != null)
        {
            context.CheckBossBulletCollision();
        }
    }
    
    public override void OnExit(GameManager context)
    {
        // 終了時の処理（必要に応じて）
    }
}
