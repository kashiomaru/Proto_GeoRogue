using UnityEngine;
using System.Collections.Generic;

public class DamageTextManager : MonoBehaviour
{
    [SerializeField] private GameObject textPrefab; // TMPが入ったプレハブ
    [SerializeField] private Transform damageTextPoolTransform;
    [SerializeField] private int poolSize = 100;

    private List<DamageText> _pool = new List<DamageText>();

    void Start()
    {
        for(int i=0; i<poolSize; i++)
        {
            var obj = Instantiate(textPrefab, damageTextPoolTransform);
            obj.SetActive(false);
            _pool.Add(obj.GetComponent<DamageText>());
        }
    }

    public void ShowDamage(Vector3 worldPos, int damage)
    {
        // 非アクティブなものを探す（リングバッファでも可）
        var text = _pool.Find(t => !t.gameObject.activeSelf);
        text?.Initialize(worldPos, damage);
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
