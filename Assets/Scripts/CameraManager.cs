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
    [SerializeField] private CinemachineCamera[] virtualCameras; // 切り替え可能なカメラの配列
    
    [Header("Settings")]
    [SerializeField] private CameraMode defaultCameraMode = CameraMode.QuarterView; // デフォルトのカメラモード
    [SerializeField] private int quarterViewCameraIndex = 0; // クオータービューに対応するカメラのインデックス
    [SerializeField] private int tpsCameraIndex = 1; // TPSに対応するカメラのインデックス
    [SerializeField] private LookAtController lookAtController; // 回転処理を行うLookAtControllerへの参照
    [SerializeField] private CinemachineBrain cinemachineBrain; // 即時切り替え時に使用（未設定ならシーンから取得）
    
    private int _currentCameraIndex = -1;

    /// <summary>モードからカメラインデックスを取得する</summary>
    private int GetCameraIndexForMode(CameraMode mode)
    {
        return mode switch
        {
            CameraMode.QuarterView => quarterViewCameraIndex,
            CameraMode.TPS => tpsCameraIndex,
            _ => quarterViewCameraIndex
        };
    }

    protected override void InitializeInternal()
    {
        // 時間停止時でもカメラブレンドが動くようにする
        var brain = GetBrain();
        if (brain != null)
        {
            brain.IgnoreTimeScale = true;
        }

        // デフォルトモードのカメラを設定
        if (virtualCameras != null && virtualCameras.Length > 0)
        {
            SwitchCamera(defaultCameraMode);
        }
    }

    private CinemachineBrain GetBrain()
    {
        return cinemachineBrain != null ? cinemachineBrain : FindFirstObjectByType<CinemachineBrain>();
    }
    
    protected override void FinalizeInternal()
    {
        // クリーンアップ処理が必要な場合はここに実装
    }
    
    // カメラを切り替える（インデックス指定・内部用。外部からはモード指定のみ使用する）
    // immediate: true のときブレンドなしで即時切り替え、false のときはブレンド補間
    private void SwitchCamera(int cameraIndex, bool immediate = false)
    {
        if (virtualCameras == null || cameraIndex < 0 || cameraIndex >= virtualCameras.Length)
        {
            Debug.LogWarning($"CameraManager: Invalid camera index {cameraIndex}");
            return;
        }
        
        // 現在のカメラのPriorityを下げる
        if (_currentCameraIndex >= 0 && _currentCameraIndex < virtualCameras.Length)
        {
            if (virtualCameras[_currentCameraIndex] != null)
            {
                virtualCameras[_currentCameraIndex].Priority = 0;
            }
        }
        
        // 新しいカメラのPriorityを上げる
        if (virtualCameras[cameraIndex] != null)
        {
            virtualCameras[cameraIndex].Priority = 10;
            _currentCameraIndex = cameraIndex;
        }
        else
        {
            Debug.LogWarning($"CameraManager: Camera at index {cameraIndex} is null");
            return;
        }
        
        // 即時切り替えのときはブレンドをキャンセルして即座に反映
        if (immediate)
        {
            GetBrain()?.ResetState();
        }
    }

    /// <summary>カメラをモードで切り替える（クオータービュー / TPS）</summary>
    /// <param name="mode">切り替え先のカメラモード</param>
    /// <param name="immediate">true のときブレンドなしで即時切り替え</param>
    public void SwitchCamera(CameraMode mode, bool immediate = false)
    {
        int index = GetCameraIndexForMode(mode);
        SwitchCamera(index, immediate);
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
        if (virtualCameras == null)
        {
            return;
        }

        for (int i = 0; i < virtualCameras.Length; i++)
        {
            if (virtualCameras[i] != null && virtualCameras[i].name == cameraName)
            {
                SwitchCamera(i);
                return;
            }
        }
        
        Debug.LogWarning($"CameraManager: Camera with name '{cameraName}' not found");
    }
    
    // 現在のカメラインデックスを取得
    public int GetCurrentCameraIndex()
    {
        return _currentCameraIndex;
    }

    /// <summary>現在のカメラモードを取得する</summary>
    public CameraMode GetCurrentCameraMode()
    {
        if (_currentCameraIndex == tpsCameraIndex)
        {
            return CameraMode.TPS;
        }
        return CameraMode.QuarterView;
    }
    
    // カメラの数を取得
    public int GetCameraCount()
    {
        return virtualCameras != null ? virtualCameras.Length : 0;
    }
    
    // ボスのTransformをLookAtターゲットに設定
    public void SetBossLookAtTarget(Transform bossTransform)
    {
        // LookAtControllerにTransformを設定（nullの場合は回転を停止）
        lookAtController?.SetLookAtTarget(bossTransform);
    }
}
