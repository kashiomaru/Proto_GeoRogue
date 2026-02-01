using UnityEngine;

/// <summary>
/// ステージ構成を定義する ScriptableObject。
/// 通常敵は EnemyData を複数参照し、ボス Prefab とステージメタを保持する。
/// </summary>
[CreateAssetMenu(fileName = "Stage_01", menuName = "Proto GeoRogue/Stage Data", order = 0)]
public class StageData : ScriptableObject
{
    [Header("Stage Info")]
    [Tooltip("ステージ表示名（UI 用）。未使用の場合は空でよい")]
    [SerializeField] private string stageDisplayName;
    [Tooltip("カウントダウン時間（秒）。0 以下なら GameManager のデフォルトを使用")]
    [SerializeField] private float countdownDuration = 60f;

    [Header("Normal Enemy")]
    [Tooltip("このステージで出現させる敵データのリスト。先頭 1 件が現在の適用対象（複数対応は今後拡張）")]
    [SerializeField] private EnemyData[] enemyDatas;

    [Header("Boss")]
    [SerializeField] private GameObject bossPrefab;
    [SerializeField] private float bossSpawnDistance = 20f;

    // --- プロパティ（読み取り専用、他スクリプトから参照用）---

    public string StageDisplayName => stageDisplayName;
    public float CountdownDuration => countdownDuration;

    /// <summary>敵設定のリスト。複数設定可能（現状は先頭 1 件を適用）。</summary>
    public EnemyData[] EnemyDatas => enemyDatas;

    /// <summary>適用対象の先頭敵設定。null のときは通常敵なし。</summary>
    public EnemyData FirstEnemyData =>
        (enemyDatas != null && enemyDatas.Length > 0) ? enemyDatas[0] : null;

    public Mesh EnemyMesh => FirstEnemyData?.Mesh;
    public Material EnemyMaterial => FirstEnemyData?.Material;
    public Vector3 EnemyScale => FirstEnemyData != null ? FirstEnemyData.Scale : Vector3.one;
    public float EnemySpeed => FirstEnemyData?.Speed ?? 0f;
    public float EnemyMaxHp => FirstEnemyData?.MaxHp ?? 0f;
    public float EnemyFlashDuration => FirstEnemyData?.FlashDuration ?? 0f;
    public float EnemyDamageRadius => FirstEnemyData?.DamageRadius ?? 0f;
    public float CellSize => FirstEnemyData?.CellSize ?? 0f;
    public int SpawnCount => FirstEnemyData?.SpawnCount ?? 0;

    public GameObject BossPrefab => bossPrefab;
    public float BossSpawnDistance => bossSpawnDistance;
}
