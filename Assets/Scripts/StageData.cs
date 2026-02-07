using UnityEngine;

/// <summary>
/// ステージ構成を定義する ScriptableObject。
/// 通常敵は最大 5 種類の EnemyData をスロットで保持し、ボス Prefab とステージメタを保持する。
/// </summary>
[CreateAssetMenu(fileName = "Stage_00", menuName = "Geo Rogue/Stage Data", order = 0)]
public class StageData : ScriptableObject
{
    /// <summary>敵データの最大スロット数。</summary>
    public const int MaxEnemyDataSlots = 5;

    [Header("Stage Info")]
    [Tooltip("ステージ表示名（UI 用）。未使用の場合は空でよい")]
    [SerializeField] private string stageDisplayName;
    [Tooltip("カウントダウン時間（秒）。0 以下なら GameManager のデフォルトを使用")]
    [SerializeField] private float countdownDuration = 60f;

    [Header("Normal Enemy")]
    [Tooltip("このステージで出現させる敵データ（最大5種類）。先頭の非nullが現在の適用対象（複数対応は今後拡張）")]
    [SerializeField] private EnemyData enemyData0;
    [SerializeField] private EnemyData enemyData1;
    [SerializeField] private EnemyData enemyData2;
    [SerializeField] private EnemyData enemyData3;
    [SerializeField] private EnemyData enemyData4;

    [Header("Boss")]
    [SerializeField] private GameObject bossPrefab;

    // --- プロパティ（読み取り専用、他スクリプトから参照用）---

    public string StageDisplayName => stageDisplayName;
    public float CountdownDuration => countdownDuration;

    /// <summary>指定スロットの敵データを取得する（0～4）。範囲外は null。</summary>
    public EnemyData GetEnemyData(int index)
    {
        switch (index)
        {
            case 0: return enemyData0;
            case 1: return enemyData1;
            case 2: return enemyData2;
            case 3: return enemyData3;
            case 4: return enemyData4;
            default: return null;
        }
    }

    /// <summary>先頭の非null敵設定。すべて null のときは null。</summary>
    public EnemyData FirstEnemyData
    {
        get
        {
            for (int i = 0; i < MaxEnemyDataSlots; i++)
            {
                var data = GetEnemyData(i);
                if (data != null) return data;
            }
            return null;
        }
    }

    public Mesh EnemyMesh => FirstEnemyData?.Mesh;
    public Material EnemyMaterial => FirstEnemyData?.Material;
    public Vector3 EnemyScale => FirstEnemyData != null ? FirstEnemyData.Scale : Vector3.one;
    public float EnemySpeed => FirstEnemyData?.Speed ?? 0f;
    public float EnemyMaxHp => FirstEnemyData?.MaxHp ?? 0f;
    public float EnemyDamageRadius => FirstEnemyData?.DamageRadius ?? 0f;
    /// <summary>空間分割のセルサイズ（当たり半径から算出。FirstEnemyData が null のときは 0）。</summary>
    public float CellSize => FirstEnemyData != null ? Mathf.Max(0.5f, FirstEnemyData.CollisionRadius * 0.51f) : 0f;
    public int SpawnCount => FirstEnemyData?.SpawnCount ?? 0;

    public GameObject BossPrefab => bossPrefab;
}
