using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using System.Collections.Generic;

// 敵へのダメージ情報を保持する構造体
public struct EnemyDamageInfo
{
    public float3 position;
    public float damage;
    
    public EnemyDamageInfo(float3 pos, float dmg)
    {
        position = pos;
        damage = dmg;
    }
}

// ゲームモード
public enum GameMode
{
    None,       // モードなし（弾も敵も出ない）
    Title,      // タイトル
    Normal,     // 通常モード
    Boss,       // ボスモード
    GameClear,  // ゲームクリア
    GameOver    // ゲームオーバー
}

public class GameManager : MonoBehaviour
{
    [Header("Params")]
    [SerializeField] private Transform playerTransform;

    [Header("Title")]
    [Tooltip("タイトル画面に入ったときにプレイヤーを移動させる位置")]
    [SerializeField] private Vector3 titlePlayerPosition = Vector3.zero;

    [Header("References")]
    [SerializeField] private BulletManager bulletManager;
    [SerializeField] private EnemyManager enemyManager;
    [SerializeField] private GemManager gemManager;
    [SerializeField] private LevelUpManager levelUpManager;
    [SerializeField] private Player player;
    [SerializeField] private UIManager uiManager;
    [SerializeField] private DamageTextManager damageTextManager;
    [SerializeField] private CameraManager cameraManager;
    
    [Header("Countdown Timer")]
    [SerializeField] private float countdownDuration = 60f; // カウントダウン時間（秒、デフォルト1分）
    
    [Header("Debug (Editor Only)")]
    [Tooltip("初期ゲームモード。エディタでのデバッグ用。ビルドでは無視され、常にタイトルから開始します。")]
    [SerializeField] private GameMode initialGameMode = GameMode.None;

    [Tooltip("ノーマルステートのカウントダウン時間をデバッグ用に上書きするか。ビルドでは無視されます。")]
    [SerializeField] private bool enableDebugCountdown = false;

    [Tooltip("有効時、ノーマルステートで使用するカウントダウン時間（秒）。enableDebugCountdown が true のときのみ使用されます。")]
    [SerializeField] private float debugCountdownTime = 10f;

    [Tooltip("ボスの最大HPをデバッグ用に上書きするか。ビルドでは無視されます。")]
    [SerializeField] private bool enableDebugBossHp = false;

    [Tooltip("有効時、ボス生成時に使用する最大HP。enableDebugBossHp が true のときのみ使用されます。")]
    [SerializeField] private float debugBossHp = 10f;

    [Tooltip("プレイヤーのHPをデバッグ用に上書きするか。ビルドでは無視されます。")]
    [SerializeField] private bool enableDebugPlayerHp = false;

    [Tooltip("有効時、プレイヤーの最大HPと現在HPに設定する値。enableDebugPlayerHp が true のときのみ使用されます。")]
    [SerializeField] private int debugPlayerHp = 1;

    // プレイヤーへのダメージを記録するキュー（敵移動 Job 内からメインスレッドへ通知）
    private NativeQueue<int> _playerDamageQueue;

    private float _countdownTimer;

    // ステートマシン
    private StateMachine<GameMode, GameManager> _stateMachine;

    public GameMode CurrentMode => _stateMachine?.CurrentStateKey ?? GameMode.None;

    /// <summary>
    /// プレイ中（弾・敵の処理を行う）かどうか。現在のステートの IsPlaying を返す。
    /// </summary>
    public bool IsPlaying => (_stateMachine?.CurrentState as GameStateBase)?.IsPlaying ?? false;

    /// <summary>
    /// 遷移先のゲームモード（OnExit 呼び出し中のみ有効。時間復帰の判定に使用）
    /// </summary>
    public GameMode? NextGameMode { get; private set; }
    
    // ステートからアクセスするためのプロパティ
    public EnemyManager EnemyManager => enemyManager;
    public UIManager UIManager => uiManager;
    public CameraManager CameraManager => cameraManager;
    public Transform PlayerTransform => playerTransform;

