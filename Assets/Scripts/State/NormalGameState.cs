/// <summary>
/// Normalモードのステート
/// 通常のゲームプレイ状態
/// </summary>
public class NormalGameState : GameStateBase
{
    public override void OnEnter(GameManager context)
    {
        // UIのタイマーを表示
        if (context.UIManager != null)
        {
            context.UIManager.ShowCountdownTimer();
        }
    }
    
    public override void OnUpdate(GameManager context)
    {
        // カウントダウンタイマーの更新
        if (context.GetCountdownTime() > 0f)
        {
            context.UpdateCountdownTimer(UnityEngine.Time.deltaTime);
            
            // タイマーがゼロになった瞬間、ボスモードに切り替える
            if (context.IsCountdownFinished())
            {
                context.ChangeGameMode(GameMode.Boss);
            }
        }
    }
    
    public override void OnExit(GameManager context)
    {
        // 終了時の処理（必要に応じて）
    }
}
