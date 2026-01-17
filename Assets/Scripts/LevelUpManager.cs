using UnityEngine;
using UnityEngine.UI;
using TMPro; // TextMeshProを使う場合
using System.Collections.Generic;

public class LevelUpManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject levelUpPanel;
    [SerializeField] private Button[] optionButtons;
    [SerializeField] private TextMeshProUGUI[] optionTexts; // ボタン内のテキスト参照

    [Header("Game References")]
    [SerializeField] private GameManager gameManager; // パラメータ変更用
    [SerializeField] private Player player; // プレイヤー参照
    [SerializeField] private GemManager gemManager; // GemManager参照

    // 抽選テーブル（本来はScriptableObjectやCSVからロード推奨）
    private List<UpgradeData> _upgradeDatabase = new List<UpgradeData>()
    {
        new UpgradeData { type = UpgradeType.FireRateUp, title = "Fire Rate", description = "Shoot faster!" },
        new UpgradeData { type = UpgradeType.BulletSpeedUp, title = "Bullet Speed", description = "Faster bullets!" },
        new UpgradeData { type = UpgradeType.MoveSpeedUp, title = "Move Speed", description = "Run faster!" },
        new UpgradeData { type = UpgradeType.MagnetRange, title = "Magnet", description = "Larger collection range!" },
        new UpgradeData { type = UpgradeType.MultiShot, title = "Multi Shot", description = "Shoot multiple bullets!" },
    };

    void Start()
    {
        // パネルを隠す
        if (levelUpPanel != null)
        {
            levelUpPanel.SetActive(false);
        }
    }

    // 外部（Player）から経験値が溜まったら呼ばれる
    public void ShowLevelUpOptions()
    {
        // 1. ゲームを止める
        Time.timeScale = 0f; 
        if (levelUpPanel != null)
        {
            levelUpPanel.SetActive(true);
        }

        // 2. ランダムに3つ選出（重複なし）
        // 簡易的なシャッフルロジック
        List<UpgradeData> deck = new List<UpgradeData>(_upgradeDatabase);
        
        for (int i = 0; i < optionButtons.Length && i < optionTexts.Length; i++)
        {
            if (deck.Count == 0) break;

            int randIndex = Random.Range(0, deck.Count);
            UpgradeData data = deck[randIndex];
            deck.RemoveAt(randIndex); // 選んだものはリストから消す（重複防止）

            // UI反映
            int index = i; // クロージャ用
            if (optionTexts[i] != null)
            {
                optionTexts[i].text = $"{data.title}\n<size=70%>{data.description}</size>";
            }
            
            // ボタンのクリックイベントをリセットして登録
            if (optionButtons[i] != null)
            {
                optionButtons[i].onClick.RemoveAllListeners();
                optionButtons[i].onClick.AddListener(() => OnUpgradeSelected(data.type));
            }
        }
    }

    private void OnUpgradeSelected(UpgradeType type)
    {
        // 3. 効果を適用（GameManagerのパラメータをいじる）
        ApplyUpgradeEffect(type);

        // 4. ゲーム再開
        if (levelUpPanel != null)
        {
            levelUpPanel.SetActive(false);
        }
        Time.timeScale = 1f;
    }

    private void ApplyUpgradeEffect(UpgradeType type)
    {
        // ここでGameManagerのpublic変数などを直接書き換えます
        // ※GameManager側にはメソッドを用意して呼ぶのが行儀が良いです
        
        switch (type)
        {
            case UpgradeType.FireRateUp:
                if (gameManager != null)
                {
                    gameManager.SetFireRate(gameManager.GetFireRate() * 0.9f); // 10%短縮
                }
                break;
            case UpgradeType.BulletSpeedUp:
                if (gameManager != null)
                {
                    gameManager.SetBulletSpeed(gameManager.GetBulletSpeed() + 5f);
                }
                break;
            case UpgradeType.MoveSpeedUp:
                if (player != null)
                {
                    player.SetMoveSpeed(player.GetMoveSpeed() + 1f);
                }
                break;
            case UpgradeType.MagnetRange:
                if (gemManager != null)
                {
                    gemManager.SetMagnetDist(gemManager.GetMagnetDist() + 2.0f);
                }
                break;
                
            case UpgradeType.MultiShot:
                if (gameManager != null)
                {
                    gameManager.SetBulletCountPerShot(gameManager.GetBulletCountPerShot() + 1);
                }
                break;
        }
        
        Debug.Log($"Upgraded: {type}");
    }
}