    void Start()
    {
        _playerDamageQueue = new NativeQueue<int>(Allocator.Persistent);

        // カウントダウンタイマーを初期化
        _countdownTimer = GetEffectiveCountdownDuration();

        // ステートマシンを初期化
        InitializeStateMachine();

#if UNITY_EDITOR
        ApplyDebugPlayerHp();
#endif
    }

#if UNITY_EDITOR
    private void ApplyDebugPlayerHp()
    {
        if (enableDebugPlayerHp)
        {
            player?.SetHpForDebug(debugPlayerHp);
        }
    }
#endif
    
    void InitializeStateMachine()
    {
        cameraManager?.Initialize();
        bulletManager?.Initialize();
        enemyManager?.Initialize();

#if UNITY_EDITOR
        GameMode startMode = initialGameMode;
#else
        GameMode startMode = GameMode.Title;
#endif
        ApplyEnemyManagerFlags(startMode);

        _stateMachine = new StateMachine<GameMode, GameManager>(this);

        // 各ステートを登録
        _stateMachine.RegisterState(GameMode.None, new NoneGameState());
        _stateMachine.RegisterState(GameMode.Title, new TitleGameState());
        _stateMachine.RegisterState(GameMode.Normal, new NormalGameState());
        _stateMachine.RegisterState(GameMode.Boss, new BossGameState());
        _stateMachine.RegisterState(GameMode.GameClear, new GameClearGameState());
        _stateMachine.RegisterState(GameMode.GameOver, new GameOverGameState());

        // 初期ステートを設定
        _stateMachine.Initialize(startMode);
    }

    void Update()
    {
        // ステートマシンを更新
        _stateMachine?.Update();
        
        // プレイ中でない場合は処理をスキップ
        if (IsPlaying == false)
        {
            return;
        }

        bulletManager?.HandleShooting();

        float deltaTime = Time.deltaTime;
        float3 playerPos = playerTransform.position;

        if (enemyManager != null && bulletManager != null)
        {
            JobHandle enemyHandle = enemyManager.ScheduleEnemyMoveJob(deltaTime, playerPos, _playerDamageQueue.AsParallelWriter());
            JobHandle bulletHandle = bulletManager.ScheduleMoveAndCollideJob(deltaTime, enemyHandle, enemyManager);
            bulletHandle.Complete();
        }
        
        // 3. プレイヤーへのダメージ処理
        HandlePlayerDamage();
        
        // 4. 経験値の取得と加算
        HandleExperience();
    }

    void LateUpdate()
    {
        // LateUpdateでの処理は不要（ステートマシンが自動的に処理する）
    }
    
    // --- 初期化 & ユーティリティ ---

    void HandlePlayerDamage()
    {
        // キューからダメージを取得してプレイヤーに適用
        if (player != null && player.IsDead == false)
        {
            int totalDamage = 0;
            while (_playerDamageQueue.TryDequeue(out int damage))
            {
                totalDamage += damage;
            }
            
            if (totalDamage > 0)
            {
                // ダメージを適用し、実際に与えたダメージを取得
                int actualDamage = player.TakeDamage(totalDamage);
                
                // 実際にダメージが与えられた場合のみダメージテキストを表示
                if (actualDamage > 0)
                {
                    damageTextManager?.ShowDamage(playerTransform.position, actualDamage);
                }
                
                // HPが0になったらゲームオーバーステートへ
                if (player.IsDead)
                {
                    ChangeGameMode(GameMode.GameOver);
                }
            }
        }
    }
    
    void HandleExperience()
    {
        // GemManagerから回収されたジェムの数を取得して経験値を加算
        if (gemManager != null && player != null)
        {
            int expGained = gemManager.GetCollectedGemCount();
            if (expGained > 0)
            {
                player.AddExperience(expGained);
            }
        }
    }
    

    void OnDestroy()
    {
        if (_playerDamageQueue.IsCreated)
        {
            _playerDamageQueue.Dispose();
        }
    }
    
