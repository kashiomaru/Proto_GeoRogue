using UnityEngine;
using System.Collections.Generic;

public class DamageTextManager : MonoBehaviour
{
    [SerializeField] private GameObject textPrefab; // TMPが入ったプレハブ
    [SerializeField] private Transform canvasTransform;
    [SerializeField] private int poolSize = 100;

    private List<DamageText> _pool = new List<DamageText>();

    void Start()
    {
        for(int i=0; i<poolSize; i++)
        {
            var obj = Instantiate(textPrefab, canvasTransform);
            obj.SetActive(false);
            _pool.Add(obj.GetComponent<DamageText>());
        }
    }

    public void ShowDamage(Vector3 worldPos, int damage)
    {
        // 非アクティブなものを探す（リングバッファでも可）
        var text = _pool.Find(t => !t.gameObject.activeSelf);
        if(text != null)
        {
            text.Initialize(worldPos, damage);
        }
    }
}
