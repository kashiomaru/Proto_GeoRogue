using UnityEngine;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
struct GemInitFlagsJob : IJobParallelFor
{
    public NativeArray<bool> active;
    public NativeArray<bool> flying;

    public void Execute(int index)
    {
        active[index] = false;
        flying[index] = false;
    }
}

public class GemManager : InitializeMonobehaviour
{
    [SerializeField] private int maxGems = 1000;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private float gemSpeed = 15.0f;  // 吸い寄せ速度
    [SerializeField] private float gemScale = 0.4f;  // 描画スケール（Matrix4x4 用）

    [Header("References")]
    [SerializeField] private Player player; // 吸い寄せ距離は Player.GetMagnetDist() から取得
    [SerializeField] private RenderManager renderManager;

    // --- Gem Data（座標のみ保持、Prefab インスタンスは生成しない・敵と同様）---
    private NativeArray<float3> _gemPositions;
    private NativeArray<bool> _gemActive;
    private NativeArray<bool> _gemIsFlying;
    /// <summary>描画用。Job で詰めた Matrix4x4 を RenderManager に直接渡す。</summary>
    private NativeArray<Matrix4x4> _gemMatrices;
    private NativeArray<int> _gemDrawCount;
    /// <summary>DrawMatrixJob 用。ジェムは回転不要のためゼロ配列（identity になる）。</summary>
    private NativeArray<float3> _gemDirections;
    /// <summary>DrawMatrixJob の書き込みインデックス用。毎フレーム 0 にリセット。</summary>
    private NativeReference<int> _gemMatrixCounter;

    // 毎フレーム new を避けるため Job をキャッシュ
    private GemMagnetJob _gemMagnetJob;
    private DrawMatrixJob _gemMatrixJob;

    // 経験値加算用（Job内からメインスレッドへ通知）
    private NativeQueue<int> _collectedGemQueue;
    private int _gemHeadIndex = 0;

    // 外部（EnemyManager 等）から呼ぶ
    public void SpawnGem(Vector3 position)
    {
        if (IsInitialized == false)
        {
            return;
        }

        int id = _gemHeadIndex;
        _gemHeadIndex = (_gemHeadIndex + 1) % maxGems;

        _gemActive[id] = true;
        _gemIsFlying[id] = false;
        _gemPositions[id] = (float3)position;
    }

    protected override void InitializeInternal()
    {
        _gemPositions = new NativeArray<float3>(maxGems, Allocator.Persistent);
        _gemActive = new NativeArray<bool>(maxGems, Allocator.Persistent);
        _gemIsFlying = new NativeArray<bool>(maxGems, Allocator.Persistent);
        _gemMatrices = new NativeArray<Matrix4x4>(maxGems, Allocator.Persistent);
        _gemDrawCount = new NativeArray<int>(1, Allocator.Persistent);
        _gemDirections = new NativeArray<float3>(maxGems, Allocator.Persistent);
        _gemMatrixCounter = new NativeReference<int>(0, Allocator.Persistent);
        _collectedGemQueue = new NativeQueue<int>(Allocator.Persistent);

        var initJob = new GemInitFlagsJob { active = _gemActive, flying = _gemIsFlying };
        initJob.Schedule(maxGems, 64).Complete();

        // フレーム共通の Job フィールドを一度だけ設定
        _gemMagnetJob.moveSpeed = gemSpeed;
        _gemMagnetJob.positions = _gemPositions;
        _gemMagnetJob.activeFlags = _gemActive;
        _gemMagnetJob.flyingFlags = _gemIsFlying;

        _gemMatrixJob.positions = _gemPositions;
        _gemMatrixJob.directions = _gemDirections;
        _gemMatrixJob.activeFlags = _gemActive;
        _gemMatrixJob.matrices = _gemMatrices;
        _gemMatrixJob.counter = _gemMatrixCounter;
        _gemMatrixJob.scale = gemScale;
    }

    void Update()
    {
        if (IsInitialized == false)
        {
            return;
        }

        float magnetDist = player != null ? player.GetMagnetDist() : 5f;
        _gemMagnetJob.deltaTime = Time.deltaTime;
        _gemMagnetJob.playerPos = (float3)playerTransform.position;
        _gemMagnetJob.magnetDistSq = magnetDist * magnetDist;
        _gemMagnetJob.collectedGemQueue = _collectedGemQueue.AsParallelWriter();
        _gemMagnetJob.Schedule(maxGems, 64).Complete();

        _gemMatrixCounter.Value = 0;
        _gemMatrixJob.Schedule(maxGems, 64).Complete();
        _gemDrawCount[0] = _gemMatrixCounter.Value;
        renderManager?.RenderGems(_gemMatrices, _gemDrawCount[0]);
    }

    protected override void FinalizeInternal()
    {
        if (_gemPositions.IsCreated)
            _gemPositions.Dispose();
        if (_gemActive.IsCreated)
            _gemActive.Dispose();
        if (_gemIsFlying.IsCreated)
            _gemIsFlying.Dispose();
        if (_gemMatrices.IsCreated)
            _gemMatrices.Dispose();
        if (_gemDrawCount.IsCreated)
            _gemDrawCount.Dispose();
        if (_gemDirections.IsCreated)
            _gemDirections.Dispose();
        if (_gemMatrixCounter.IsCreated)
            _gemMatrixCounter.Dispose();
        if (_collectedGemQueue.IsCreated)
            _collectedGemQueue.Dispose();
    }
    
    /// <summary>
    /// ジェムをすべて非表示にし、キューをクリアする。ゲームリセット時（タイトル戻り・リトライ）に呼ぶ。
    /// </summary>
    public void ResetGems()
    {
        if (IsInitialized == false)
        {
            return;
        }

        for (int i = 0; i < maxGems; i++)
        {
            _gemActive[i] = false;
            _gemIsFlying[i] = false;
        }
        while (_collectedGemQueue.TryDequeue(out _)) { }
    }

    // GameManager用：回収されたジェムの数を取得（キューから取得して返す）
    public int GetCollectedGemCount()
    {
        if (IsInitialized == false)
        {
            return 0;
        }

        int count = 0;
        while (_collectedGemQueue.TryDequeue(out int _))
        {
            count++;
        }
        return count;
    }
}
