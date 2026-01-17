using UnityEngine;
using UnityEngine.Jobs; // TransformAccessArray用
using Unity.Collections; // NativeArray用
using Unity.Burst; // Burst用
using Unity.Mathematics; // float3, math用

public class CubeMover : MonoBehaviour
{
    [SerializeField] private GameObject cubePrefab;
    [SerializeField] private int objectCount = 3000;
    [SerializeField] private float speed = 5f;
    [SerializeField] private Vector3 targetPosition = Vector3.zero;

    private TransformAccessArray _transformAccessArray;
    private NativeArray<float3> _velocities; // 拡張用：個別の速度を持たせたい場合など

    void Start()
    {
        // 配列初期化
        _transformAccessArray = new TransformAccessArray(objectCount);
        
        // 3000個生成
        for (int i = 0; i < objectCount; i++)
        {
            // ランダムな位置に配置
            Vector3 randomPos = UnityEngine.Random.insideUnitSphere * 50f;
            randomPos.y = 0; // 平面にするなら
            GameObject obj = Instantiate(cubePrefab, randomPos, Quaternion.identity);
            
            // Transformを登録
            _transformAccessArray.Add(obj.transform);
        }
    }

    void Update()
    {
        // Jobの作成
        var moveJob = new MoveJob
        {
            deltaTime = Time.deltaTime,
            target = targetPosition,
            moveSpeed = speed
        };

        // Jobのスケジュール（IJobParallelForTransform）
        // batchSizeは32~64あたりが適当
        var handle = moveJob.Schedule(_transformAccessArray);

        // 即時完了待ち（プロトタイプならこれでOK。本来はLateUpdateで待つなど非同期推奨）
        handle.Complete();
    }

    void OnDestroy()
    {
        // 【重要】Nativeコンテナは手動解放しないとメモリリークします
        if (_transformAccessArray.isCreated) _transformAccessArray.Dispose();
        if (_velocities.IsCreated) _velocities.Dispose();
    }
}

// Burstコンパイルを有効化
[BurstCompile]
public struct MoveJob : IJobParallelForTransform
{
    public float deltaTime;
    public float3 target;
    public float moveSpeed;

    public void Execute(int index, TransformAccess transform)
    {
        // 現在位置を取得
        float3 currentPos = transform.position;

        // ターゲットへの方向ベクトル
        float3 direction = math.normalize(target - currentPos);

        // 移動
        float3 newPos = currentPos + (direction * moveSpeed * deltaTime);

        // 座標更新
        transform.position = newPos;

        // 向きを変える（オプション）
        // transform.rotation = quaternion.LookRotation(direction, math.up());
    }
}
