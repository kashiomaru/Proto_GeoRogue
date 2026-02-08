using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;
using Unity.Collections;
using Unity.Burst;
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
    [SerializeField] private RenderManager renderManager;

    private const float DefaultCountdownDuration = 60f;

    [Header("Stages")]
    [Tooltip("ステージの並び。各ステージの通常敵・ボス・カウントダウンが適用される")]
    [SerializeField] private StageData[] stages;

    private int _currentStageIndex;

#if UNITY_EDITOR
    [Header("Debug")]
    [Tooltip("デバッグ設定。空の場合はデバッグ無効。GameDebugSettings コンポーネントを付けた GameObject を割り当てると、そのインスペクタで一括 On/Off 可能。")]
    [SerializeField] private GameDebugSettings debugSettings;
#endif

    // プレイヤーへのダメージを記録するキュー（敵移動 Job 内からメインスレッドへ通知）
    private NativeQueue<int> _playerDamageQueue;

    private float _countdownTimer;

    // ステートマシン
    private StateMachine<GameMode, GameManager> _stateMachine;

    /// <summary>予約された遷移先。Update の末尾（現在ステートの OnUpdate 後）に実際に遷移する。</summary>
    private GameMode? _pendingGameMode;

    public GameMode CurrentMode => _stateMachine?.CurrentStateKey ?? GameMode.None;

    /// <summary>
    /// プレイ中（弾・敵の処理を行う）かどうか。現在のステートの IsPlaying を返す。
    /// </summary>
    public bool IsPlaying => (_stateMachine?.CurrentState as GameStateBase)?.IsPlaying ?? false;

    // ステートからアクセスするためのプロパティ
    public EnemyManager EnemyManager => enemyManager;
    public UIManager UIManager => uiManager;
    public CameraManager CameraManager => cameraManager;
    public Transform PlayerTransform => playerTransform;

    /// <summary>現在プレイ中のステージデータ。ステージ未設定の場合は null。</summary>
    public StageData GetCurrentStageData()
    {
        if (stages == null || stages.Length == 0) return null;
        if (_currentStageIndex < 0 || _currentStageIndex >= stages.Length) return null;
        return stages[_currentStageIndex];
    }

    /// <summary>次のステージが存在するか。</summary>
    public bool HasNextStage()
    {
        return stages != null && stages.Length > 0 && _currentStageIndex + 1 < stages.Length;
    }

    /// <summary>次のステージへ進める（GameClear で次ステージへ行くときに呼ぶ）。</summary>
    public void AdvanceToNextStage()
    {
        if (stages != null && _currentStageIndex + 1 < stages.Length)
        {
            _currentStageIndex++;
        }
    }

    /// <summary>Normal 開始時に現在ステージの通常敵設定とカウントダウンを適用する。</summary>
    public void PrepareForNormalStage()
    {
        StageData stage = GetCurrentStageData();
        if (stage != null)
        {
            enemyManager?.ApplyNormalEnemyConfig(stage);
        }
        _countdownTimer = GetEffectiveCountdownDuration();
    }

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
        if (debugSettings != null && debugSettings.EnableDebug && debugSettings.EnableDebugPlayerHp)
        {
            player?.SetHpForDebug(debugSettings.DebugPlayerHp);
        }
    }
#endif
    
    void InitializeStateMachine()
    {
        cameraManager?.Initialize();
        bulletManager?.Initialize();
        player?.Initialize();
        enemyManager?.Initialize();
        gemManager?.Initialize();
        renderManager?.Initialize();

#if UNITY_EDITOR
        GameMode startMode = (debugSettings != null && debugSettings.EnableDebug)
            ? debugSettings.InitialGameMode
            : GameMode.Title;
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
        // 参照はインスペクターで必ず指定すること
        Debug.Assert(enemyManager != null && bulletManager != null, "[GameManager] enemyManager または bulletManager が未設定です。インスペクターで指定してください。");

        // ステートマシンを更新（現在ステートの OnUpdate）
        _stateMachine?.Update();

        // 予約された遷移を適用（OnUpdate の後に実行するため、OnEnter 内で ChangeGameMode されても順序が明確）
        if (_pendingGameMode.HasValue)
        {
            GameMode next = _pendingGameMode.Value;
            _pendingGameMode = null;
            _stateMachine?.ChangeState(next);
            ApplyEnemyManagerFlags(next);
        }

        // プレイ中でない場合は処理をスキップ
        if (IsPlaying == false)
        {
            return;
        }

        float deltaTime = Time.deltaTime;
        float3 playerPos = playerTransform.position;

        // 1. プレイヤー・敵・ボスの移動
        player.ProcessMovement(deltaTime);
        enemyManager.ProcessMovement(deltaTime, playerPos, _playerDamageQueue);

        // 2. 弾発射（プレイヤー・敵・ボス）
        player.ProcessFiring(deltaTime);
        enemyManager.ProcessFiring(deltaTime, playerPos);

        // 3. 弾移動
        bulletManager.ProcessMovement(deltaTime);

        // 4. 当たり判定（プレイヤー弾vs敵・敵弾vsプレイヤー）
        bulletManager.ScheduleCollideJob(enemyManager);
        bulletManager.CheckEnemyBulletVsPlayer();

        // 5. ボス死亡チェック・通常敵の死亡・ダメージ表示・リスポーン
        enemyManager.ProcessDamage(gemManager);
        enemyManager.HandleRespawn();

        // 6. プレイヤーへのダメージ処理
        HandlePlayerDamage();

        // 7. 経験値の取得と加算
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
        _currentStageIndex = 0;

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
    /// ノーマルステートで使用するカウントダウン時間を取得。デバッグ上書き → 現在ステージの値 → DefaultCountdownDuration の順で採用。
    /// </summary>
    private float GetEffectiveCountdownDuration()
    {
#if UNITY_EDITOR
        if (debugSettings != null && debugSettings.EnableDebug && debugSettings.EnableDebugCountdown)
        {
            return debugSettings.DebugCountdownTime;
        }
#endif
        StageData stage = GetCurrentStageData();
        if (stage != null && stage.CountdownDuration > 0f)
        {
            return stage.CountdownDuration;
        }
        return DefaultCountdownDuration;
    }

    /// <summary>
    /// ボス生成時に使用するHPのデバッグ用上書き値。デバッグ有効かつボスHP上書き時のみ値が入る（エディタのみ）。
    /// </summary>
    public float? GetDebugBossHpOverride()
    {
#if UNITY_EDITOR
        if (debugSettings != null && debugSettings.EnableDebug && debugSettings.EnableDebugBossHp)
        {
            return debugSettings.DebugBossHp;
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
    /// ゲームモードの遷移を予約する。実際の遷移はこのフレームの Update の末尾（現在ステートの OnUpdate の後）に行う。
    /// </summary>
    public void ChangeGameMode(GameMode newMode)
    {
        _pendingGameMode = newMode;
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

    /// <summary>
    /// ダメージテキストをリセットする。ゲームクリア進入時などに呼ぶ。
    /// </summary>
    public void ResetDamageText()
    {
        damageTextManager?.Reset();
    }

    /// <summary>
    /// 弾をリセットする。ボスステート進入時などに呼ぶ。
    /// </summary>
    public void ResetBullets()
    {
        bulletManager?.ResetBullets();
    }
}
