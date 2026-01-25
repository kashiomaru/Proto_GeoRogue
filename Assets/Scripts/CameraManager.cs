using UnityEngine;
using Unity.Cinemachine;

public class CameraManager : MonoBehaviour
{
    [Header("Cinemachine Cameras")]
    [SerializeField] private CinemachineCamera[] virtualCameras; // 切り替え可能なカメラの配列
    
    [Header("Settings")]
    [SerializeField] private int defaultCameraIndex = 0; // デフォルトのカメラインデックス
    
    private int _currentCameraIndex = -1;
    
    void Start()
    {
        // デフォルトカメラを設定
        if (virtualCameras != null && virtualCameras.Length > 0)
        {
            SwitchCamera(defaultCameraIndex);
        }
    }
    
    // カメラを切り替える（インデックス指定）
    public void SwitchCamera(int cameraIndex)
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
        }
    }
    
    // カメラを名前で切り替える
    public void SwitchCameraByName(string cameraName)
    {
        if (virtualCameras == null) return;
        
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
}
