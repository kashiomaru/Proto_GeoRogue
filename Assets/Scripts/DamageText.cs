using UnityEngine;
using TMPro;

public class DamageText : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI tmp;
    [SerializeField] private float lifeTime = 0.8f;
    [SerializeField] private float floatSpeed = 50f;
    
    private float _timer;
    private Vector3 _worldPos;
    private Camera _cam;

    public void Initialize(Vector3 worldPos, int damage, Camera cam)
    {
        _timer = 0;
        _worldPos = worldPos;
        _cam = cam;
        tmp.text = damage.ToString();

        // 色を変える（クリティカルなら赤、とか）
        tmp.color = Color.white;
        tmp.alpha = 0.0f;

        gameObject.SetActive(true);
    }

    void Update()
    {
        if (_cam == null)
        {
            gameObject.SetActive(false);
            return;
        }

        _timer += Time.deltaTime;
        if (_timer >= lifeTime)
        {
            gameObject.SetActive(false);
            return;
        }

        // 必要があればこの位置でカメラ方向との内積で簡易画面外判定をする

        Vector3 screenPos = _cam.WorldToScreenPoint(_worldPos);
        screenPos.y += floatSpeed * _timer; // 上に移動
        transform.position = screenPos;

        // フェードアウト
        float alpha = 1.0f - (_timer / lifeTime);
        tmp.alpha = alpha;
    }
}
