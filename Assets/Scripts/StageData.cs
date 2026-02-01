using UnityEngine;

/// <summary>
/// ステージ構成を定義する ScriptableObject。
/// 通常敵の見た目・ステータスとボス Prefab を保持し、GameManager から順番に参照して構成する。
/// </summary>
[CreateAssetMenu(fileName = "Stage_01", menuName = "Proto GeoRogue/Stage Data", order = 0)]
public class StageData : ScriptableObject
{
    [Header("Stage Info")]
    [Tooltip("ステージ表示名（UI 用）。未使用の場合は空でよい")]
    [SerializeField] private string stageDisplayName;
    [Tooltip("カウントダウン時間（秒）。0 以下なら GameManager のデフォルトを使用")]
    [SerializeField] private float countdownDuration = 60f;

    [Header("Normal Enemy - Appearance")]
    [SerializeField] private Mesh enemyMesh;
    [SerializeField] private Material enemyMaterial;
    [SerializeField] private Vector3 enemyScale = Vector3.one;

    [Header("Normal Enemy - Stats")]
    [SerializeField] private float enemySpeed = 4f;
    [SerializeField] private float enemyMaxHp = 1f;
    [SerializeField] private float enemyFlashDuration = 0.1f;
    [SerializeField] private float enemyDamageRadius = 1f;
    [SerializeField] private float cellSize = 2f;

    [Header("Normal Enemy - Spawn")]
    [Tooltip("出現数。0 のときは適用側で既存値を使う想定")]
    [SerializeField] private int spawnCount = 10;
    [SerializeField] private float respawnDistance = 50f;
    [SerializeField] private float respawnMinRadius = 20f;
    [SerializeField] private float respawnMaxRadius = 30f;

    [Header("Boss")]
    [SerializeField] private GameObject bossPrefab;
    [SerializeField] private float bossSpawnDistance = 20f;

    // --- プロパティ（読み取り専用、他スクリプトから参照用）---

    public string StageDisplayName => stageDisplayName;
    public float CountdownDuration => countdownDuration;

    public Mesh EnemyMesh => enemyMesh;
    public Material EnemyMaterial => enemyMaterial;
    public Vector3 EnemyScale => enemyScale;

    public float EnemySpeed => enemySpeed;
    public float EnemyMaxHp => enemyMaxHp;
    public float EnemyFlashDuration => enemyFlashDuration;
    public float EnemyDamageRadius => enemyDamageRadius;
    public float CellSize => cellSize;

    public int SpawnCount => spawnCount;
    public float RespawnDistance => respawnDistance;
    public float RespawnMinRadius => respawnMinRadius;
    public float RespawnMaxRadius => respawnMaxRadius;

    public GameObject BossPrefab => bossPrefab;
    public float BossSpawnDistance => bossSpawnDistance;
}
