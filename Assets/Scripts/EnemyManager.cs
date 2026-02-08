using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using System.Collections.Generic;

public class EnemyManager : InitializeMonobehaviour
{
    [Header("Enemy Settings")]
    [Tooltip("1グループ（1種類の敵データ）あたりの最大数。各 EnemyGroup の配列サイズとして使用。")]
    [SerializeField] private int maxEnemyCountPerGroup = 1000;

    [Tooltip("ダメージ受けた際のヒットフラッシュ表示時間（全敵共通）。ステージからは上書きしない。")]
    [SerializeField] private float enemyFlashDuration = 0.1f;

    [Header("Respawn (一律)")]
    [Tooltip("プレイヤーからこの距離以上離れた敵を削除し、リスポーン候補にする")]
    [SerializeField] private float respawnDistance = 50f;
    [Tooltip("リスポーン位置のプレイヤーからの最小半径")]
    [SerializeField] private float respawnMinRadius = 20f;
    [Tooltip("リスポーン位置のプレイヤーからの最大半径")]
    [SerializeField] private float respawnMaxRadius = 30f;
    private GameObject bossPrefab;
    [Header("References")]
    [SerializeField] private RenderManager renderManager;
    [SerializeField] private DamageTextManager damageTextManager;
    [SerializeField] private GemManager gemManager; // GemManagerへの参照
    [SerializeField] private GameManager gameManager; // GameManagerへの参照
    [SerializeField] private BulletManager bulletManager;
    [SerializeField] private float bulletScale = 0.5f;
    
    // ボス関連
    private GameObject _currentBoss; // 現在のボスインスタンス
    private BossBase _currentBossComponent; // 毎フレーム GetComponent しないようキャッシュ
    private GameObject _bossPrefabOverride; // ステージ適用時のボス Prefab（null なら上記 bossPrefab を使用）

    // 通常敵・ボスの有効フラグ（GameManager が SetNormalEnemiesEnabled / SetBossActive で設定）
    private bool _normalEnemiesEnabled;
    private bool _bossActive;

    // --- 通常敵は EnemyGroup で管理（1種類＝1グループ、複数種類の場合は複数グループ）---
    private List<EnemyGroup> _groups = new List<EnemyGroup>();

    /// <summary>通常敵グループのリスト（弾衝突などで参照）。空の場合は未適用。</summary>
    public IReadOnlyList<EnemyGroup> GetGroups() => _groups;
    
    void Update()
    {
        // 処理順は GameManager.Update で制御するため、ここでは何もしない
    }

    void LateUpdate()
    {
        // 参照はインスペクターで必ず指定すること
        Debug.Assert(gameManager != null, "[EnemyManager] gameManager が未設定です。インスペクターで指定してください。");

        // 描画は LateUpdate で行う（Update で完了した Job の結果を描画）
        if (_normalEnemiesEnabled && _groups != null)
            RenderEnemies();
    }

    protected override void InitializeInternal()
    {
        Debug.Assert(bulletManager != null, "[EnemyManager] bulletManager が未設定です。インスペクターで BulletManager を指定してください。");
        Debug.Assert(renderManager != null, "[EnemyManager] renderManager が未設定です。インスペクターで RenderManager を指定してください。");
        Debug.Assert(damageTextManager != null, "[EnemyManager] damageTextManager が未設定です。インスペクターで DamageTextManager を指定してください。");
        Debug.Assert(gemManager != null, "[EnemyManager] gemManager が未設定です。インスペクターで GemManager を指定してください。");
        Debug.Assert(gameManager != null, "[EnemyManager] gameManager が未設定です。インスペクターで GameManager を指定してください。");

        bulletManager.Initialize();
        bulletManager.InitializeEnemyBullets(bulletScale);
    }

