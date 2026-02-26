using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 入力モード「CursorMove_AutoLook」：マウスカーソル位置へ移動、向きは一番近い敵の方向（ヨー＋ピッチ）。
/// 画面上のプレイヤーとマウスの距離に応じて移動速度が変わる。ブーストで上空にいるときも弾の向きが敵方向を向く。
/// </summary>
public class CursorMoveAutoLookInputState : IState<Player>
{
    /// <summary>この距離（ピクセル）以内は移動しない。</summary>
    private const float DeadZonePx = 10f;
    /// <summary>この距離（画面幅の割合）以上で最大速度。ピクセルに変換時に最小 100px を保証。</summary>
    private const float MaxDistanceScreenRatio = 0.15f;

    private Camera _cachedCamera;
    private UnityEngine.Plane _plane;
    private UnityEngine.Ray _ray;

    /// <summary>敵がいないときは前フレームの目標角度を維持する。</summary>
    private float _targetLookAngle;
    /// <summary>敵がいないときは前フレームの目標ピッチを維持する。</summary>
    private float _targetLookPitch;

    public void OnEnter(Player context)
    {
        _cachedCamera = context.GetPlayerCamera();
        _targetLookAngle = context.CachedTransform.eulerAngles.y;
        _targetLookPitch = context.CachedTransform.eulerAngles.x;
    }

    public void OnUpdate(Player context)
    {
        if (_cachedCamera == null) return;

        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;
        if (mouse == null) return;

        context.SetBoostInput(keyboard != null && keyboard.spaceKey.isPressed);

        Vector3 playerPos = context.CachedTransform.position;
        Vector2 mousePos = mouse.position.ReadValue();
        Vector3 playerScreenPos = _cachedCamera.WorldToScreenPoint(playerPos);
        float screenDistancePx = Vector2.Distance(mousePos, new Vector2(playerScreenPos.x, playerScreenPos.y));

        float maxDistancePx = Mathf.Max(100f, MaxDistanceScreenRatio * Screen.width);
        float speedScale = screenDistancePx <= DeadZonePx
            ? 0f
            : Mathf.Clamp01((screenDistancePx - DeadZonePx) / (maxDistancePx - DeadZonePx));

        _plane = new UnityEngine.Plane(Vector3.up, playerPos);
        _ray = _cachedCamera.ScreenPointToRay(mousePos);

        float horizontal = 0f;
        float vertical = 0f;
        if (_plane.Raycast(_ray, out float enter) && enter > 0f)
        {
            Vector3 hitPoint = _ray.GetPoint(enter);
            Vector3 moveDirWorld = hitPoint - playerPos;
            moveDirWorld.y = 0f;
            if (moveDirWorld.sqrMagnitude >= 0.0001f)
            {
                moveDirWorld.Normalize();
                float cameraY = _cachedCamera.transform.eulerAngles.y;
                Vector3 cameraSpace = Quaternion.Euler(0f, -cameraY, 0f) * moveDirWorld;
                horizontal = cameraSpace.x;
                vertical = cameraSpace.z;
            }
        }

        if (context.TryGetNearestEnemyPosition(out Vector3 enemyPos))
        {
            Vector3 dir = enemyPos - playerPos;
            float lenSq = dir.x * dir.x + dir.y * dir.y + dir.z * dir.z;
            if (lenSq >= 0.0001f)
            {
                Vector3 dirNorm = dir / Mathf.Sqrt(lenSq);
                _targetLookAngle = Mathf.Atan2(dirNorm.x, dirNorm.z) * Mathf.Rad2Deg;
                _targetLookPitch = -Mathf.Asin(Mathf.Clamp(dirNorm.y, -1f, 1f)) * Mathf.Rad2Deg;
            }
        }

        context.ApplyMovementInput(horizontal, vertical, false, false, _targetLookAngle, _targetLookPitch, speedScale);
    }

    public void OnExit(Player context)
    {
        _cachedCamera = null;
    }
}
