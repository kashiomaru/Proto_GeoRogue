using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;

public class RenderManager : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private Mesh enemyMesh;
    [SerializeField] private Material enemyMaterial;
    [Serializefield] private float flashIntensity = 0.8f;

    // 一度に描画できる最大数（API制限）
    private const int BATCH_SIZE = 1023;

    // 描画用データ配列（再利用する）
    private Matrix4x4[] _matrices = new Matrix4x4[BATCH_SIZE];
    private Vector4[] _emissionColors = new Vector4[BATCH_SIZE]; // ヒットフラッシュ用

    private MaterialPropertyBlock _mpb;
    private int _propertyID_EmissionColor;

    void Start()
    {
        _mpb = new MaterialPropertyBlock();
        _propertyID_EmissionColor = Shader.PropertyToID("_EmissionColor");
    }

    // 毎フレームGameManagerから呼ばれる描画メソッド
    // transforms: 敵のTransformリスト
    // flashTimers: 各敵のフラッシュ残り時間（0なら通常、>0なら白）
    // activeFlags: 各敵のアクティブフラグ
    public void RenderEnemies(List<Transform> transforms, List<float> flashTimers, List<bool> activeFlags)
    {
        int count = transforms.Count;
        int batchIndex = 0; // 現在のバッチ内のインデックス

        // 1023個ずつに区切って描画
        for (int i = 0; i < count; i++)
        {
            if (!activeFlags[i]) continue; // 死んでる敵はスキップ

            // 1. 行列（位置・回転・スケール）を作成
            // ※ここが最適化ポイント：回転やスケールが変わらないなら固定値で高速化可
            _matrices[batchIndex] = Matrix4x4.TRS(
                transforms[i].position, 
                transforms[i].rotation, 
                transforms[i].localScale // ヒット時に少し大きくする演出もここで可
            );

            // 2. フラッシュ色の計算
            // タイマーが残っていれば「白」、なければ「元の色（黒）」
            if (flashTimers[i] > 0f)
            {
                // 強烈な白（HDR）
                _emissionColors[batchIndex] = new Vector4(flashIntensity, flashIntensity, flashIntensity, 1f); 
            }
            else
            {
                // 通常色（黒、またはマテリアルの設定色）
                _emissionColors[batchIndex] = Vector4.zero;
            }

            batchIndex++;

            // バッチが満タン、または最後の要素なら描画実行
            if (batchIndex >= BATCH_SIZE || i == count - 1)
            {
                if (batchIndex > 0)
                {
                    ExecuteDraw(batchIndex);
                }
                batchIndex = 0;
            }
        }
    }

    private void ExecuteDraw(int count)
    {
        // プロパティブロックに配列をセット
        _mpb.SetVectorArray(_propertyID_EmissionColor, _emissionColors);

        // 描画発行
        // count引数には「実際に配列に入れた数」を渡す（配列全部ではない）
        Graphics.DrawMeshInstanced(
            enemyMesh, 
            0, 
            enemyMaterial, 
            _matrices, 
            count, 
            _mpb, 
            UnityEngine.Rendering.ShadowCastingMode.On, 
            true // Receive Shadows
        );
    }
}
