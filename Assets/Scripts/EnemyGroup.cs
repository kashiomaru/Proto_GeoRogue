using UnityEngine;
using UnityEngine.Assertions;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using System.Collections.Generic;

/// <summary>
/// 1種類の敵データ（1つの EnemyData）に対応する1グループを管理するクラス。
/// 座標・回転・HP・空間マップ・キューを保持し、移動Job・リスポーン・描画・弾衝突用のAPIを提供する。
/// MonoBehaviour ではなく通常の C# クラス。EnemyManager が new / Dispose で管理する。
/// </summary>
public class EnemyGroup
{
    private readonly int _maxCount;
    private readonly int _spawnCount; // 実際に出現させる数（1～_maxCount）
    private readonly float _speed;
    private readonly float _maxHp;
    private readonly float _flashDuration;
    private readonly float _damageRadius;
    private readonly float _collisionRadius;
    private readonly float _cellSize;
    private readonly float _respawnDistance;
    private readonly float _respawnMinRadius;
    private readonly float _respawnMaxRadius;
    private readonly Mesh _mesh;
    private readonly Material _material;
    private readonly Vector3 _scale;

    private NativeArray<float3> _positions;
    private NativeArray<quaternion> _rotations;
    private NativeArray<bool> _active;
    private NativeArray<float> _hp;
    private List<float> _flashTimers;
    private List<Vector3> _positionList;
    private List<Quaternion> _rotationList;
    private List<bool> _activeList;
    private NativeParallelMultiHashMap<int, int> _spatialMap;
    private NativeQueue<float3> _deadPositions;
    private NativeQueue<EnemyDamageInfo> _damageQueue;
    private NativeQueue<int> _flashQueue;

    /// <summary>実際に出現させる敵の数。</summary>
    public int SpawnCount => _spawnCount;
    /// <summary>空間マップ（弾衝突Job用）。</summary>
    public NativeParallelMultiHashMap<int, int> SpatialMap => _spatialMap;
    /// <summary>敵座標（弾衝突Job用）。</summary>
    public NativeArray<float3> EnemyPositions => _positions;
    /// <summary>敵アクティブフラグ（弾衝突Job用）。</summary>
    public NativeArray<bool> EnemyActive => _active;
    /// <summary>敵HP（弾衝突Job用）。</summary>
    public NativeArray<float> EnemyHp => _hp;
    /// <summary>弾との当たり判定に使う半径（弾衝突Job用）。</summary>
    public float CollisionRadius => _collisionRadius;
    /// <summary>セルサイズ（弾衝突Job用）。</summary>
    public float CellSize => _cellSize;
    /// <summary>表示用メッシュ。</summary>
    public Mesh Mesh => _mesh;
    /// <summary>表示用マテリアル。</summary>
    public Material Material => _material;
    /// <summary>表示用スケール。</summary>
    public Vector3 Scale => _scale;

