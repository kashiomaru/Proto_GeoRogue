using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// アクティブなジェムの座標を詰めた Matrix4x4 配列と描画数を出力する。
/// 描画用に RenderManager へ直接渡すためのバッファを埋める。
/// </summary>
public struct GemMatrixJob : IJob
{
    [ReadOnly] public NativeArray<float3> positions;
    [ReadOnly] public NativeArray<bool> activeFlags;
    [WriteOnly] public NativeArray<Matrix4x4> matrices;
    [WriteOnly] public NativeArray<int> drawCount;
    public float scale;

    public void Execute()
    {
        int w = 0;
        Vector3 scaleVec = new Vector3(scale, scale, scale);
        for (int i = 0; i < positions.Length; i++)
        {
            if (!activeFlags[i])
                continue;
            matrices[w] = Matrix4x4.TRS((Vector3)positions[i], Quaternion.identity, scaleVec);
            w++;
        }
        drawCount[0] = w;
    }
}
