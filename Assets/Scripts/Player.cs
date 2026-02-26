using UnityEngine;
using Unity.Mathematics;

public class Player : InitializeMonobehaviour
{
    // データ未設定時に使う内部デフォルト値
    private const float DefaultInitialMoveSpeed = 5f;
    private const float DefaultInitialMagnetDist = 5f;
    private const float DefaultRotationSpeed = 10f;
    private const float DefaultAccelerationTime = 0.15f;
    private const int DefaultMaxHp = 3;
    private const int DefaultMaxLevel = 99;
    private const int DefaultInitialNextLevelExp = 6;
    private const float DefaultNextLevelExpMultiplier = 1.15f;
    private const float DefaultBoostGaugeMax = 1f;
    private const float DefaultBoostConsumeRate = 1f;
    private const float DefaultBoostRecoverRate = 0.25f;

    [Header("Data")]
    [Tooltip("未設定時は内部デフォルト値を使用。弾は PlayerData の BulletData で指定。")]
    [SerializeField] private PlayerData playerData;

    [Header("Hit / Flash")]
    [Tooltip("無敵時間（秒）")]
    [SerializeField] private float invincibleDuration = 1f;
    [Tooltip("フラッシュの最大強度")]
    [SerializeField] private float flashIntensity = 0.8f;
    [Tooltip("最初の点滅の間隔（秒）")]
    [SerializeField] private float initialFlashInterval = 0.1f;

    [Header("Collision")]
    [Tooltip("プレイヤー弾と敵の当たり判定に使うプレイヤー側の半径")]
    [SerializeField] private float collisionRadius = 1f;

    [Header("Boost / Altitude")]
    [Tooltip("ブースト開始時の上昇速度（勢いをつける）")]
    [SerializeField] private float boostRiseSpeedInitial = 6f;
    [Tooltip("ブースト持続時の上昇速度（初速からこの値へ落ち着く）")]
    [SerializeField] private float boostRiseSpeedSustain = 3f;
    [Tooltip("初速から持続速度へ落ち着くまでの時間（秒）")]
    [SerializeField] private float boostRiseEaseTime = 0.35f;
    [Tooltip("非ブースト時の下降速度")]
    [SerializeField] private float fallSpeed = 3f;
    [Tooltip("地面の高さ（Y）。これより下には行かない")]
    [SerializeField] private float groundLevel = 0f;
    [Tooltip("高度上限（Y）。これより上には行かない")]
    [SerializeField] private float maxAltitude = 2f;

    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private BulletManager bulletManager;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private EnemyManager enemyManager;
    [SerializeField] private Renderer playerRenderer;

    /// <summary>現在の移動速度。LevelUp で変化し、Reset でデータの初期値に戻る。</summary>
    private float _moveSpeed;
    /// <summary>現在の吸い寄せ範囲。LevelUp で変化し、Reset でデータの初期値に戻る。</summary>
    private float _magnetDist;

    private int _currentHp;
    /// <summary>現在の最大HP。LevelUp のプレイヤーHPアップで増加。Reset で initial に戻る。</summary>
    private int _maxHp;
    private float _invincibleTimer = 0f;
    private bool _isInvincible = false;
    private float _initialFlashTimer = 0f; // 最初の点滅タイマー
    
    // レンダリング関連
    private MaterialPropertyBlock _mpb;
    private int _propertyID_EmissionColor;
    
    // --- Experience & Level ---
    private int _currentExp = 0;
    private int _nextLevelExp = 10;
    private int _currentLevel = 1;
    private bool _canLevelUp = false; // レベルアップ可能フラグ
    /// <summary>成長倍率。ジェム取得時の経験値に乗算する。Growth アップグレード選択時のみ+1（初期1→2→3…）。</summary>
    private int _growthMultiplier = 1;
    
