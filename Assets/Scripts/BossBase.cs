using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using System;

/// <summary>
/// ボスの抽象基底クラス。HP・フラッシュ・当たり判定・Initialize は共通。
/// 動き・攻撃などの挙動は UpdateBehavior でサブクラスごとにカスタムする。
/// </summary>
public abstract class BossBase : MonoBehaviour
{
    [Header("Boss Settings")]
    [SerializeField] protected float speed = 4f;
    [SerializeField] protected float rotationSpeed = 3f;
    [SerializeField] protected float damageRadius = 1.0f;
    [SerializeField] protected int damageAmount = 1;
    [SerializeField] protected float maxHp = 100f;
    [SerializeField] protected float collisionRadius = 2.0f;

    [Header("Flash Settings")]
    [SerializeField] protected float flashDuration = 0.1f;
    [SerializeField] protected float flashInterval = 0.1f;
    [SerializeField] protected float flashIntensity = 0.4f;

    [Header("References")]
    [SerializeField] protected Renderer bossRenderer;
    [SerializeField] protected Transform damageTextPositionAnchor;

    protected float _currentHp;
    protected float _effectiveMaxHp;
    protected Func<Vector3> getPlayerPosition;
    protected BulletManager _bulletManager;
    protected float _currentRotationVelocity;
    protected float _flashTimer;
    protected float _flashTotalTime;
    protected MaterialPropertyBlock _mpb;
    protected int _propertyID_EmissionColor;

    /// <summary>
    /// ボスの初期化（生成時に呼び出す）
    /// </summary>
    public virtual void Initialize(Func<Vector3> getPlayerPosition, BulletManager bulletManager = null)
    {
        this.getPlayerPosition = getPlayerPosition;
        _bulletManager = bulletManager;
        _effectiveMaxHp = maxHp;
        _currentHp = _effectiveMaxHp;

        if (bossRenderer != null)
        {
            _mpb = new MaterialPropertyBlock();
            _propertyID_EmissionColor = Shader.PropertyToID("_EmissionColor");
        }
    }

    public float TakeDamage(float damage)
    {
        float actualDamage = math.min(damage, _currentHp);
        _currentHp -= actualDamage;
        _flashTimer = flashDuration;
        if (_currentHp <= 0f)
        {
            _currentHp = 0f;
        }
        return actualDamage;
    }

    public float CurrentHp => _currentHp;
    public float MaxHp => _effectiveMaxHp;
    public bool IsDead => _currentHp <= 0f;
    public Vector3 Position => transform.position;
    public float CollisionRadius => collisionRadius;

    public Vector3 GetDamageTextPosition()
    {
        return damageTextPositionAnchor != null ? damageTextPositionAnchor.position : transform.position;
    }

    /// <summary>
    /// ボスの移動・挙動処理。プレイヤーへの接触ダメージは playerDamageQueue に登録する。
    /// </summary>
    public void ProcessMovement(float3 targetPos, NativeQueue<int> playerDamageQueue)
    {
        if (!playerDamageQueue.IsCreated) return;
        
        UpdateFlashColor();
        UpdateBehavior(targetPos, playerDamageQueue);
    }

    /// <summary>
    /// ボスの弾発射処理。GameManager から順序制御のため呼ばれる。サブクラスでオーバーライドして弾を発射できる。
    /// </summary>
    public virtual void ProcessFiring(float3 playerPos)
    {
    }

    /// <summary>
    /// サブクラスで実装。移動・攻撃・接触ダメージなどの挙動を記述する。弾発射は ProcessFiring で行う。プレイヤーへのダメージは playerDamageQueue に Enqueue する。
    /// </summary>
    protected abstract void UpdateBehavior(float3 targetPos, NativeQueue<int> playerDamageQueue);

    /// <summary>
    /// ターゲット方向を向く（Y軸のみ）。サブクラスから利用可能。
    /// </summary>
    protected void LookAtPlayer(float3 targetPos)
    {
        float3 pos = transform.position;
        float3 direction = targetPos - pos;
        direction.y = 0f;
        if (math.lengthsq(direction) > 0.0001f)
        {
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref _currentRotationVelocity, 1.0f / rotationSpeed);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);
        }
    }

    protected void UpdateFlashColor()
    {
        if (bossRenderer == null || _mpb == null)
        {
            return;
        }

        if (_flashTimer > 0f)
        {
            _flashTimer -= Time.deltaTime;
            _flashTotalTime += Time.deltaTime;
            if (_flashTimer < 0f)
            {
                _flashTimer = 0f;
                _flashTotalTime = 0f;
            }
        }

        if (_flashTimer > 0f)
        {
            if (_flashTotalTime > flashDuration + flashInterval)
            {
                _flashTotalTime -= flashDuration + flashInterval;
            }
            if (_flashTotalTime < flashDuration)
            {
                _mpb.SetColor(_propertyID_EmissionColor, new Color(flashIntensity, flashIntensity, flashIntensity, 1f));
            }
            else
            {
                _mpb.SetColor(_propertyID_EmissionColor, Color.black);
            }
        }
        else
        {
            _mpb.SetColor(_propertyID_EmissionColor, Color.black);
        }

        bossRenderer.SetPropertyBlock(_mpb);
    }
}
