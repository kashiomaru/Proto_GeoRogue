using UnityEngine;

public class Plane : MonoBehaviour
{
    [Header("Camera Reference")]
    [SerializeField] private Camera targetCamera; // カメラ参照（未設定の場合はMainCameraを自動取得）
    
    private void Start()
    {
        // カメラが未設定の場合、MainCameraを自動取得
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }
    
    private void LateUpdate()
    {
        if (targetCamera != null)
        {
            // カメラのx, z座標を取得し、y座標は0に固定
            Vector3 cameraPosition = targetCamera.transform.position;
            transform.position = new Vector3(cameraPosition.x, 0f, cameraPosition.z);
        }
    }
}
