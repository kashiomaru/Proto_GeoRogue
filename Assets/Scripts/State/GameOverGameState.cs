/// <summary>
/// ゲームオーバーモードのステート
/// ゲームオーバー画面の状態
/// </summary>
public class GameOverGameState : GameStateBase
{
    public override void OnEnter(GameManager context)
    {
        UnityEngine.Time.timeScale = 0f;
        context.UIManager?.ShowGameOver(() => context.ResetGame());
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
            UnityEngine.Time.timeScale = 1f;
        }
        context.UIManager?.HideGameOver();
    }
}
