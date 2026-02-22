using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

public class GemManager : InitializeMonobehaviour
{
    [SerializeField] private int maxGems = 1000;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private float initialGemSpeed = 5.0f;   // 吸い寄せ開始時の初速
    [SerializeField] private float maxGemSpeed = 30.0f;       // 吸い寄せ中の最大速度
    [SerializeField] private float gemAcceleration = 25.0f;  // 吸い寄せ中の加速度（/秒）
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
    /// <summary>DrawMatrixJob 用。ジェムは回転不要のため (0,0,1) で identity 相当。</summary>
    private NativeArray<float3> _gemDirections;
    /// <summary>各ジェムの加算値。SpawnGem で設定し、回収時にキューへ enqueue する。</summary>
    private NativeArray<int> _gemAddValues;
    /// <summary>描画スケール倍率。加算値 n に対して 1 + 0.1*n（1→1.1, 2→1.2）。</summary>
    private NativeArray<float> _gemScaleMultipliers;
    /// <summary>各ジェムの現在速度。吸い寄せ中に加速する。</summary>
    private NativeArray<float> _gemSpeeds;
    /// <summary>DrawMatrixJob の書き込みインデックス用。毎フレーム 0 にリセット。</summary>
    private NativeReference<int> _gemMatrixCounter;

    // 毎フレーム new を避けるため Job をキャッシュ
    private GemInitFlagsJob _cachedInitJob;
    private GemMagnetJob _gemMagnetJob;
    private DrawMatrixJob _gemMatrixJob;

    // 経験値加算用（Job内からメインスレッドへ通知）
    private NativeQueue<int> _collectedGemQueue;
    private int _gemHeadIndex = 0;

    /// <summary>指定位置にジェムを生成する。加算値に応じて表示スケールが変わる（1→1.1, 2→1.2, 3→1.3 の倍率）。</summary>
    /// <param name="position">出現位置</param>
    /// <param name="addValue">回収時に加算する値。デフォルト 1。表示スケールは 1 + 0.1*addValue。</param>
    public void SpawnGem(Vector3 position, int addValue = 1)
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
        _gemAddValues[id] = addValue;
        _gemScaleMultipliers[id] = 1f + 0.1f * addValue;
        _gemDirections[id] = new float3(0f, 0f, 1f); // 回転なし（LookRotation で identity 相当）
        _gemSpeeds[id] = initialGemSpeed;
    }

    protected override void InitializeInternal()
    {
        _gemPositions = new NativeArray<float3>(maxGems, Allocator.Persistent);
        _gemActive = new NativeArray<bool>(maxGems, Allocator.Persistent);
        _gemIsFlying = new NativeArray<bool>(maxGems, Allocator.Persistent);
        _gemMatrices = new NativeArray<Matrix4x4>(maxGems, Allocator.Persistent);
        _gemDrawCount = new NativeArray<int>(1, Allocator.Persistent);
        _gemDirections = new NativeArray<float3>(maxGems, Allocator.Persistent);
        _gemAddValues = new NativeArray<int>(maxGems, Allocator.Persistent);
        _gemScaleMultipliers = new NativeArray<float>(maxGems, Allocator.Persistent);
        _gemSpeeds = new NativeArray<float>(maxGems, Allocator.Persistent);
        _gemMatrixCounter = new NativeReference<int>(0, Allocator.Persistent);
        _collectedGemQueue = new NativeQueue<int>(Allocator.Persistent);

        for (int i = 0; i < maxGems; i++)
            _gemSpeeds[i] = initialGemSpeed;

        _cachedInitJob.active = _gemActive;
        _cachedInitJob.flying = _gemIsFlying;
        _cachedInitJob.directions = _gemDirections;
        _cachedInitJob.Schedule(maxGems, 64).Complete();

        // フレーム共通の Job フィールドを一度だけ設定
        _gemMagnetJob.positions = _gemPositions;
        _gemMagnetJob.activeFlags = _gemActive;
        _gemMagnetJob.flyingFlags = _gemIsFlying;
        _gemMagnetJob.gemAddValues = _gemAddValues;
        _gemMagnetJob.speeds = _gemSpeeds;
        _gemMagnetJob.acceleration = gemAcceleration;
        _gemMagnetJob.maxSpeed = maxGemSpeed;

        _gemMatrixJob.positions = _gemPositions;
        _gemMatrixJob.directions = _gemDirections;
        _gemMatrixJob.activeFlags = _gemActive;
        _gemMatrixJob.matrices = _gemMatrices;
        _gemMatrixJob.counter = _gemMatrixCounter;
        _gemMatrixJob.scale = new Vector3(gemScale, gemScale, gemScale);
        _gemMatrixJob.scaleMultipliers = _gemScaleMultipliers;
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
        _gemMagnetJob.acceleration = gemAcceleration;
        _gemMagnetJob.maxSpeed = maxGemSpeed;
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
        if (_gemAddValues.IsCreated)
            _gemAddValues.Dispose();
        if (_gemScaleMultipliers.IsCreated)
            _gemScaleMultipliers.Dispose();
        if (_gemSpeeds.IsCreated)
            _gemSpeeds.Dispose();
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

        _cachedInitJob.Schedule(maxGems, 64).Complete();
        while (_collectedGemQueue.TryDequeue(out _)) { }
    }

    /// <summary>回収されたジェムの加算値の合計を取得（キューをドレインして返す）。</summary>
    public int GetCollectedGemCount()
    {
        if (IsInitialized == false)
        {
            return 0;
        }

        int total = 0;
        while (_collectedGemQueue.TryDequeue(out int value))
        {
            total += value;
        }
        return total;
    }
}
