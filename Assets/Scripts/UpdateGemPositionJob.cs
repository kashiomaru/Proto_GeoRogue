using UnityEngine.Jobs;
using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;

[BurstCompile]
public struct UpdateGemPositionJob : IJobParallelForTransform
{
    [ReadOnly] public NativeArray<float3> positions;
    [ReadOnly] public NativeArray<bool> activeFlags;

    public void Execute(int index, TransformAccess transform)
    {
        // アクティブなジェムのみ位置を更新
        if (activeFlags[index])
        {
            transform.position = positions[index];
        }
        else
        {
            // 非アクティブなジェムは画面外へ
            transform.position = new float3(0, -500, 0);
        }
    }
}
