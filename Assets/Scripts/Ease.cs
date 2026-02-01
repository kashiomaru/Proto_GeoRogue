using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>
/// イージング用ユーティリティ。duration のあいだに 0→1 の値を渡しつつ onUpdate を呼ぶ。
/// </summary>
public static class Ease
{
    /// <summary>入力 t (0～1) をそのまま返す。</summary>
    public static float Linear(float t) => t;

    /// <summary>EaseIn Quad: t^2</summary>
    public static float EaseInQuad(float t) => t * t;

    /// <summary>EaseOut Quad: t*(2-t)</summary>
    public static float EaseOutQuad(float t) => t * (2f - t);

    /// <summary>EaseInOut Quad</summary>
    public static float EaseInOutQuad(float t) => t < 0.5f ? 2f * t * t : -1f + (4f - 2f * t) * t;

    /// <summary>
    /// 指定時間のあいだ、イージングした値 (0→1) で onUpdate を呼ぶ。UnscaledDeltaTime を使用。
    /// </summary>
    /// <param name="ease">イージング関数 (0～1 を入力に 0～1 を返す)</param>
    /// <param name="duration">秒</param>
    /// <param name="onUpdate">毎フレーム value (0～1) で呼ばれる</param>
    /// <param name="cancellationToken">キャンセル用</param>
    public static async UniTask Do(
        System.Func<float, float> ease,
        float duration,
        System.Action<float> onUpdate,
        CancellationToken cancellationToken = default)
    {
        if (duration <= 0f)
        {
            onUpdate?.Invoke(1f);
            return;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            cancellationToken.ThrowIfCancellationRequested();
            float t = Mathf.Clamp01(elapsed / duration);
            float value = ease != null ? ease(t) : t;
            onUpdate?.Invoke(value);
            elapsed += Time.unscaledDeltaTime;
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
        }

        onUpdate?.Invoke(1f);
    }
}
