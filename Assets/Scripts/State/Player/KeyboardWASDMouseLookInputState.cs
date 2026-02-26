using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 入力モード「KeyboardWASD_MouseLook」：WASDで移動、マウスカーソル方向へ回転。
/// 画面上のプレイヤー位置とマウス位置から向く方向を決める。
/// </summary>
public class KeyboardWASDMouseLookInputState : IState<Player>
{
    private Camera _cachedCamera;
    private UnityEngine.Plane _plane = new ();
    private UnityEngine.Ray _ray;

    public void OnEnter(Player context)
    {
        _cachedCamera = context.GetPlayerCamera();
    }

    public void OnUpdate(Player context)
    {
        if (_cachedCamera == null) return;

        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;
        if (mouse == null || keyboard == null) return;

        context.SetBoostInput(keyboard.spaceKey.isPressed);

        Vector3 playerPos = context.CachedTransform.position;
        _plane.SetNormalAndPosition(Vector3.up, playerPos);
        _ray = _cachedCamera.ScreenPointToRay(mouse.position.ReadValue());

        float lookAngleDeg;
        if (_plane.Raycast(_ray, out float enter) && enter > 0f)
        {
            Vector3 hitPoint = _ray.GetPoint(enter);
            Vector3 dir = (hitPoint - playerPos).normalized;
            lookAngleDeg = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        }
        else
        {
            lookAngleDeg = context.CachedTransform.eulerAngles.y;
        }

        float horizontal = 0f;
        float vertical = 0f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) horizontal -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) horizontal += 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) vertical += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) vertical -= 1f;

        context.ApplyMovementInput(horizontal, vertical, false, false, lookAngleDeg);
    }

    public void OnExit(Player context)
    {
        _cachedCamera = null;
    }
}
