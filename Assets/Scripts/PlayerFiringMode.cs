/// <summary>
/// プレイヤーの発射モード（弾の広がり方）。ステートマシンで切り替える。
/// </summary>
public enum PlayerFiringMode
{
    /// <summary>扇形に広がって発射する。</summary>
    Fan,

    /// <summary>直線型（1方向または並列）。将来実装。</summary>
    Straight,

    /// <summary>四方型（前後左右など）。将来実装。</summary>
    FourDirections,
}
