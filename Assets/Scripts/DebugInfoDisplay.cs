using UnityEngine;
using TMPro;

/// <summary>
/// デバッグ情報を画面上に TextMeshPro で表示する。
/// GameManager と Player の参照を持ち、ゲームステート・ステージ・レベル・HP・その他ステータスを表示する。
/// </summary>
public class DebugInfoDisplay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private Player player;
    [SerializeField] private TextMeshProUGUI debugText;

    [Header("Display")]
    [Tooltip("デバッグ情報の表示をオンにする。インスペクターで切り替え可能。")]
    [SerializeField] private bool showDebugInfo = false;

    void Update()
    {
        if (debugText == null)
            return;

        // トグルキーが押されたら表示を反転
        if (!showDebugInfo)
        {
            debugText.enabled = false;
            return;
        }

        debugText.enabled = true;
        var sb = new System.Text.StringBuilder();

        // ゲームステート
        sb.AppendLine($"<b>State</b> {GetGameStateLabel()}");

        // ステージ（1始まりで表示）
        int stageIndex = gameManager != null ? gameManager.CurrentStageIndex : 0;
        int totalStages = gameManager != null ? gameManager.TotalStageCount : 0;
        sb.AppendLine($"<b>Stage</b> {stageIndex + 1}/{totalStages}");

        if (player != null)
        {
            // プレイヤーレベル
            sb.AppendLine($"<b>Level</b> {player.CurrentLevel}/{player.MaxLevel}");

            // プレイヤーHP
            sb.AppendLine($"<b>HP</b> {player.CurrentHp}/{player.MaxHp}");

            // 経験値（現在/次のレベル必要）
            sb.AppendLine($"<b>EXP</b> {player.CurrentExp}/{player.NextLevelExp}");
            // 成長倍率（ジェム取得時の経験値倍率）
            sb.AppendLine($"<b>Growth</b> x{player.GrowthMultiplier}");

            // その他ステータス
            sb.AppendLine($"<b>MoveSpeed</b>: {player.GetMoveSpeed():F1}");
            sb.AppendLine($"<b>BulletSpeed</b>: {player.GetBulletSpeed():F1}");
            sb.AppendLine($"<b>FireRate</b>: {player.GetFireRate():F2}");
            sb.AppendLine($"<b>BulletCount</b>: {player.GetBulletCountPerShot()}");
            sb.AppendLine($"<b>Damage</b>: {player.GetBulletDamage()}");
            sb.AppendLine($"<b>CritRate</b>: {player.GetCriticalChance():P0}");
            sb.AppendLine($"<b>CritMult</b>: {player.GetCriticalMultiplier():F1}x");
            sb.AppendLine($"<b>Magnet</b>: {player.GetMagnetDist():F1}");
            sb.AppendLine($"<b>BulletLife</b>: {player.GetBulletLifeTimeBase():F1} (+{player.GetBulletLifeTimeBonus():F1})s");
        }
        else
        {
            sb.AppendLine("<i>Player not assigned</i>");
        }

        debugText.text = sb.ToString();
    }

    private string GetGameStateLabel()
    {
        if (gameManager == null)
            return "—";
        switch (gameManager.CurrentMode)
        {
            case GameMode.None: return "None";
            case GameMode.Title: return "Title";
            case GameMode.Normal: return "Normal";
            case GameMode.Boss: return "Boss";
            case GameMode.Pause: return "Pause";
            case GameMode.LevelUp: return "LevelUp";
            case GameMode.GameClear: return "GameClear";
            case GameMode.GameOver: return "GameOver";
            default: return gameManager.CurrentMode.ToString();
        }
    }
}
