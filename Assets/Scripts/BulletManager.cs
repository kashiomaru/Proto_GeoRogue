using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using System.Collections.Generic;

/// <summary>弾が敵に与えたダメージ情報を保持する構造体。</summary>
public struct BulletDamageInfo
{
    public float3 position;
    public int damage;
    public int index;

    public BulletDamageInfo(float3 pos, int dmg, int idx)
    {
        position = pos;
        damage = dmg;
        index = idx;
    }
}

/// <summary>
/// プレイヤー弾と敵弾の 2 つの BulletGroup を保持し、発射・移動スケジュール・当たり判定・描画をまとめて行う。
/// </summary>
public class BulletManager : InitializeMonobehaviour
{
    [Header("Settings")]
    [SerializeField] private int maxBullets = 1000;

    [Header("References")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private RenderManager renderManager;

    private Dictionary<int, BulletGroup> _bulletGroups;
    private int _bulletGroupIdCounter = 0;

    /// <summary>敵グループループ内で再利用する当たり判定 Job。グループごとに参照だけ差し替える。</summary>
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

    /// <summary>弾グループを追加する。mesh と material が null の場合は RenderBullets でプレイヤー/敵のデフォルトを使用。</summary>
    public int AddBulletGroup(int damage, float scale, Mesh mesh, Material material)
    {
        if (IsInitialized == false)
        {
            return -1;
        }

        var groupId = _bulletGroupIdCounter++;
        var group = new BulletGroup(maxBullets, damage, scale, mesh, material);
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
    /// 弾の移動 Job をスケジュールし完了まで待機する。プレイヤー弾の移動 → 敵弾の移動の順に依存させる。
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
    /// 指定した弾グループとターゲットグループの当たり判定 Job をスケジュールする
    /// </summary>
    public JobHandle ProcessDamage(
        int bulletGroupId,
        float targetCellSize,
        float targetCollisionRadiusSq,
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

        _cachedCollideJob.cellSize = targetCellSize;
        _cachedCollideJob.targetCollisionRadiusSq = targetCollisionRadiusSq;
        _cachedCollideJob.spatialMap = targetSpatialMap;
        _cachedCollideJob.targetPositions = targetPositions;
        _cachedCollideJob.targetActive = targetActive;
        _cachedCollideJob.damageQueue = targetDamageQueue;

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

    /// <summary>
    /// 弾をすべてリセット（プレイヤー弾・敵弾とも）。ResetGameState 時などに呼ぶ。
    /// </summary>
    public void ResetBullets()
    {
        foreach (var group in _bulletGroups)
        {
            group.Value.Reset();
        }
    }

    /// <summary>
    /// 指定円（中心・半径）と当たった弾を収集し、該当弾を無効化する。ヒットした弾のダメージを damageQueueOut に Enqueue する。
    /// プレイヤー弾vsボスなど、呼び出し側でダメージ適用する場合に使用する。
    /// </summary>
    /// <param name="bulletGroupId">弾グループ ID</param>
    /// <param name="targetPosition">当たり判定の中心</param>
    /// <param name="targetCollisionRadius">当たり判定の半径</param>
    /// <param name="damageQueueOut">ヒットした弾のダメージを格納するキュー（呼び出し側で用意）</param>
    public void ProcessDamage(int bulletGroupId, Vector3 targetPosition, float targetCollisionRadius, NativeQueue<int> damageQueueOut)
    {
        if (_bulletGroups.TryGetValue(bulletGroupId, out var group) == false)
        {
            return;
        }

        group.CollectHitsAgainstCircle((float3)targetPosition, targetCollisionRadius, damageQueueOut);
    }
}
