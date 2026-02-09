/// <summary>
/// プレイヤーの入力モード。ステートマシンで切り替える。
/// </summary>
public enum PlayerInputMode
{
    /// <summary>WASD＋矢印キー＋スペース＋Shift で移動・回転</summary>
    KeyboardWASD,

    /// <summary>WASDで移動、マウスカーソル方向へ回転</summary>
    KeyboardWASD_MouseLook,
}
