using UnityEngine;

/// <summary>
/// タイトルモードのステート
/// タイトル画面の状態
/// </summary>
public class TitleGameState : GameStateBase
{
    public override void OnEnter(GameManager context)
    {
        Time.timeScale = 0f;
        context.UIManager?.HideStatus();
        context.UIManager?.ShowTitle(() => context.ChangeGameMode(GameMode.Normal));
    }

    public override void OnUpdate(GameManager context)
    {
        // 何もしない
    }
    
    public override void OnExit(GameManager context)
    {
        // 次のステートがプレイ中（Normal/Boss）のときだけ時間を再開する
        if (context.NextGameMode is GameMode.Normal or GameMode.Boss)
        {
            Time.timeScale = 1f;
        }
        context.UIManager?.HideTitle();
    }
}
