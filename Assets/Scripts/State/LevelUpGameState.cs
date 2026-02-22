using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// レベルアップ選択モードのステート
/// Normal / Boss 中に経験値が溜まると進入。オプション選択でポーズ前のモードに復帰する。
/// </summary>
public class LevelUpGameState : GameStateBase
{
    public override bool IsPlaying => false;

    public override void OnEnter(GameManager context)
    {
        Time.timeScale = 0f;

        if (context.LevelUpManager == null || context.UIManager == null)
        {
            context.ChangeGameMode(context.GetPreviousMode());
            return;
        }

        List<UpgradeData> options = context.LevelUpManager.GetRandomUpgrades(3);
        context.UIManager.ShowLevelUp(options, (UpgradeType type) =>
        {
            context.LevelUpManager?.ApplyUpgrade(type);
            context.Player?.LevelUp();
            context.ChangeGameMode(context.GetPreviousMode());
        });
    }

    public override void OnUpdate(GameManager context)
    {
        // 何もしない（オプション選択で復帰）
    }

    public override void OnExit(GameManager context)
    {
        context.UIManager?.HideLevelUp();
        Time.timeScale = 1f;
    }
}