    /// <summary>
    /// 敵データと共通パラメータでグループを生成する。バッファを確保し初期配置を行う。
    /// </summary>
    /// <param name="data">敵データ（null 不可）。</param>
    /// <param name="maxCount">このグループの最大数（配列サイズ）。</param>
    /// <param name="flashDuration">ヒットフラッシュ表示時間。</param>
    /// <param name="respawnDistance">リスポーン判定距離。</param>
    /// <param name="respawnMinRadius">リスポーン最小半径。</param>
    /// <param name="respawnMaxRadius">リスポーン最大半径。</param>
    public EnemyGroup(
        EnemyData data,
        int maxCount,
        float flashDuration,
        float respawnDistance,
        float respawnMinRadius,
        float respawnMaxRadius)
    {
        if (data == null)
        {
            Debug.LogWarning("EnemyGroup: EnemyData is null. Using default values.");
            _maxCount = Mathf.Max(1, maxCount);
            _spawnCount = Mathf.Clamp(10, 1, _maxCount);
            _speed = 4f;
            _maxHp = 1f;
            _flashDuration = flashDuration;
            _damageRadius = 1f;
            _collisionRadius = 1f;
            _respawnDistance = respawnDistance;
            _respawnMinRadius = respawnMinRadius;
            _respawnMaxRadius = respawnMaxRadius;
            _mesh = null;
            _material = null;
            _scale = Vector3.one;
        }
        else
        {
            _maxCount = Mathf.Max(1, maxCount);
            _spawnCount = data.SpawnCount > 0 ? Mathf.Clamp(data.SpawnCount, 1, _maxCount) : Mathf.Clamp(10, 1, _maxCount);
            _speed = data.Speed;
            _maxHp = data.MaxHp;
            _flashDuration = flashDuration;
            _damageRadius = data.DamageRadius;
            _collisionRadius = data.CollisionRadius;
            _respawnDistance = respawnDistance;
            _respawnMinRadius = respawnMinRadius;
            _respawnMaxRadius = respawnMaxRadius;
            _mesh = data.Mesh;
            _material = data.Material;
            _scale = data.Scale;
        }

        // 空間分割のセルサイズは当たり半径から算出（R < 2*cellSize を満たす）
        _cellSize = Mathf.Max(0.5f, _collisionRadius * 0.51f);

        _positions = new NativeArray<float3>(_maxCount, Allocator.Persistent);
        _rotations = new NativeArray<quaternion>(_maxCount, Allocator.Persistent);
        _active = new NativeArray<bool>(_maxCount, Allocator.Persistent);
        _hp = new NativeArray<float>(_maxCount, Allocator.Persistent);
        _flashTimers = new List<float>(_maxCount);
        _positionList = new List<Vector3>(_maxCount);
        _rotationList = new List<Quaternion>(_maxCount);
        _activeList = new List<bool>(_maxCount);

        for (int i = 0; i < _maxCount; i++)
        {
            bool active = i < _spawnCount;
            _active[i] = active;
            if (active)
            {
                var pos = (float3)UnityEngine.Random.insideUnitSphere * 40f;
                pos.y = 0;
                _positions[i] = pos;
                _rotations[i] = quaternion.identity;
                _hp[i] = _maxHp;
            }
            _flashTimers.Add(0f);
            _positionList.Add(Vector3.zero);
            _rotationList.Add(Quaternion.identity);
            _activeList.Add(false);
        }

        _spatialMap = new NativeParallelMultiHashMap<int, int>(_maxCount, Allocator.Persistent);
        _deadPositions = new NativeQueue<float3>(Allocator.Persistent);
        _damageQueue = new NativeQueue<EnemyDamageInfo>(Allocator.Persistent);
        _flashQueue = new NativeQueue<int>(Allocator.Persistent);
    }

    /// <summary>確保したバッファを破棄する。二重呼び出し防止は呼び出し側で行う。</summary>
    public void Dispose()
    {
        if (_positions.IsCreated) _positions.Dispose();
        if (_rotations.IsCreated) _rotations.Dispose();
        if (_active.IsCreated) _active.Dispose();
        if (_hp.IsCreated) _hp.Dispose();
        if (_spatialMap.IsCreated) _spatialMap.Dispose();
        if (_deadPositions.IsCreated) _deadPositions.Dispose();
        if (_damageQueue.IsCreated) _damageQueue.Dispose();
        if (_flashQueue.IsCreated) _flashQueue.Dispose();
    }

    /// <summary>このグループの敵移動Jobをスケジュールする。複数グループ時は前のグループの Job を dependsOn に渡すこと（同一 playerDamageQueue への書き込み競合を防ぐ）。</summary>
    public JobHandle ScheduleEnemyMoveJob(float deltaTime, float3 playerPos, NativeQueue<int>.ParallelWriter playerDamageQueue, JobHandle dependsOn = default)
    {
        if (_spatialMap.IsCreated)
            _spatialMap.Clear();
        else
            _spatialMap = new NativeParallelMultiHashMap<int, int>(_maxCount, Allocator.Persistent);

        var job = new EnemyMoveAndHashJob
        {
            deltaTime = deltaTime,
            target = playerPos,
            speed = _speed,
            cellSize = _cellSize,
            damageRadius = _damageRadius,
            spatialMap = _spatialMap.AsParallelWriter(),
            positions = _positions,
            rotations = _rotations,
            activeFlags = _active,
            damageQueue = playerDamageQueue
        };
        return job.Schedule(_spawnCount, 64, dependsOn);
    }

    /// <summary>死んだ敵の位置をキューから取り出しジェム生成に渡す。</summary>
    public void ProcessDeadEnemies(GemManager gemManager)
    {
        while (_deadPositions.TryDequeue(out float3 position))
        {
            gemManager?.SpawnGem(position);
        }
    }

