using UnityEngine;

[System.Serializable]
public struct UpgradeData
{
    public UpgradeType type;
    public string title;
    /// <summary>出現確率の重み。0=出現しない。大きいほど出現しやすい。</summary>
    public int weight;
}
