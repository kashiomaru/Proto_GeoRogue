using UnityEngine;
using UnityEngine.InputSystem;
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
        UnityEngine.Time.timeScale = 1f;

        // ポーズから復帰した場合は再初期化せず、状態を継続する（ボス・弾・カメラはそのまま）
        if (context.GetPreviousMode() == GameMode.Pause)
            return;

        // カメラブレンド中は時間を止める（ブレンド完了後に再開）
        UnityEngine.Time.timeScale = 0f;
        context.ResetBullets();
        context.UIManager?.ShowStatus();
        context.UIManager?.ShowBossHpBar();

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
            context.CameraManager?.SwitchCamera(CameraMode.TPS);
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
        // Esc でポーズへ
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            context.ChangeGameMode(GameMode.Pause);
            return;
        }

        // ボスと弾の当たり判定
        if (context.EnemyManager != null)
        {
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
        // ポーズへ遷移するときはボスHPバー等は非表示にしない（そのまま表示）
        if (context.GetNextMode() == GameMode.Pause)
            return;
        context.UIManager?.HideBossHpBar();
    }
}
