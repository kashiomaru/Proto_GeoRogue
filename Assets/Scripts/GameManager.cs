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
    Normal, // 通常モード
    Boss    // ボスモード
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
    [SerializeField] private GameMode initialGameMode = GameMode.Normal; // 初期ゲームモード（インスペクターで設定可能）

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
    private GameMode _currentMode = GameMode.Normal; // 現在のゲームモード

    void Start()
    {
        InitializeBullets();
        
        // プレイヤーへのダメージを記録するキューを初期化
        _playerDamageQueue = new NativeQueue<int>(Allocator.Persistent);
        
        // カウントダウンタイマーを初期化
        _countdownTimer = countdownDuration;
        
        // 初期ゲームモードを設定
        _currentMode = initialGameMode;
        
        // 初期モードがボスモードの場合、ボスモードに切り替える
        if (_currentMode == GameMode.Boss)
        {
            SwitchToBossMode();
        }
    }

    void Update()
    {
        // カウントダウンタイマーの更新
        bool wasTimerRunning = _countdownTimer > 0f;
        if (_countdownTimer > 0f)
        {
            _countdownTimer -= Time.deltaTime;
            if (_countdownTimer < 0f)
            {
                _countdownTimer = 0f;
            }
        }
        
        // タイマーがゼロになった瞬間、ボスモードに切り替える
        if (wasTimerRunning && _countdownTimer <= 0f)
        {
            SwitchToBossMode();
        }
        
        // 1. 弾の発射（プレイヤー位置から）
        HandleShooting();

        float deltaTime = Time.deltaTime;
        float3 playerPos = playerTransform.position;

        // 2. 敵の移動Jobをスケジュール
        JobHandle enemyHandle = default;
        if (enemyManager != null)
        {
            enemyHandle = enemyManager.ScheduleEnemyMoveJob(deltaTime, playerPos, _playerDamageQueue.AsParallelWriter());
        }

        // --- JOB 2: 弾の移動 & 衝突判定 ---
        // 敵の移動が終わってから実行する必要があるため、enemyHandleに依存させる
        JobHandle bulletHandle = default;
        if (enemyManager != null)
        {
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
        
            bulletHandle = bulletJob.Schedule(_bulletTransforms, enemyHandle);
        }

        // 完了待ち
        if (enemyManager != null)
        {
            bulletHandle.Complete();
        }
        
        // 死んだ敵の位置からジェムを生成
        if (enemyManager != null)
        {
            enemyManager.ProcessDeadEnemies(gemManager);
        }
        
        // 3. 敵へのダメージ表示処理
        if (enemyManager != null)
        {
            enemyManager.ProcessEnemyDamage();
        }
        
        // 4. プレイヤーへのダメージ処理
        HandlePlayerDamage();
        
        // 5. 経験値の取得と加算
        HandleExperience();

        // 6. 敵のリスポーン処理（タイマーがゼロでない場合のみ）
        if (_countdownTimer > 0f && enemyManager != null)
        {
            enemyManager.HandleRespawn(playerPos);
        }
        
        // 7. フラッシュタイマーの更新とRenderManagerによる描画
        if (enemyManager != null)
        {
            enemyManager.UpdateAndRender(deltaTime);
        }
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
    
    // ゲームリセット処理
    public void ResetGame()
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
        
        // 通常モードに戻す
        _currentMode = GameMode.Normal;
        
        // タイマーを表示する
        if (uiManager != null)
        {
            uiManager.ShowCountdownTimer();
        }
        
        // カメラをインデックス0に戻す
        if (cameraManager != null)
        {
            cameraManager.SwitchCamera(0);
        }
    }
    
    private void OnRetryClicked()
    {
        ResetGame();
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
    
    // カメラ切り替え処理
    public void SwitchCamera(int cameraIndex)
    {
        if (cameraManager != null)
        {
            cameraManager.SwitchCamera(cameraIndex);
        }
    }
    
    public void SwitchCameraByName(string cameraName)
    {
        if (cameraManager != null)
        {
            cameraManager.SwitchCameraByName(cameraName);
        }
    }
    
    // ボスモードに切り替え
    private void SwitchToBossMode()
    {
        _currentMode = GameMode.Boss;
        
        // すべての敵を非アクティブにする
        if (enemyManager != null)
        {
            enemyManager.ClearAllEnemies();
        }
        
        // ボスを生成（プレイヤーの位置と方向を渡す）
        if (enemyManager != null && playerTransform != null)
        {
            Vector3 playerPosition = playerTransform.position;
            Vector3 playerForward = playerTransform.forward;
            enemyManager.SpawnBoss(playerPosition, playerForward);
            
            // ボスのTransformをLookAtConstraintのターゲットに設定
            if (cameraManager != null)
            {
                GameObject boss = enemyManager.GetCurrentBoss();
                if (boss != null)
                {
                    cameraManager.SetBossLookAtTarget(boss.transform);
                }
            }
        }
        
        // カメラをインデックス0から1に切り替え
        if (cameraManager != null)
        {
            cameraManager.SwitchCamera(1);
        }
        
        // タイマーを非表示にする
        if (uiManager != null)
        {
            uiManager.HideCountdownTimer();
        }
    }
    
    // 現在のゲームモードを取得
    public GameMode GetCurrentMode()
    {
        return _currentMode;
    }
    
    // 通常モードかどうか
    public bool IsNormalMode()
    {
        return _currentMode == GameMode.Normal;
    }
    
    // ボスモードかどうか
    public bool IsBossMode()
    {
        return _currentMode == GameMode.Boss;
    }
    
    // プレイヤーへのダメージを追加（ボス用）
    public void AddPlayerDamage(int damage)
    {
        if (_playerDamageQueue.IsCreated)
        {
            _playerDamageQueue.Enqueue(damage);
        }
    }
    
}
