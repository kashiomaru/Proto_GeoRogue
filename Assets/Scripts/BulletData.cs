using UnityEngine;

/// <summary>
/// 弾の飛ぶ方向の種類。
/// </summary>
public enum BulletDirectionType
{
    /// <summary>発射元の向いている方向に飛ばす。</summary>
    Forward,
    /// <summary>発射時にプレイヤー方向へ向けて飛ばす。</summary>
    TowardPlayer
}

/// <summary>
/// 弾 1 種類分の設定を定義する ScriptableObject。
/// 敵・ボスが撃つ弾の速度・ダメージ・ライフタイムなどを保持し、EnemyData や Boss から参照できる。
/// </summary>
[CreateAssetMenu(fileName = "Bullet_00", menuName = "Geo Rogue/Bullet Data", order = 2)]
public class BulletData : ScriptableObject
{
    [Header("Direction")]
    [Tooltip("弾の飛ぶ方向。Forward=発射元の向き、TowardPlayer=プレイヤー方向")]
    [SerializeField] private BulletDirectionType directionType = BulletDirectionType.Forward;

    [Header("Stats")]
    [Tooltip("弾の飛ぶ速度")]
    [SerializeField] private float speed = 15f;
    [Tooltip("プレイヤーに与えるダメージ（敵・ボス弾の場合）")]
    [SerializeField] private int damage = 1;
    [Tooltip("弾の生存時間（秒）。この時間経過で弾は消える")]
    [SerializeField] private float lifeTime = 3f;

    [Header("Rendering")]
    [Tooltip("描画に使うメッシュ")]
    [SerializeField] private Mesh mesh;
    [Tooltip("描画に使うマテリアル")]
    [SerializeField] private Material material;
    [Tooltip("描画スケール（BulletGroup の scale に渡す）")]
    [SerializeField] private float scale = 0.5f;

    [Header("Critical")]
    [Tooltip("クリティカル発生確率（0～1）。例: 0 = 0%")]
    [SerializeField] [Range(0f, 1f)] private float criticalChance = 0f;
    [Tooltip("クリティカル時のダメージ倍率。例: 2 = 200%")]
    [SerializeField] [Min(1f)] private float criticalMultiplier = 2f;

    [Header("Shot Pattern")]
    [Tooltip("発射間隔（秒）。この間隔で弾を撃つ")]
    [SerializeField] private float fireInterval = 1f;
    [Tooltip("1 回の発射で出す弾数。1 のときは拡散角は無視される")]
    [SerializeField] private int countPerShot = 1;
    [Tooltip("複数発時のみ有効。弾同士の間の角度（度）")]
    [SerializeField] private float spreadAngle = 10f;

    public BulletDirectionType DirectionType => directionType;
    public Mesh Mesh => mesh;
    public Material Material => material;
    public float Scale => scale;
    public float Speed => speed;
    public int Damage => damage;
    public float LifeTime => lifeTime;
    public float FireInterval => Mathf.Max(0.01f, fireInterval);
    public int CountPerShot => Mathf.Max(1, countPerShot);
    public float SpreadAngle => spreadAngle;
    /// <summary>クリティカル発生確率（0～1）。BulletGroup の初期値に使う。</summary>
    public float CriticalChance => criticalChance;
    /// <summary>クリティカル時のダメージ倍率。BulletGroup の初期値に使う。</summary>
    public float CriticalMultiplier => Mathf.Max(1f, criticalMultiplier);
}
