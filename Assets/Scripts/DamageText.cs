using UnityEngine;
using TMPro;

public class DamageText : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI tmp;
    [SerializeField] private float lifeTime = 0.8f;
    [SerializeField] private float floatSpeed = 50f;
    
    private float _timer;
    private Vector3 _startWorldPos;
    private Camera _cam;

    public void Initialize(Vector3 worldPos, int damage)
    {
        _startWorldPos = worldPos;
        _timer = 0;
        tmp.text = damage.ToString();
        _cam = Camera.main;
        
        // 色を変える（クリティカルなら赤、とか）
        tmp.color = Color.white; 
        
        gameObject.SetActive(true);
    }

    void Update()
    {
        _timer += Time.deltaTime;
        if(_timer >= lifeTime)
        {
            gameObject.SetActive(false);
            return;
        }

        // 簡易的な移動演出：上に上がりながらフェードアウト
        // Screen Space Overlayなら WorldToScreenPoint が必要
        Vector3 screenPos = _cam.WorldToScreenPoint(_startWorldPos);
        screenPos.y += floatSpeed * _timer; // 上に移動
        
        transform.position = screenPos;
        
        // フェードアウト
        float alpha = 1.0f - (_timer / lifeTime);
        tmp.alpha = alpha;
    }
}
