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

        // ポーズまたはレベルアップから復帰した場合は初期化せず、状態を継続する（タイマー・敵・カメラはそのまま）
        GameMode prev = context.GetPreviousMode();
        if (prev == GameMode.Pause || prev == GameMode.LevelUp)
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

        // レベルアップ可能ならレベルアップステートへ
        if (context.PlayerCanLevelUp)
        {
            context.ChangeGameMode(GameMode.LevelUp);
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
        // ポーズまたはレベルアップへ遷移するときはステータス・タイマーは非表示にしない（そのまま表示）
        GameMode? next = context.GetNextMode();
        if (next == GameMode.Pause || next == GameMode.LevelUp)
            return;
        context.UIManager?.HideStatus();
        context.UIManager?.HideCountdownTimer();
    }
}
