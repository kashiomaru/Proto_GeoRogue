using UnityEngine;
using System.Collections.Generic;

public class RenderManager : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private Mesh enemyMesh;
    [SerializeField] private Material enemyMaterial;
    [SerializeField] private float flashIntensity = 0.8f;

    private const int BATCH_SIZE = 1023;

    private Matrix4x4[] _matrices = new Matrix4x4[BATCH_SIZE];
    private Vector4[] _emissionColors = new Vector4[BATCH_SIZE];

    private MaterialPropertyBlock _mpb;
    private int _propertyID_EmissionColor;

    void Start()
    {
        _mpb = new MaterialPropertyBlock();
        _propertyID_EmissionColor = Shader.PropertyToID("_EmissionColor");
    }

    /// <summary>
    /// 敵を座標リストで描画（Transform なし）。位置・回転(identity)・スケール(1)で行列を生成。
    /// </summary>
    public void RenderEnemies(IList<Vector3> positions, IList<float> flashTimers, IList<bool> activeFlags)
    {
        int count = positions.Count;
        int batchIndex = 0;

        for (int i = 0; i < count; i++)
        {
            if (activeFlags[i] == false)
            {
                continue;
            }

            _matrices[batchIndex] = Matrix4x4.TRS(
                positions[i],
                Quaternion.identity,
                Vector3.one
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
