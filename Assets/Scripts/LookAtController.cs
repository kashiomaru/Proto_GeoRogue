using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 対象のTransformを見るように回転させるコンポーネント。
/// ターゲットが設定されているとき（ボス戦中）は十字キーでカメラを左右・上下に傾けられる。
/// キーを離すと元の向きに戻る。
/// </summary>
public class LookAtController : MonoBehaviour
{
    private Transform _lookAtTarget;

    [Header("十字キーオフセット（ターゲット設定時のみ有効）")]
    [SerializeField] [Min(0f)] private float _maxOffsetYaw = 30f;       // 左右の最大傾き（度）
    [SerializeField] [Min(0f)] private float _maxOffsetPitch = 10f;      // 上下の最大傾き（度）
    [SerializeField] [Min(0.01f)] private float _offsetSmoothTime = 0.12f; // 目標へ向かうイーズ時間（秒）※初速が速く、近づくとゆっくり

    private float _offsetYaw;   // 現在の左右オフセット（度）
    private float _offsetPitch; // 現在の上下オフセット（度）
    private float _velocityYaw; // SmoothDamp用
    private float _velocityPitch;

    void Update()
    {
        if (_lookAtTarget == null)
            return;

        Vector3 direction = (_lookAtTarget.position - transform.position).normalized;
        if (direction == Vector3.zero)
            return;

        Quaternion baseRotation = Quaternion.LookRotation(direction);

        // 十字キーでオフセット目標値を設定（ターゲットあり時のみ）
        float targetYaw = 0f;
        float targetPitch = 0f;
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.leftArrowKey.isPressed) targetYaw -= _maxOffsetYaw;
            if (keyboard.rightArrowKey.isPressed) targetYaw += _maxOffsetYaw;
            if (keyboard.downArrowKey.isPressed) targetPitch += _maxOffsetPitch;
            if (keyboard.upArrowKey.isPressed) targetPitch -= _maxOffsetPitch;
        }

        float dt = Time.deltaTime;
        // SmoothDamp: 初速が速く、目標付近でゆっくりになるイーズアウト
        _offsetYaw = Mathf.SmoothDamp(_offsetYaw, targetYaw, ref _velocityYaw, _offsetSmoothTime, Mathf.Infinity, dt);
        _offsetPitch = Mathf.SmoothDamp(_offsetPitch, targetPitch, ref _velocityPitch, _offsetSmoothTime, Mathf.Infinity, dt);

        // 基準回転にローカルでオフセット（ピッチ・ヨー）を適用
        Quaternion offset = Quaternion.Euler(_offsetPitch, _offsetYaw, 0f);
        transform.rotation = baseRotation * offset;
    }

    /// <summary>
    /// 見る対象のTransformを設定。nullを設定すると回転を停止し、オフセットもリセットします。
    /// </summary>
    public void SetLookAtTarget(Transform target)
    {
        _lookAtTarget = target;
        if (target == null)
        {
            _offsetYaw = 0f;
            _offsetPitch = 0f;
            _velocityYaw = 0f;
            _velocityPitch = 0f;
        }
    }
}
