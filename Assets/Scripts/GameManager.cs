using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using System.Collections.Generic;

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

    // プレイヤーへのダメージを記録するキュー（敵移動 Job 内からメインスレッドへ通知）
    private NativeQueue<int> _playerDamageQueue;
    /// <summary>プレイヤー弾vsボスの当たり判定でヒットした弾のダメージを格納するキュー。</summary>
    private NativeQueue<int> _bossDamageQueue;
    /// <summary>敵弾vsプレイヤーの当たり判定でヒットした弾のダメージを格納するキュー。</summary>
    private NativeQueue<int> _enemyBulletHitDamages;

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
    public Transform PlayerTransform => player != null ? player.CachedTransform : null;

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
        _bossDamageQueue = new NativeQueue<int>(Allocator.Persistent);
        _enemyBulletHitDamages = new NativeQueue<int>(Allocator.Persistent);

        // カウントダウンタイマーを初期化
        _countdownTimer = GetEffectiveCountdownDuration();

        // ステートマシンを初期化
        InitializeStateMachine();
    }

    void InitializeStateMachine()
    {
        cameraManager?.Initialize();
        bulletManager?.Initialize();
        player?.Initialize();
        enemyManager?.Initialize();
        gemManager?.Initialize();
        renderManager?.Initialize();

        GameMode startMode = GameMode.Title;
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
        Debug.Assert(damageTextManager != null, "[GameManager] damageTextManager が未設定です。インスペクターで指定してください。");

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

        float3 playerPos = player.CachedTransform.position;

        // 1. プレイヤー・敵・ボスの移動
        player.ProcessMovement();
        enemyManager.ProcessMovement(playerPos, _playerDamageQueue);

        // 2. 弾発射（プレイヤー・敵・ボス）
        player.ProcessFiring();
        enemyManager.ProcessFiring(playerPos);

        // 3. 弾移動
        bulletManager.ProcessMovement();

        // 4. 当たり判定（プレイヤー弾vs敵・敵弾vsプレイヤー）
        CheckPlayerBulletVsEnemy();
        CheckEnemyBulletVsPlayer();

        // 5. ボス死亡チェック・通常敵の死亡・ダメージ表示・リスポーン
        enemyManager.ProcessDamage(gemManager);
        enemyManager.ProcessRespawn(playerPos);
        CheckPlayerBulletVsBoss();

        // 6. プレイヤーへのダメージ処理
        HandlePlayerDamage();

        // 7. 経験値の取得と加算
        HandleExperience();
    }

    void LateUpdate()
    {
        // LateUpdateでの処理は不要（ステートマシンが自動的に処理する）
    }

    /// <summary>
    /// プレイヤー弾と敵の当たり判定 Job をスケジュールし完了まで待機する。
    /// </summary>
    void CheckPlayerBulletVsEnemy()
    {
        var groups = enemyManager.GetGroups();
        if (groups == null || groups.Count == 0)
            return;

        JobHandle dep = default;
        foreach (var g in groups)
        {
            dep = bulletManager.ProcessDamage(
                player.BulletGroupId,
                g.CellSize,
                g.CollisionRadius * g.CollisionRadius,
                g.SpatialMap,
                g.Positions,
                g.Active,
                g.GetEnemyDamageQueueWriter(),
                dep);
        }
        dep.Complete();
    }

    /// <summary>
    /// 敵弾とプレイヤーの当たり判定。ヒットした弾は無効化し、プレイヤーにダメージを通知する。
    /// </summary>
    void CheckEnemyBulletVsPlayer()
    {
        var groups = enemyManager.GetGroups();
        if (groups == null) return;

        foreach (var group in groups)
        {
            if (group.EnemyBulletGroupId < 0)
            {
                continue;
            }

            bulletManager.ProcessDamage(group.EnemyBulletGroupId, player.CachedTransform.position, player.CollisionRadius, _enemyBulletHitDamages);

            while (_enemyBulletHitDamages.TryDequeue(out int damage))
            {
                _playerDamageQueue.Enqueue(damage);
            }
        }
    }

    /// <summary>
    /// ボスとプレイヤー弾の当たり判定。ヒットした弾は無効化し、ボスにダメージを適用する。
    /// </summary>
    void CheckPlayerBulletVsBoss()
    {
        BossBase boss = enemyManager.GetCurrentBossComponent();
        if (boss == null || boss.IsDead)
            return;

        bulletManager.ProcessDamage(player.BulletGroupId, boss.Position, boss.CollisionRadius, _bossDamageQueue);

        while (_bossDamageQueue.TryDequeue(out int damage))
        {
            int actualDamage = boss.TakeDamage(damage);
            if (actualDamage > 0)
            {
                damageTextManager.ShowDamage(boss.GetDamageTextPosition(), actualDamage, boss.CollisionRadius);
            }
            if (boss.IsDead)
                break;
        }
    }

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
                    damageTextManager?.ShowDamage(player.CachedTransform.position, actualDamage);
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
            _playerDamageQueue.Dispose();
        if (_bossDamageQueue.IsCreated)
            _bossDamageQueue.Dispose();
        if (_enemyBulletHitDamages.IsCreated)
            _enemyBulletHitDamages.Dispose();
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
    /// ノーマルステートで使用するカウントダウン時間を取得。現在ステージの値 → DefaultCountdownDuration の順で採用。
    /// </summary>
    private float GetEffectiveCountdownDuration()
    {
        StageData stage = GetCurrentStageData();
        if (stage != null && stage.CountdownDuration > 0f)
        {
            return stage.CountdownDuration;
        }
        return DefaultCountdownDuration;
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
    public void UpdateCountdownTimer()
    {
        float deltaTime = Time.deltaTime;
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
    
    // プレイヤーの位置を取得（ボス用）
    public Vector3 GetPlayerPosition()
    {
        return player != null ? player.CachedTransform.position : Vector3.zero;
    }

    /// <summary>
    /// タイトル画面用にプレイヤー位置とカメラをリセットする。タイトル進入時に呼ぶ。
    /// </summary>
    public void ResetPlayerAndCameraForTitle()
    {
        if (player != null)
        {
            player.CachedTransform.position = titlePlayerPosition;
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
