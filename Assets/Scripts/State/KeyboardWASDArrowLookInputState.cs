using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 入力モード「KeyboardWASD_ArrowLook」：WASDで移動、十字キーで向きを指定。
/// 十字キーの組み合わせで画面上の方向に向く（上＝画面上、左＋上＝左上など）。
/// </summary>
public class KeyboardWASDArrowLookInputState : IState<Player>
{
    /// <summary>十字キー未押下時の目標角度を保持（向きが飛ばないようにする）</summary>
    private float _targetLookAngle;

    public void OnEnter(Player context)
    {
        _targetLookAngle = context.CachedTransform.eulerAngles.y;
    }

    public void OnUpdate(Player context)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        float horizontal = 0f;
        float vertical = 0f;
        if (keyboard.aKey.isPressed) horizontal -= 1f;
        if (keyboard.dKey.isPressed) horizontal += 1f;
        if (keyboard.wKey.isPressed) vertical += 1f;
        if (keyboard.sKey.isPressed) vertical -= 1f;

        float cameraY = 0f;
        Camera cam = context.GetPlayerCamera();
        if (cam != null)
        {
            cameraY = cam.transform.eulerAngles.y;
        }

        // 十字キーを画面方向のベクトルに（上=+1, 右=+1）
        float arrowHorizontal = keyboard.rightArrowKey.isPressed ? 1f : (keyboard.leftArrowKey.isPressed ? -1f : 0f);
        float arrowVertical = keyboard.upArrowKey.isPressed ? 1f : (keyboard.downArrowKey.isPressed ? -1f : 0f);

        if (arrowHorizontal != 0f || arrowVertical != 0f)
        {
            // 画面上の方向（上=0°, 右=90°）をワールド角度に変換
            _targetLookAngle = cameraY + Mathf.Atan2(arrowHorizontal, arrowVertical) * Mathf.Rad2Deg;
        }

        context.ApplyMovementInput(horizontal, vertical, false, false, _targetLookAngle);
    }

    public void OnExit(Player context) { }
}
