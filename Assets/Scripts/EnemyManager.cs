using UnityEngine;
using UnityEngine.Jobs;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using System.Collections.Generic;

public class EnemyManager : MonoBehaviour
{
    [Header("Enemy Settings")]
    [SerializeField] private GameObject cubePrefab;
    [SerializeField] private int enemyCount = 10;
    [SerializeField] private float enemySpeed = 4f;
    [SerializeField] private float enemyMaxHp = 1.0f;
    [SerializeField] private float enemyFlashDuration = 0.1f;
    [SerializeField] private float enemyDamageRadius = 1.0f;
    [SerializeField] private float cellSize = 2.0f;
    
    [Header("Respawn Settings")]
    [SerializeField] private float respawnDistance = 50f;
    [SerializeField] private float respawnMinRadius = 20f;
    [SerializeField] private float respawnMaxRadius = 30f;
    
    [Header("Boss Settings")]
    [SerializeField] private GameObject bossPrefab; // ボスのプレハブ
    [SerializeField] private float bossSpawnDistance = 20f; // ボス生成位置の距離（プレイヤーからの距離）
    
    [Header("References")]
    [SerializeField] private RenderManager renderManager;
    [SerializeField] private DamageTextManager damageTextManager;
    [SerializeField] private GemManager gemManager; // GemManagerへの参照
    [SerializeField] private Transform playerTransform; // プレイヤーのTransform
    
    // ボス関連
    private GameObject _currentBoss; // 現在のボスインスタンス
    
    // ゲームモード
    private GameMode _currentMode = GameMode.None; // 現在のゲームモード
    
    // --- Enemy Data ---
    private TransformAccessArray _enemyTransforms;
    private NativeArray<float3> _enemyPositions;
    private NativeArray<bool> _enemyActive; // 生存フラグ
    private NativeArray<float> _enemyHp; // 敵のHP
    private List<float> _enemyFlashTimers; // フラッシュの残り時間を管理
    private List<Transform> _enemyTransformList; // 描画メソッドに渡す用（TransformAccessArrayとは別管理が楽）
    private List<bool> _enemyActiveList; // 生存フラグ
    
    // --- Spatial Partitioning ---
    private NativeParallelMultiHashMap<int, int> _spatialMap;
    
    // --- Queues ---
    private NativeQueue<float3> _deadEnemyPositions; // 死んだ敵の位置を記録するキュー
    private NativeQueue<EnemyDamageInfo> _enemyDamageQueue; // 敵へのダメージ情報を記録するキュー
    private NativeQueue<int> _enemyFlashQueue; // ダメージを受けた敵のインデックスを記録するキュー
    
    // プロパティ（外部アクセス用）
    public TransformAccessArray EnemyTransforms => _enemyTransforms;
    public NativeArray<float3> EnemyPositions => _enemyPositions;
    public NativeArray<bool> EnemyActive => _enemyActive;
    public NativeArray<float> EnemyHp => _enemyHp;
    public NativeParallelMultiHashMap<int, int> SpatialMap => _spatialMap;
    public int EnemyCount => enemyCount;
    public float EnemySpeed => enemySpeed;
    public float CellSize => cellSize;
    public float EnemyDamageRadius => enemyDamageRadius;
    public float EnemyMaxHp => enemyMaxHp;
    
