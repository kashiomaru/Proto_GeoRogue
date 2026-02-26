using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 入力モード「KeyboardWASD_Auto」：WASDで移動、一番近い敵の方向へ自動で回転（ヨー＋ピッチ）。
/// ブーストで上空にいるときも弾の向きが敵方向を向く。
/// </summary>
public class KeyboardWASDAutoInputState : IState<Player>
{
    /// <summary>敵がいないときは前フレームの目標角度を維持する。</summary>
    private float _targetLookAngle;
    /// <summary>敵がいないときは前フレームの目標ピッチを維持する。</summary>
    private float _targetLookPitch;

    public void OnEnter(Player context)
    {
        _targetLookAngle = context.CachedTransform.eulerAngles.y;
        _targetLookPitch = context.CachedTransform.eulerAngles.x;
    }

    public void OnUpdate(Player context)
    {
        float horizontal = 0f;
        float vertical = 0f;
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            context.SetBoostInput(keyboard.spaceKey.isPressed);
            var k = keyboard;
            if (k.aKey.isPressed) horizontal -= 1f;
            if (k.dKey.isPressed) horizontal += 1f;
            if (k.wKey.isPressed) vertical += 1f;
            if (k.sKey.isPressed) vertical -= 1f;
        }
        else
        {
            context.SetBoostInput(false);
        }

        if (context.TryGetNearestEnemyPosition(out Vector3 enemyPos))
        {
            Vector3 playerPos = context.CachedTransform.position;
            Vector3 dir = enemyPos - playerPos;
            float lenSq = dir.x * dir.x + dir.y * dir.y + dir.z * dir.z;
            if (lenSq >= 0.0001f)
            {
                Vector3 dirNorm = dir / Mathf.Sqrt(lenSq);
                _targetLookAngle = Mathf.Atan2(dirNorm.x, dirNorm.z) * Mathf.Rad2Deg;
                _targetLookPitch = -Mathf.Asin(Mathf.Clamp(dirNorm.y, -1f, 1f)) * Mathf.Rad2Deg;
            }
        }

        context.ApplyMovementInput(horizontal, vertical, false, false, _targetLookAngle, _targetLookPitch);
    }

    public void OnExit(Player context) { }
}
