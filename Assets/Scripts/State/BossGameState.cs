using UnityEngine;

/// <summary>
/// Bossモードのステート
/// ボス戦の状態
/// </summary>
public class BossGameState : GameStateBase
{
    public override bool IsPlaying => true;

    public override void OnEnter(GameManager context)
    {
        UnityEngine.Time.timeScale = 1f;
        context.UIManager?.ShowStatus();
        
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
    }
    
    public override void OnUpdate(GameManager context)
    {
        // ボスと弾の当たり判定
        if (context.EnemyManager != null)
        {
            context.CheckBossBulletCollision();
            
            // ボス撃破時はゲームクリアへ遷移
            var bossObj = context.EnemyManager.GetCurrentBoss();
            if (bossObj == null)
            {
                context.ChangeGameMode(GameMode.GameClear);
                return;
            }
            var boss = bossObj.GetComponent<Boss>();
            if (boss != null && boss.IsDead)
            {
                context.ChangeGameMode(GameMode.GameClear);
            }
        }
    }
    
    public override void OnExit(GameManager context)
    {
    }
}
