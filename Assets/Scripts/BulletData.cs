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

    [Header("Appearance")]
    [Tooltip("弾のメッシュ。未設定時は BulletManager / RenderManager のデフォルトを使用")]
    [SerializeField] private Mesh mesh;
    [Tooltip("弾のマテリアル。未設定時は BulletManager / RenderManager のデフォルトを使用")]
    [SerializeField] private Material material;

    [Header("Stats")]
    [Tooltip("弾の飛ぶ速度")]
    [SerializeField] private float speed = 15f;
    [Tooltip("プレイヤーに与えるダメージ（敵・ボス弾の場合）")]
    [SerializeField] private float damage = 1f;
    [Tooltip("弾の生存時間（秒）。この時間経過で弾は消える")]
    [SerializeField] private float lifeTime = 3f;

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
    public float Speed => speed;
    public float Damage => damage;
    public float LifeTime => lifeTime;
    public float FireInterval => Mathf.Max(0.01f, fireInterval);
    public int CountPerShot => Mathf.Max(1, countPerShot);
    public float SpreadAngle => spreadAngle;
}
