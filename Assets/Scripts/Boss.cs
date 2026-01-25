using UnityEngine;
using Unity.Mathematics;

public class Boss : MonoBehaviour
{
    [Header("Boss Settings")]
    [SerializeField] private float speed = 4f; // 移動速度
    [SerializeField] private float damageRadius = 1.0f; // プレイヤーとの当たり判定半径
    [SerializeField] private int damageAmount = 1; // プレイヤーへのダメージ量
    
    // 参照（生成時に設定）
    private Transform playerTransform; // プレイヤーのTransform
    private GameManager gameManager; // GameManagerへの参照
    
    /// <summary>
    /// ボスの初期化（生成時に呼び出す）
    /// </summary>
    /// <param name="playerTransform">プレイヤーのTransform</param>
    /// <param name="gameManager">GameManagerへの参照</param>
    public void Initialize(Transform playerTransform, GameManager gameManager)
    {
        this.playerTransform = playerTransform;
        this.gameManager = gameManager;
    }
    
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
