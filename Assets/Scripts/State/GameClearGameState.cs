/// <summary>
/// ゲームクリアモードのステート
/// ゲームクリア画面の状態
/// </summary>
public class GameClearGameState : GameStateBase
{
    public override bool IsPlaying => false;

    public override void OnEnter(GameManager context)
    {
        UnityEngine.Time.timeScale = 0f;
        context.ResetDamageText();

        // 次のステージがあれば Normal に戻す（ボス撃破 → 次ステージの通常プレイ）
        if (context.HasNextStage())
        {
            context.AdvanceToNextStage();
            context.ChangeGameMode(GameMode.Normal);
            return;
        }

        context.UIManager?.ShowGameClear(() =>
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
        context.UIManager?.HideGameClear();
    }
}
