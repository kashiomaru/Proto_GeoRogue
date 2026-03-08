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

/// <summary>弾グループへの操作を表すハンドル。Spawn やダメージ設定はこのインターフェース経由で行う。</summary>
public interface IBulletGroupHandler
{
    void Spawn(Vector3 position, Vector3 direction, float speed, float lifeTime, float directionRotation = 0f);
    int GetDamage();
    void SetDamage(int damage);
    void SetCritical(float criticalChance, float criticalMultiplier);
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
    /// <param name="curveValue">弾の進行方向を回転させる速度（度/秒）。0で直進。</param>
    /// <param name="collisionRadius">当たり判定の半径。0 のとき弾は点として扱う。</param>
    /// <returns>弾グループ操作用のハンドル。Spawn やダメージ設定はこのハンドル経由で行う。</returns>
    public IBulletGroupHandler AddBulletGroup(int damage, float scale, Mesh mesh, Material material, float criticalChance = 0f, float criticalMultiplier = 1f, float curveValue = 0f, float collisionRadius = 0f)
    {
        if (IsInitialized == false)
        {
            return null;
        }

        var groupId = _bulletGroupIdCounter++;
        var group = new BulletGroup(maxBullets, damage, scale, collisionRadius, mesh, material);
        group.SetCriticalParams(criticalChance, criticalMultiplier);
        group.SetCurveValue(curveValue);
        _bulletGroups.Add(groupId, group);
        return new BulletGroupHandler(this, groupId);
    }

    /// <summary>指定した弾グループを削除する。ハンドルは以降使用不可。</summary>
    public void RemoveBulletGroup(IBulletGroupHandler handler)
    {
        if (IsInitialized == false || handler == null)
        {
            return;
        }

        if (handler is BulletGroupHandler h && _bulletGroups.TryGetValue(h.GroupId, out var group))
        {
            group.Dispose();
            _bulletGroups.Remove(h.GroupId);
        }
    }

    /// <summary>指定した弾グループのダメージを取得する（内部・Handler 用）。</summary>
    private int GetBulletGroupDamage(int bulletGroupId)
    {
        if (_bulletGroups.TryGetValue(bulletGroupId, out var group))
        {
            return group.Damage;
        }
        return 0;
    }

    /// <summary>指定した弾グループのダメージを設定する（内部・Handler 用）。</summary>
    private void SetBulletGroupDamage(int bulletGroupId, int damage)
    {
        if (_bulletGroups.TryGetValue(bulletGroupId, out var group))
        {
            group.SetDamage(damage);
        }
    }

    /// <summary>指定した弾グループのクリティカル用パラメータを設定する（内部・Handler 用）。</summary>
    private void SetBulletGroupCritical(int bulletGroupId, float criticalChance, float criticalMultiplier)
    {
        if (_bulletGroups.TryGetValue(bulletGroupId, out var group))
        {
            group.SetCriticalParams(criticalChance, criticalMultiplier);
        }
    }

    /// <param name="directionRotation">発射時に指定方向を回転させる角度（度）。0で回転なし。</param>
    private void SpawnBullet(int bulletGroupId, Vector3 position, Vector3 direction, float speed, float lifeTime, float directionRotation = 0f)
    {
        if (IsInitialized == false)
        {
            return;
        }

        if (_bulletGroups.TryGetValue(bulletGroupId, out var group) == false)
        {
            return;
        }

        Vector3 dir = direction;
        if (directionRotation != 0f)
            dir = Quaternion.AngleAxis(directionRotation, Vector3.up) * direction.normalized;
        group.Spawn(position, dir, speed, lifeTime);
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
        IBulletGroupHandler bulletGroup,
        float targetCellSize,
        float targetCollisionRadius,
        NativeParallelMultiHashMap<int, int> targetSpatialMap,
        NativeArray<float3> targetPositions,
        NativeArray<bool> targetActive,
        NativeQueue<BulletDamageInfo>.ParallelWriter targetDamageQueue,
        JobHandle dependency = default)
    {
        if (bulletGroup is BulletGroupHandler h)
        {
            return ProcessDamageByGroupId(h.GroupId, targetCellSize, targetCollisionRadius, targetSpatialMap, targetPositions, targetActive, targetDamageQueue, dependency);
        }
        return dependency;
    }

