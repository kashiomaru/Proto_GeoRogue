using UnityEngine.InputSystem;

/// <summary>
/// Normalモードのステート
/// 通常のゲームプレイ状態
/// </summary>
public class NormalGameState : GameStateBase
{
    public override bool IsPlaying => true;

    public override void OnEnter(GameManager context)
    {
        UnityEngine.Time.timeScale = 1f;

        // ポーズから復帰した場合は初期化せず、状態を継続する（タイマー・敵・カメラはそのまま）
        if (context.GetPreviousMode() == GameMode.Pause)
            return;

        context.PrepareForNormalStage();
        context.SwitchCamera(CameraMode.QuarterView, immediate: true);
        context.UIManager?.ShowStatus();
        context.UIManager?.ShowCountdownTimer();

        string stageName = context.GetCurrentStageData()?.StageDisplayName ?? "";
        context.UIManager?.ShowStageName(stageName);
    }
    
    public override void OnUpdate(GameManager context)
    {
        // Esc でポーズへ
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            context.ChangeGameMode(GameMode.Pause);
            return;
        }

        // カウントダウンタイマーの更新
        if (context.GetCountdownTime() > 0f)
        {
            context.UpdateCountdownTimer();
            
            // タイマーがゼロになった瞬間の分岐
            if (context.IsCountdownFinished())
            {
                var stage = context.GetCurrentStageData();
                if (stage != null && stage.BossPrefab != null)
                {
                    context.ChangeGameMode(GameMode.Boss);
                }
                else
                {
                    context.ChangeGameMode(GameMode.GameClear);
                }
            }
        }
    }
    
    public override void OnExit(GameManager context)
    {
        // ポーズへ遷移するときはステータス・タイマーは非表示にしない（そのまま表示）
        if (context.GetNextMode() == GameMode.Pause)
            return;
        context.UIManager?.HideStatus();
        context.UIManager?.HideCountdownTimer();
    }
}
