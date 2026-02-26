using UnityEngine;
using Unity.Mathematics;

public class Player : InitializeMonobehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 10f; // 回転速度
    [Tooltip("慣性の強さ。目標速度に近づくまでの目安時間（秒）。大きいほど入力やめても滑る")]
    [SerializeField] private float accelerationTime = 0.15f;
    [Tooltip("ゲーム開始時の入力モード。インスペクターで切り替え可能")]
    [SerializeField] private PlayerInputMode initialInputMode = PlayerInputMode.KeyboardWASD;
    [Tooltip("ゲーム開始時の発射モード。インスペクターで切り替え可能")]
    [SerializeField] private PlayerFiringMode initialFiringMode = PlayerFiringMode.Fan;

    [Header("Upgrade Params (LevelUp で変化)")]
    [SerializeField] private float magnetDist = 5f;

    [Header("Reset 時の復元値（上記の初期値。インスペクターで編集可）")]
    [SerializeField] private float initialMoveSpeed = 5f;
    [SerializeField] private float initialMagnetDist = 5f;

    [Header("Camera Reference")]
    [SerializeField] private Camera playerCamera; // カメラ参照（未設定の場合はMainCameraを自動取得）
    
    [Header("Health")]
    [SerializeField] private int maxHp = 3;
    [SerializeField] private float invincibleDuration = 1.0f; // 無敵時間（秒）
    [SerializeField] private float flashIntensity = 0.8f; // フラッシュの最大強度
    [SerializeField] private float initialFlashInterval = 0.1f; // 最初の点滅の間隔（秒）
    
    [Header("Level System")]
    [SerializeField] private int maxLevel = 99;
    [Tooltip("レベル2に上がるために必要な経験値（初期値）。Reset 時にもこの値に戻る")]
    [SerializeField] private int initialNextLevelExp = 6;
    [Tooltip("レベルアップごとに「次のレベル必要EXP」にかける倍率（例: 1.2 で毎回20%増）")]
    [SerializeField] private float nextLevelExpMultiplier = 1.15f;

    [Header("References")]
    [SerializeField] private BulletManager bulletManager;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private EnemyManager enemyManager;
    [Tooltip("プレイヤー弾の設定（メッシュ・マテリアルなど）。未設定の場合は BulletManager のデフォルトを使用。")]
    [SerializeField] private BulletData bulletData;
    [Tooltip("プレイヤー弾と敵の当たり判定に使うプレイヤー側の半径")]
    [SerializeField] private float collisionRadius = 1f;

    [Header("Boost / Altitude")]
    [Tooltip("ブーストゲージの最大値（正規化なら1）")]
    [SerializeField] private float boostGaugeMax = 1f;
    [Tooltip("ブースト中のゲージ消費速度（/秒）")]
    [SerializeField] private float boostConsumeRate = 0.5f;
    [Tooltip("非ブースト時のゲージ回復速度（/秒）")]
    [SerializeField] private float boostRecoverRate = 0.25f;
    [Tooltip("ブースト開始時の上昇速度（勢いをつける）")]
    [SerializeField] private float boostRiseSpeedInitial = 8f;
    [Tooltip("ブースト持続時の上昇速度（初速からこの値へ落ち着く）")]
    [SerializeField] private float boostRiseSpeedSustain = 4f;
    [Tooltip("初速から持続速度へ落ち着くまでの時間（秒）")]
    [SerializeField] private float boostRiseEaseTime = 0.35f;
    [Tooltip("非ブースト時の下降速度")]
    [SerializeField] private float fallSpeed = 3f;
    [Tooltip("地面の高さ（Y）。これより下には行かない")]
    [SerializeField] private float groundLevel = 0f;
    [Tooltip("高度上限（Y）。これより上には行かない")]
    [SerializeField] private float maxAltitude = 10f;

    private int _currentHp;
    /// <summary>現在の最大HP。LevelUp のプレイヤーHPアップで増加。Reset で initial に戻る。</summary>
    private int _maxHp;
    private float _invincibleTimer = 0f;
    private bool _isInvincible = false;
    private float _initialFlashTimer = 0f; // 最初の点滅タイマー
    
    // レンダリング関連
    [SerializeField] private Renderer playerRenderer;
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

    public int CurrentHp => _currentHp;
    public int MaxHp => _maxHp;
    public bool IsInvincible => _isInvincible;

    public bool IsDead => _currentHp <= 0;
    
    // 経験値・レベル情報のプロパティ
    public int CurrentExp => _currentExp;
    public int NextLevelExp => _nextLevelExp;
    public int CurrentLevel => _currentLevel;
    /// <summary>最大レベル。デバッグ表示などに使用。</summary>
    public int MaxLevel => maxLevel;
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
    public BulletData BulletData => bulletData;
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
        Debug.Assert(bulletData != null, "[Player] bulletData が未設定です。インスペクターで Bullet Data を指定してください。");

        _cachedTransform = transform;
        _maxHp = maxHp;
        _currentHp = maxHp;
        _nextLevelExp = initialNextLevelExp;
        _mpb = new MaterialPropertyBlock();
        _propertyID_EmissionColor = Shader.PropertyToID("_EmissionColor");

        _fireRate = bulletData.FireInterval;
        _bulletSpeed = bulletData.Speed;
        _bulletCountPerShot = bulletData.CountPerShot;
        _criticalChance = bulletData.CriticalChance;
        _criticalMultiplier = bulletData != null ? bulletData.CriticalMultiplier : 1f;

        bulletManager.Initialize();
        _bulletHandler = bulletManager.AddBulletGroup(bulletData.Damage, bulletData.Scale, bulletData.Mesh, bulletData.Material, _criticalChance, _criticalMultiplier, bulletData.CurveValue);

        _inputModeStateMachine = new StateMachine<PlayerInputMode, Player>(this);
        _inputModeStateMachine.RegisterState(PlayerInputMode.KeyboardWASD, new KeyboardWASDInputState());
        _inputModeStateMachine.RegisterState(PlayerInputMode.KeyboardWASD_MouseLook, new KeyboardWASDMouseLookInputState());
        _inputModeStateMachine.RegisterState(PlayerInputMode.KeyboardWASD_ArrowLook, new KeyboardWASDArrowLookInputState());
        _inputModeStateMachine.RegisterState(PlayerInputMode.KeyboardWASD_Auto, new KeyboardWASDAutoInputState());
        _inputModeStateMachine.RegisterState(PlayerInputMode.CursorMove_AutoLook, new CursorMoveAutoLookInputState());
        _inputModeStateMachine.Initialize(initialInputMode);

        _boostGauge = new PlayerBoostGauge();
        _boostGauge.Initialize(boostGaugeMax, boostConsumeRate, boostRecoverRate);

        _firingStateMachine = new StateMachine<PlayerFiringMode, Player>(this);
        _firingStateMachine.RegisterState(PlayerFiringMode.Fan, new FanFiringState());
        _firingStateMachine.RegisterState(PlayerFiringMode.Straight, new StraightFiringState());
        _firingStateMachine.RegisterState(PlayerFiringMode.Omnidirectional, new OmnidirectionalFiringState());
        _firingStateMachine.Initialize(initialFiringMode);
    }

    protected override void FinalizeInternal()
    {
        // 特になし（Native 等の解放は行っていない）
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

        // 成長倍率を経験値に乗算（ジェム取得時など）
        int expGain = amount * _growthMultiplier;
        _currentExp += expGain;

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
        _nextLevelExp = Mathf.CeilToInt(_nextLevelExp * nextLevelExpMultiplier);

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
        _maxHp = maxHp;
        _currentHp = maxHp;
        _isInvincible = false;
        _invincibleTimer = 0f;
        _initialFlashTimer = 0f;
        _currentExp = 0;
        _nextLevelExp = initialNextLevelExp;
        _currentLevel = 1;
        _canLevelUp = false;
        _growthMultiplier = 1;

        moveSpeed = initialMoveSpeed;
        magnetDist = initialMagnetDist;

        Debug.Assert(bulletData != null, "[Player] Reset: bulletData が未設定です。インスペクターで Bullet Data を指定してください。");
        _fireRate = bulletData.FireInterval;
        _bulletSpeed = bulletData.Speed;
        _bulletCountPerShot = bulletData.CountPerShot;
        SetBulletDamage(bulletData.Damage);
        _criticalChance = bulletData.CriticalChance;
        _criticalMultiplier = bulletData != null ? bulletData.CriticalMultiplier : 1f;
        _bulletHandler?.SetCritical(_criticalChance, _criticalMultiplier);
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
        if (bulletManager == null || bulletData == null)
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
        if (bulletManager == null || bulletData == null)
        {
            return;
        }

        float speed = GetBulletSpeed();
        float lifeTime = (bulletData != null ? bulletData.LifeTime : 0f) + _bulletLifeTimeBonus;
        _bulletHandler?.Spawn(position, direction, speed, lifeTime, bulletData.DirectionRotation);
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
    public float GetBulletLifeTimeBase() => bulletData != null ? bulletData.LifeTime : 0f;
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
                targetVelocity = moveDir.normalized * (moveSpeed * speedMult);
            }
        }

        // 回転：override 時はマウス方向、それ以外は入力方向＋カメラ
        float rotationAngle = overrideLookAngleDeg ?? movementAngle;
        bool applyRotation = overrideLookAngleDeg.HasValue || (hasInput && isSpacePressed == false);
        if (applyRotation)
        {
            float angle = Mathf.SmoothDampAngle(_cachedTransform.eulerAngles.y, rotationAngle, ref _currentRotationVelocity, 1.0f / rotationSpeed);
            _cachedTransform.rotation = Quaternion.Euler(0f, angle, 0f);
        }

        _currentVelocity = Vector3.SmoothDamp(_currentVelocity, targetVelocity, ref _smoothDampVelocity, accelerationTime);
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
