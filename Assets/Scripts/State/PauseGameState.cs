using UnityEngine;

/// <summary>
/// ポーズモードのステート
/// Normal / Boss 中に Esc で進入。Continue でポーズ前のモードに復帰する。
/// </summary>
public class PauseGameState : GameStateBase
{
    public override bool IsPlaying => false;

    public override void OnEnter(GameManager context)
    {
        Time.timeScale = 0f;
        context.UIManager?.ShowPause(() =>
        {
            context.ChangeGameMode(context.GetPreviousMode());
        });
    }

    public override void OnUpdate(GameManager context)
    {
        // 何もしない（Continue ボタンで復帰）
    }

    public override void OnExit(GameManager context)
    {
        context.UIManager?.HidePause();
        Time.timeScale = 1f;
    }
}
