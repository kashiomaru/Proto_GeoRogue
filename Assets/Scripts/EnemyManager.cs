using UnityEngine;
using UnityEngine.Assertions;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using System.Collections.Generic;

public class EnemyManager : InitializeMonobehaviour
{
    [Header("Enemy Settings")]
    [Tooltip("管理する敵の最大数（配列・リストのサイズ）。初期化時に固定。")]
    [SerializeField] private int maxEnemyCount = 1000;
    [Tooltip("実際に出現させる敵の数。ランタイムで変更可能。")]
    [SerializeField] private int spawnCount = 10;
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
    [SerializeField] private GameManager gameManager; // GameManagerへの参照
    
    // ボス関連
    private GameObject _currentBoss; // 現在のボスインスタンス
    private GameObject _bossPrefabOverride; // ステージ適用時のボス Prefab（null なら serialized を使用）
    private float _bossSpawnDistanceOverride = -1f; // ステージ適用時の距離（< 0 なら serialized を使用）

    // 通常敵・ボスの有効フラグ（GameManager が SetNormalEnemiesEnabled / SetBossActive で設定）
    private bool _normalEnemiesEnabled;
    private bool _bossActive;
    
    // --- Enemy Data（座標・回転のみ保持、Prefab インスタンスは生成しない）---
    private NativeArray<float3> _enemyPositions;
    private NativeArray<quaternion> _enemyRotations; // Job で計算（プレイヤー方向）
    private NativeArray<bool> _enemyActive;
    private NativeArray<float> _enemyHp;
    private List<float> _enemyFlashTimers;
    private List<Vector3> _enemyPositionList;
    private List<Quaternion> _enemyRotationList; // 描画用（_enemyRotations をコピー）
    private List<bool> _enemyActiveList;
    
    // --- Spatial Partitioning ---
    private NativeParallelMultiHashMap<int, int> _spatialMap;
    
    // --- Queues ---
    private NativeQueue<float3> _deadEnemyPositions; // 死んだ敵の位置を記録するキュー
    private NativeQueue<EnemyDamageInfo> _enemyDamageQueue; // 敵へのダメージ情報を記録するキュー
    private NativeQueue<int> _enemyFlashQueue; // ダメージを受けた敵のインデックスを記録するキュー
    
    // プロパティ（外部アクセス用）
    public NativeArray<float3> EnemyPositions => _enemyPositions;
    public NativeArray<bool> EnemyActive => _enemyActive;
    public NativeArray<float> EnemyHp => _enemyHp;
    public NativeParallelMultiHashMap<int, int> SpatialMap => _spatialMap;
    /// <summary>管理最大数（配列サイズ）。初期化時に固定。</summary>
    public int MaxEnemyCount => maxEnemyCount;
    /// <summary>実際に出現させる敵の数。ランタイムで変更可能。</summary>
    public int SpawnCount { get => spawnCount; set => spawnCount = Mathf.Clamp(value, 1, maxEnemyCount); }
    /// <summary>敵の処理数（SpawnCount と同じ）。後方互換用。</summary>
    public int EnemyCount => spawnCount;
    public float EnemySpeed => enemySpeed;
    public float CellSize => cellSize;
    public float EnemyDamageRadius => enemyDamageRadius;
    public float EnemyMaxHp => enemyMaxHp;
    
    void Update()
    {
        if (gameManager == null)
        {
            return;
        }
        
        // ボスが有効なときはボス死亡チェック（通常敵と共存可能）
        if (_bossActive)
        {
            CheckBossDeath();
        }

        // 通常敵が有効なときは移動・ジェム・ダメージ表示・リスポーン・描画
        if (_normalEnemiesEnabled)
        {
            ProcessDeadEnemies(gemManager);
            ProcessEnemyDamage();
            HandleRespawn();
            UpdateFlashTimers();
            RenderEnemies();
        }
    }