    private float _currentRotationVelocity; // 回転の滑らかさ用
    private Vector3 _currentVelocity; // 慣性用の現在速度
    private Vector3 _smoothDampVelocity; // SmoothDamp 用（内部用）

    /// <summary>ブーストゲージ管理。ProcessMovement で更新される。</summary>
    private PlayerBoostGauge _boostGauge;
    private float _verticalVelocity; // Y方向の速度（上昇・下降）
    private bool _wantBoost; // 入力ステートから設定される「ブーストしたいか」
    /// <summary>連続でブーストしている時間（秒）。初速→持続速度の緩急に使用。</summary>
    private float _boostHoldTimer;

    /// <summary>入力モード用ステートマシン。ProcessMovement で更新される。</summary>
    private StateMachine<PlayerInputMode, Player> _inputModeStateMachine;
    /// <summary>発射モード用ステートマシン。ProcessFiring で更新される。</summary>
    private StateMachine<PlayerFiringMode, Player> _firingStateMachine;

    // プレイヤー弾発射用（発射間隔のタイマー。配置ロジックは各発射ステートが持つ）
    private float _playerShotTimer;

    // 毎フレームの new を避けるためのキャッシュ
    private Color _cachedFlashColor;
    private Vector2 _cachedMoveInput;
    private Vector3 _cachedDirection;
    private IBulletGroupHandler _bulletHandler;
    /// <summary>transform のキャッシュ。InitializeInternal で設定。</summary>
    private Transform _cachedTransform;

    /// <summary>BulletData から初期化し、LevelUp で変更可能な弾パラメータ。</summary>
    private float _fireRate;
    private float _bulletSpeed;
    private int _bulletCountPerShot;
    /// <summary>クリティカル発生確率（0～1）。LevelUp で増加。</summary>
    private float _criticalChance;
    /// <summary>クリティカル時のダメージ倍率。LevelUp で増加。</summary>
    private float _criticalMultiplier;
    /// <summary>弾の寿命ボーナス（秒）。LevelUp で増加。発射時に BulletData.LifeTime に加算する。</summary>
    private float _bulletLifeTimeBonus;

    // PlayerData または内部デフォルトから取得するヘルパー
    private BulletData GetBulletData() => playerData != null ? playerData.BulletData : null;
    private float GetInitialMoveSpeed() => playerData != null ? playerData.InitialMoveSpeed : DefaultInitialMoveSpeed;
    private float GetInitialMagnetDist() => playerData != null ? playerData.InitialMagnetDist : DefaultInitialMagnetDist;
    private float GetRotationSpeed() => playerData != null ? playerData.RotationSpeed : DefaultRotationSpeed;
    private float GetAccelerationTime() => playerData != null ? playerData.AccelerationTime : DefaultAccelerationTime;
    private PlayerInputMode GetInitialInputMode() => playerData != null ? playerData.InitialInputMode : PlayerInputMode.KeyboardWASD;
    private PlayerFiringMode GetInitialFiringMode() => playerData != null ? playerData.InitialFiringMode : PlayerFiringMode.Fan;
    private int GetMaxHp() => playerData != null ? playerData.MaxHp : DefaultMaxHp;
    private int GetMaxLevel() => playerData != null ? playerData.MaxLevel : DefaultMaxLevel;
    private float GetBoostGaugeMax() => playerData != null ? playerData.BoostGaugeMax : DefaultBoostGaugeMax;
    private float GetBoostConsumeRate() => playerData != null ? playerData.BoostConsumeRate : DefaultBoostConsumeRate;
    private float GetBoostRecoverRate() => playerData != null ? playerData.BoostRecoverRate : DefaultBoostRecoverRate;
    private int GetInitialNextLevelExp() => playerData != null ? playerData.InitialNextLevelExp : DefaultInitialNextLevelExp;
    private float GetNextLevelExpMultiplier() => playerData != null ? playerData.NextLevelExpMultiplier : DefaultNextLevelExpMultiplier;
    public int CurrentHp => _currentHp;
    public int MaxHp => _maxHp;
    public bool IsInvincible => _isInvincible;

