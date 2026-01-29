/// <summary>
/// タイトルモードのステート
/// タイトル画面の状態
/// </summary>
public class TitleGameState : GameStateBase
{
    public override void OnEnter(GameManager context)
    {
        context.UIManager?.ShowTitle();
    }
    
    public override void OnUpdate(GameManager context)
    {
        if (context.UIManager?.StartButton.onClick.GetPersistentEventCount() > 0)
        {
            context.ChangeGameMode(GameMode.Normal);
        }
    }
    
    public override void OnExit(GameManager context)
    {
        context.UIManager?.HideTitle();
    }
}
