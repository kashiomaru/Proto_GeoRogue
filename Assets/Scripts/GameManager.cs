using UnityEngine;
using UnityEngine.Jobs;
using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;

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
    
    [Header("Combat")]
    [SerializeField] private float enemyDamageRadius = 1.0f; // 敵とプレイヤーの当たり判定半径
    
    [Header("Level System")]
    [SerializeField] private int maxLevel = 99;
    
    // --- Experience & Level ---
    private int _currentExp = 0;
    private int _nextLevelExp = 10;
    private int _currentLevel = 1;

    // --- Enemy Data ---
    private TransformAccessArray _enemyTransforms;
    private NativeArray<float3> _enemyPositions;
    private NativeArray<bool> _enemyActive; // 生存フラグ

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

    private float _timer;
    private int _bulletIndexHead = 0; // リングバッファ用

    void Start()
    {
        InitializeEnemies();
        InitializeBullets();
        
        // 死んだ敵の位置を記録するキューを初期化
        _deadEnemyPositions = new NativeQueue<float3>(Allocator.Persistent);
        
        // プレイヤーへのダメージを記録するキューを初期化
        _playerDamageQueue = new NativeQueue<int>(Allocator.Persistent);
    }

    void Update()
    {
        // 1. 弾の発射（プレイヤー位置から）
        HandleShooting();

        // 2. 空間ハッシュマップのクリア
        if (_spatialMap.IsCreated) _spatialMap.Clear();
        // 敵の数より少し多めに確保（リサイズ回避）
        if (!_spatialMap.IsCreated) _spatialMap = new NativeParallelMultiHashMap<int, int>(enemyCount, Allocator.Persistent);

        float deltaTime = Time.deltaTime;
        float3 playerPos = playerTransform.position;

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
            deadEnemyPositions = _deadEnemyPositions.AsParallelWriter() // 死んだ敵の位置を記録
        };
        
        var bulletHandle = bulletJob.Schedule(_bulletTransforms, enemyHandle);

        // 完了待ち
        bulletHandle.Complete();
        
        // 死んだ敵の位置からジェムを生成
        HandleDeadEnemies();
        
        // 3. プレイヤーへのダメージ処理
        HandlePlayerDamage();
        
        // 4. 経験値の取得と加算
        HandleExperience();

        // 5. 敵のリスポーン処理
        HandleEnemyRespawn(playerPos);

        // （オプション）死んだ敵を非表示にする処理
        // 本来はCommandBufferやComputeShaderで描画自体をスキップしますが、
        // プロトタイプなのでScaleを0にする等の簡易処理で対応
        SyncVisuals();
    }
    
    // --- 初期化 & ユーティリティ ---
    
    void InitializeEnemies()
    {
        _enemyTransforms = new TransformAccessArray(enemyCount);
        _enemyPositions = new NativeArray<float3>(enemyCount, Allocator.Persistent);
        _enemyActive = new NativeArray<bool>(enemyCount, Allocator.Persistent);

        for (int i = 0; i < enemyCount; i++)
        {
            var pos = (float3)UnityEngine.Random.insideUnitSphere * 40f;
            pos.y = 0;
            var obj = Instantiate(cubePrefab, pos, Quaternion.identity);
            if(obj.TryGetComponent<Collider>(out var col)) col.enabled = false; // コライダー必須OFF
            
            _enemyTransforms.Add(obj.transform);
            _enemyPositions[i] = pos;
            _enemyActive[i] = true;
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
                player.TakeDamage(totalDamage);
                
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
        if (gemManager != null)
        {
            int expGained = gemManager.GetCollectedGemCount();
            if (expGained > 0)
            {
                _currentExp += expGained;
            }
        }
        
        // レベルアップ判定
        while (_currentExp >= _nextLevelExp && _currentLevel < maxLevel)
        {
            _currentExp -= _nextLevelExp;
            _currentLevel++;
            _nextLevelExp = (int)(_nextLevelExp * 1.2f); // 必要経験値を増やす（カーブは要調整）

            // レベルアップ画面を表示！
            if (levelUpManager != null)
            {
                levelUpManager.ShowLevelUpOptions();
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

    void OnDestroy()
    {
        if (_enemyTransforms.isCreated) _enemyTransforms.Dispose();
        if (_enemyPositions.IsCreated) _enemyPositions.Dispose();
        if (_enemyActive.IsCreated) _enemyActive.Dispose();
        
        if (_bulletTransforms.isCreated) _bulletTransforms.Dispose();
        if (_bulletPositions.IsCreated) _bulletPositions.Dispose();
        if (_bulletDirections.IsCreated) _bulletDirections.Dispose();
        if (_bulletVelocities.IsCreated) _bulletVelocities.Dispose();
        if (_bulletActive.IsCreated) _bulletActive.Dispose();
        if (_bulletLifeTime.IsCreated) _bulletLifeTime.Dispose();
        
        if (_spatialMap.IsCreated) _spatialMap.Dispose();
        
        if (_deadEnemyPositions.IsCreated) _deadEnemyPositions.Dispose();
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
        
        // 経験値とレベルをリセット
        _currentExp = 0;
        _nextLevelExp = 10;
        _currentLevel = 1;
        
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
        
        // タイマーをリセット
        _timer = 0f;
        _bulletIndexHead = 0;
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
}