    public bool IsDead => _currentHp <= 0;
    
    // 経験値・レベル情報のプロパティ
    public int CurrentExp => _currentExp;
    public int NextLevelExp => _nextLevelExp;
    public int CurrentLevel => _currentLevel;
    /// <summary>最大レベル。デバッグ表示などに使用。</summary>
    public int MaxLevel => GetMaxLevel();
    public bool CanLevelUp => _canLevelUp;
    /// <summary>成長倍率。ジェム取得時の経験値に乗算する値（1, 2, 3…）。アップグレード「Growth」を選択したときのみ増加。</summary>
    public int GrowthMultiplier => _growthMultiplier;

    /// <summary>成長倍率を加算する。レベルアップ時のアップグレード「Growth」選択で呼ばれる。</summary>
    public void AddGrowthMultiplier(int amount)
    {
        if (amount > 0)
            _growthMultiplier += amount;
    }

    /// <summary>プレイヤー弾グループのハンドル（当たり判定などで BulletManager に渡す）。</summary>
    public IBulletGroupHandler GetBulletHandler() => _bulletHandler;
    /// <summary>キャッシュした Transform。GameManager など外部から参照する。</summary>
    public Transform CachedTransform => _cachedTransform;
    /// <summary>プレイヤー弾の設定（未設定の場合は null）。</summary>
    public BulletData BulletData => GetBulletData();
    /// <summary>プレイヤー弾と敵の当たり判定に使うプレイヤー側の半径。</summary>
    public float CollisionRadius => collisionRadius;
    /// <summary>現在の入力モード。</summary>
    public PlayerInputMode CurrentInputMode => _inputModeStateMachine?.CurrentStateKey ?? PlayerInputMode.KeyboardWASD;
    /// <summary>現在の発射モード。</summary>
    public PlayerFiringMode CurrentFiringMode => _firingStateMachine?.CurrentStateKey ?? PlayerFiringMode.Fan;

    protected override void InitializeInternal()
    {
        Debug.Assert(playerCamera != null, "[Player] playerCamera が未設定です。インスペクターでカメラを指定してください。");
        Debug.Assert(playerRenderer != null, "[Player] playerRenderer が未設定です。インスペクターで Renderer を指定してください。");
        Debug.Assert(bulletManager != null, "[Player] bulletManager が未設定です。インスペクターで BulletManager を指定してください。");
        Debug.Assert(gameManager != null, "[Player] gameManager が未設定です。インスペクターで GameManager を指定してください。");
        Debug.Assert(enemyManager != null, "[Player] enemyManager が未設定です。インスペクターで EnemyManager を指定してください。");

        _cachedTransform = transform;
        _moveSpeed = GetInitialMoveSpeed();
        _magnetDist = GetInitialMagnetDist();
        _maxHp = GetMaxHp();
        _currentHp = _maxHp;
        _nextLevelExp = GetInitialNextLevelExp();
        _mpb = new MaterialPropertyBlock();
        _propertyID_EmissionColor = Shader.PropertyToID("_EmissionColor");

        BulletData dataBullet = GetBulletData();
        if (dataBullet != null)
        {
            _fireRate = dataBullet.FireInterval;
            _bulletSpeed = dataBullet.Speed;
            _bulletCountPerShot = dataBullet.CountPerShot;
            _criticalChance = dataBullet.CriticalChance;
            _criticalMultiplier = dataBullet.CriticalMultiplier;
            bulletManager.Initialize();
            _bulletHandler = bulletManager.AddBulletGroup(dataBullet.Damage, dataBullet.Scale, dataBullet.Mesh, dataBullet.Material, _criticalChance, _criticalMultiplier, dataBullet.CurveValue);
        }
        else
        {
            bulletManager.Initialize();
            _bulletHandler = null;
        }

        _inputModeStateMachine = new StateMachine<PlayerInputMode, Player>(this);
        _inputModeStateMachine.RegisterState(PlayerInputMode.KeyboardWASD, new KeyboardWASDInputState());
        _inputModeStateMachine.RegisterState(PlayerInputMode.KeyboardWASD_MouseLook, new KeyboardWASDMouseLookInputState());
        _inputModeStateMachine.RegisterState(PlayerInputMode.KeyboardWASD_ArrowLook, new KeyboardWASDArrowLookInputState());
        _inputModeStateMachine.RegisterState(PlayerInputMode.KeyboardWASD_Auto, new KeyboardWASDAutoInputState());
        _inputModeStateMachine.RegisterState(PlayerInputMode.CursorMove_AutoLook, new CursorMoveAutoLookInputState());
        _inputModeStateMachine.Initialize(GetInitialInputMode());

        _boostGauge = new PlayerBoostGauge();
        _boostGauge.Initialize(GetBoostGaugeMax(), GetBoostConsumeRate(), GetBoostRecoverRate());

        _firingStateMachine = new StateMachine<PlayerFiringMode, Player>(this);
        _firingStateMachine.RegisterState(PlayerFiringMode.Fan, new FanFiringState());
        _firingStateMachine.RegisterState(PlayerFiringMode.Straight, new StraightFiringState());
        _firingStateMachine.RegisterState(PlayerFiringMode.Omnidirectional, new OmnidirectionalFiringState());
        _firingStateMachine.Initialize(GetInitialFiringMode());
    }

