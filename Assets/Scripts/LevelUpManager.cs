using UnityEngine;
using System.Collections.Generic;

public class LevelUpManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private UIManager uiManager; // UIManagerへの参照
    [SerializeField] private Player player; // アップグレードパラメータはすべて Player で保持

    [Header("抽選テーブル")]
    [Tooltip("weight: 0=出現しない、大きいほど出現しやすい。インスペクターで編集可。")]
    [SerializeField] private List<UpgradeData> _upgradeDatabase = new List<UpgradeData>()
    {
        new UpgradeData { type = UpgradeType.FireRateUp, title = "Fire Rate", weight = 1 },
        new UpgradeData { type = UpgradeType.BulletSpeedUp, title = "Bullet Speed", weight = 1 },
        new UpgradeData { type = UpgradeType.MoveSpeedUp, title = "Move Speed", weight = 1 },
        new UpgradeData { type = UpgradeType.MagnetRange, title = "Magnet", weight = 1 },
        new UpgradeData { type = UpgradeType.MultiShot, title = "Multi Shot", weight = 1 },
        new UpgradeData { type = UpgradeType.DamageUp, title = "Damage", weight = 1 },
        new UpgradeData { type = UpgradeType.CriticalDamage, title = "Critical Damage", weight = 1 },
        new UpgradeData { type = UpgradeType.CriticalRate, title = "Critical Rate", weight = 1 },
        new UpgradeData { type = UpgradeType.BulletLifeTimeUp, title = "Bullet Life", weight = 1 },
        new UpgradeData { type = UpgradeType.PlayerHpUp, title = "Player HP", weight = 1 },
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
        // weight > 0 のものだけ候補にする
        List<UpgradeData> candidates = new List<UpgradeData>();
        foreach (var data in _upgradeDatabase)
        {
            if (data.weight > 0)
                candidates.Add(data);
        }
        List<UpgradeData> selected = new List<UpgradeData>();

        for (int i = 0; i < count && candidates.Count > 0; i++)
        {
            int totalWeight = 0;
            foreach (var data in candidates)
                totalWeight += data.weight;
            if (totalWeight <= 0)
                break;

            int r = Random.Range(0, totalWeight);
            int pickIndex = 0;
            for (int j = 0; j < candidates.Count; j++)
            {
                r -= candidates[j].weight;
                if (r < 0)
                {
                    pickIndex = j;
                    break;
                }
            }
            selected.Add(candidates[pickIndex]);
            candidates.RemoveAt(pickIndex); // 重複防止
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
                player.SetFireRate(player.GetBaseFireRate() * 0.9f); // 基準間隔を10%短縮
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
            case UpgradeType.DamageUp:
                player.SetBulletDamage(player.GetBulletDamage() + 1);
                break;
            case UpgradeType.CriticalDamage:
                player.SetCriticalChance(player.GetCriticalChance() + 0.1f);
                break;
            case UpgradeType.CriticalRate:
                player.SetCriticalChance(player.GetCriticalChance() + 0.01f);
                break;
            case UpgradeType.BulletLifeTimeUp:
                player.SetBulletLifeTimeBonus(player.GetBulletLifeTimeBonus() + 0.1f);
                break;
            case UpgradeType.PlayerHpUp:
                player.AddMaxHpAndCurrent(1);
                break;
        }
    }
}
