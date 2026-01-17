using UnityEngine;

[System.Serializable]
public struct UpgradeData
{
    public UpgradeType type;
    public string title;
    public string description;
    public Sprite icon; // アイコンがあれば
}
