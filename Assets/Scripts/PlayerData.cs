using UnityEngine;

/// <summary>
/// プレイヤー 1 種類分の初期パラメータを定義する ScriptableObject。
/// 移動・体力・レベル・弾（BulletData）・ブーストなどを保持し、Player から参照する。
/// </summary>
[CreateAssetMenu(fileName = "PlayerData_Default", menuName = "Geo Rogue/Player Data", order = 0)]
public class PlayerData : ScriptableObject
{
    [Header("Bullet")]
    [Tooltip("プレイヤー弾の設定。未設定時は Player の内部デフォルトで弾を撃たないか、Resources から読み込むなど実装に依存。")]
    [SerializeField] private BulletData bulletData;

    [Header("Movement")]
    [SerializeField] private float initialMoveSpeed = 5f;
    [Tooltip("回転速度")]
    [SerializeField] private float rotationSpeed = 10f;
    [Tooltip("慣性の強さ。目標速度に近づくまでの目安時間（秒）。大きいほど入力やめても滑る")]
    [SerializeField] private float accelerationTime = 0.15f;
    [Tooltip("ゲーム開始時の入力モード")]
    [SerializeField] private PlayerInputMode initialInputMode = PlayerInputMode.KeyboardWASD;
    [Tooltip("ゲーム開始時の発射モード")]
    [SerializeField] private PlayerFiringMode initialFiringMode = PlayerFiringMode.Fan;

    [Header("Magnet")]
    [SerializeField] private float initialMagnetDist = 5f;

    [Header("Health")]
    [SerializeField] private int maxHp = 3;

    [Header("Level System")]
    [SerializeField] private int maxLevel = 99;
    [Tooltip("レベル2に上がるために必要な経験値（初期値）。Reset 時にもこの値に戻る")]
    [SerializeField] private int initialNextLevelExp = 6;
    [Tooltip("レベルアップごとに「次のレベル必要EXP」にかける倍率")]
    [SerializeField] private float nextLevelExpMultiplier = 1.15f;

    [Header("Boost Gauge")]
    [Tooltip("ブーストゲージの最大値（正規化なら1）")]
    [SerializeField] private float boostGaugeMax = 1f;
    [Tooltip("ブースト中のゲージ消費速度（/秒）")]
    [SerializeField] private float boostConsumeRate = 1f;
    [Tooltip("非ブースト時のゲージ回復速度（/秒）")]
    [SerializeField] private float boostRecoverRate = 0.25f;

    public BulletData BulletData => bulletData;
    public float InitialMoveSpeed => initialMoveSpeed;
    public float RotationSpeed => rotationSpeed;
    public float AccelerationTime => accelerationTime;
    public PlayerInputMode InitialInputMode => initialInputMode;
    public PlayerFiringMode InitialFiringMode => initialFiringMode;
    public float InitialMagnetDist => initialMagnetDist;
    public int MaxHp => maxHp;
    public int MaxLevel => maxLevel;
    public int InitialNextLevelExp => initialNextLevelExp;
    public float NextLevelExpMultiplier => nextLevelExpMultiplier;
    public float BoostGaugeMax => boostGaugeMax;
    public float BoostConsumeRate => boostConsumeRate;
    public float BoostRecoverRate => boostRecoverRate;
}
