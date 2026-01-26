using UnityEngine;

/// <summary>
/// 対象のTransformを見るように回転させるコンポーネント
/// このスクリプトを回転させる対象のGameObjectにアタッチします
/// </summary>
public class LookAtController : MonoBehaviour
{
    private Transform _lookAtTarget; // 見る対象のTransform
    
    void Update()
    {
        // 対象のTransformがある場合のみ回転を適用
        if (_lookAtTarget != null)
        {
            // 対象の方向を計算
            Vector3 direction = (_lookAtTarget.position - transform.position).normalized;
            
            // 方向が有効な場合のみ回転を適用
            if (direction != Vector3.zero)
            {
                // 対象を見るように回転
                transform.rotation = Quaternion.LookRotation(direction);
            }
        }
    }
    
    /// <summary>
    /// 見る対象のTransformを設定
    /// nullを設定すると回転を停止します
    /// </summary>
    /// <param name="target">見る対象のTransform</param>
    public void SetLookAtTarget(Transform target)
    {
        _lookAtTarget = target;
    }
}
