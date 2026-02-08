using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// プレイヤー位置を元にリスポーン処理を行う Job。
/// 非アクティブまたは削除距離外の敵を、プレイヤー周りのドーナツ状に再配置する。
/// Unity.Mathematics.Random でインデックス＋seed から乱数を生成（Burst 対応）。
/// </summary>
[BurstCompile]
public struct EnemyRespawnJob : IJobParallelFor
{
    public float3 playerPos;
    public float deleteDistSq;
    public float respawnMinRadius;
    public float respawnMaxRadius;
    public int maxHp;
    /// <summary>0 以下なら発射タイマーは 0。正なら 0～この値の乱数で初期化。</summary>
    public float fireIntervalMax;
    public uint seed;

    public NativeArray<float3> positions;
    public NativeArray<float3> directions;
    public NativeArray<bool> active;
    public NativeArray<int> hp;
    public NativeArray<float> fireTimers;
    public NativeArray<float> flashTimers;

    public void Execute(int index)
    {
        if (active[index] && math.distancesq(positions[index], playerPos) <= deleteDistSq)
            return;

        var rng = Random.CreateFromIndex((uint)index + seed);
        float angle = rng.NextFloat(0f, math.PI * 2f);
        float dist = rng.NextFloat(respawnMinRadius, respawnMaxRadius);
        float3 offset = new float3(math.cos(angle) * dist, 0f, math.sin(angle) * dist);

        positions[index] = playerPos + offset;
        directions[index] = new float3(0f, 0f, 1f);
        active[index] = true;
        hp[index] = maxHp;
        fireTimers[index] = fireIntervalMax > 0f ? rng.NextFloat(0f, fireIntervalMax) : 0f;
        flashTimers[index] = 0f;
    }
}
