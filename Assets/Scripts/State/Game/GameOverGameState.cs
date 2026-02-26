/// <summary>
/// ゲームオーバーモードのステート
/// ゲームオーバー画面の状態
/// </summary>
public class GameOverGameState : GameStateBase
{
    public override bool IsPlaying => false;

    public override void OnEnter(GameManager context)
    {
        UnityEngine.Time.timeScale = 0f;
        context.UIManager?.ShowGameOver(() =>
        {
            context.ResetGameState();
            context.ChangeGameMode(GameMode.Title);
        });
    }

    public override void OnUpdate(GameManager context)
    {
        // 何もしない
    }

    public override void OnExit(GameManager context)
    {
        context.UIManager?.HideGameOver();
    }
}
