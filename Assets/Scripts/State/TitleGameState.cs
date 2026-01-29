using UnityEngine;

/// <summary>
/// タイトルモードのステート
/// タイトル画面の状態
/// </summary>
public class TitleGameState : GameStateBase
{
    public override void OnEnter(GameManager context)
    {
        context.UIManager?.ShowTitle();

        context.UIManager?.StartButton.onClick.AddListener(() => context.ChangeGameMode(GameMode.Normal));
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
