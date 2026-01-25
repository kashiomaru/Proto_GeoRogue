using UnityEngine;
using UnityEngine.Jobs;
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

public class GameManager : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private GameObject cubePrefab;
    [SerializeField] private GameObject bulletPrefab; // 弾のプレハブ（GPU Instancing ONのマテリアル推奨）
    [SerializeField] private int enemyCount = 3000;
    [SerializeField] private int maxBullets = 1000; // 画面内に出せる弾の上限
    
    [Header("Params")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private float enemySpeed = 5f;
    [SerializeField] private float bulletSpeed = 20f;
    [SerializeField] private float fireRate = 0.1f;
    [SerializeField] private float cellSize = 2.0f; // 空間分割のグリッドサイズ（敵のサイズの2倍程度が目安）
    
    [Header("MultiShot Settings")]
    public int bulletCountPerShot = 1; // 1回の発射数（レベルアップでこれを増やす）
    [SerializeField] private float multiShotSpreadAngle = 10f; // 弾の拡散角度（10度ずつ広がるなど）
    [SerializeField] private float respawnDistance = 50f; // リスポーン判定距離（プレイヤーからこの距離以上離れた敵をリスポーン）
    [SerializeField] private float respawnMinRadius = 20f; // リスポーン最小半径
    [SerializeField] private float respawnMaxRadius = 30f; // リスポーン最大半径
    
    [Header("References")]
    [SerializeField] private GemManager gemManager; // GemManagerへの参照
    [SerializeField] private LevelUpManager levelUpManager; // LevelUpManagerへの参照
    [SerializeField] private Player player; // Playerへの参照
    [SerializeField] private UIManager uiManager; // UIManagerへの参照
    [SerializeField] private DamageTextManager damageTextManager; // ダメージテキスト表示用
    [SerializeField] private RenderManager renderManager; // RenderManagerへの参照
    
    [Header("Combat")]
    [SerializeField] private float enemyDamageRadius = 1.0f; // 敵とプレイヤーの当たり判定半径
    [SerializeField] private float enemyMaxHp = 1.0f; // 敵の最大HP
    [SerializeField] private float bulletDamage = 1.0f; // 弾のダメージ
    [SerializeField] private float enemyFlashDuration = 0.1f; // 敵のフラッシュ持続時間（秒）
    
    [Header("Countdown Timer")]
    [SerializeField] private float countdownDuration = 60f; // カウントダウン時間（秒、デフォルト1分）

    // --- Enemy Data ---
    private TransformAccessArray _enemyTransforms;
    private NativeArray<float3> _enemyPositions;
    private NativeArray<bool> _enemyActive; // 生存フラグ
    private NativeArray<float> _enemyHp; // 敵のHP
    private List<float> _enemyFlashTimers; // フラッシュの残り時間を管理
    private List<Transform> _enemyTransformList; // 描画メソッドに渡す用（TransformAccessArrayとは別管理が楽）
    private List<bool> _enemyActiveList; // 生存フラグ

    // --- Bullet Data ---
    private TransformAccessArray _bulletTransforms; // 今回は簡易的にTransformを使いますが、本来はMatrix配列で描画すべき
    private NativeArray<float3> _bulletPositions;
    private NativeArray<float3> _bulletDirections; // 弾の方向ベクトル（後方互換性のため残す）
    private NativeArray<float3> _bulletVelocities; // 弾ごとの速度ベクトルを保存する配列
    private NativeArray<bool> _bulletActive;
    private NativeArray<float> _bulletLifeTime;
    
    // --- Spatial Partitioning ---
    // Key: グリッドのハッシュ値, Value: 敵のインデックス
    private NativeParallelMultiHashMap<int, int> _spatialMap;
    
    // --- Gem Spawn Queue ---
    // 敵が死んだ位置を記録するキュー（Job内からメインスレッドへ通知）
    private NativeQueue<float3> _deadEnemyPositions;
    
    // --- Damage Queue ---
    // プレイヤーへのダメージを記録するキュー（Job内からメインスレッドへ通知）
    private NativeQueue<int> _playerDamageQueue;
    
    // 敵へのダメージ情報を記録するキュー（Job内からメインスレッドへ通知）
    private NativeQueue<EnemyDamageInfo> _enemyDamageQueue;
    
    // ダメージを受けた敵のインデックスを記録するキュー（フラッシュタイマー設定用）
    private NativeQueue<int> _enemyFlashQueue;

    private float _timer;
    private int _bulletIndexHead = 0; // リングバッファ用
    private float _countdownTimer; // カウントダウンタイマー

    void Start()
    {
        InitializeEnemies();
        InitializeBullets();
        
        // 死んだ敵の位置を記録するキューを初期化
        _deadEnemyPositions = new NativeQueue<float3>(Allocator.Persistent);
        
        // プレイヤーへのダメージを記録するキューを初期化
        _playerDamageQueue = new NativeQueue<int>(Allocator.Persistent);
        
        // 敵へのダメージ情報を記録するキューを初期化
        _enemyDamageQueue = new NativeQueue<EnemyDamageInfo>(Allocator.Persistent);
        
        // フラッシュタイマー設定用のキューを初期化
        _enemyFlashQueue = new NativeQueue<int>(Allocator.Persistent);
        
        // カウントダウンタイマーを初期化
        _countdownTimer = countdownDuration;
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
        
        // タイマーがゼロになった瞬間、すべての敵を非アクティブにする
        if (wasTimerRunning && _countdownTimer <= 0f)
        {
            ClearAllEnemies();
        }
        
        // 1. 弾の発射（プレイヤー位置から）
        HandleShooting();

        float deltaTime = Time.deltaTime;
        float3 playerPos = playerTransform.position;

        // 2. 空間ハッシュマップのクリア
        if (_spatialMap.IsCreated) _spatialMap.Clear();
        // 敵の数より少し多めに確保（リサイズ回避）
        if (!_spatialMap.IsCreated) _spatialMap = new NativeParallelMultiHashMap<int, int>(enemyCount, Allocator.Persistent);

        // --- JOB 1: 敵の移動 & グリッド登録 ---
        var enemyJob = new EnemyMoveAndHashJob
        {
            deltaTime = deltaTime,
            target = playerPos,
            speed = enemySpeed,
            cellSize = cellSize,
            damageRadius = enemyDamageRadius,
            spatialMap = _spatialMap.AsParallelWriter(), // 並列書き込み用
            positions = _enemyPositions,
            activeFlags = _enemyActive,
            damageQueue = _playerDamageQueue.AsParallelWriter() // プレイヤーへのダメージを記録
        };
        var enemyHandle = enemyJob.Schedule(_enemyTransforms);

        // --- JOB 2: 弾の移動 & 衝突判定 ---
        // 敵の移動が終わってから実行する必要があるため、enemyHandleに依存させる
        var bulletJob = new BulletMoveAndCollideJob
        {
            deltaTime = deltaTime,
            speed = bulletSpeed,
            cellSize = cellSize,
            spatialMap = _spatialMap, // 読み込みのみ
            enemyPositions = _enemyPositions, // 敵の位置参照
            bulletPositions = _bulletPositions,
            bulletDirections = _bulletDirections, // 弾の方向（後方互換性のため）
            bulletVelocities = _bulletVelocities, // 弾の速度ベクトル
            bulletActive = _bulletActive,
            bulletLifeTime = _bulletLifeTime,
            enemyActive = _enemyActive, // ヒットしたらfalseにする
            enemyHp = _enemyHp, // 敵のHP配列
            bulletDamage = bulletDamage, // 弾のダメージ
            deadEnemyPositions = _deadEnemyPositions.AsParallelWriter(), // 死んだ敵の位置を記録
            enemyDamageQueue = _enemyDamageQueue.AsParallelWriter(), // 敵へのダメージ情報を記録
            enemyFlashQueue = _enemyFlashQueue.AsParallelWriter() // フラッシュタイマー設定用
        };
        
        var bulletHandle = bulletJob.Schedule(_bulletTransforms, enemyHandle);

        // 完了待ち
        bulletHandle.Complete();
        
        // 死んだ敵の位置からジェムを生成
        HandleDeadEnemies();
        
        // 3. 敵へのダメージ表示処理
        HandleEnemyDamage();
        
        // 4. プレイヤーへのダメージ処理
        HandlePlayerDamage();
        
        // 4. 経験値の取得と加算
        HandleExperience();

        // 5. 敵のリスポーン処理（タイマーがゼロでない場合のみ）
        if (_countdownTimer > 0f)
        {
            HandleEnemyRespawn(playerPos);
        }

        // （オプション）死んだ敵を非表示にする処理
        // 本来はCommandBufferやComputeShaderで描画自体をスキップしますが、
        // プロトタイプなのでScaleを0にする等の簡易処理で対応
        SyncVisuals();
        
        // 6. フラッシュタイマーの更新とRenderManagerによる描画
        UpdateFlashTimers(deltaTime);
        RenderEnemies();
    }
    
    // --- 初期化 & ユーティリティ ---
    
    void InitializeEnemies()
    {
        _enemyTransforms = new TransformAccessArray(enemyCount);
        _enemyPositions = new NativeArray<float3>(enemyCount, Allocator.Persistent);
        _enemyActive = new NativeArray<bool>(enemyCount, Allocator.Persistent);
        _enemyHp = new NativeArray<float>(enemyCount, Allocator.Persistent);
        
        // RenderManager用のリストを初期化
        _enemyFlashTimers = new List<float>(new float[enemyCount]);
        _enemyTransformList = new List<Transform>(enemyCount);
        _enemyActiveList = new List<bool>(enemyCount);

        for (int i = 0; i < enemyCount; i++)
        {
            var pos = (float3)UnityEngine.Random.insideUnitSphere * 40f;
            pos.y = 0;
            var obj = Instantiate(cubePrefab, pos, Quaternion.identity);
            if(obj.TryGetComponent<Collider>(out var col)) col.enabled = false; // コライダー必須OFF
            
            _enemyTransforms.Add(obj.transform);
            _enemyPositions[i] = pos;
            _enemyActive[i] = true;
            _enemyHp[i] = enemyMaxHp; // HPを最大値に設定
            
            // TransformAccessArrayに入れるタイミングでListにも入れておく
            _enemyTransformList.Add(obj.transform);
            _enemyActiveList.Add(true);
        }
    }

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
    
    void HandleEnemyRespawn(float3 playerPos)
    {
        // 死んでいる敵（または画面外にはるか遠くに行った敵）を見つけて再配置
        float deleteDistSq = respawnDistance * respawnDistance;
        
        for (int i = 0; i < enemyCount; i++)
        {
            // アクティブでない、またはプレイヤーから離れすぎた敵をリサイクル
            if (!_enemyActive[i] || math.distancesq(_enemyPositions[i], playerPos) > deleteDistSq)
            {
                // 画面外（半径20〜30mのドーナツ状の範囲）に再配置
                float angle = UnityEngine.Random.Range(0f, math.PI * 2f);
                float dist = UnityEngine.Random.Range(respawnMinRadius, respawnMaxRadius);
                float3 offset = new float3(math.cos(angle) * dist, 0f, math.sin(angle) * dist);
                
                float3 newPos = playerPos + offset;
                _enemyPositions[i] = newPos;
                _enemyActive[i] = true; // 復活
                _enemyHp[i] = enemyMaxHp; // HPをリセット
                
                // Transformも更新（重要）
                // TransformAccessArrayは直接インデクサーでアクセスできないため、
                // GameObjectを経由して位置を更新する必要があります
                // ただし、TransformAccessArrayの要素に直接アクセスできないため、
                // 別の方法で位置を更新する必要があります
                // ここでは、Job内で位置を更新する前提で、位置配列のみ更新します
                // 実際のTransform位置は、次のフレームのEnemyMoveAndHashJobで更新されます
            }
        }
        
        // Transform位置を更新するためのJobを実行
        // リスポーンした敵の位置をTransformに反映
        var updateRespawnJob = new UpdateRespawnedEnemyPositionJob
        {
            positions = _enemyPositions,
            activeFlags = _enemyActive
        };
        updateRespawnJob.Schedule(_enemyTransforms).Complete();
    }
    
    void HandleDeadEnemies()
    {
        // キューから死んだ敵の位置を取得してジェムを生成
        if (gemManager != null)
        {
            while (_deadEnemyPositions.TryDequeue(out float3 position))
            {
                gemManager.SpawnGem(position);
            }
        }
    }
    
    void HandleEnemyDamage()
    {
        // キューから敵へのダメージ情報を取得してダメージテキストを表示
        if (damageTextManager != null)
        {
            while (_enemyDamageQueue.TryDequeue(out EnemyDamageInfo damageInfo))
            {
                // ダメージをintに変換して表示（小数点以下は切り捨て）
                int damageInt = (int)damageInfo.damage;
                if (damageInt > 0)
                {
                    damageTextManager.ShowDamage(damageInfo.position, damageInt);
                }
            }
        }
        
        // フラッシュタイマーを設定（ダメージを受けた敵）
        while (_enemyFlashQueue.TryDequeue(out int enemyIndex))
        {
            if (enemyIndex >= 0 && enemyIndex < enemyCount && enemyIndex < _enemyFlashTimers.Count)
            {
                // ★ヒットフラッシュ開始（0.1秒光る）
                _enemyFlashTimers[enemyIndex] = enemyFlashDuration;
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
    
    void SyncVisuals()
    {
        // 簡易処理：死んだ敵を消す（重いので本来は間引く）
        // ここだけはMainThreadで走るので、数が多いとボトルネックになります。
        // ※本格実装では、描画自体をGraphics.DrawMeshInstancedIndirectにするため、この処理は不要になります。
        /*
        for (int i = 0; i < enemyCount; i++)
        {
            if (!_enemyActive[i]) _enemyTransforms[i].localScale = Vector3.zero;
        }
        */
    }
    
    void UpdateFlashTimers(float deltaTime)
    {
        // --- フラッシュタイマーの更新 ---
        for (int i = 0; i < _enemyFlashTimers.Count; i++)
        {
            if (_enemyFlashTimers[i] > 0)
            {
                _enemyFlashTimers[i] -= deltaTime;
            }
        }
        
        // アクティブフラグを同期
        for (int i = 0; i < enemyCount && i < _enemyActiveList.Count; i++)
        {
            _enemyActiveList[i] = _enemyActive[i];
        }
    }
    
    void RenderEnemies()
    {
        if (renderManager == null) return;
        
        // --- 描画呼び出し ---
        renderManager.RenderEnemies(_enemyTransformList, _enemyFlashTimers, _enemyActiveList);
    }

    void OnDestroy()
    {
        if (_enemyTransforms.isCreated) _enemyTransforms.Dispose();
        if (_enemyPositions.IsCreated) _enemyPositions.Dispose();
        if (_enemyActive.IsCreated) _enemyActive.Dispose();
        if (_enemyHp.IsCreated) _enemyHp.Dispose();
        
        if (_bulletTransforms.isCreated) _bulletTransforms.Dispose();
        if (_bulletPositions.IsCreated) _bulletPositions.Dispose();
        if (_bulletDirections.IsCreated) _bulletDirections.Dispose();
        if (_bulletVelocities.IsCreated) _bulletVelocities.Dispose();
        if (_bulletActive.IsCreated) _bulletActive.Dispose();
        if (_bulletLifeTime.IsCreated) _bulletLifeTime.Dispose();
        
        if (_spatialMap.IsCreated) _spatialMap.Dispose();
        
        if (_deadEnemyPositions.IsCreated) _deadEnemyPositions.Dispose();
        if (_playerDamageQueue.IsCreated) _playerDamageQueue.Dispose();
        if (_enemyDamageQueue.IsCreated) _enemyDamageQueue.Dispose();
        if (_enemyFlashQueue.IsCreated) _enemyFlashQueue.Dispose();
        // _enemyFlashTimersはList<float>なので、Dispose()は不要（ガベージコレクタが自動管理）
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
        for (int i = 0; i < enemyCount; i++)
        {
            var pos = (float3)UnityEngine.Random.insideUnitSphere * 40f;
            pos.y = 0;
            _enemyPositions[i] = pos;
            _enemyActive[i] = true;
            _enemyHp[i] = enemyMaxHp; // HPをリセット
        }
        
        // 敵のTransform位置を更新
        var updateRespawnJob = new UpdateRespawnedEnemyPositionJob
        {
            positions = _enemyPositions,
            activeFlags = _enemyActive
        };
        updateRespawnJob.Schedule(_enemyTransforms).Complete();
        
        // 弾をリセット
        for (int i = 0; i < maxBullets; i++)
        {
            _bulletActive[i] = false;
            _bulletPositions[i] = new float3(0, -100, 0);
        }
        
        // キューをクリア
        while (_deadEnemyPositions.TryDequeue(out _)) { }
        while (_playerDamageQueue.TryDequeue(out _)) { }
        while (_enemyDamageQueue.TryDequeue(out _)) { }
        while (_enemyFlashQueue.TryDequeue(out _)) { }
        
        // フラッシュタイマーをリセット
        for (int i = 0; i < _enemyFlashTimers.Count; i++)
        {
            _enemyFlashTimers[i] = 0f;
        }
        
        // タイマーをリセット
        _timer = 0f;
        _bulletIndexHead = 0;
        
        // カウントダウンタイマーをリセット
        _countdownTimer = countdownDuration;
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
    
    // すべての敵を非アクティブにする（タイマーがゼロになった時）
    private void ClearAllEnemies()
    {
        for (int i = 0; i < enemyCount; i++)
        {
            _enemyActive[i] = false;
            if (i < _enemyActiveList.Count)
            {
                _enemyActiveList[i] = false;
            }
        }
        
        // Transform位置を更新するためのJobを実行して、敵を非表示にする
        var updateRespawnJob = new UpdateRespawnedEnemyPositionJob
        {
            positions = _enemyPositions,
            activeFlags = _enemyActive
        };
        updateRespawnJob.Schedule(_enemyTransforms).Complete();
    }
    
}
