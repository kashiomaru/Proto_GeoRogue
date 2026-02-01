using UnityEngine;
using TMPro;
using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>
/// ステージ名を TextMeshPro で表示する。Show 呼び出し後、一定時間表示してからアルファアウトする。
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class StageNameDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI tmp;
    [SerializeField] private float displayDuration = 1.0f;
    [SerializeField] private float fadeOutDuration = 1.0f;

    private CancellationTokenSource _cts;

    void OnDestroy()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }

    /// <summary>
    /// ステージ名を表示する。表示時間のあとアルファアウトする。
    /// </summary>
    public void Show(string stageName)
    {
        if (tmp == null) return;
        if (string.IsNullOrEmpty(stageName))
        {
            tmp.alpha = 0f;
            return;
        }

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        CancellationTokenSource cts = _cts;
        tmp.text = stageName;
        gameObject.SetActive(true);
        tmp.alpha = 1f;
        DisplayAndFadeOutAsync(cts.Token, cts).Forget();
    }

    private async UniTaskVoid DisplayAndFadeOutAsync(CancellationToken cancellationToken, CancellationTokenSource cts)
    {
        try
        {
            await UniTask.Delay((int)(displayDuration * 1000), DelayType.UnscaledDeltaTime, cancellationToken: cancellationToken);

            await Ease.Do(Ease.Linear, fadeOutDuration, (value) => { tmp.alpha = 1f - value; }, cancellationToken);
            gameObject.SetActive(false);
        }
        catch (System.OperationCanceledException)
        {
            // Show の再呼び出しまたは Destroy でキャンセルされた
        }
        finally
        {
            if (_cts == cts)
            {
                _cts = null;
            }
            cts.Dispose();
        }
    }
}
