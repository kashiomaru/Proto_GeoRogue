using UnityEngine;
using System.Collections.Generic;

public class LevelUpManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private UIManager uiManager; // UIManagerへの参照
    [SerializeField] private Player player; // アップグレードパラメータはすべて Player で保持

    // 抽選テーブル（本来はScriptableObjectやCSVからロード推奨）
    private List<UpgradeData> _upgradeDatabase = new List<UpgradeData>()
    {
        new UpgradeData { type = UpgradeType.FireRateUp, title = "Fire Rate" },
        new UpgradeData { type = UpgradeType.BulletSpeedUp, title = "Bullet Speed" },
        new UpgradeData { type = UpgradeType.MoveSpeedUp, title = "Move Speed" },
        new UpgradeData { type = UpgradeType.MagnetRange, title = "Magnet" },
        new UpgradeData { type = UpgradeType.MultiShot, title = "Multi Shot" },
    };

    // 外部（GameManager）から経験値が溜まったら呼ばれる（互換性のため残す）
    public void ShowLevelUpOptions()
    {
        // ランダムに3つ選出（重複なし）
        List<UpgradeData> selectedOptions = SelectRandomUpgrades(3);
        
        // UIManagerに表示を委譲
        uiManager?.ShowLevelUpOptions(selectedOptions, OnUpgradeSelected);
    }
    
    // ランダムなアップグレードオプションを取得（UIManager用）
    public List<UpgradeData> GetRandomUpgrades(int count)
    {
        return SelectRandomUpgrades(count);
    }
    
    // アップグレードを適用（UIManager用）
    public void ApplyUpgrade(UpgradeType type)
    {
        ApplyUpgradeEffect(type);
    }
    
    private List<UpgradeData> SelectRandomUpgrades(int count)
    {
        List<UpgradeData> deck = new List<UpgradeData>(_upgradeDatabase);
        List<UpgradeData> selected = new List<UpgradeData>();
        
        for (int i = 0; i < count && deck.Count > 0; i++)
        {
            int randIndex = Random.Range(0, deck.Count);
            UpgradeData data = deck[randIndex];
            deck.RemoveAt(randIndex); // 選んだものはリストから消す（重複防止）
            selected.Add(data);
        }
        
        return selected;
    }

    private void OnUpgradeSelected(UpgradeType type)
    {
        // 効果を適用
        ApplyUpgradeEffect(type);
    }

    private void ApplyUpgradeEffect(UpgradeType type)
    {
        if (player == null)
        {
            return;
        }

        switch (type)
        {
            case UpgradeType.FireRateUp:
                player.SetFireRate(player.GetFireRate() * 0.9f); // 10%短縮
                break;
            case UpgradeType.BulletSpeedUp:
                player.SetBulletSpeed(player.GetBulletSpeed() + 5f);
                break;
            case UpgradeType.MoveSpeedUp:
                player.SetMoveSpeed(player.GetMoveSpeed() + 1f);
                break;
            case UpgradeType.MagnetRange:
                player.SetMagnetDist(player.GetMagnetDist() + 2.0f);
                break;
            case UpgradeType.MultiShot:
                player.SetBulletCountPerShot(player.GetBulletCountPerShot() + 1);
                break;
        }
    }
}
