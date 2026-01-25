using UnityEngine;
using Unity.Mathematics;
using System;

public class Boss : MonoBehaviour
{
    [Header("Boss Settings")]
    [SerializeField] private float speed = 4f; // 移動速度
    [SerializeField] private float damageRadius = 1.0f; // プレイヤーとの当たり判定半径
    [SerializeField] private int damageAmount = 1; // プレイヤーへのダメージ量
    
    // デリゲート（生成時に設定）
    private Func<Vector3> getPlayerPosition; // プレイヤー位置を取得する関数
    private Action<int> addPlayerDamage; // プレイヤーにダメージを与える関数
    
    /// <summary>
    /// ボスの初期化（生成時に呼び出す）
    /// </summary>
    /// <param name="getPlayerPosition">プレイヤー位置を取得する関数</param>
    /// <param name="addPlayerDamage">プレイヤーにダメージを与える関数</param>
    public void Initialize(Func<Vector3> getPlayerPosition, Action<int> addPlayerDamage)
    {
        this.getPlayerPosition = getPlayerPosition;
        this.addPlayerDamage = addPlayerDamage;
    }
    
    private void Update()
    {
        if (getPlayerPosition == null || addPlayerDamage == null) return;
        
        float3 pos = transform.position;
        float3 target = (float3)getPlayerPosition();
        float distSq = math.distancesq(pos, target);
        float damageRadiusSq = damageRadius * damageRadius;
        
        // ダメージ範囲内かどうかで処理を分岐（二乗で比較してsqrtを回避）
        if (distSq <= damageRadiusSq)
        {
            // ダメージ範囲内：ダメージを与える（移動しない）
            addPlayerDamage(damageAmount);
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