    /// <summary>敵の移動Jobをスケジュールし完了まで待機（全グループ分を直列に依存させる。同一 playerDamageQueue への書き込み競合を防ぐ）。</summary>
    public void ScheduleEnemyMoveJob(float deltaTime, float3 playerPos, NativeQueue<int>.ParallelWriter playerDamageQueue)
    {
        if (_normalEnemiesEnabled == false || _groups == null || _groups.Count == 0)
        {
            return;
        }
        JobHandle dep = default;
        foreach (var g in _groups)
        {
            dep = g.ScheduleEnemyMoveJob(deltaTime, playerPos, playerDamageQueue, dep);
        }
        dep.Complete();
    }

    /// <summary>通常敵の移動 Job とボスの移動処理をまとめて実行する。GameManager から呼ぶ。プレイヤーへのダメージは playerDamageQueue に登録する。</summary>
    public void ProcessMovement(float deltaTime, float3 playerPos, NativeQueue<int> playerDamageQueue)
    {
        ScheduleEnemyMoveJob(deltaTime, playerPos, playerDamageQueue.AsParallelWriter());
        ProcessBossMovement(deltaTime, playerDamageQueue);
    }

    /// <summary>死んだ敵の位置を取得（ジェム生成用）。</summary>
    public void ProcessDeadEnemies(GemManager gemManager)
    {
        if (_groups == null) return;
        foreach (var g in _groups)
            g.ProcessDeadEnemies(gemManager);
    }

    /// <summary>敵へのダメージ表示処理。</summary>
    public void ProcessEnemyDamage()
    {
        if (_groups == null) return;
        foreach (var g in _groups)
            g.ProcessEnemyDamage(damageTextManager);
    }

    /// <summary>敵のリスポーン処理。</summary>
    public void HandleRespawn()
    {
        if (_groups == null || gameManager == null) return;
        float3 playerPos = (float3)gameManager.GetPlayerPosition();
        uint seed = (uint)Time.frameCount;
        foreach (var g in _groups)
            g.HandleRespawn(playerPos, seed);
    }

    /// <summary>
    /// 弾を撃つ敵の発射処理。各グループの ProcessFiring を呼ぶ。BulletManager の Job 完了後に GameManager から呼ぶ。
    /// </summary>
    public void ProcessEnemyBulletFiring(float deltaTime, float3 playerPos)
    {
        // ボスステート時は通常敵を無効化しているため、通常敵の弾発射は行わない
        if (_normalEnemiesEnabled == false || _groups == null || bulletManager == null) return;

        foreach (var g in _groups)
            g.ProcessFiring(deltaTime, playerPos, bulletManager);
    }

    void RenderEnemies()
    {
        if (renderManager == null || _groups == null) return;
        float dt = Time.deltaTime;
        foreach (var g in _groups)
            g.Render(renderManager, dt);
    }

    /// <summary>全グループの敵を非表示にする。</summary>
    public void ClearAllEnemies()
    {
        if (_groups == null) return;
        foreach (var g in _groups)
            g.ClearAllEnemies();
    }

    /// <summary>全グループの敵を初期配置にリセットする。</summary>
    public void ResetEnemies()
    {
        if (_groups == null) return;
        foreach (var g in _groups)
            g.ResetEnemies();
    }
    
