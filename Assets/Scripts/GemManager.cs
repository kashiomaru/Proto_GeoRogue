using UnityEngine;
using UnityEngine.Jobs;
using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;

public class GemManager : MonoBehaviour
{
    [SerializeField] private GameObject gemPrefab; // 小さな光るCube/Sphere
    [SerializeField] private int maxGems = 2000;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private float magnetDist = 5.0f; // 吸い寄せ開始距離
    [SerializeField] private float gemSpeed = 15.0f;  // 吸い寄せ速度
    
    [Header("References")]
    [SerializeField] private Player player; // Playerへの参照

    private TransformAccessArray _gemTransforms;
    private NativeArray<float3> _gemPositions; // ジェムの位置配列
    private NativeArray<bool> _gemActive;      // 画面に出ているか
    private NativeArray<bool> _gemIsFlying;    // プレイヤーに向かって飛んでいるか
    
    // 描画最適化用（Transformから座標を取るのは重いので、座標だけの配列も持つ）
    // 今回は簡易的にTransformAccessArrayで動かしますが、本来はこれもNativeArray<float3>で管理推奨
    
    // 経験値加算用（Job内からメインスレッドへ通知）
    // 回収されたジェムの数を記録するキュー（各ジェムごとに1を追加）
    private NativeQueue<int> _collectedGemQueue;
    
    private int _gemHeadIndex = 0; // リングバッファ用

    // 外部（GameManager）から呼ぶ
    public void SpawnGem(Vector3 position)
    {
        int id = _gemHeadIndex;
        _gemHeadIndex = (_gemHeadIndex + 1) % maxGems;

        // すでに使われていても強制上書き（古いGemは消える仕様でOK）
        _gemActive[id] = true;
        _gemIsFlying[id] = false;
        _gemPositions[id] = (float3)position;
        
        // Transformの位置を更新するJobを実行
        var updatePosJob = new UpdateGemPositionJob
        {
            positions = _gemPositions,
            activeFlags = _gemActive
        };
        updatePosJob.Schedule(_gemTransforms).Complete();
    }

    void Start()
    {
        _gemTransforms = new TransformAccessArray(maxGems);
        _gemPositions = new NativeArray<float3>(maxGems, Allocator.Persistent);
        _gemActive = new NativeArray<bool>(maxGems, Allocator.Persistent);
        _gemIsFlying = new NativeArray<bool>(maxGems, Allocator.Persistent);
        
        // 回収されたジェムを記録するキューを初期化
        _collectedGemQueue = new NativeQueue<int>(Allocator.Persistent);

        // プール生成（画面外へ）
        for (int i = 0; i < maxGems; i++)
        {
            var obj = Instantiate(gemPrefab, new Vector3(0, -500, 0), Quaternion.identity);
            _gemTransforms.Add(obj.transform);
            _gemPositions[i] = new float3(0, -500, 0);
            _gemActive[i] = false;
            _gemIsFlying[i] = false;
        }
    }

    void Update()
    {
        // プレイヤーのレベルアップ管理などはここで行う

        // --- JOB: Gemの吸い寄せと回収 ---
        var gemJob = new GemMagnetJob
        {
            deltaTime = Time.deltaTime,
            playerPos = (float3)playerTransform.position,
            magnetDistSq = magnetDist * magnetDist,
            moveSpeed = gemSpeed,
            positions = _gemPositions,
            activeFlags = _gemActive,
            flyingFlags = _gemIsFlying,
            collectedGemQueue = _collectedGemQueue.AsParallelWriter() // 回収されたジェムを記録
        };

        var handle = gemJob.Schedule(_gemTransforms);
        handle.Complete();
        
        // 位置をTransformに反映
        var updatePosJob = new UpdateGemPositionJob
        {
            positions = _gemPositions,
            activeFlags = _gemActive
        };
        updatePosJob.Schedule(_gemTransforms).Complete();
        
        // 回収されたジェムの数はGameManagerで取得するため、ここでは処理しない
        // （GameManager.GetCollectedGemCount()で取得可能）
    }

    void OnDestroy()
    {
        if (_gemTransforms.isCreated) _gemTransforms.Dispose();
        if (_gemPositions.IsCreated) _gemPositions.Dispose();
        if (_gemActive.IsCreated) _gemActive.Dispose();
        if (_gemIsFlying.IsCreated) _gemIsFlying.Dispose();
        if (_collectedGemQueue.IsCreated) _collectedGemQueue.Dispose();
    }
    
    // LevelUpManager用のパラメータ取得・設定メソッド
    public float GetMagnetDist()
    {
        return magnetDist;
    }
    
    public void SetMagnetDist(float value)
    {
        magnetDist = value;
    }
    
    // GameManager用：回収されたジェムの数を取得（キューから取得して返す）
    public int GetCollectedGemCount()
    {
        int count = 0;
        while (_collectedGemQueue.TryDequeue(out int _))
        {
            count++;
        }
        return count;
    }
}
