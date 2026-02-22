using UnityEngine;

/// <summary>
/// 複数のステージデータをまとめる ScriptableObject。
/// GameManager に設定し、プレイ順（0, 1, 2...）でステージが進行する。
/// </summary>
[CreateAssetMenu(fileName = "StageSet_00", menuName = "Geo Rogue/Stage Set Data", order = 1)]
public class StageSetData : ScriptableObject
{
    [Header("Stages")]
    [Tooltip("ステージの並び。インデックス順にプレイされる。各ステージの通常敵・ボス・制限時間が適用される")]
    [SerializeField] private StageData[] stages = new StageData[0];

    /// <summary>ステージ数。</summary>
    public int StageCount => stages != null ? stages.Length : 0;

    /// <summary>指定インデックスのステージデータを取得する。範囲外は null。</summary>
    public StageData GetStage(int index)
    {
        if (stages == null || index < 0 || index >= stages.Length)
            return null;
        return stages[index];
    }

    /// <summary>ステージ配列（読み取り用）。GameManager の内部参照用。</summary>
    public StageData[] Stages => stages;
}
