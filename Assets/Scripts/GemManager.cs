using UnityEngine;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using System.Collections.Generic;

public class GemManager : InitializeMonobehaviour
{
    [SerializeField] private int maxGems = 1000;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private float gemSpeed = 15.0f;  // 吸い寄せ速度

    [Header("References")]
    [SerializeField] private Player player; // 吸い寄せ距離は Player.GetMagnetDist() から取得
    [SerializeField] private RenderManager renderManager;

    // --- Gem Data（座標のみ保持、Prefab インスタンスは生成しない・敵と同様）---
    private NativeArray<float3> _gemPositions;
    private NativeArray<bool> _gemActive;
    private NativeArray<bool> _gemIsFlying;
    private List<Vector3> _gemPositionList;
    private List<bool> _gemActiveList;

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
        _gemPositionList = new List<Vector3>(maxGems);
        _gemActiveList = new List<bool>(maxGems);
        _collectedGemQueue = new NativeQueue<int>(Allocator.Persistent);

        for (int i = 0; i < maxGems; i++)
        {
            _gemActive[i] = false;
            _gemIsFlying[i] = false;
            _gemPositionList.Add(default);
            _gemActiveList.Add(false);
        }
    }

    void Update()
    {
        if (IsInitialized == false)
        {
            return;
        }

        float magnetDist = player != null ? player.GetMagnetDist() : 5f;
        var gemJob = new GemMagnetJob
        {
            deltaTime = Time.deltaTime,
            playerPos = (float3)playerTransform.position,
            magnetDistSq = magnetDist * magnetDist,
            moveSpeed = gemSpeed,
            positions = _gemPositions,
            activeFlags = _gemActive,
            flyingFlags = _gemIsFlying,
            collectedGemQueue = _collectedGemQueue.AsParallelWriter()
        };
        gemJob.Schedule(maxGems, 64).Complete();

        // NativeArray → List にコピーして描画（敵と同様）
        for (int i = 0; i < maxGems; i++)
        {
            _gemPositionList[i] = _gemPositions[i];
            _gemActiveList[i] = _gemActive[i];
        }
        renderManager?.RenderGems(_gemPositionList, _gemActiveList);
    }

    protected override void FinalizeInternal()
    {
        if (_gemPositions.IsCreated)
        {
            _gemPositions.Dispose();
        }
        if (_gemActive.IsCreated)
        {
            _gemActive.Dispose();
        }
        if (_gemIsFlying.IsCreated)
        {
            _gemIsFlying.Dispose();
        }
        if (_collectedGemQueue.IsCreated)
        {
            _collectedGemQueue.Dispose();
        }
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
