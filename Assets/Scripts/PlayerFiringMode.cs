/// <summary>
/// プレイヤーの発射モード（弾の広がり方）。ステートマシンで切り替える。
/// </summary>
public enum PlayerFiringMode
{
    /// <summary>扇形に広がって発射する。</summary>
    Fan,

    /// <summary>直線型。プレイヤー向きに横並びで同方向に発射する。</summary>
    Straight,

    /// <summary>四方型（前後左右など）。将来実装。</summary>
    FourDirections,
}
