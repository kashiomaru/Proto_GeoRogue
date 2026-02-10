using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using System.Collections.Generic;

public struct BulletDamageInfo
{
    public float3 position;
    public int damage;
    public int index;
    public bool isCritical;

    public BulletDamageInfo(float3 pos, int dmg, int idx, bool critical = false)
    {
        position = pos;
        damage = dmg;
        index = idx;
        isCritical = critical;
    }
}

/// <summary>円との当たり判定でヒットした弾のダメージ情報（ボス用など）。</summary>
public struct HitDamageInfo
{
    public int damage;
    public bool isCritical;
}

public class BulletManager : InitializeMonobehaviour
{
    [Header("Settings")]
    [SerializeField] private int maxBullets = 1000;

    [Header("References")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private RenderManager renderManager;

    private Dictionary<int, BulletGroup> _bulletGroups;
    private int _bulletGroupIdCounter = 0;

    private BulletCollideJob _cachedCollideJob;

    protected override void InitializeInternal()
    {
        Debug.Assert(renderManager != null, "[BulletManager] renderManager が未設定です。インスペクターで指定してください。");
        Debug.Assert(gameManager != null, "[BulletManager] gameManager が未設定です。インスペクターで指定してください。");

        _bulletGroups = new Dictionary<int, BulletGroup>();
    }

    protected override void FinalizeInternal()
    {
        foreach (var group in _bulletGroups)
        {
            group.Value.Dispose();
        }
        _bulletGroups.Clear();
        _bulletGroups = null;
    }

    /// <param name="criticalChance">クリティカル発生確率（0～1）。敵弾などで使わない場合は 0。</param>
    /// <param name="criticalMultiplier">クリティカル時のダメージ倍率。使わない場合は 1。</param>
    public int AddBulletGroup(int damage, float scale, Mesh mesh, Material material, float criticalChance = 0f, float criticalMultiplier = 1f)
    {
        if (IsInitialized == false)
        {
            return -1;
        }

        var groupId = _bulletGroupIdCounter++;
        var group = new BulletGroup(maxBullets, damage, scale, mesh, material);
        group.SetCriticalParams(criticalChance, criticalMultiplier);
        _bulletGroups.Add(groupId, group);
        return groupId;
    }

    public void RemoveBulletGroup(int groupId)
    {
        if (IsInitialized == false)
        {
            return;
        }

        if (_bulletGroups.TryGetValue(groupId, out var group))
        {
            group.Dispose();
            _bulletGroups.Remove(groupId);
        }
    }

    /// <summary>指定した弾グループのダメージを取得する。</summary>
    public int GetBulletGroupDamage(int bulletGroupId)
    {
        if (_bulletGroups.TryGetValue(bulletGroupId, out var group))
        {
            return group.Damage;
        }
        return 0;
    }

    /// <summary>指定した弾グループのダメージを設定する。</summary>
    public void SetBulletGroupDamage(int bulletGroupId, int damage)
    {
        if (_bulletGroups.TryGetValue(bulletGroupId, out var group))
        {
            group.SetDamage(damage);
        }
    }

    /// <summary>指定した弾グループのクリティカル用パラメータを設定する（Player の弾グループ用）。</summary>
    public void SetBulletGroupCritical(int bulletGroupId, float criticalChance, float criticalMultiplier)
    {
        if (_bulletGroups.TryGetValue(bulletGroupId, out var group))
        {
            group.SetCriticalParams(criticalChance, criticalMultiplier);
        }
    }

    public void SpawnBullet(int bulletGroupId, Vector3 position, Vector3 direction, float speed, float lifeTime)
    {
        if (IsInitialized == false)
        {
            return;
        }

        if (_bulletGroups.TryGetValue(bulletGroupId, out var group) == false)
        {
            return;
        }

        group.Spawn(position, direction, speed, lifeTime);
    }

    /// <summary>
    /// 弾の移動 Job をスケジュールし完了まで待機する
    /// </summary>
    public void ProcessMovement()
    {
        if (IsInitialized == false)
        {
            return;
        }

        JobHandle dep = default;
        foreach (var group in _bulletGroups)
        {
            dep = group.Value.ScheduleMoveJob(dep);
        }
        dep.Complete();
    }

    /// <summary>
    /// 指定した弾グループとターゲットグループの当たり判定 Job をスケジュールする。
    /// targetCollisionRadius はメソッド内で二乗して Job に渡す。
    /// </summary>
    public JobHandle ProcessDamage(
        int bulletGroupId,
        float targetCellSize,
        float targetCollisionRadius,
        NativeParallelMultiHashMap<int, int> targetSpatialMap,
        NativeArray<float3> targetPositions,
        NativeArray<bool> targetActive,
        NativeQueue<BulletDamageInfo>.ParallelWriter targetDamageQueue,
        JobHandle dependency = default)
    {
        if (IsInitialized == false)
        {
            return dependency;
        }

        if (_bulletGroups.TryGetValue(bulletGroupId, out var bulletGroup) == false)
        {
            return dependency;
        }

        _cachedCollideJob.bulletPositions = bulletGroup.Positions;
        _cachedCollideJob.bulletActive = bulletGroup.Active;
        _cachedCollideJob.bulletDamage = bulletGroup.Damage;
        _cachedCollideJob.criticalChance = bulletGroup.CriticalChance;
        _cachedCollideJob.criticalMultiplier = bulletGroup.CriticalMultiplier;
        _cachedCollideJob.seed = (uint)Time.frameCount;

        _cachedCollideJob.targetCellSize = targetCellSize;
        _cachedCollideJob.targetCollisionRadiusSq = targetCollisionRadius * targetCollisionRadius;
        _cachedCollideJob.targetSpatialMap = targetSpatialMap;
        _cachedCollideJob.targetPositions = targetPositions;
        _cachedCollideJob.targetActive = targetActive;
        _cachedCollideJob.targetDamageQueue = targetDamageQueue;

        return _cachedCollideJob.Schedule(bulletGroup.MaxCount, 64, dependency);
    }

    void LateUpdate()
    {
        // 描画は LateUpdate で行う（Update で完了した Job の結果を描画）
        if (IsInitialized == false) return;
        if (gameManager.IsPlaying == false) return;
        RenderBullets();
    }

    /// <summary>
    /// 弾の座標を Matrix に詰め、RenderManager で描画する。LateUpdate から呼ぶ。
    /// </summary>
    public void RenderBullets()
    {
        if (IsInitialized == false)
        {
            return;
        }

        foreach (var group in _bulletGroups)
        {
            group.Value.RunMatrixJob();
            renderManager.RenderBullets(group.Value.RenderParams, group.Value.Mesh, group.Value.Matrices, group.Value.DrawCount);
        }
    }

    public void ResetBullets()
    {
        foreach (var group in _bulletGroups)
        {
            group.Value.Reset();
        }
    }

    /// <summary>
    /// 指定円（中心・半径）と当たった弾を収集し、該当弾を無効化する。ヒットした弾のダメージとクリティカル有無を damageQueueOut に Enqueue する。
    /// </summary>
    /// <param name="bulletGroupId">弾グループ ID</param>
    /// <param name="targetPosition">当たり判定の中心</param>
    /// <param name="targetCollisionRadius">当たり判定の半径</param>
    /// <param name="damageQueueOut">ヒットした弾のダメージ・クリティカル有無を格納するキュー（呼び出し側で用意）</param>
    public void ProcessDamage(int bulletGroupId, Vector3 targetPosition, float targetCollisionRadius, NativeQueue<HitDamageInfo> damageQueueOut)
    {
        if (_bulletGroups.TryGetValue(bulletGroupId, out var group) == false)
        {
            return;
        }

        group.CollectHitsAgainstCircle((float3)targetPosition, targetCollisionRadius, damageQueueOut);
    }
}
