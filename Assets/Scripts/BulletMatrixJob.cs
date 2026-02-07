using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// アクティブな弾の座標・方向を詰めた Matrix4x4 配列と描画数を出力する。
/// 描画用に RenderManager へ直接渡すためのバッファを埋める。
/// </summary>
public struct BulletMatrixJob : IJob
{
    [ReadOnly] public NativeArray<float3> positions;
    [ReadOnly] public NativeArray<float3> directions;
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
            Vector3 dir = (Vector3)directions[i];
            Quaternion rot = dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir) : Quaternion.identity;
            matrices[w] = Matrix4x4.TRS((Vector3)positions[i], rot, scaleVec);
            w++;
        }
        drawCount[0] = w;
    }
}
