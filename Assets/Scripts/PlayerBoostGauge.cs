using UnityEngine;

/// <summary>
/// プレイヤーのブーストゲージを管理するクラス。
/// ブースト中はゲージを消費し、非ブースト時は一定速度で回復する。
/// ゲージが切れるとブースト終了、満タンで再度ブースト可能。
/// </summary>
public class PlayerBoostGauge
{
    private float _currentGauge;
    private float _maxGauge;
    private float _consumeRatePerSecond;
    private float _recoverRatePerSecond;
    private bool _isBoosting;

    /// <summary>現在ブースト中か（キーが押されていてかつゲージが残っている）。</summary>
    public bool IsBoosting => _isBoosting;

    /// <summary>ゲージ量の正規化値（0～1）。UI表示用。</summary>
    public float CurrentGaugeNormalized => _maxGauge > 0f ? Mathf.Clamp01(_currentGauge / _maxGauge) : 0f;

    /// <summary>
    /// ブーストゲージを初期化する。
    /// </summary>
    /// <param name="maxGauge">ゲージ最大値（正規化なら1）</param>
    /// <param name="consumeRatePerSecond">ブースト中の消費速度（/秒）</param>
    /// <param name="recoverRatePerSecond">非ブースト時の回復速度（/秒）</param>
    public void Initialize(float maxGauge, float consumeRatePerSecond, float recoverRatePerSecond)
    {
        _maxGauge = Mathf.Max(0.001f, maxGauge);
        _consumeRatePerSecond = Mathf.Max(0f, consumeRatePerSecond);
        _recoverRatePerSecond = Mathf.Max(0f, recoverRatePerSecond);
        _currentGauge = _maxGauge;
        _isBoosting = false;
    }

    /// <summary>
    /// 毎フレーム呼ぶ。ブースト希望時はゲージを消費、そうでなければ回復する。
    /// </summary>
    /// <param name="deltaTime">経過時間（秒）</param>
    /// <param name="wantBoost">ブーストしたいか（キー押下など）</param>
    public void Update(float deltaTime, bool wantBoost)
    {
        if (wantBoost && _currentGauge > 0f)
        {
            _currentGauge -= _consumeRatePerSecond * deltaTime;
            _currentGauge = Mathf.Max(0f, _currentGauge);
            _isBoosting = true;
        }
        else
        {
            _currentGauge += _recoverRatePerSecond * deltaTime;
            _currentGauge = Mathf.Min(_maxGauge, _currentGauge);
            _isBoosting = false;
        }
    }

    /// <summary>
    /// ゲージを満タンにリセットする。Reset 時などに使用。
    /// </summary>
    public void Reset()
    {
        _currentGauge = _maxGauge;
        _isBoosting = false;
    }
}
