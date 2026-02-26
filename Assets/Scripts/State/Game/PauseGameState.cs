using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ポーズモードのステート
/// Normal / Boss 中に Esc で進入。Continue ボタンまたは Esc でポーズ前のモードに復帰する。
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
        // Esc でゲームに戻る（Continue ボタンと同様）
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            context.ChangeGameMode(context.GetPreviousMode());
        }
    }

    public override void OnExit(GameManager context)
    {
        context.UIManager?.HidePause();
        Time.timeScale = 1f;
    }
}
