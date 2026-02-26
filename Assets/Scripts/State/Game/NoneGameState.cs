/// <summary>
/// Noneモードのステート
/// 弾も敵も出ない状態
/// </summary>
public class NoneGameState : GameStateBase
{
    public override bool IsPlaying => false;

    public override void OnEnter(GameManager context)
    {
        // UIのタイマーを非表示
        context.UIManager?.HideCountdownTimer();
    }

    public override void OnUpdate(GameManager context)
    {
        // Noneモードでは何も処理しない
    }

    public override void OnExit(GameManager context)
    {
        // 終了時の処理（必要に応じて）
    }
}
