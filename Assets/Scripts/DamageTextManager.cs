using UnityEngine;
using System.Collections.Generic;

public class DamageTextManager : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    [SerializeField] private GameObject textPrefab; // TMPが入ったプレハブ
    [SerializeField] private Transform damageTextPoolTransform;
    [SerializeField] private int poolSize = 100;

    private List<DamageText> _pool = new List<DamageText>();
    private int _nextIndex;

    void Start()
    {
        for (int i = 0; i < poolSize; i++)
        {
            var obj = Instantiate(textPrefab, damageTextPoolTransform);
            obj.SetActive(false);
            _pool.Add(obj.GetComponent<DamageText>());
        }
    }

    /// <param name="collisionRadius">表示位置をワールド座標でランダムにオフセットする範囲の半径（XZ平面）。ボスなどは CollisionRadius を渡す。0 ならオフセットなし。</param>
    public void ShowDamage(Vector3 worldPos, int damage, float collisionRadius = 0f)
    {
        if (_pool.Count == 0 || mainCamera == null) return;

        worldPos += Random.insideUnitSphere * collisionRadius * 0.5f;

        var text = _pool[_nextIndex];
        _nextIndex = (_nextIndex + 1) % _pool.Count;
        text.Initialize(worldPos, damage, mainCamera);
    }

    /// <summary>
    /// 表示中のダメージテキストをすべて非表示にする。タイトルに戻る前のリセット時に呼ぶ。
    /// </summary>
    public void Reset()
    {
        foreach (var t in _pool)
        {
            if (t != null && t.gameObject.activeSelf)
            {
                t.gameObject.SetActive(false);
            }
        }
    }
}