    void Start()
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
            _enemyActiveList.Add(false);
        }

        _spatialMap = new NativeParallelMultiHashMap<int, int>(enemyCount, Allocator.Persistent);
        
        // 死んだ敵の位置を記録するキューを初期化
        _deadEnemyPositions = new NativeQueue<float3>(Allocator.Persistent);
        
        // 敵へのダメージ情報を記録するキューを初期化
        _enemyDamageQueue = new NativeQueue<EnemyDamageInfo>(Allocator.Persistent);
        
        // フラッシュタイマー設定用のキューを初期化
        _enemyFlashQueue = new NativeQueue<int>(Allocator.Persistent);
    }
    
    void Update()
    {
        if (playerTransform == null) return;
        
        // 通常モードでない場合は処理をスキップ
        if (_currentMode != GameMode.Normal) return;
        
        float deltaTime = Time.deltaTime;
        float3 playerPos = playerTransform.position;
        
        // 1. 敵の移動Jobをスケジュール（JobHandleは返すが、ここでは完了待ちしない）
        // GameManagerでbulletHandleの依存関係として使用されるため、publicメソッドとして残す
        // 実際の処理はGameManagerのUpdateで行う
        
        // 2. 死んだ敵の位置からジェムを生成
        ProcessDeadEnemies(gemManager);
        
        // 3. 敵へのダメージ表示処理
        ProcessEnemyDamage();
        
        // 4. 敵のリスポーン処理（通常モードの場合のみ実行）
        HandleRespawn(playerPos);
        
        // 5. フラッシュタイマーの更新とRenderManagerによる描画
        UpdateAndRender(deltaTime);
    }
    
    // 敵の移動Jobをスケジュール
    public JobHandle ScheduleEnemyMoveJob(float deltaTime, float3 playerPos, NativeQueue<int>.ParallelWriter playerDamageQueue)
    {
        if (_currentMode != GameMode.Normal) return default;

        // 空間ハッシュマップのクリア
        if (_spatialMap.IsCreated) _spatialMap.Clear();

        // 敵の数より少し多めに確保（リサイズ回避）
        if (!_spatialMap.IsCreated) _spatialMap = new NativeParallelMultiHashMap<int, int>(enemyCount, Allocator.Persistent);
        
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
            damageQueue = playerDamageQueue // プレイヤーへのダメージを記録
        };
        
        return enemyJob.Schedule(_enemyTransforms);
    }
    
    // 死んだ敵の位置を取得（ジェム生成用）
    public void ProcessDeadEnemies(GemManager gemManager)
    {
        if (gemManager != null)
        {
            while (_deadEnemyPositions.TryDequeue(out float3 position))
            {
                gemManager.SpawnGem(position);
            }
        }
    }
    
    // 敵へのダメージ表示処理
    public void ProcessEnemyDamage()
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
    
    // 敵のリスポーン処理
    public void HandleRespawn(float3 playerPos)
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
    
    // フラッシュタイマーの更新と描画
    public void UpdateAndRender(float deltaTime)
    {
        UpdateFlashTimers(deltaTime);
        RenderEnemies();
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
    
    // すべての敵を非アクティブにする（タイマーがゼロになった時）
    public void ClearAllEnemies()
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
    
    // 敵をリセット
    public void ResetEnemies()
    {
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
        
        // フラッシュタイマーをリセット
        for (int i = 0; i < _enemyFlashTimers.Count; i++)
        {
            _enemyFlashTimers[i] = 0f;
        }
        
        // キューをクリア
        while (_deadEnemyPositions.TryDequeue(out _)) { }
        while (_enemyDamageQueue.TryDequeue(out _)) { }
        while (_enemyFlashQueue.TryDequeue(out _)) { }
    }
    
    // ボスを生成（プレイヤーの位置と方向、GameManagerを受け取る）
    public void SpawnBoss(Vector3 playerPosition, Vector3 playerForward, GameManager gameManager)
    {
        // 既存のボスがいる場合は削除
        if (_currentBoss != null)
        {
            Destroy(_currentBoss);
        }
        
        // ボスを生成（プレイヤーの真後ろ、指定距離の位置）
        Vector3 playerBackward = -playerForward; // プレイヤーの後ろ方向
        Vector3 bossPosition = playerPosition + playerBackward * bossSpawnDistance; // 指定距離の位置
        bossPosition.y = 0f; // Y座標を0に固定
        
        if (bossPrefab != null)
        {
            _currentBoss = Instantiate(bossPrefab, bossPosition, Quaternion.identity);
            
            // Bossコンポーネントを取得して初期化
            Boss bossComponent = _currentBoss.GetComponent<Boss>();
            if (bossComponent != null && playerTransform != null && gameManager != null)
            {
                bossComponent.Initialize(playerTransform, gameManager);
            }
            else
            {
                Debug.LogWarning("EnemyManager: Failed to initialize Boss component!");
            }
        }
        else
        {
            Debug.LogWarning("EnemyManager: Boss prefab is not assigned!");
        }
    }
    
    // 現在のボスを取得
    public GameObject GetCurrentBoss()
    {
        return _currentBoss;
    }
    
    // キューへのアクセス（BulletMoveAndCollideJob用）
    public NativeQueue<float3>.ParallelWriter GetDeadEnemyPositionsWriter()
    {
        return _deadEnemyPositions.AsParallelWriter();
    }
    
    public NativeQueue<EnemyDamageInfo>.ParallelWriter GetEnemyDamageQueueWriter()
    {
        return _enemyDamageQueue.AsParallelWriter();
    }
    
    public NativeQueue<int>.ParallelWriter GetEnemyFlashQueueWriter()
    {
        return _enemyFlashQueue.AsParallelWriter();
    }
    
    // ゲームモードを設定
    public void SetGameMode(GameMode mode)
    {
        _currentMode = mode;

        if (_currentMode != GameMode.Normal)
        {
            ClearAllEnemies();
        }
    }
    
    // 現在のゲームモードを取得
    public GameMode GetCurrentMode()
    {
        return _currentMode;
    }
    
    void OnDestroy()
    {
        if (_enemyTransforms.isCreated) _enemyTransforms.Dispose();
        if (_enemyPositions.IsCreated) _enemyPositions.Dispose();
        if (_enemyActive.IsCreated) _enemyActive.Dispose();
        if (_enemyHp.IsCreated) _enemyHp.Dispose();
        
        if (_spatialMap.IsCreated) _spatialMap.Dispose();
        
        if (_deadEnemyPositions.IsCreated) _deadEnemyPositions.Dispose();
        if (_enemyDamageQueue.IsCreated) _enemyDamageQueue.Dispose();
        if (_enemyFlashQueue.IsCreated) _enemyFlashQueue.Dispose();
        
        // ボスを削除
        if (_currentBoss != null)
        {
            Destroy(_currentBoss);
        }
        
        // _enemyFlashTimersはList<float>なので、Dispose()は不要（ガベージコレクタが自動管理）
    }
}