    /// <summary>ダメージキューとフラッシュキューを処理し、ダメージ表示とヒットフラッシュを適用する。</summary>
    public void ProcessEnemyDamage(DamageTextManager damageTextManager)
    {
        while (_damageQueue.TryDequeue(out EnemyDamageInfo damageInfo))
        {
            int damageInt = (int)damageInfo.damage;
            if (damageInt > 0)
                damageTextManager?.ShowDamage(damageInfo.position, damageInt);
        }
        while (_flashQueue.TryDequeue(out int enemyIndex))
        {
            if (enemyIndex >= 0 && enemyIndex < _spawnCount && enemyIndex < _flashTimers.Count)
                _flashTimers[enemyIndex] = _flashDuration;
        }
    }

    /// <summary>プレイヤー位置を元にリスポーン処理を行う。</summary>
    public void HandleRespawn(float3 playerPos)
    {
        float deleteDistSq = _respawnDistance * _respawnDistance;
        for (int i = 0; i < _spawnCount; i++)
        {
            if (_active[i] == false || math.distancesq(_positions[i], playerPos) > deleteDistSq)
            {
                float angle = UnityEngine.Random.Range(0f, math.PI * 2f);
                float dist = UnityEngine.Random.Range(_respawnMinRadius, _respawnMaxRadius);
                float3 offset = new float3(math.cos(angle) * dist, 0f, math.sin(angle) * dist);
                float3 newPos = playerPos + offset;
                _positions[i] = newPos;
                _active[i] = true;
                _hp[i] = _maxHp;
                _flashTimers[i] = 0f;
            }
        }
    }

    /// <summary>フラッシュタイマーを更新し、Native から List へコピーする（描画用）。</summary>
    public void UpdateFlashTimers()
    {
        for (int i = 0; i < _spawnCount; i++)
        {
            if (i < _flashTimers.Count && _flashTimers[i] > 0)
                _flashTimers[i] -= Time.deltaTime;
        }
        Assert.IsTrue(_maxCount == _activeList.Count);
        for (int i = 0; i < _spawnCount; i++)
        {
            _activeList[i] = _active[i];
            _positionList[i] = _positions[i];
            var q = _rotations[i];
            _rotationList[i] = new Quaternion(q.value.x, q.value.y, q.value.z, q.value.w);
        }
    }

    /// <summary>このグループの敵を RenderManager で描画する（SetEnemyDisplay + RenderEnemies）。</summary>
    public void Render(RenderManager renderManager)
    {
        if (renderManager == null) return;
        renderManager.SetEnemyDisplay(_mesh, _material, _scale);
        renderManager.RenderEnemies(_positionList, _rotationList, _flashTimers, _activeList, _spawnCount);
    }

    /// <summary>このグループの敵をすべて非表示（非アクティブ）にする。</summary>
    public void ClearAllEnemies()
    {
        for (int i = 0; i < _spawnCount; i++)
        {
            _active[i] = false;
            _activeList[i] = false;
        }
    }

    /// <summary>このグループの敵を初期配置にリセットする。</summary>
    public void ResetEnemies()
    {
        for (int i = 0; i < _spawnCount; i++)
        {
            var pos = (float3)UnityEngine.Random.insideUnitSphere * 40f;
            pos.y = 0;
            _positions[i] = pos;
            _active[i] = true;
            _hp[i] = _maxHp;
        }
        for (int i = _spawnCount; i < _maxCount; i++)
            _active[i] = false;
        for (int i = 0; i < _spawnCount && i < _flashTimers.Count; i++)
            _flashTimers[i] = 0f;
        while (_deadPositions.TryDequeue(out _)) { }
        while (_damageQueue.TryDequeue(out _)) { }
        while (_flashQueue.TryDequeue(out _)) { }
    }

    /// <summary>死んだ敵の位置キュー（弾衝突Job用）。</summary>
    public NativeQueue<float3>.ParallelWriter GetDeadEnemyPositionsWriter() => _deadPositions.AsParallelWriter();
    /// <summary>敵ダメージ情報キュー（弾衝突Job用）。</summary>
    public NativeQueue<EnemyDamageInfo>.ParallelWriter GetEnemyDamageQueueWriter() => _damageQueue.AsParallelWriter();
    /// <summary>敵フラッシュキュー（弾衝突Job用）。</summary>
    public NativeQueue<int>.ParallelWriter GetEnemyFlashQueueWriter() => _flashQueue.AsParallelWriter();
}
