using UnityEngine;
using Cysharp.Threading.Tasks;

/// <summary>
/// Bossモードのステート
/// ボス戦の状態
/// </summary>
public class BossGameState : GameStateBase
{
    public override bool IsPlaying => true;

    public override void OnEnter(GameManager context)
    {
        // カメラブレンド中は時間を止める（ブレンド完了後に再開）
        UnityEngine.Time.timeScale = 0f;
        context.ResetBullets();
        context.UIManager?.ShowStatus();

        // 現在ステージのボス設定を適用してから生成
        StageData stage = context.GetCurrentStageData();
        if (stage != null && context.EnemyManager != null)
        {
            context.EnemyManager.ApplyBossConfig(stage);
        }

        // ボスを生成（プレイヤーの位置と方向を渡す）
        if (context.EnemyManager != null && context.PlayerTransform != null)
        {
            Vector3 playerPosition = context.PlayerTransform.position;
            Vector3 playerForward = context.PlayerTransform.forward;
            context.EnemyManager.SpawnBoss(playerPosition, playerForward);
        }

        // カメラをインデックス0から1に切り替え（ボスのTransformをLookAtConstraintのターゲットに設定）
        GameObject boss = context.EnemyManager?.GetCurrentBoss();
        if (boss != null)
        {
            context.CameraManager?.SwitchCamera(1);
            context.CameraManager?.SetBossLookAtTarget(boss.transform);
        }

        RunAfterBossCameraBlendAsync(context).Forget();
    }

    private async UniTaskVoid RunAfterBossCameraBlendAsync(GameManager context)
    {
        if (context?.CameraManager != null)
        {
            await context.CameraManager.WaitForBlendCompleteAsync();
        }
        UnityEngine.Time.timeScale = 1f;
    }
    
    public override void OnUpdate(GameManager context)
    {
        // ボスと弾の当たり判定
        if (context.EnemyManager != null)
        {
            context.CheckBossBulletCollision();
            
            // ボス撃破時はゲームクリアへ遷移
            var boss = context.EnemyManager.GetCurrentBossComponent();
            if (boss == null || boss.IsDead)
            {
                context.ChangeGameMode(GameMode.GameClear);
            }
        }
    }
    
    public override void OnExit(GameManager context)
    {
    }
}