    /// <summary>
    /// 指定した弾グループとターゲットグループの当たり判定 Job をスケジュールする（内部用）。
    /// </summary>
    private JobHandle ProcessDamageByGroupId(
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

        float combinedRadius = targetCollisionRadius + bulletGroup.CollisionRadius;
        _cachedCollideJob.targetCellSize = targetCellSize;
        _cachedCollideJob.targetCollisionRadiusSq = combinedRadius * combinedRadius;
        _cachedCollideJob.targetSpatialMap = targetSpatialMap;
        _cachedCollideJob.targetPositions = targetPositions;
        _cachedCollideJob.targetActive = targetActive;
        _cachedCollideJob.targetDamageQueue = targetDamageQueue;

        return _cachedCollideJob.Schedule(bulletGroup.MaxCount, 64, dependency);
    }

    void LateUpdate()
    {
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
    public void ProcessDamage(IBulletGroupHandler bulletGroup, Vector3 targetPosition, float targetCollisionRadius, NativeQueue<HitDamageInfo> damageQueueOut)
    {
        if (bulletGroup is BulletGroupHandler h)
        {
            ProcessDamageByGroupId(h.GroupId, targetPosition, targetCollisionRadius, damageQueueOut);
        }
    }

    /// <summary>
    /// 指定円（中心・半径）と当たった弾を収集する（内部用）。
    /// </summary>
    private void ProcessDamageByGroupId(int bulletGroupId, Vector3 targetPosition, float targetCollisionRadius, NativeQueue<HitDamageInfo> damageQueueOut)
    {
        if (_bulletGroups.TryGetValue(bulletGroupId, out var group) == false)
        {
            return;
        }

        group.CollectHitsAgainstCircle((float3)targetPosition, targetCollisionRadius, damageQueueOut);
    }

    /// <summary>
    /// Scene View 用に全弾グループの弾を Gizmos で描画する。CollisionGizmoDrawer から呼ばれる。
    /// 当たり判定で使用している弾の半径（CollisionRadius）で円を描画する。0 のときは点として最小の円で表示する。
    /// </summary>
    public void DrawBulletGizmos(Color color)
    {
        if (IsInitialized == false || _bulletGroups == null) return;
        foreach (var kv in _bulletGroups)
        {
            var group = kv.Value;
            if (!group.Positions.IsCreated || !group.Active.IsCreated) continue;
            int n = group.Positions.Length;
            float r = group.CollisionRadius > 0f ? group.CollisionRadius : 0.05f;
            Gizmos.color = color;
            for (int i = 0; i < n; i++)
            {
                if (group.Active[i])
                    Gizmos.DrawWireSphere(group.Positions[i], r);
            }
        }
    }

    /// <summary>弾グループの内部ハンドル。BulletManager 外には IBulletGroupHandler として公開する。</summary>
    private sealed class BulletGroupHandler : IBulletGroupHandler
    {
        private readonly BulletManager _manager;
        private readonly int _groupId;

        internal int GroupId => _groupId;

        public BulletGroupHandler(BulletManager manager, int groupId)
        {
            _manager = manager;
            _groupId = groupId;
        }

        public void Spawn(Vector3 position, Vector3 direction, float speed, float lifeTime, float directionRotation = 0f)
        {
            _manager.SpawnBullet(_groupId, position, direction, speed, lifeTime, directionRotation);
        }

        public int GetDamage() => _manager.GetBulletGroupDamage(_groupId);
        public void SetDamage(int damage) => _manager.SetBulletGroupDamage(_groupId, damage);
        public void SetCritical(float criticalChance, float criticalMultiplier) => _manager.SetBulletGroupCritical(_groupId, criticalChance, criticalMultiplier);
    }
}
