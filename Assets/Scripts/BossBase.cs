using UnityEngine;
using Unity.Mathematics;
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
    protected Action<int> addPlayerDamage;
    protected BulletManager _bulletManager;
    protected float _currentRotationVelocity;
    protected float _flashTimer;
    protected float _flashTotalTime;
    protected MaterialPropertyBlock _mpb;
    protected int _propertyID_EmissionColor;

    /// <summary>
    /// ボスの初期化（生成時に呼び出す）
    /// </summary>
    public virtual void Initialize(Func<Vector3> getPlayerPosition, Action<int> addPlayerDamage, BulletManager bulletManager = null, float? maxHpOverride = null)
    {
        this.getPlayerPosition = getPlayerPosition;
        this.addPlayerDamage = addPlayerDamage;
        _bulletManager = bulletManager;
        _effectiveMaxHp = maxHpOverride ?? maxHp;
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
    /// ボスの移動・挙動処理。GameManager から順序制御のため呼ばれる。
    /// </summary>
    public void ProcessMovement(float deltaTime)
    {
        if (getPlayerPosition == null || addPlayerDamage == null) return;
        UpdateFlashColor();
        UpdateBehavior(deltaTime);
    }

    /// <summary>
    /// ボスの弾発射処理。GameManager から順序制御のため呼ばれる。サブクラスでオーバーライドして弾を発射できる。
    /// </summary>
    public virtual void ProcessBulletFiring(float deltaTime, float3 playerPos)
    {
    }

    /// <summary>
    /// サブクラスで実装。移動・攻撃・接触ダメージなどの挙動を記述する。弾発射は ProcessBulletFiring で行う。
    /// </summary>
    protected abstract void UpdateBehavior(float deltaTime);

    /// <summary>
    /// プレイヤー方向を向く（Y軸のみ）。サブクラスから利用可能。
    /// </summary>
    protected void LookAtPlayer()
    {
        float3 pos = transform.position;
        float3 target = (float3)getPlayerPosition();
        float3 direction = target - pos;
        direction.y = 0f;
        if (math.lengthsq(direction) > 0.0001f)
        {
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref _currentRotationVelocity, 1.0f / rotationSpeed);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);
        }
    }

    /// <summary>
    /// プレイヤーに接触ダメージを与える（範囲内なら addPlayerDamage を呼ぶ）。サブクラスから利用可能。
    /// </summary>
    protected void TryDealContactDamage()
    {
        float3 pos = transform.position;
        float3 target = (float3)getPlayerPosition();
        float distSq = math.distancesq(pos, target);
        float damageRadiusSq = damageRadius * damageRadius;
        if (distSq <= damageRadiusSq)
        {
            addPlayerDamage(damageAmount);
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
