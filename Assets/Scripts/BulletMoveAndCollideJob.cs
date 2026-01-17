using UnityEngine.Jobs;
using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;

[BurstCompile]
public struct BulletMoveAndCollideJob : IJobParallelForTransform
{
    public float deltaTime;
    public float speed;
    public float cellSize;

    [ReadOnly] public NativeParallelMultiHashMap<int, int> spatialMap;
    [ReadOnly] public NativeArray<float3> enemyPositions;
    
    public NativeArray<float3> bulletPositions;
    [ReadOnly] public NativeArray<float3> bulletDirections; // 弾の方向ベクトル
    public NativeArray<bool> bulletActive;
    public NativeArray<float> bulletLifeTime;
    
    // 敵を殺すために書き込み権限が必要（競合注意）
    // NativeArray<bool>への並列書き込みは、異なるインデックスなら安全です。
    // 「同じ敵に複数の弾が同時に当たった」場合も、両方がfalseを書き込むだけなので問題なし。
    [NativeDisableParallelForRestriction] 
    public NativeArray<bool> enemyActive;

    public void Execute(int index, TransformAccess transform)
    {
        if (!bulletActive[index]) return;

        float life = bulletLifeTime[index] - deltaTime;
        bulletLifeTime[index] = life;

        if (life <= 0)
        {
            bulletActive[index] = false;
            transform.position = new float3(0, -100, 0);
            return;
        }

        // 弾の移動（プレイヤーの向きに飛ばす）
        float3 pos = bulletPositions[index];
        // 方向ベクトルを使用して移動
        float3 velocity = bulletDirections[index] * speed; 
        
        pos += velocity * deltaTime;
        transform.position = pos;
        bulletPositions[index] = pos;

        // --- 衝突判定（空間分割） ---
        int2 gridCoords = new int2((int)math.floor(pos.x / cellSize), (int)math.floor(pos.z / cellSize));

        // 自分のセルと、隣接するセル（最大9セル）をチェック
        // 弾がセルの境界にいる場合、隣の敵に当たる可能性があるため
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                int2 neighborCoords = gridCoords + new int2(x, y);
                int hash = (int)math.hash(neighborCoords);

                // ハッシュマップから敵を取得
                if (spatialMap.TryGetFirstValue(hash, out int enemyIndex, out var iterator))
                {
                    do
                    {
                        // 死んでる敵は無視（マップには入っている可能性があるため念のため）
                        if (!enemyActive[enemyIndex]) continue;

                        float3 enemyPos = enemyPositions[enemyIndex];
                        float distSq = math.distancesq(pos, enemyPos);

                        // 当たり判定（半径1.0同士とする）
                        if (distSq < 1.0f)
                        {
                            // ヒット！
                            enemyActive[enemyIndex] = false; // 敵死亡
                            bulletActive[index] = false;     // 弾消滅
                            transform.position = new float3(0, -100, 0); // 弾隠す
                            return; // 弾は1回当たったら消える
                        }

                    } while (spatialMap.TryGetNextValue(out enemyIndex, ref iterator));
                }
            }
        }
    }
}
