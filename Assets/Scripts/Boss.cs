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
        
        float3 pos = transform.position;
        float3 target = playerTransform.position;
        float distSq = math.distancesq(pos, target);
        float damageRadiusSq = damageRadius * damageRadius;
        
        // ダメージ範囲内かどうかで処理を分岐（二乗で比較してsqrtを回避）
        if (distSq <= damageRadiusSq)
        {
            // ダメージ範囲内：ダメージを与える（移動しない）
            gameManager.AddPlayerDamage(damageAmount);
        }
        else
        {
            // ダメージ範囲外：プレイヤーに近づく
            float3 dir = math.normalize(target - pos);
            pos += dir * speed * Time.deltaTime;
            transform.position = pos;
        }
    }
}
