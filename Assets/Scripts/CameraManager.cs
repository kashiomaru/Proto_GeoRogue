using UnityEngine;
using Unity.Cinemachine;
using Cysharp.Threading.Tasks;
using System.Threading;

public class CameraManager : InitializeMonobehaviour
{
    [Header("Cinemachine Cameras")]
    [SerializeField] private CinemachineCamera[] virtualCameras; // 切り替え可能なカメラの配列
    
    [Header("Settings")]
    [SerializeField] private int defaultCameraIndex = 0; // デフォルトのカメラインデックス
    [SerializeField] private LookAtController lookAtController; // 回転処理を行うLookAtControllerへの参照
    [SerializeField] private CinemachineBrain cinemachineBrain; // 即時切り替え時に使用（未設定ならシーンから取得）
    
    private int _currentCameraIndex = -1;

    protected override void InitializeInternal()
    {
        // 時間停止時でもカメラブレンドが動くようにする
        var brain = GetBrain();
        if (brain != null)
        {
            brain.IgnoreTimeScale = true;
        }

        // デフォルトカメラを設定
        if (virtualCameras != null && virtualCameras.Length > 0)
        {
            SwitchCamera(defaultCameraIndex);
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
    
    // カメラを切り替える（インデックス指定）
    // immediate: true のときブレンドなしで即時切り替え、false のときはブレンド補間
    public void SwitchCamera(int cameraIndex, bool immediate = false)
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
