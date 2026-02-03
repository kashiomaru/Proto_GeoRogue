using UnityEngine;

/// <summary>
/// 通常敵 1 種類分の設定を定義する ScriptableObject。
/// 見た目・ステータス・スポーンを保持し、StageData から複数参照できる。
/// </summary>
[CreateAssetMenu(fileName = "Enemy_00", menuName = "Geo Rogue/Enemy Data", order = 1)]
public class EnemyData : ScriptableObject
{
    [Header("Appearance")]
    [SerializeField] private Mesh mesh;
    [SerializeField] private Material material;
    [SerializeField] private Vector3 scale = Vector3.one;

    [Header("Stats")]
    [SerializeField] private float speed = 4f;
    [SerializeField] private float maxHp = 1f;
    [SerializeField] private float damageRadius = 1f;
    [Tooltip("弾との当たり判定に使う半径。敵の見た目の大きさに合わせて設定する。")]
    [SerializeField] private float collisionRadius = 1f;

    [Header("Spawn")]
    [Tooltip("出現数。0 のときは適用側で既存値を使う想定")]
    [SerializeField] private int spawnCount = 10;

    public Mesh Mesh => mesh;
    public Material Material => material;
    public Vector3 Scale => scale;
    public float Speed => speed;
    public float MaxHp => maxHp;
    public float DamageRadius => damageRadius;
    public float CollisionRadius => collisionRadius;
    public int SpawnCount => spawnCount;
}
