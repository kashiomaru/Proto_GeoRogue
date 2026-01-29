/// <summary>
/// ゲームクリアモードのステート
/// ゲームクリア画面の状態
/// </summary>
public class GameClearGameState : GameStateBase
{
    public override void OnEnter(GameManager context)
    {
        context.UIManager?.ShowGameClear();
    }
    
    public override void OnUpdate(GameManager context)
    {
        // todo: OKボタンでタイトル画面に戻る
    }
    
    public override void OnExit(GameManager context)
    {
        context.UIManager?.HideGameClear();
    }
}
