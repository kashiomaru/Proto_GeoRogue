using UnityEngine.Jobs;
using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;

[BurstCompile]
public struct UpdateRespawnedEnemyPositionJob : IJobParallelForTransform
{
    [ReadOnly] public NativeArray<float3> positions;
    [ReadOnly] public NativeArray<bool> activeFlags;

    public void Execute(int index, TransformAccess transform)
    {
        // アクティブな敵のみ位置を更新
        if (activeFlags[index])
        {
            transform.position = positions[index];
        }
    }
}
