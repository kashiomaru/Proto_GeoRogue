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

    // --- Enemy Data ---
    private TransformAccessArray _enemyTransforms;
    private NativeArray<float3> _enemyPositions;
    private NativeArray<bool> _enemyActive; // 生存フラグ

    // --- Bullet Data ---
    private TransformAccessArray _bulletTransforms; // 今回は簡易的にTransformを使いますが、本来はMatrix配列で描画すべき
    private NativeArray<float3> _bulletPositions;
    private NativeArray<bool> _bulletActive;
    private NativeArray<float> _bulletLifeTime;
    
    // --- Spatial Partitioning ---
    // Key: グリッドのハッシュ値, Value: 敵のインデックス
    private NativeParallelMultiHashMap<int, int> _spatialMap;

    private float _timer;
    private int _bulletIndexHead = 0; // リングバッファ用

    void Start()
    {
        InitializeEnemies();
        InitializeBullets();
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
            spatialMap = _spatialMap.AsParallelWriter(), // 並列書き込み用
            positions = _enemyPositions,
            activeFlags = _enemyActive
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
            bulletActive = _bulletActive,
            bulletLifeTime = _bulletLifeTime,
            enemyActive = _enemyActive // ヒットしたらfalseにする
        };
        
        var bulletHandle = bulletJob.Schedule(_bulletTransforms, enemyHandle);

        // 完了待ち
        bulletHandle.Complete();

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
            // 一番近い敵を探して撃つロジックは省略し、とりあえずランダム or 正面へ
            // リングバッファで弾を再利用
            int id = _bulletIndexHead;
            _bulletIndexHead = (_bulletIndexHead + 1) % maxBullets;

            _bulletActive[id] = true;
            _bulletPositions[id] = playerTransform.position;
            _bulletLifeTime[id] = 2.0f; // 2秒で消える
            
            // Transformも更新しておく（描画用）
            // 注意: TransformAccessArrayへの直接書き込みはJob外ではできないため、
            // ここではGameObject経由で動かすか、Job内で初期位置セットが必要。
            // 簡易的にGameObjectを動かす：
            // _bulletTransforms[id].position = playerTransform.position; 
            // ↑ TransformAccessArrayはインデクサでGameObjectにアクセスできないため、
            // 実運用ではJob内で「初期化フラグ」を見て位置セットするのが定石です。
            // 今回は簡略化のため、「発射された弾はJob内で位置更新される」前提で進めます。
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
        if (_bulletActive.IsCreated) _bulletActive.Dispose();
        if (_bulletLifeTime.IsCreated) _bulletLifeTime.Dispose();
        
        if (_spatialMap.IsCreated) _spatialMap.Dispose();
    }
}