    /// <summary>
    /// ゲーム状態のみリセット（モードは切り替えない）。タイトルに戻る前やリトライ時に使用。
    /// </summary>
    public void ResetGameState()
    {
        // プレイヤーをリセット（HP・経験値・レベル・アップグレードパラメータは Player.Reset() で復元）
        player?.Reset();

        gemManager?.ResetGems();

        enemyManager?.ResetEnemies();
        bulletManager?.ResetBullets();

        while (_playerDamageQueue.TryDequeue(out _)) { }
        damageTextManager?.Reset();

        // カウントダウンタイマーをリセット
        _countdownTimer = GetEffectiveCountdownDuration();
    }

    /// <summary>
    /// ノーマルステートで使用するカウントダウン時間を取得。デバッグ有効時はデバッグ用の値（エディタのみ）。
    /// </summary>
    private float GetEffectiveCountdownDuration()
    {
#if UNITY_EDITOR
        if (enableDebugCountdown)
        {
            return debugCountdownTime;
        }
#endif
        return countdownDuration;
    }

    /// <summary>
    /// ボス生成時に使用するHPのデバッグ用上書き値。エディタでデバッグ有効時のみ値が入る。ビルドでは常に null。
    /// </summary>
    public float? GetDebugBossHpOverride()
    {
#if UNITY_EDITOR
        if (enableDebugBossHp)
        {
            return debugBossHp;
        }
#endif
        return null;
    }

    // カウントダウンタイマー取得用メソッド
    public float GetCountdownTime()
    {
        return _countdownTimer;
    }
    
    public bool IsCountdownFinished()
    {
        return _countdownTimer <= 0f;
    }
    
    /// <summary>
    /// カウントダウンタイマーを更新する（ステートから呼ばれる）
    /// </summary>
    public void UpdateCountdownTimer(float deltaTime)
    {
        if (_countdownTimer > 0f)
        {
            _countdownTimer -= deltaTime;
            if (_countdownTimer < 0f)
            {
                _countdownTimer = 0f;
            }
        }
    }
    
    /// <summary>
    /// ゲームモードを変更する（ステートから呼ばれる）
    /// </summary>
    public void ChangeGameMode(GameMode newMode)
    {
        if (_stateMachine != null)
        {
            NextGameMode = newMode;
            _stateMachine.ChangeState(newMode);
            NextGameMode = null;
            ApplyEnemyManagerFlags(newMode);
        }
    }

    private void ApplyEnemyManagerFlags(GameMode mode)
    {
        enemyManager?.SetNormalEnemiesEnabled(mode == GameMode.Normal);
        enemyManager?.SetBossActive(mode == GameMode.Boss);
    }
    
    /// <summary>
    /// ボスと弾の当たり判定（ステートから呼ばれる）
    /// </summary>
    public void CheckBossBulletCollision()
    {
        bulletManager?.CheckBossBulletCollision(enemyManager);
    }
    
    // カメラ切り替え処理
    // immediate: true のときブレンドなしで即時切り替え、false のときはブレンド補間
    public void SwitchCamera(int cameraIndex, bool immediate = false)
    {
        cameraManager?.SwitchCamera(cameraIndex, immediate);
    }
    
    public void SwitchCameraByName(string cameraName)
    {
        cameraManager?.SwitchCameraByName(cameraName);
    }
    
    // プレイヤーへのダメージを追加（ボス用）
    public void AddPlayerDamage(int damage)
    {
        if (_playerDamageQueue.IsCreated)
        {
            _playerDamageQueue.Enqueue(damage);
        }
    }
    
    // プレイヤーの位置を取得（ボス用）
    public Vector3 GetPlayerPosition()
    {
        return playerTransform != null ? playerTransform.position : Vector3.zero;
    }

    /// <summary>
    /// タイトル画面用にプレイヤー位置とカメラをリセットする。タイトル進入時に呼ぶ。
    /// </summary>
    public void ResetPlayerAndCameraForTitle()
    {
        if (playerTransform != null)
        {
            playerTransform.position = titlePlayerPosition;
        }
        SwitchCamera(0, immediate: true);
    }
}
