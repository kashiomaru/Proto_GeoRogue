using UnityEngine;
using Unity.Mathematics;

public class Boss : MonoBehaviour
{
    [Header("Boss Settings")]
    [SerializeField] private float speed = 4f; // 移動速度
    [SerializeField] private float damageRadius = 1.0f; // プレイヤーとの当たり判定半径
    [SerializeField] private int damageAmount = 1; // プレイヤーへのダメージ量
    
    [Header("References")]
    [SerializeField] private Transform playerTransform; // プレイヤーのTransform
    [SerializeField] private GameManager gameManager; // GameManagerへの参照
    
    private void Update()
    {
        if (playerTransform == null || gameManager == null) return;
        
        // プレイヤーに向かって移動
        float3 pos = transform.position;
        float3 target = playerTransform.position;
        float3 dir = math.normalize(target - pos);
        pos += dir * speed * Time.deltaTime;
        
        transform.position = pos;
        
        // プレイヤーとの当たり判定
        float distSq = math.distancesq(pos, target);
        if (distSq < damageRadius * damageRadius)
        {
            // プレイヤーにダメージを与える
            gameManager.AddPlayerDamage(damageAmount);
        }
    }
}
