/// <summary>
/// プレイヤーの発射モード（弾の広がり方）。ステートマシンで切り替える。
/// </summary>
public enum PlayerFiringMode
{
    /// <summary>扇形に広がって発射する。</summary>
    Fan,

    /// <summary>直線型。プレイヤー向きに横並びで同方向に発射する。</summary>
    Straight,

    /// <summary>全方向等分割。360度を弾数で等分割した方向に発射する。</summary>
    Omnidirectional,
}