    // ボスを生成（プレイヤーの位置と方向を受け取る）
    public void SpawnBoss(Vector3 playerPosition, Vector3 playerForward)
    {
        // 既存のボスがいる場合は削除
        if (_currentBoss != null)
        {
            Destroy(_currentBoss);
            _currentBoss = null;
            _currentBossComponent = null;
        }
        
        // ボスを生成（プレイヤーの真後ろ、指定距離の位置）
        GameObject prefabToUse = _bossPrefabOverride != null ? _bossPrefabOverride : bossPrefab;
        float distanceToUse = respawnMinRadius;
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

            // BossBase コンポーネントを取得して初期化（Boss / 将来のサブクラスに対応）
            _currentBossComponent = _currentBoss.GetComponent<BossBase>();
            if (_currentBossComponent != null && gameManager != null)
            {
                float? bossHpOverride = gameManager.GetDebugBossHpOverride();
                _currentBossComponent.Initialize(
                    () => gameManager.GetPlayerPosition(),
                    bulletManager,
                    bossHpOverride
                );
            }
            else
            {
                Debug.LogWarning("EnemyManager: Failed to initialize BossBase component!");
                _currentBossComponent = null;
            }
        }
        else
        {
            Debug.LogWarning("EnemyManager: Boss prefab is not assigned (stage override and serialized both null)!");
        }
    }

    /// <summary>現在のボス GameObject を取得（カメラの LookAt など Transform が必要なとき用）。</summary>
    public GameObject GetCurrentBoss()
    {
        return _currentBoss;
    }

    /// <summary>現在のボス BossBase を取得（毎フレーム GetComponent しないようキャッシュを返す）。</summary>
    public BossBase GetCurrentBossComponent()
    {
        return _currentBossComponent;
    }

    /// <summary>ボスの移動処理。プレイヤーへの接触ダメージは playerDamageQueue に登録する。</summary>
    public void ProcessBossMovement(float deltaTime, NativeQueue<int> playerDamageQueue)
    {
        if (_bossActive == false || _currentBossComponent == null) return;
        _currentBossComponent.ProcessMovement(deltaTime, playerDamageQueue);
    }

    /// <summary>ボスの弾発射処理。GameManager から順序制御のため呼ばれる。</summary>
    public void ProcessBossBulletFiring(float deltaTime, float3 playerPos)
    {
        if (_bossActive == false || _currentBossComponent == null) return;
        _currentBossComponent.ProcessFiring(deltaTime, playerPos);
    }

    /// <summary>通常敵・ボスの弾発射処理をまとめて実行。GameManager から呼ばれる。</summary>
    public void ProcessFiring(float deltaTime, float3 playerPos)
    {
        ProcessEnemyBulletFiring(deltaTime, playerPos);
        ProcessBossBulletFiring(deltaTime, playerPos);
    }

    /// <summary>ボス死亡チェック・通常敵の死亡・ダメージ表示・リスポーンをまとめて実行。GameManager から呼ばれる。</summary>
    public void ProcessDamage(GemManager gemManager)
    {
        CheckBossDeath();
        ProcessDeadEnemies(gemManager);
        ProcessEnemyDamage();
    }

    // ボスの死亡チェックと削除処理
    /// <summary>ボス死亡チェック。GameManager から順序制御のため呼ばれる。</summary>
    public void CheckBossDeath()
    {
        if (_currentBossComponent == null)
        {
            return;
        }

        if (_currentBossComponent.IsDead)
        {
            Destroy(_currentBoss);
            _currentBoss = null;
            _currentBossComponent = null;
        }
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
            _currentBossComponent = null;
        }
    }

    /// <summary>
    /// ステージの通常敵設定を適用する。既存のグループを破棄し、ステージの敵データスロット（最大5種類）のうち
    /// 非 null のものそれぞれで EnemyGroup を作成する。各グループの最大数は maxEnemyCountPerGroup。
    /// </summary>
    public void ApplyNormalEnemyConfig(StageData stage)
    {
        if (stage is null)
        {
            return;
        }
        // 既存グループを破棄
        foreach (var g in _groups)
            g.Dispose();
        _groups.Clear();

        int maxPerGroup = Mathf.Max(1, maxEnemyCountPerGroup);
        for (int i = 0; i < StageData.MaxEnemyDataSlots; i++)
        {
            var data = stage.GetEnemyData(i);
            if (data == null)
                continue;
            var group = new EnemyGroup(
                data,
                maxPerGroup,
                enemyFlashDuration,
                respawnDistance,
                respawnMinRadius,
                respawnMaxRadius);
            _groups.Add(group);
        }
    }

    /// <summary>
    /// ステージのボス設定を適用する。次回 SpawnBoss で使用される。
    /// </summary>
    public void ApplyBossConfig(StageData stage)
    {
        if (stage == null) return;
        _bossPrefabOverride = stage.BossPrefab;
    }

    protected override void FinalizeInternal()
    {
        foreach (var g in _groups)
            g.Dispose();
        _groups.Clear();

        if (_currentBoss != null)
        {
            Destroy(_currentBoss);
            _currentBoss = null;
            _currentBossComponent = null;
        }
    }
}
