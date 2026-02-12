using UnityEngine;
using Unity.Cinemachine;
using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>カメラの表示モード</summary>
public enum CameraMode
{
    /// <summary>クオータービュー（斜め上から見下ろし）</summary>
    QuarterView,
    /// <summary>TPS（第三人称視点）</summary>
    TPS
}

public class CameraManager : InitializeMonobehaviour
{
    [Header("Cinemachine Cameras")]
    [SerializeField] private CinemachineCamera normalCamera;   // Normal（クオータービュー）用
    [SerializeField] private CinemachineCamera bossCamera;     // Boss（TPS）用

    [Header("Settings")]
    [SerializeField] private CameraMode defaultCameraMode = CameraMode.QuarterView; // デフォルトのカメラモード
    [SerializeField] private LookAtController lookAtController; // 回転処理を行うLookAtControllerへの参照
    [SerializeField] private CinemachineBrain cinemachineBrain; // 即時切り替え時に使用（未設定ならシーンから取得）

    private CameraMode? _currentCameraMode;

    /// <summary>モードに対応するカメラを取得する</summary>
    private CinemachineCamera GetCameraByMode(CameraMode mode)
    {
        return mode switch
        {
            CameraMode.QuarterView => normalCamera,
            CameraMode.TPS => bossCamera,
            _ => normalCamera
        };
    }

    protected override void InitializeInternal()
    {
        UnityEngine.Debug.Assert(normalCamera != null, "CameraManager: Normal カメラが未設定です");
        UnityEngine.Debug.Assert(bossCamera != null, "CameraManager: Boss カメラが未設定です");

        // 時間停止時でもカメラブレンドが動くようにする
        var brain = GetBrain();
        if (brain != null)
        {
            brain.IgnoreTimeScale = true;
        }

        // デフォルトモードのカメラを設定
        SwitchCamera(defaultCameraMode);
    }

    private CinemachineBrain GetBrain()
    {
        return cinemachineBrain != null ? cinemachineBrain : FindFirstObjectByType<CinemachineBrain>();
    }
    
    protected override void FinalizeInternal()
    {
        // クリーンアップ処理が必要な場合はここに実装
    }
    
    /// <summary>カメラをモードで切り替える（クオータービュー / TPS）</summary>
    /// <param name="mode">切り替え先のカメラモード</param>
    /// <param name="immediate">true のときブレンドなしで即時切り替え</param>
    public void SwitchCamera(CameraMode mode, bool immediate = false)
    {
        var target = GetCameraByMode(mode);
        if (target == null)
        {
            Debug.LogWarning($"CameraManager: モード {mode} に対応するカメラがありません");
            return;
        }

        // 現在のカメラのPriorityを下げる
        if (_currentCameraMode.HasValue)
        {
            var current = GetCameraByMode(_currentCameraMode.Value);
            if (current != null)
            {
                current.Priority = 0;
            }
        }

        // 新しいカメラのPriorityを上げる
        target.Priority = 10;
        _currentCameraMode = mode;

        // 即時切り替えのときはブレンドをキャンセルして即座に反映
        if (immediate)
        {
            GetBrain()?.ResetState();
        }
    }

    /// <summary>
    /// 現在のカメラブレンドが完了するまで待つ。タイムスケール0のときもブレンドは進む（IgnoreTimeScale）。
    /// </summary>
    public async UniTask WaitForBlendCompleteAsync(CancellationToken cancellationToken = default)
    {
        var brain = GetBrain();
        if (brain == null)
        {
            return;
        }
        var token = cancellationToken != default ? cancellationToken : this.GetCancellationTokenOnDestroy();
        // 1フレーム待ってブレンド開始を確実にしてから完了を待つ
        await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, token);
        await UniTask.WaitUntil(() => brain.IsBlending == false, PlayerLoopTiming.LastPostLateUpdate, token);
    }
    
    // カメラを名前で切り替える
    public void SwitchCameraByName(string cameraName)
    {
        if (normalCamera != null && normalCamera.name == cameraName)
        {
            SwitchCamera(CameraMode.QuarterView);
            return;
        }
        if (bossCamera != null && bossCamera.name == cameraName)
        {
            SwitchCamera(CameraMode.TPS);
            return;
        }
        Debug.LogWarning($"CameraManager: Camera with name '{cameraName}' not found");
    }

    // ボスのTransformをLookAtターゲットに設定
    public void SetBossLookAtTarget(Transform bossTransform)
    {
        // LookAtControllerにTransformを設定（nullの場合は回転を停止）
        lookAtController?.SetLookAtTarget(bossTransform);
    }
}
