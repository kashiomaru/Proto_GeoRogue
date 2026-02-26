using UnityEngine;

/// <summary>
/// タイトルモードのステート
/// タイトル画面の状態
/// </summary>
public class TitleGameState : GameStateBase
{
    public override bool IsPlaying => false;

    public override void OnEnter(GameManager context)
    {
        Time.timeScale = 0f;
        context.ResetPlayerAndCameraForTitle();
        context.UIManager?.HideStatus();
        context.UIManager?.ShowTitle(() => context.ChangeGameMode(GameMode.Normal));
    }

    public override void OnUpdate(GameManager context)
    {
        // 何もしない
    }

    public override void OnExit(GameManager context)
    {
        context.UIManager?.HideTitle();
    }
}
