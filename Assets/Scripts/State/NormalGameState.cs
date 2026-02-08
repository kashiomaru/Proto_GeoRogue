/// <summary>
/// Normalモードのステート
/// 通常のゲームプレイ状態
/// </summary>
public class NormalGameState : GameStateBase
{
    public override bool IsPlaying => true;

    public override void OnEnter(GameManager context)
    {
        UnityEngine.Time.timeScale = 1f;
        context.PrepareForNormalStage();
        context.SwitchCamera(0, immediate: true);
        context.UIManager?.ShowStatus();
        context.UIManager?.ShowCountdownTimer();

        string stageName = context.GetCurrentStageData()?.StageDisplayName ?? "";
        context.UIManager?.ShowStageName(stageName);
    }
    
    public override void OnUpdate(GameManager context)
    {
        // カウントダウンタイマーの更新
        if (context.GetCountdownTime() > 0f)
        {
            context.UpdateCountdownTimer();
            
            // タイマーがゼロになった瞬間の分岐
            if (context.IsCountdownFinished())
            {
                var stage = context.GetCurrentStageData();
                if (stage != null && stage.BossPrefab != null)
                {
                    context.ChangeGameMode(GameMode.Boss);
                }
                else
                {
                    context.ChangeGameMode(GameMode.GameClear);
                }
            }
        }
    }
    
    public override void OnExit(GameManager context)
    {
        context.UIManager?.HideStatus();
    }
}
