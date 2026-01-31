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
    GameClear   // ゲームクリア
}

public class GameManager : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private GameObject bulletPrefab; // 弾のプレハブ（GPU Instancing ONのマテリアル推奨）
    [SerializeField] private int maxBullets = 1000; // 画面内に出せる弾の上限
    
    [Header("Params")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private float bulletSpeed = 20f;
    [SerializeField] private float fireRate = 0.1f;
    
    [Header("MultiShot Settings")]
    public int bulletCountPerShot = 1; // 1回の発射数（レベルアップでこれを増やす）
    [SerializeField] private float multiShotSpreadAngle = 10f; // 弾の拡散角度（10度ずつ広がるなど）
    
    [Header("References")]
    [SerializeField] private EnemyManager enemyManager; // EnemyManagerへの参照
    [SerializeField] private GemManager gemManager; // GemManagerへの参照
    [SerializeField] private LevelUpManager levelUpManager; // LevelUpManagerへの参照
    [SerializeField] private Player player; // Playerへの参照
    [SerializeField] private UIManager uiManager; // UIManagerへの参照
    [SerializeField] private DamageTextManager damageTextManager; // ダメージテキスト表示用
    [SerializeField] private CameraManager cameraManager; // CameraManagerへの参照
    
    [Header("Combat")]
    [SerializeField] private float bulletDamage = 1.0f; // 弾のダメージ
    
    [Header("Countdown Timer")]
    [SerializeField] private float countdownDuration = 60f; // カウントダウン時間（秒、デフォルト1分）
    
    [Header("Game Mode")]
    [SerializeField] private GameMode initialGameMode = GameMode.None; // 初期ゲームモード（インスペクターで設定可能）

    // --- Bullet Data ---
    private TransformAccessArray _bulletTransforms; // 今回は簡易的にTransformを使いますが、本来はMatrix配列で描画すべき
    private NativeArray<float3> _bulletPositions;
    private NativeArray<float3> _bulletDirections; // 弾の方向ベクトル（後方互換性のため残す）
    private NativeArray<float3> _bulletVelocities; // 弾ごとの速度ベクトルを保存する配列
    private NativeArray<bool> _bulletActive;
    private NativeArray<float> _bulletLifeTime;
    
    // --- Damage Queue ---
    // プレイヤーへのダメージを記録するキュー（Job内からメインスレッドへ通知）
    private NativeQueue<int> _playerDamageQueue;

    private float _timer;
    private int _bulletIndexHead = 0; // リングバッファ用
    private float _countdownTimer; // カウントダウンタイマー
    
    // ステートマシン
    private StateMachine<GameMode, GameManager> _stateMachine;

    public GameMode CurrentMode => _stateMachine?.CurrentStateKey ?? GameMode.None;
    
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
        InitializeBullets();
        
        // プレイヤーへのダメージを記録するキューを初期化
        _playerDamageQueue = new NativeQueue<int>(Allocator.Persistent);
        
        // カウントダウンタイマーを初期化
        _countdownTimer = countdownDuration;
        
        // ステートマシンを初期化
        InitializeStateMachine();
    }
    
    void InitializeStateMachine()
    {
        cameraManager?.Initialize();
        enemyManager?.Initialize();
        enemyManager?.SetGameMode(initialGameMode);

        _stateMachine = new StateMachine<GameMode, GameManager>(this);
        
        // 各ステートを登録
        _stateMachine.RegisterState(GameMode.None, new NoneGameState());
        _stateMachine.RegisterState(GameMode.Title, new TitleGameState());
        _stateMachine.RegisterState(GameMode.Normal, new NormalGameState());
        _stateMachine.RegisterState(GameMode.Boss, new BossGameState());
        _stateMachine.RegisterState(GameMode.GameClear, new GameClearGameState());
        
        // 初期ステートを設定
        _stateMachine.Initialize(initialGameMode);
    }

    void Update()
    {
        // ステートマシンを更新
        if (_stateMachine != null)
        {
            _stateMachine.Update();
        }
        
        // プレイ中でないモードの場合は処理をスキップ
        if (CurrentMode == GameMode.None || CurrentMode == GameMode.Title || CurrentMode == GameMode.GameClear)
        {
            return;
        }
        
        // 1. 弾の発射（プレイヤー位置から）
        HandleShooting();

        float deltaTime = Time.deltaTime;
        float3 playerPos = playerTransform.position;

        // 2. 敵の移動Jobをスケジュール
        if (enemyManager != null)
        {
            JobHandle enemyHandle = enemyManager.ScheduleEnemyMoveJob(deltaTime, playerPos, _playerDamageQueue.AsParallelWriter());

            // --- JOB 2: 弾の移動 & 衝突判定 ---
            // 敵の移動が終わってから実行する必要があるため、enemyHandleに依存させる
            var bulletJob = new BulletMoveAndCollideJob
            {
                deltaTime = deltaTime,
                speed = bulletSpeed,
                cellSize = enemyManager.CellSize,
                spatialMap = enemyManager.SpatialMap, // 読み込みのみ
                enemyPositions = enemyManager.EnemyPositions, // 敵の位置参照
                bulletPositions = _bulletPositions,
                bulletDirections = _bulletDirections, // 弾の方向（後方互換性のため）
                bulletVelocities = _bulletVelocities, // 弾の速度ベクトル
                bulletActive = _bulletActive,
                bulletLifeTime = _bulletLifeTime,
                enemyActive = enemyManager.EnemyActive, // ヒットしたらfalseにする
                enemyHp = enemyManager.EnemyHp, // 敵のHP配列
                bulletDamage = bulletDamage, // 弾のダメージ
                deadEnemyPositions = enemyManager.GetDeadEnemyPositionsWriter(), // 死んだ敵の位置を記録
                enemyDamageQueue = enemyManager.GetEnemyDamageQueueWriter(), // 敵へのダメージ情報を記録
                enemyFlashQueue = enemyManager.GetEnemyFlashQueueWriter() // フラッシュタイマー設定用
            };
        
            JobHandle bulletHandle = bulletJob.Schedule(_bulletTransforms, enemyHandle);

            // 完了待ち
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

    void InitializeBullets()
    {
        _bulletTransforms = new TransformAccessArray(maxBullets);
        _bulletPositions = new NativeArray<float3>(maxBullets, Allocator.Persistent);
        _bulletDirections = new NativeArray<float3>(maxBullets, Allocator.Persistent);
        _bulletVelocities = new NativeArray<float3>(maxBullets, Allocator.Persistent);
        _bulletActive = new NativeArray<bool>(maxBullets, Allocator.Persistent);
        _bulletLifeTime = new NativeArray<float>(maxBullets, Allocator.Persistent);

        // プール生成（最初は見えない場所に置く）
        for (int i = 0; i < maxBullets; i++)
        {
            var obj = Instantiate(bulletPrefab, new Vector3(0, -100, 0), Quaternion.identity);
            if(obj.TryGetComponent<Collider>(out var col)) col.enabled = false;
            _bulletTransforms.Add(obj.transform);
            _bulletActive[i] = false;
        }
    }

    void HandleShooting()
    {
        _timer += Time.deltaTime;
        if (_timer >= fireRate)
        {
            _timer = 0;

            // プレイヤーの正面方向
            Vector3 baseDir = playerTransform.forward; 

            // 発射数ぶんループ
            for (int i = 0; i < bulletCountPerShot; i++)
            {
                // リングバッファからID取得
                int id = _bulletIndexHead;
                _bulletIndexHead = (_bulletIndexHead + 1) % maxBullets;

                // --- 角度計算（重要） ---
                // i=0, count=1 -> 0
                // i=0,1 count=2 -> -5, +5
                // i=0,1,2 count=3 -> -10, 0, +10
                
                float angle = 0f;
                if (bulletCountPerShot > 1)
                {
                    // 全体の開き幅の中心を0としてオフセット計算
                    angle = -multiShotSpreadAngle * (bulletCountPerShot - 1) * 0.5f + (multiShotSpreadAngle * i);
                }

                // クォータニオンで回転させる
                Quaternion rot = Quaternion.AngleAxis(angle, Vector3.up);
                Vector3 finalDir = rot * baseDir;

                // データのセット
                _bulletActive[id] = true;
                _bulletLifeTime[id] = 2.0f;
                
                // 位置：プレイヤー位置
                _bulletPositions[id] = (float3)playerTransform.position;
                
                // 方向ベクトル（後方互換性のため）
                _bulletDirections[id] = (float3)finalDir;

                // ★ここで計算したベクトルをNativeArrayに入れる
                _bulletVelocities[id] = (float3)(finalDir.normalized * bulletSpeed);
            }
        }
    }
    
    void HandlePlayerDamage()
    {
        // キューからダメージを取得してプレイヤーに適用
        if (player != null && !player.IsDead)
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
                if (actualDamage > 0 && damageTextManager != null)
                {
                    damageTextManager.ShowDamage(playerTransform.position, actualDamage);
                }
                
                // HPが0になったらゲームオーバー
                if (player.IsDead && uiManager != null)
                {
                    uiManager.ShowGameOver(OnRetryClicked);
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
        if (_bulletTransforms.isCreated) _bulletTransforms.Dispose();
        if (_bulletPositions.IsCreated) _bulletPositions.Dispose();
        if (_bulletDirections.IsCreated) _bulletDirections.Dispose();
        if (_bulletVelocities.IsCreated) _bulletVelocities.Dispose();
        if (_bulletActive.IsCreated) _bulletActive.Dispose();
        if (_bulletLifeTime.IsCreated) _bulletLifeTime.Dispose();
        
        if (_playerDamageQueue.IsCreated) _playerDamageQueue.Dispose();
    }
    
    /// <summary>
    /// ゲーム状態のみリセット（モードは切り替えない）。タイトルに戻る前やリトライ時に使用。
    /// </summary>
    public void ResetGameState()
    {
        // プレイヤーをリセット
        if (player != null)
        {
            player.ResetPlayer();
        }
        
        // 経験値とレベルはPlayerのResetPlayer()でリセットされる
        
        // パラメータをリセット
        fireRate = 0.1f;
        bulletSpeed = 20f;
        bulletCountPerShot = 1;
        if (player != null)
        {
            player.SetMoveSpeed(5f);
        }
        if (gemManager != null)
        {
            gemManager.SetMagnetDist(5.0f);
        }
        
        // 敵をリセット
        if (enemyManager != null)
        {
            enemyManager.ResetEnemies();
        }
        
        // 弾をリセット
        for (int i = 0; i < maxBullets; i++)
        {
            _bulletActive[i] = false;
            _bulletPositions[i] = new float3(0, -100, 0);
        }
        
        // キューをクリア
        while (_playerDamageQueue.TryDequeue(out _)) { }
        
        // タイマーをリセット
        _timer = 0f;
        _bulletIndexHead = 0;
        
        // カウントダウンタイマーをリセット
        _countdownTimer = countdownDuration;
    }
    
    private void OnRetryClicked()
    {
        ResetGameState();

        // 通常モードに戻す（NormalGameState.OnEnter でカメラ0に即時切り替えされる）
        ChangeGameMode(GameMode.Normal);
    }
    
    // LevelUpManager用のパラメータ取得・設定メソッド
    public float GetFireRate()
    {
        return fireRate;
    }
    
    public void SetFireRate(float value)
    {
        fireRate = value;
    }
    
    public float GetBulletSpeed()
    {
        return bulletSpeed;
    }
    
    public void SetBulletSpeed(float value)
    {
        bulletSpeed = value;
    }
    
    public int GetBulletCountPerShot()
    {
        return bulletCountPerShot;
    }
    
    public void SetBulletCountPerShot(int value)
    {
        bulletCountPerShot = value;
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
            
            // EnemyManagerにモードを設定
            if (enemyManager != null)
            {
                enemyManager.SetGameMode(newMode);
            }
        }
    }
    
    /// <summary>
    /// ボスと弾の当たり判定（ステートから呼ばれる）
    /// </summary>
    public void CheckBossBulletCollision()
    {
        if (enemyManager == null) return;
        
        GameObject bossObject = enemyManager.GetCurrentBoss();
        if (bossObject == null) return;
        
        Boss boss = bossObject.GetComponent<Boss>();
        if (boss == null || boss.IsDead) return;
        
        float3 bossPos = (float3)boss.Position;
        float bossRadius = boss.CollisionRadius;
        float bossRadiusSq = bossRadius * bossRadius;
        
        // 全てのアクティブな弾をチェック
        for (int i = 0; i < maxBullets; i++)
        {
            if (!_bulletActive[i]) continue;
            
            float3 bulletPos = _bulletPositions[i];
            float distSq = math.distancesq(bulletPos, bossPos);
            
            // 当たり判定
            if (distSq < bossRadiusSq)
            {
                // ヒット！
                float actualDamage = boss.TakeDamage(bulletDamage);
                
                // 弾を無効化
                _bulletActive[i] = false;
                if (_bulletTransforms.isCreated && i < _bulletTransforms.length)
                {
                    _bulletTransforms[i].position = new Vector3(0, -100, 0);
                }
                _bulletPositions[i] = new float3(0, -100, 0);
                
                // ボスが死亡した場合、それ以降の弾のチェックをスキップ
                if (boss.IsDead)
                {
                    // ボスを削除（EnemyManagerで処理される可能性もあるが、念のため）
                    // ここではGameManagerからは削除しない（EnemyManagerに任せる）
                    break; // ボスが死んだら、それ以降の弾のチェックをスキップ
                }
            }
        }
    }
    
    // カメラ切り替え処理
    // immediate: true のときブレンドなしで即時切り替え、false のときはブレンド補間
    public void SwitchCamera(int cameraIndex, bool immediate = false)
    {
        if (cameraManager != null)
        {
            cameraManager.SwitchCamera(cameraIndex, immediate);
        }
    }
    
    public void SwitchCameraByName(string cameraName)
    {
        if (cameraManager != null)
        {
            cameraManager.SwitchCameraByName(cameraName);
        }
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
        if (playerTransform != null)
        {
            return playerTransform.position;
        }
        return Vector3.zero;
    }
    
}