    protected override void FinalizeInternal()
    {
        if (_bulletHandler != null)
            bulletManager.RemoveBulletGroup(_bulletHandler);
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

    /// <summary>プレイヤーの移動処理。GameManager から順序制御のため呼ばれる。入力モードのステートが入力を読んで移動を適用する。</summary>
    public void ProcessMovement()
    {
        if (IsInitialized == false) return;

        _inputModeStateMachine?.Update();

        // ブーストゲージ更新と垂直移動・高度クランプ
        float dt = Time.deltaTime;
        _boostGauge?.Update(dt, _wantBoost);

        if (_boostGauge != null)
        {
            if (_boostGauge.IsBoosting)
            {
                _boostHoldTimer += dt;
                float t = boostRiseEaseTime > 0f ? Mathf.Clamp01(_boostHoldTimer / boostRiseEaseTime) : 1f;
                float riseSpeed = Mathf.Lerp(boostRiseSpeedInitial, boostRiseSpeedSustain, t);
                _verticalVelocity = riseSpeed;
            }
            else
            {
                _boostHoldTimer = 0f;
                _verticalVelocity = -fallSpeed;
            }

            Vector3 pos = _cachedTransform.position;
            pos.y += _verticalVelocity * dt;
            pos.y = Mathf.Clamp(pos.y, groundLevel, maxAltitude);
            _cachedTransform.position = pos;

            if (pos.y <= groundLevel || pos.y >= maxAltitude)
                _verticalVelocity = 0f;
        }
    }

    /// <summary>ブースト入力を設定する。入力モードのステートから呼ばれる（例: Space 押下時 true）。</summary>
    public void SetBoostInput(bool wantBoost)
    {
        _wantBoost = wantBoost;
    }

    /// <summary>入力モードを切り替える。</summary>
    public void ChangeInputMode(PlayerInputMode mode)
    {
        _inputModeStateMachine?.ChangeState(mode);
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

        // 成長倍率を経験値に乗算（ジェム取得時など）
        int expGain = amount * _growthMultiplier;
        _currentExp += expGain;

        // レベルアップ可能かチェック
        if (_currentExp >= _nextLevelExp && _currentLevel < GetMaxLevel())
        {
            _canLevelUp = true;
        }
    }
    
    // レベルアップ処理（UIで選択後に呼ばれる）
    public void LevelUp()
    {
        if (_canLevelUp == false || _currentLevel >= GetMaxLevel())
        {
            return;
        }
        
        _currentExp -= _nextLevelExp;
        _currentLevel++;
        _nextLevelExp = Mathf.CeilToInt(_nextLevelExp * GetNextLevelExpMultiplier());
        _canLevelUp = false;
        
        if (_currentExp >= _nextLevelExp && _currentLevel < GetMaxLevel())
        {
            _canLevelUp = true;
        }
    }
    
    /// <summary>
    /// プレイヤーを初期状態にリセット（HP・経験値・レベル・アップグレードパラメータ）
    /// </summary>
    public void Reset()
    {
        _maxHp = GetMaxHp();
        _currentHp = _maxHp;
        _isInvincible = false;
        _invincibleTimer = 0f;
        _initialFlashTimer = 0f;
        _currentExp = 0;
        _nextLevelExp = GetInitialNextLevelExp();
        _currentLevel = 1;
        _canLevelUp = false;
        _growthMultiplier = 1;

        _moveSpeed = GetInitialMoveSpeed();
        _magnetDist = GetInitialMagnetDist();

        BulletData dataBullet = GetBulletData();
        if (dataBullet != null)
        {
            _fireRate = dataBullet.FireInterval;
            _bulletSpeed = dataBullet.Speed;
            _bulletCountPerShot = dataBullet.CountPerShot;
            SetBulletDamage(dataBullet.Damage);
            _criticalChance = dataBullet.CriticalChance;
            _criticalMultiplier = dataBullet.CriticalMultiplier;
            _bulletHandler?.SetCritical(_criticalChance, _criticalMultiplier);
        }
        _bulletLifeTimeBonus = 0f;

        _playerShotTimer = 0f;

        _currentVelocity = Vector3.zero;
        _smoothDampVelocity = Vector3.zero;
        _verticalVelocity = 0f;
        _wantBoost = false;
        _boostHoldTimer = 0f;
        _boostGauge?.Reset();

        UpdateFlashColor();
    }

    /// <summary>ブーストゲージの正規化値（0～1）。UI表示用。</summary>
    public float GetBoostGaugeNormalized() => _boostGauge != null ? _boostGauge.CurrentGaugeNormalized : 0f;

    /// <summary>
    /// プレイヤー弾の発射処理（プレイ中に GameManager の Update から呼ぶ）。発射モードのステートに委譲する。
    /// </summary>
    public void ProcessFiring()
    {
        if (IsInitialized == false || bulletManager == null)
        {
            return;
        }

        _firingStateMachine?.Update();
    }

    /// <summary>
    /// 発射間隔のタイマーを進め、間隔が来ていれば 1 回分の「発射権」を消費して true を返す。
    /// 発射ステートはこれが true のときだけ、配置ロジックに従って SpawnPlayerBullet を呼ぶ。
    /// </summary>
    /// <returns>発射してよいタイミングなら true</returns>
    public bool TryConsumeFireInterval()
    {
        if (bulletManager == null || GetBulletData() == null)
        {
            return false;
        }

        float deltaTime = Time.deltaTime;
        float rate = GetFireRate();

        _playerShotTimer += deltaTime;
        if (_playerShotTimer < rate)
        {
            return false;
        }

        _playerShotTimer = 0f;
        return true;
    }

    /// <summary>
    /// 指定位置・方向にプレイヤー弾を 1 発生成する。発射ステートの配置ロジックから呼ばれる。
    /// </summary>
    public void SpawnPlayerBullet(Vector3 position, Vector3 direction)
    {
        BulletData dataBullet = GetBulletData();
        if (bulletManager == null || dataBullet == null || _bulletHandler == null)
        {
            return;
        }

        float speed = GetBulletSpeed();
        float lifeTime = dataBullet.LifeTime + _bulletLifeTimeBonus;
        _bulletHandler.Spawn(position, direction, speed, lifeTime, dataBullet.DirectionRotation);
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
    public float GetMagnetDist() => _magnetDist;
    public void SetMagnetDist(float value) { _magnetDist = value; }
    /// <summary>プレイヤー弾のダメージ（BulletManager のグループから取得）。</summary>
    public int GetBulletDamage() => _bulletHandler != null ? _bulletHandler.GetDamage() : 0;
    /// <summary>プレイヤー弾のダメージを設定する（LevelUp のダメージアップなどで使用）。</summary>
    public void SetBulletDamage(int value) { _bulletHandler?.SetDamage(value); }
    /// <summary>クリティカル発生確率（0～1）。</summary>
    public float GetCriticalChance() => _criticalChance;
    /// <summary>クリティカル発生確率を設定する。獲得ごとに+10%などで使用。最大1でクランプ。</summary>
    public void SetCriticalChance(float value)
    {
        _criticalChance = Mathf.Clamp01(value);
        _bulletHandler?.SetCritical(_criticalChance, _criticalMultiplier);
    }
    /// <summary>クリティカル時のダメージ倍率。</summary>
    public float GetCriticalMultiplier() => _criticalMultiplier;
    /// <summary>クリティカル時のダメージ倍率を設定する（LevelUp のクリティカルダメージアップで使用）。</summary>
    public void SetCriticalMultiplier(float value)
    {
        _criticalMultiplier = Mathf.Max(1f, value);
        _bulletHandler?.SetCritical(_criticalChance, _criticalMultiplier);
    }
    /// <summary>弾の基本寿命（秒）。BulletData の値。発射時にボーナスを加算した値が使われる。</summary>
    public float GetBulletLifeTimeBase()
    {
        var b = GetBulletData();
        return b != null ? b.LifeTime : 0f;
    }
    /// <summary>弾の寿命ボーナス（秒）。発射時に BulletData.LifeTime に加算される。</summary>
    public float GetBulletLifeTimeBonus() => _bulletLifeTimeBonus;
    /// <summary>弾の寿命ボーナスを設定する（LevelUp の弾の寿命アップなどで使用）。</summary>
    public void SetBulletLifeTimeBonus(float value) { _bulletLifeTimeBonus = Mathf.Max(0f, value); }
    /// <summary>最大HPと現在HPを同じ量だけ増やす（LevelUp のプレイヤーHPアップで使用）。減った分は回復しない。</summary>
    public void AddMaxHpAndCurrent(int amount)
    {
        if (amount <= 0) return;
        _maxHp += amount;
        _currentHp += amount;
    }

    // ヒットフラッシュの色を更新（最初の1回だけ点滅、その後徐々に弱くなる）
    private void UpdateFlashColor()
    {
        if (playerRenderer == null || _mpb == null)
        {
            return;
        }
        
        // 無敵時間中
        if (_isInvincible && _invincibleTimer > 0f)
        {
            // 最初の点滅中かどうか
            if (_initialFlashTimer > 0f)
            {
                float timeSinceFlash = (initialFlashInterval * 2f) - _initialFlashTimer;
                int flashCycle = Mathf.FloorToInt(timeSinceFlash / initialFlashInterval);
                bool isFlashing = (flashCycle % 2 == 0);
                
                if (isFlashing)
                {
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
                float timeAfterInitialFlash = invincibleDuration - _invincibleTimer - (initialFlashInterval * 2f);
                float fadeDuration = invincibleDuration - (initialFlashInterval * 2f);
                
                if (fadeDuration > 0f)
                {
                    float fadeRatio = 1f - (timeAfterInitialFlash / fadeDuration);
                    fadeRatio = Mathf.Clamp01(fadeRatio);
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
        
        playerRenderer.SetPropertyBlock(_mpb);
    }
    
    /// <summary>
    /// 入力モードのステートから呼ばれる。指定された入力に応じて移動・回転・慣性を適用する。
    /// </summary>
    /// <param name="horizontal">左右入力（-1～1）</param>
    /// <param name="vertical">前後入力（-1～1）</param>
    /// <param name="isSpacePressed">スペース押下時は回転しない（キーボード向きモード用）</param>
    /// <param name="shiftOnlyRotate">true のときは回転のみで移動しない</param>
    /// <param name="overrideLookAngleDeg">指定時は回転のみこの角度にする。移動は KeyboardWASD と同じ（カメラ基準）。</param>
    /// <param name="moveSpeedScale">指定時は移動速度にこの倍率（0～1）を掛ける。カーソル移動モードで距離に応じた速度に使う。</param>
    public void ApplyMovementInput(float horizontal, float vertical, bool isSpacePressed, bool shiftOnlyRotate, float? overrideLookAngleDeg = null, float? moveSpeedScale = null)
    {
        _cachedMoveInput.x = horizontal;
        _cachedMoveInput.y = vertical;
        _cachedDirection.x = _cachedMoveInput.x;
        _cachedDirection.y = 0f;
        _cachedDirection.z = _cachedMoveInput.y;
        _cachedDirection.Normalize();
        bool hasInput = _cachedDirection.sqrMagnitude >= 0.01f;

        // 移動方向は常に KeyboardWASD と同じ（カメラ基準の入力方向）
        float movementAngle = Mathf.Atan2(_cachedDirection.x, _cachedDirection.z) * Mathf.Rad2Deg + GetPlayerCameraAngle();

        float speedMult = moveSpeedScale.HasValue ? Mathf.Clamp01(moveSpeedScale.Value) : 1f;

        Vector3 targetVelocity = Vector3.zero;
        if (hasInput && shiftOnlyRotate == false)
        {
            Vector3 moveDir = Quaternion.Euler(0f, movementAngle, 0f) * Vector3.forward;
            if (moveDir.sqrMagnitude >= 0.01f)
            {
                targetVelocity = moveDir.normalized * (_moveSpeed * speedMult);
            }
        }

        // 回転：override 時はマウス方向、それ以外は入力方向＋カメラ
        float rotationAngle = overrideLookAngleDeg ?? movementAngle;
        bool applyRotation = overrideLookAngleDeg.HasValue || (hasInput && isSpacePressed == false);
        if (applyRotation)
        {
            float angle = Mathf.SmoothDampAngle(_cachedTransform.eulerAngles.y, rotationAngle, ref _currentRotationVelocity, 1.0f / GetRotationSpeed());
            _cachedTransform.rotation = Quaternion.Euler(0f, angle, 0f);
        }

        _currentVelocity = Vector3.SmoothDamp(_currentVelocity, targetVelocity, ref _smoothDampVelocity, GetAccelerationTime());
        _cachedTransform.position += _currentVelocity * Time.deltaTime;
    }

    private float GetPlayerCameraAngle()
    {
        if (playerCamera == null)
        {
            return 0f;
        }

        return playerCamera.transform.eulerAngles.y;
    }

    /// <summary>入力ステートからマウス向き計算などに使用する。未設定時は null。</summary>
    public Camera GetPlayerCamera()
    {
        return playerCamera;
    }

    /// <summary>一番近い敵の位置を取得する。入力モード（Auto など）から使用。敵がいない場合は false。</summary>
    public bool TryGetNearestEnemyPosition(out Vector3 position)
    {
        position = default;
        if (enemyManager == null) return false;
        Vector3 p = _cachedTransform.position;
        if (!enemyManager.TryGetClosestEnemyPosition(new float3(p.x, p.y, p.z), out float3 enemyPos))
            return false;
        position = new Vector3(enemyPos.x, enemyPos.y, enemyPos.z);
        return true;
    }

    public float GetMoveSpeed() => _moveSpeed;
    public void SetMoveSpeed(float value) { _moveSpeed = value; }
}
