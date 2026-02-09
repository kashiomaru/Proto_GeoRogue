/// <summary>
/// プレイヤーの入力モード。ステートマシンで切り替える。
/// </summary>
public enum PlayerInputMode
{
    /// <summary>WASD＋矢印キー＋スペース＋Shift で移動・回転</summary>
    KeyboardWASD,

    /// <summary>WASDで移動、マウスカーソル方向へ回転</summary>
    KeyboardWASD_MouseLook,

    /// <summary>WASDで移動、十字キー（上下左右）で回転</summary>
    KeyboardWASD_ArrowLook,

    /// <summary>WASDで移動、一番近い敵の方向へ自動回転</summary>
    KeyboardWASD_Auto,
}
