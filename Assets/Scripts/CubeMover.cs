using UnityEngine;
using UnityEngine.Jobs;
using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;

public class CubeMover : MonoBehaviour
{
    [SerializeField] private GameObject cubePrefab;
    [SerializeField] private int objectCount = 3000;
    [SerializeField] private float speed = 5f;
    [SerializeField] private float separationDistance = 1.5f; // 敵同士が離れる距離
    [SerializeField] private Transform playerTransform; // プレイヤー（ターゲット）

    private TransformAccessArray _transformAccessArray;
    private NativeArray<float3> _positions; // 敵同士の判定用に位置を保存する配列

    void Start()
    {
        _transformAccessArray = new TransformAccessArray(objectCount);
        _positions = new NativeArray<float3>(objectCount, Allocator.Persistent); // 位置配列の初期化

        for (int i = 0; i < objectCount; i++)
        {
            Vector3 randomPos = UnityEngine.Random.insideUnitSphere * 40f;
            randomPos.y = 0;
            GameObject obj = Instantiate(cubePrefab, randomPos, Quaternion.identity);
            
            // Colliderは削除またはDisableにしておくこと！
            if(obj.TryGetComponent<Collider>(out var col)) col.enabled = false;

            _transformAccessArray.Add(obj.transform);
            _positions[i] = randomPos;
        }
    }

    void Update()
    {
        float3 targetPos = playerTransform != null ? (float3)playerTransform.position : float3.zero;

        // Job作成
        var moveJob = new BoidMoveJob
        {
            deltaTime = Time.deltaTime,
            target = targetPos,
            moveSpeed = speed,
            separationDist = separationDistance,
            // 読み取り専用として渡す（前のフレームの位置情報を参照するため）
            allPositions = _positions.AsReadOnly() 
        };

        // Jobスケジュール
        var handle = moveJob.Schedule(_transformAccessArray);
        handle.Complete();

        // 次のフレームのために現在の位置をNativeArrayにコピーするJobが必要
        // （本来はここも並列化できますが、プロトタイプなので簡易的にMainスレッドでコピー、
        //  あるいはTransformAccessArrayからコピーするJobを別途走らせるのが定石です）
        // 今回はとりあえず簡易更新Jobを追加します。
        
        var updatePosJob = new UpdatePositionArrayJob
        {
            positions = _positions
        };
        updatePosJob.Schedule(_transformAccessArray).Complete();
    }

    void OnDestroy()
    {
        if (_transformAccessArray.isCreated) _transformAccessArray.Dispose();
        if (_positions.IsCreated) _positions.Dispose();
    }
}

[BurstCompile]
public struct BoidMoveJob : IJobParallelForTransform
{
    public float deltaTime;
    public float3 target;
    public float moveSpeed;
    public float separationDist;
    
    [ReadOnly] public NativeArray<float3>.ReadOnly allPositions; // 全敵の位置（重いので注意）

    public void Execute(int index, TransformAccess transform)
    {
        float3 currentPos = transform.position;

        // 1. ターゲットへのベクトル
        float3 toTarget = target - currentPos;
        float3 moveDir = math.normalize(toTarget);

        // 2. 簡易的な「分離（Separation）」
        // 本来は全探索するとO(N^2)で死ぬので、
        // 「自分のIDに近い数個」だけチェックする簡易ロジックで誤魔化すのがヴァンサバ系のコツです。
        // あるいはSpatialHashを使いますが、まずは簡易版で。
        
        float3 separationForce = float3.zero;
        int checkCount = 10; // 自分に近いインデックスの10体だけ気にする
        
        int start = math.max(0, index - checkCount);
        int end = math.min(allPositions.Length, index + checkCount);

        for (int i = start; i < end; i++)
        {
            if (i == index) continue;

            float3 otherPos = allPositions[i];
            float distSq = math.distancesq(currentPos, otherPos);

            // 指定距離より近ければ反発
            if (distSq < separationDist * separationDist && distSq > 0.001f)
            {
                float3 pushDir = currentPos - otherPos;
                separationForce += math.normalize(pushDir) / math.sqrt(distSq); 
            }
        }

        // 合成（ターゲットへ向かう力 + 離れる力）
        float3 finalDir = math.normalize(moveDir + (separationForce * 1.5f));

        // 移動
        transform.position = currentPos + (finalDir * moveSpeed * deltaTime);
        
        // プレイヤーとの当たり判定（距離チェック）
        float distToPlayerSq = math.distancesq(currentPos, target);
        if (distToPlayerSq < 1.0f) // 半径1.0m以内ならヒット
        {
            // ここでヒット処理（本来はNativeQueueなどにイベントを積む）
        }
    }
}

[BurstCompile]
public struct UpdatePositionArrayJob : IJobParallelForTransform
{
    // 次フレームの計算用に、移動後の位置を配列に書き戻す
    [WriteOnly] public NativeArray<float3> positions;

    public void Execute(int index, TransformAccess transform)
    {
        positions[index] = transform.position;
    }
}
