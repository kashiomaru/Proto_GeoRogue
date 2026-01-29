/// <summary>
/// ゲームクリアモードのステート
/// ゲームクリア画面の状態
/// </summary>
public class GameClearGameState : GameStateBase
{
    public override void OnEnter(GameManager context)
    {
        context.UIManager?.ShowGameClear();
        context.UIManager?.GameClearOkButton.onClick.AddListener(() => context.ChangeGameMode(GameMode.Title));
    }

    public override void OnUpdate(GameManager context)
    {
        // 何もしない
    }
    
    public override void OnExit(GameManager context)
    {
        context.UIManager?.HideGameClear();
    }
}
