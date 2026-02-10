// 強化の種類
public enum UpgradeType
{
    FireRateUp,    // 連射速度アップ
    BulletSpeedUp, // 弾速アップ
    MoveSpeedUp,   // 移動速度アップ
    MultiShot,     // 弾数増加（Way数を増やすなど）
    MagnetRange,   // 吸い寄せ範囲拡大
    DamageUp,      // ダメージアップ
    CriticalDamage,  // クリティカルダメージ（確率で倍率適用、獲得ごとに確率+10%）
    CriticalRate,    // クリティカル率（選択で+1%）
    BulletLifeTimeUp, // 弾の寿命（選択で+0.1秒）
    PlayerHpUp       // プレイヤーHP（最大HP+1、現在HP+1。減った分は回復しない）
}