    protected override void InitializeInternal()
    {
        spawnCount = Mathf.Clamp(spawnCount, 1, maxEnemyCount);

        _enemyPositions = new NativeArray<float3>(maxEnemyCount, Allocator.Persistent);
        _enemyRotations = new NativeArray<quaternion>(maxEnemyCount, Allocator.Persistent);
        _enemyActive = new NativeArray<bool>(maxEnemyCount, Allocator.Persistent);
        _enemyHp = new NativeArray<float>(maxEnemyCount, Allocator.Persistent);
        _enemyFlashTimers = new List<float>(new float[maxEnemyCount]);
        _enemyPositionList = new List<Vector3>(maxEnemyCount);
        _enemyRotationList = new List<Quaternion>(maxEnemyCount);
        _enemyActiveList = new List<bool>(maxEnemyCount);

        for (int i = 0; i < maxEnemyCount; i++)
        {
            bool active = i < spawnCount;
            _enemyActive[i] = active;
            if (active)
            {
                var pos = (float3)UnityEngine.Random.insideUnitSphere * 40f;
                pos.y = 0;
                _enemyPositions[i] = pos;
                _enemyRotations[i] = quaternion.identity;
                _enemyHp[i] = enemyMaxHp;
            }
            _enemyPositionList.Add(Vector3.zero);
            _enemyRotationList.Add(Quaternion.identity);
            _enemyActiveList.Add(false);
        }

        _spatialMap = new NativeParallelMultiHashMap<int, int>(maxEnemyCount, Allocator.Persistent);
        
        // 死んだ敵の位置を記録するキューを初期化
        _deadEnemyPositions = new NativeQueue<float3>(Allocator.Persistent);
        
        // 敵へのダメージ情報を記録するキューを初期化
        _enemyDamageQueue = new NativeQueue<EnemyDamageInfo>(Allocator.Persistent);
        
        // フラッシュタイマー設定用のキューを初期化
        _enemyFlashQueue = new NativeQueue<int>(Allocator.Persistent);
    }
    
    // 敵の移動Jobをスケジュール
    public JobHandle ScheduleEnemyMoveJob(float deltaTime, float3 playerPos, NativeQueue<int>.ParallelWriter playerDamageQueue)
    {
        if (_normalEnemiesEnabled == false)
        {
            return default;
        }

        // 空間ハッシュマップのクリア
        if (_spatialMap.IsCreated)
        {
            _spatialMap.Clear();
        }
        else
        {
            _spatialMap = new NativeParallelMultiHashMap<int, int>(maxEnemyCount, Allocator.Persistent);
        }

        var enemyJob = new EnemyMoveAndHashJob
        {
            deltaTime = deltaTime,
            target = playerPos,
            speed = enemySpeed,
            cellSize = cellSize,
            damageRadius = enemyDamageRadius,
            spatialMap = _spatialMap.AsParallelWriter(), // 並列書き込み用
            positions = _enemyPositions,
            rotations = _enemyRotations,
            activeFlags = _enemyActive,
            damageQueue = playerDamageQueue // プレイヤーへのダメージを記録
        };
        
        return enemyJob.Schedule(spawnCount, 64);
    }
    
    // 死んだ敵の位置を取得（ジェム生成用）
    public void ProcessDeadEnemies(GemManager gemManager)
    {
        while (_deadEnemyPositions.TryDequeue(out float3 position))
        {
            gemManager?.SpawnGem(position);
        }
    }
    
    // 敵へのダメージ表示処理
    public void ProcessEnemyDamage()
    {
        // キューから敵へのダメージ情報を取得してダメージテキストを表示
        while (_enemyDamageQueue.TryDequeue(out EnemyDamageInfo damageInfo))
        {
            int damageInt = (int)damageInfo.damage;
            if (damageInt > 0)
            {
                damageTextManager?.ShowDamage(damageInfo.position, damageInt);
            }
        }
        
        // フラッシュタイマーを設定（ダメージを受けた敵）
        while (_enemyFlashQueue.TryDequeue(out int enemyIndex))
        {
            if (enemyIndex >= 0 && enemyIndex < spawnCount && enemyIndex < _enemyFlashTimers.Count)
            {
                // ★ヒットフラッシュ開始（0.1秒光る）
                _enemyFlashTimers[enemyIndex] = enemyFlashDuration;
            }
        }
    }
    
