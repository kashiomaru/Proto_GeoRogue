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
    [ReadOnly] public NativeArray<float3> bulletDirections; // 弾の方向ベクトル（後方互換性のため）
    [ReadOnly] public NativeArray<float3> bulletVelocities; // 弾の速度ベクトル
    public NativeArray<bool> bulletActive;
    public NativeArray<float> bulletLifeTime;
    
    // 敵を殺すために書き込み権限が必要（競合注意）
    // NativeArray<bool>への並列書き込みは、異なるインデックスなら安全です。
    // 「同じ敵に複数の弾が同時に当たった」場合も、両方がfalseを書き込むだけなので問題なし。
    [NativeDisableParallelForRestriction] 
    public NativeArray<bool> enemyActive;
    
    // 敵のHP配列（並列書き込み可能）
    [NativeDisableParallelForRestriction]
    public NativeArray<float> enemyHp;
    
    // 弾のダメージ
    public float bulletDamage;
    
    // 死んだ敵の位置を記録するキュー（並列書き込み用）
    public NativeQueue<float3>.ParallelWriter deadEnemyPositions;
    
    // 敵へのダメージ情報を記録するキュー（並列書き込み用）
    public NativeQueue<EnemyDamageInfo>.ParallelWriter enemyDamageQueue;
    
    // フラッシュタイマー設定用のキュー（並列書き込み用）
    public NativeQueue<int>.ParallelWriter enemyFlashQueue;

    public void Execute(int index, TransformAccess transform)
    {
        if (!bulletActive[index])
        {
            // 非アクティブな弾は画面外に移動
            transform.position = new float3(0, -100, 0);
            bulletPositions[index] = new float3(0, -100, 0);
            return;
        }

        float life = bulletLifeTime[index] - deltaTime;
        bulletLifeTime[index] = life;

        if (life <= 0)
        {
            bulletActive[index] = false;
            transform.position = new float3(0, -100, 0);
            return;
        }

        // 弾の移動（速度ベクトルを使用）
        float3 pos = bulletPositions[index];
        // 速度ベクトルを使用して移動（MultiShot対応）
        float3 velocity = bulletVelocities[index];
        
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
                            // 敵がまだ生きている場合のみ処理
                            if (enemyActive[enemyIndex])
                            {
                                // HPを減らす
                                float currentHp = enemyHp[enemyIndex];
                                currentHp -= bulletDamage;
                                enemyHp[enemyIndex] = currentHp;
                                
                                // ダメージ情報をキューに追加（ダメージテキスト表示用）
                                enemyDamageQueue.Enqueue(new EnemyDamageInfo(enemyPos, bulletDamage));
                                
                                // フラッシュタイマーを設定（ヒットフラッシュ用）
                                enemyFlashQueue.Enqueue(enemyIndex);
                                
                                // HPが0以下になったら敵を死亡させる
                                if (currentHp <= 0f)
                                {
                                    enemyActive[enemyIndex] = false; // 敵死亡
                                    // 死んだ敵の位置をキューに追加
                                    deadEnemyPositions.Enqueue(enemyPos);
                                }
                            }
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
