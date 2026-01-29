/// <summary>
/// ゲームクリアモードのステート
/// ゲームクリア画面の状態
/// </summary>
public class GameClearGameState : GameStateBase
{
    public override void OnEnter(GameManager context)
    {
        // UIのタイマーを非表示
        if (context.UIManager != null)
        {
            context.UIManager.HideCountdownTimer();
        }
    }
    
    public override void OnUpdate(GameManager context)
    {
        // ゲームクリアでは何も処理しない（リトライ等はUIなどで処理）
    }
    
    public override void OnExit(GameManager context)
    {
        // 終了時の処理（必要に応じて）
    }
}
