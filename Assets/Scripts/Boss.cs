using UnityEngine;
using Unity.Mathematics;
using System;

public class Boss : MonoBehaviour
{
    [Header("Boss Settings")]
    [SerializeField] private float speed = 4f; // 移動速度
    [SerializeField] private float rotationSpeed = 3f; // 回転速度（Playerと同じ単位）
    [SerializeField] private float damageRadius = 1.0f; // プレイヤーとの当たり判定半径
    [SerializeField] private int damageAmount = 1; // プレイヤーへのダメージ量
    [SerializeField] private float maxHp = 100f; // ボスの最大HP
    [SerializeField] private float collisionRadius = 2.0f; // 弾との当たり判定半径
    
    // HP
    private float _currentHp;
    
    // デリゲート（生成時に設定）
    private Func<Vector3> getPlayerPosition; // プレイヤー位置を取得する関数
    private Action<int> addPlayerDamage; // プレイヤーにダメージを与える関数
    
    private float _currentRotationVelocity; // 回転の滑らかさ用（Playerと同じ方式）
    
    /// <summary>
    /// ボスの初期化（生成時に呼び出す）
    /// </summary>
    /// <param name="getPlayerPosition">プレイヤー位置を取得する関数</param>
    /// <param name="addPlayerDamage">プレイヤーにダメージを与える関数</param>
    public void Initialize(Func<Vector3> getPlayerPosition, Action<int> addPlayerDamage)
    {
        this.getPlayerPosition = getPlayerPosition;
        this.addPlayerDamage = addPlayerDamage;
        _currentHp = maxHp; // HPを初期化
    }
    
    /// <summary>
    /// ボスにダメージを与える
    /// </summary>
    /// <param name="damage">ダメージ量</param>
    /// <returns>実際に与えたダメージ</returns>
    public float TakeDamage(float damage)
    {
        float actualDamage = math.min(damage, _currentHp);
        _currentHp -= actualDamage;
        
        if (_currentHp <= 0f)
        {
            _currentHp = 0f;
            // ボス死亡時の処理は呼び出し側で行う
        }
        
        return actualDamage;
    }
    
    /// <summary>
    /// ボスの現在のHPを取得
    /// </summary>
    public float CurrentHp => _currentHp;
    
    /// <summary>
    /// ボスの最大HPを取得
    /// </summary>
    public float MaxHp => maxHp;
    
    /// <summary>
    /// ボスが死亡しているかどうか
    /// </summary>
    public bool IsDead => _currentHp <= 0f;
    
    /// <summary>
    /// ボスの位置を取得
    /// </summary>
    public Vector3 Position => transform.position;
    
    /// <summary>
    /// ボスの当たり判定半径を取得
    /// </summary>
    public float CollisionRadius => collisionRadius;
    
    private void Update()
    {
        if (getPlayerPosition == null || addPlayerDamage == null) return;
        
        float3 pos = transform.position;
        float3 target = (float3)getPlayerPosition();
        float distSq = math.distancesq(pos, target);
        float damageRadiusSq = damageRadius * damageRadius;
        
        // プレイヤーの方向を向く（Y軸のみ回転、補間しながら、Playerと同じ方式）
        float3 direction = target - pos;
        direction.y = 0f; // Y軸の回転を無視（水平方向のみ）
        if (math.lengthsq(direction) > 0.0001f) // ゼロ除算を防ぐ
        {
            // 目標角度を計算
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            
            // Playerと同じ方式で滑らかに補間（SmoothDampAngleを使用）
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref _currentRotationVelocity, 1.0f / rotationSpeed);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);
        }
        
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
