using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class Player : InitializeMonobehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 10f; // 回転速度

    [Header("Upgrade Params (LevelUp で変化)")]
    [SerializeField] private float magnetDist = 5f;

    [Header("Reset 時の復元値（上記の初期値。インスペクターで編集可）")]
    [SerializeField] private float initialMoveSpeed = 5f;
    [SerializeField] private float initialMagnetDist = 5f;

    [Header("Camera Reference")]
    [SerializeField] private Camera playerCamera; // カメラ参照（未設定の場合はMainCameraを自動取得）
    
    [Header("Health")]
    [SerializeField] private int maxHp = 10;
    [SerializeField] private float invincibleDuration = 1.0f; // 無敵時間（秒）
    [SerializeField] private float flashIntensity = 0.8f; // フラッシュの最大強度
    [SerializeField] private float initialFlashInterval = 0.1f; // 最初の点滅の間隔（秒）
    
    [Header("Level System")]
    [SerializeField] private int maxLevel = 99;

    [Header("References")]
    [SerializeField] private BulletManager bulletManager;
    [SerializeField] private GameManager gameManager;
    [Tooltip("プレイヤー弾の設定（メッシュ・マテリアルなど）。未設定の場合は BulletManager のデフォルトを使用。")]
    [SerializeField] private BulletData bulletData;

    private int _currentHp;
    private float _invincibleTimer = 0f;
    private bool _isInvincible = false;
    private float _initialFlashTimer = 0f; // 最初の点滅タイマー
    
    // レンダリング関連
    [SerializeField] private Renderer _renderer;
    private MaterialPropertyBlock _mpb;
    private int _propertyID_EmissionColor;
    
    // --- Experience & Level ---
    private int _currentExp = 0;
    private int _nextLevelExp = 10;
    private int _currentLevel = 1;
    private bool _canLevelUp = false; // レベルアップ可能フラグ
    
    private float _currentRotationVelocity; // 回転の滑らかさ用

    // プレイヤー弾発射用
    private float _playerShotTimer;
    private int _lastBulletCountPerShot = -1;
    private readonly List<Vector3> _cachedShotDirections = new List<Vector3>();

    // 毎フレームの new を避けるためのキャッシュ
    private Color _cachedFlashColor;
    private Vector2 _cachedMoveInput;
    private Vector3 _cachedDirection;
    private int _cachedBulletGroupId;
    /// <summary>transform のキャッシュ。InitializeInternal で設定。</summary>
    private Transform _cachedTransform;

    /// <summary>BulletData から初期化し、LevelUp で変更可能な弾パラメータ。</summary>
    private float _fireRate;
    private float _bulletSpeed;
    private int _bulletCountPerShot;

    public int CurrentHp => _currentHp;
    public int MaxHp => maxHp;
    public bool IsInvincible => _isInvincible;

    public bool IsDead => _currentHp <= 0;
    
    // 経験値・レベル情報のプロパティ
    public int CurrentExp => _currentExp;
    public int NextLevelExp => _nextLevelExp;
    public int CurrentLevel => _currentLevel;
    public bool CanLevelUp => _canLevelUp;

    public int BulletGroupId => _cachedBulletGroupId;
    /// <summary>キャッシュした Transform。GameManager など外部から参照する。</summary>
    public Transform CachedTransform => _cachedTransform;
    /// <summary>プレイヤー弾の設定（未設定の場合は null）。</summary>
    public BulletData BulletData => bulletData;

    protected override void InitializeInternal()
    {
        Debug.Assert(playerCamera != null, "[Player] playerCamera が未設定です。インスペクターでカメラを指定してください。");
        Debug.Assert(_renderer != null, "[Player] _renderer が未設定です。インスペクターで Renderer を指定してください。");
        Debug.Assert(bulletManager != null, "[Player] bulletManager が未設定です。インスペクターで BulletManager を指定してください。");
        Debug.Assert(bulletData != null, "[Player] bulletData が未設定です。インスペクターで Bullet Data を指定してください。");

        _cachedTransform = transform;
        _currentHp = maxHp;
        _mpb = new MaterialPropertyBlock();
        _propertyID_EmissionColor = Shader.PropertyToID("_EmissionColor");

        _fireRate = bulletData.FireInterval;
        _bulletSpeed = bulletData.Speed;
        _bulletCountPerShot = bulletData.CountPerShot;

        bulletManager.Initialize();
        _cachedBulletGroupId = bulletManager.AddBulletGroup(bulletData.Scale, bulletData.Mesh, bulletData.Material);
    }

    protected override void FinalizeInternal()
    {
        // 特になし（Native 等の解放は行っていない）
        bulletManager.RemoveBulletGroup(_cachedBulletGroupId);
    }
    
    private void Update()
    {
        if (IsInitialized == false)
        {
            return;
        }
        // 無敵時間の更新
        if (_isInvincible)
        {
            _invincibleTimer -= Time.deltaTime;
            if (_invincibleTimer <= 0f)
            {
                _isInvincible = false;
            }
        }
        
        // 最初の点滅タイマーの更新
        if (_initialFlashTimer > 0f)
        {
            _initialFlashTimer -= Time.deltaTime;
            if (_initialFlashTimer < 0f)
            {
                _initialFlashTimer = 0f;
            }
        }
        
        // エミッション色の更新
        UpdateFlashColor();
    }

    /// <summary>プレイヤーの移動処理。GameManager から順序制御のため呼ばれる。</summary>
    public void ProcessMovement()
    {
        if (IsInitialized == false) return;
        
        HandleMovement();
    }
    
    // ダメージを受ける（実際に与えたダメージを返す）
    public int TakeDamage(int damage)
    {
        // 無敵時間中または既に死んでいる場合はダメージを与えない
        if (_isInvincible || _currentHp <= 0)
        {
            return 0;
        }
        
        // 実際に与えるダメージを計算
        int actualDamage = damage;
        int newHp = _currentHp - actualDamage;
        if (newHp < 0)
        {
            // HPが0を下回る場合は、実際に与えたダメージを調整
            actualDamage = _currentHp;
            newHp = 0;
        }
        
        _currentHp = newHp;
        
        // 無敵時間を開始
        if (_currentHp > 0)
        {
            _isInvincible = true;
            _invincibleTimer = invincibleDuration;
            // 最初の点滅を開始（2回の点滅 = ON/OFF）
            _initialFlashTimer = initialFlashInterval * 2f;
        }
        
        return actualDamage;
    }
    
    // 経験値を加算し、レベルアップ可能フラグを設定
    public void AddExperience(int amount)
    {
        if (amount <= 0)
        {
            return;
        }
        
        _currentExp += amount;

        // レベルアップ可能かチェック
        if (_currentExp >= _nextLevelExp && _currentLevel < maxLevel)
        {
            _canLevelUp = true;
        }
    }
    
    // レベルアップ処理（UIで選択後に呼ばれる）
    public void LevelUp()
    {
        if (_canLevelUp == false || _currentLevel >= maxLevel)
        {
            return;
        }
        
        // 経験値をリセット
        _currentExp -= _nextLevelExp;
        
        // レベルを上げる
        _currentLevel++;
        _nextLevelExp = (int)(_nextLevelExp * 1.2f); // 必要経験値を増やす（カーブは要調整）
        
        // フラグをリセット
        _canLevelUp = false;
        
        // まだレベルアップ可能な場合はフラグを立てる
        if (_currentExp >= _nextLevelExp && _currentLevel < maxLevel)
        {
            _canLevelUp = true;
        }
    }
    
    /// <summary>
    /// プレイヤーを初期状態にリセット（HP・経験値・レベル・アップグレードパラメータ）
    /// </summary>
    public void Reset()
    {
        _currentHp = maxHp;
        _isInvincible = false;
        _invincibleTimer = 0f;
        _initialFlashTimer = 0f;
        _currentExp = 0;
        _nextLevelExp = 10;
        _currentLevel = 1;
        _canLevelUp = false;

        moveSpeed = initialMoveSpeed;
        magnetDist = initialMagnetDist;

        Debug.Assert(bulletData != null, "[Player] Reset: bulletData が未設定です。インスペクターで Bullet Data を指定してください。");
        _fireRate = bulletData.FireInterval;
        _bulletSpeed = bulletData.Speed;
        _bulletCountPerShot = bulletData.CountPerShot;

        _playerShotTimer = 0f;
        _lastBulletCountPerShot = -1;

        UpdateFlashColor();
    }

    /// <summary>
    /// プレイヤー弾の発射処理（プレイ中に GameManager の Update から呼ぶ）
    /// </summary>
    public void ProcessFiring()
    {
        if (IsInitialized == false || bulletManager == null)
        {
            return;
        }

        float deltaTime = Time.deltaTime;
        float rate = GetFireRate();
        int countPerShot = GetBulletCountPerShot();
        float speed = GetBulletSpeed();

        _playerShotTimer += deltaTime;
        if (_playerShotTimer >= rate)
        {
            _playerShotTimer = 0f;
            Vector3 baseDir = _cachedTransform.forward;

            if (countPerShot != _lastBulletCountPerShot)
            {
                _cachedShotDirections.Clear();
                float spreadAngle = bulletData.SpreadAngle;
                for (int i = 0; i < countPerShot; i++)
                {
                    float angle = countPerShot > 1
                        ? -spreadAngle * (countPerShot - 1) * 0.5f + (spreadAngle * i)
                        : 0f;
                    Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward;
                    _cachedShotDirections.Add(dir);
                }
                _lastBulletCountPerShot = countPerShot;
            }

            Quaternion baseRot = Quaternion.LookRotation(baseDir);
            for (int i = 0; i < countPerShot; i++)
            {
                Vector3 finalDir = baseRot * _cachedShotDirections[i];
                bulletManager.SpawnBullet(_cachedBulletGroupId, _cachedTransform.position, finalDir, speed, 1.0f, bulletData.LifeTime);
            }
        }
    }

    /// <summary>発射間隔（秒）。マルチショット時は基準×発射数で単位時間あたりの弾数が一定になる。</summary>
    public float GetFireRate() => _fireRate * Mathf.Max(1, _bulletCountPerShot);
    /// <summary>基準の発射間隔（マルチショット補正前）。FireRateUp などで変更する値。</summary>
    public float GetBaseFireRate() => _fireRate;
    public void SetFireRate(float value) { _fireRate = value; }
    public float GetBulletSpeed() => _bulletSpeed;
    public void SetBulletSpeed(float value) { _bulletSpeed = value; }
    public int GetBulletCountPerShot() => _bulletCountPerShot;
    public void SetBulletCountPerShot(int value) { _bulletCountPerShot = value; }
    public float GetMagnetDist() => magnetDist;
    public void SetMagnetDist(float value) { magnetDist = value; }
    
    // ヒットフラッシュの色を更新（最初の1回だけ点滅、その後徐々に弱くなる）
    private void UpdateFlashColor()
    {
        if (_renderer == null || _mpb == null)
        {
            return;
        }
        
        // 無敵時間中
        if (_isInvincible && _invincibleTimer > 0f)
        {
            // 最初の点滅中かどうか
            if (_initialFlashTimer > 0f)
            {
                // 最初の点滅：ON/OFFを繰り返す
                float timeSinceFlash = (initialFlashInterval * 2f) - _initialFlashTimer;
                int flashCycle = Mathf.FloorToInt(timeSinceFlash / initialFlashInterval);
                bool isFlashing = (flashCycle % 2 == 0); // 偶数サイクルで点灯
                
                if (isFlashing)
                {
                    // 強烈な白（HDR）
                    _cachedFlashColor.r = flashIntensity;
                    _cachedFlashColor.g = flashIntensity;
                    _cachedFlashColor.b = flashIntensity;
                    _cachedFlashColor.a = 1f;
                    _mpb.SetColor(_propertyID_EmissionColor, _cachedFlashColor);
                }
                else
                {
                    // 通常色（黒）
                    _mpb.SetColor(_propertyID_EmissionColor, Color.black);
                }
            }
            else
            {
                // 最初の点滅が終わった後：光が徐々に弱くなる
                // 最初の点滅が終わってからの経過時間を計算
                float timeAfterInitialFlash = invincibleDuration - _invincibleTimer - (initialFlashInterval * 2f);
                float fadeDuration = invincibleDuration - (initialFlashInterval * 2f); // 減衰にかける時間
                
                if (fadeDuration > 0f)
                {
                    // 減衰の割合を計算（1.0 = 点滅直後、0.0 = 無敵時間終了時）
                    float fadeRatio = 1f - (timeAfterInitialFlash / fadeDuration);
                    fadeRatio = Mathf.Clamp01(fadeRatio);
                    
                    // 残り時間に応じてエミッション強度を線形に減衰
                    float currentIntensity = flashIntensity * fadeRatio;
                    
                    // エミッション色を設定（時間が経つほど暗くなる）
                    _cachedFlashColor.r = currentIntensity;
                    _cachedFlashColor.g = currentIntensity;
                    _cachedFlashColor.b = currentIntensity;
                    _cachedFlashColor.a = 1f;
                    _mpb.SetColor(_propertyID_EmissionColor, _cachedFlashColor);
                }
                else
                {
                    // 減衰時間が0以下の場合は通常色
                    _mpb.SetColor(_propertyID_EmissionColor, Color.black);
                }
            }
        }
        else
        {
            // 無敵時間外は通常色
            _mpb.SetColor(_propertyID_EmissionColor, Color.black);
        }
        
        _renderer.SetPropertyBlock(_mpb);
    }
    
    private void HandleMovement()
    {
        // 新しいInput SystemでWASDキー入力を取得
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        float horizontal = 0f;
        float vertical = 0f;
        
        // 左右移動
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
        {
            horizontal -= 1f;
        }
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
        {
            horizontal += 1f;
        }

        // 前後移動
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
        {
            vertical += 1f;
        }
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
        {
            vertical -= 1f;
        }
        
        // 入力方向を計算（毎フレームの new を避けるためキャッシュを再利用）
        _cachedMoveInput.x = horizontal;
        _cachedMoveInput.y = vertical;
        _cachedDirection.x = _cachedMoveInput.x;
        _cachedDirection.y = 0f;
        _cachedDirection.z = _cachedMoveInput.y;
        _cachedDirection.Normalize();
        bool hasInput = _cachedDirection.sqrMagnitude >= 0.01f;
        
        // スペースキーが押されているかチェック
        bool isSpacePressed = keyboard.spaceKey.isPressed;
        // Shift押下時は回転のみ（移動しない）
        bool shiftOnlyRotate = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
        
        // 移動・回転処理
        if (hasInput)
        {
            // カメラの向きを基準に移動方向を計算
            float targetAngle = Mathf.Atan2(_cachedDirection.x, _cachedDirection.z) * Mathf.Rad2Deg + GetPlayerCameraAngle();
            
            // 回転：スペースが押されていないときのみ
            if (isSpacePressed == false)
            {
                float angle = Mathf.SmoothDampAngle(_cachedTransform.eulerAngles.y, targetAngle, ref _currentRotationVelocity, 1.0f / rotationSpeed);
                _cachedTransform.rotation = Quaternion.Euler(0f, angle, 0f);
            }
            
            // 移動：Shiftを押していないときのみ
            if (shiftOnlyRotate == false)
            {
                Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
                _cachedTransform.position += moveDir.normalized * moveSpeed * Time.deltaTime;
            }
        }
    }

    private float GetPlayerCameraAngle()
    {
        if (playerCamera == null)
        {
            return 0f;
        }

        return playerCamera.transform.eulerAngles.y;
    }
    
    // LevelUpManager用のパラメータ取得・設定メソッド
    public float GetMoveSpeed()
    {
        return moveSpeed;
    }
    
    public void SetMoveSpeed(float value)
    {
        moveSpeed = value;
    }
}