    // 敵のリスポーン処理
    public void HandleRespawn()
    {
        float3 playerPos = (float3)gameManager.GetPlayerPosition();

        // 死んでいる敵（または画面外にはるか遠くに行った敵）を見つけて再配置
        float deleteDistSq = respawnDistance * respawnDistance;
        
        for (int i = 0; i < spawnCount; i++)
        {
            // アクティブでない、またはプレイヤーから離れすぎた敵をリサイクル
            if (_enemyActive[i] == false || math.distancesq(_enemyPositions[i], playerPos) > deleteDistSq)
            {
                // 画面外（半径20〜30mのドーナツ状の範囲）に再配置
                float angle = UnityEngine.Random.Range(0f, math.PI * 2f);
                float dist = UnityEngine.Random.Range(respawnMinRadius, respawnMaxRadius);
                float3 offset = new float3(math.cos(angle) * dist, 0f, math.sin(angle) * dist);
                
                float3 newPos = playerPos + offset;
                _enemyPositions[i] = newPos;
                _enemyActive[i] = true; // 復活
                _enemyHp[i] = enemyMaxHp; // HPをリセット
                // ヒットフラッシュをクリア（直前でやられた敵のフラッシュが残らないように）
                _enemyFlashTimers[i] = 0f;
            }
        }
    }

    void UpdateFlashTimers()
    {
        // --- フラッシュタイマーの更新 ---
        for (int i = 0; i < spawnCount; i++)
        {
            if (i < _enemyFlashTimers.Count && _enemyFlashTimers[i] > 0)
            {
                _enemyFlashTimers[i] -= Time.deltaTime;
            }
        }
        
        Assert.IsTrue(maxEnemyCount == _enemyActiveList.Count);
        for (int i = 0; i < spawnCount; i++)
        {
            _enemyActiveList[i] = _enemyActive[i];
            _enemyPositionList[i] = _enemyPositions[i];
            var q = _enemyRotations[i];
            _enemyRotationList[i] = new Quaternion(q.value.x, q.value.y, q.value.z, q.value.w);
        }
    }

    void RenderEnemies()
    {
        if (renderManager == null)
        {
            return;
        }
        renderManager.RenderEnemies(_enemyPositionList, _enemyRotationList, _enemyFlashTimers, _enemyActiveList, spawnCount);
    }
    
    public void ClearAllEnemies()
    {
        for (int i = 0; i < spawnCount; i++)
        {
            _enemyActive[i] = false;
            _enemyActiveList[i] = false;
        }
    }
    
    public void ResetEnemies()
    {
        for (int i = 0; i < spawnCount; i++)
        {
            var pos = (float3)UnityEngine.Random.insideUnitSphere * 40f;
            pos.y = 0;
            _enemyPositions[i] = pos;
            _enemyActive[i] = true;
            _enemyHp[i] = enemyMaxHp;
        }
        for (int i = spawnCount; i < maxEnemyCount; i++)
        {
            _enemyActive[i] = false;
        }

        // フラッシュタイマーをリセット
        for (int i = 0; i < spawnCount && i < _enemyFlashTimers.Count; i++)
        {
            _enemyFlashTimers[i] = 0f;
        }
        
        // キューをクリア
        while (_deadEnemyPositions.TryDequeue(out _)) { }
        while (_enemyDamageQueue.TryDequeue(out _)) { }
        while (_enemyFlashQueue.TryDequeue(out _)) { }
    }
    
    // ボスを生成（プレイヤーの位置と方向を受け取る）
    public void SpawnBoss(Vector3 playerPosition, Vector3 playerForward)
    {
        // 既存のボスがいる場合は削除
        if (_currentBoss != null)
        {
            Destroy(_currentBoss);
        }
        
        // ボスを生成（プレイヤーの真後ろ、指定距離の位置）
        GameObject prefabToUse = _bossPrefabOverride != null ? _bossPrefabOverride : bossPrefab;
        float distanceToUse = _bossSpawnDistanceOverride >= 0f ? _bossSpawnDistanceOverride : bossSpawnDistance;
        Vector3 playerBackward = -playerForward; // プレイヤーの後ろ方向
        Vector3 bossPosition = playerPosition + playerBackward * distanceToUse; // 指定距離の位置
        bossPosition.y = 0f; // Y座標を0に固定

        if (prefabToUse != null)
        {
            _currentBoss = Instantiate(prefabToUse, bossPosition, Quaternion.identity, transform);

            // ボスの向きをプレイヤー方向に設定（Y軸は0で水平面のみ）
            Vector3 dirToPlayer = playerPosition - bossPosition;
            dirToPlayer.y = 0f;
            if (dirToPlayer.sqrMagnitude > 0.001f)
            {
                _currentBoss.transform.rotation = Quaternion.LookRotation(dirToPlayer.normalized);
            }

            // Bossコンポーネントを取得して初期化
            Boss bossComponent = _currentBoss.GetComponent<Boss>();
            if (bossComponent != null && gameManager != null)
            {
                float? bossHpOverride = gameManager.GetDebugBossHpOverride();
                bossComponent.Initialize(
                    () => gameManager.GetPlayerPosition(),
                    (damage) => gameManager.AddPlayerDamage(damage),
                    bossHpOverride
                );
            }
            else
            {
                Debug.LogWarning("EnemyManager: Failed to initialize Boss component!");
            }
        }
        else
        {
            Debug.LogWarning("EnemyManager: Boss prefab is not assigned (stage override and serialized both null)!");
        }
    }

    // 現在のボスを取得
    public GameObject GetCurrentBoss()
    {
        return _currentBoss;
    }
    
    // ボスの死亡チェックと削除処理
    private void CheckBossDeath()
    {
        if (_currentBoss == null)
        {
            return;
        }

        Boss boss = _currentBoss.GetComponent<Boss>();
        if (boss != null && boss.IsDead)
        {
            // ボスが死亡したら削除
            Destroy(_currentBoss);
            _currentBoss = null;
        }
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
    
    /// <summary>
    /// 通常敵の処理を有効／無効にする。無効にしたときは全敵をクリアする。
    /// </summary>
    public void SetNormalEnemiesEnabled(bool enabled)
    {
        _normalEnemiesEnabled = enabled;
        if (enabled == false)
        {
            ClearAllEnemies();
        }
    }

    /// <summary>
    /// ボスを有効／無効にする。無効にしたときはボスが存在すれば破棄する。
    /// </summary>
    public void SetBossActive(bool active)
    {
        _bossActive = active;
        if (active == false && _currentBoss != null)
        {
            Destroy(_currentBoss);
            _currentBoss = null;
        }
    }

    /// <summary>
    /// ステージの通常敵設定を適用する。EnemyManager のパラメータと RenderManager の表示を更新する。
    /// </summary>
    public void ApplyNormalEnemyConfig(StageData stage)
    {
        if (stage == null) return;
        enemySpeed = stage.EnemySpeed;
        enemyMaxHp = stage.EnemyMaxHp;
        enemyFlashDuration = stage.EnemyFlashDuration;
        enemyDamageRadius = stage.EnemyDamageRadius;
        cellSize = stage.CellSize;
        respawnDistance = stage.RespawnDistance;
        respawnMinRadius = stage.RespawnMinRadius;
        respawnMaxRadius = stage.RespawnMaxRadius;
        if (stage.SpawnCount > 0)
        {
            SpawnCount = Mathf.Clamp(stage.SpawnCount, 1, maxEnemyCount);
        }
        renderManager?.SetEnemyDisplay(stage.EnemyMesh, stage.EnemyMaterial, stage.EnemyScale);
    }

    /// <summary>
    /// ステージのボス設定を適用する。次回 SpawnBoss で使用される。
    /// </summary>
    public void ApplyBossConfig(StageData stage)
    {
        if (stage == null) return;
        _bossPrefabOverride = stage.BossPrefab;
        _bossSpawnDistanceOverride = stage.BossSpawnDistance;
    }

    protected override void FinalizeInternal()
    {
        if (_enemyPositions.IsCreated)
        {
            _enemyPositions.Dispose();
        }
        if (_enemyRotations.IsCreated)
        {
            _enemyRotations.Dispose();
        }
        if (_enemyActive.IsCreated)
        {
            _enemyActive.Dispose();
        }
        if (_enemyHp.IsCreated)
        {
            _enemyHp.Dispose();
        }

        if (_spatialMap.IsCreated)
        {
            _spatialMap.Dispose();
        }

        if (_deadEnemyPositions.IsCreated)
        {
            _deadEnemyPositions.Dispose();
        }
        if (_enemyDamageQueue.IsCreated)
        {
            _enemyDamageQueue.Dispose();
        }
        if (_enemyFlashQueue.IsCreated)
        {
            _enemyFlashQueue.Dispose();
        }

        // ボスを削除
        if (_currentBoss != null)
        {
            Destroy(_currentBoss);
        }
        
        // _enemyFlashTimersはList<float>なので、Dispose()は不要（ガベージコレクタが自動管理）
    }
}
