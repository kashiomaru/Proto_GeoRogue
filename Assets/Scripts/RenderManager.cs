using UnityEngine;
using System.Collections.Generic;

public class RenderManager : MonoBehaviour
{
    [Header("Enemy Settings")]
    // Mesh/Material はステージデータで SetEnemyDisplay により設定（未設定時は null で描画スキップ）
    [SerializeField] private float flashIntensity = 0.8f;

    // ステージ適用用（SetEnemyDisplay で設定）
    private Mesh _runtimeEnemyMesh;
    private Material _runtimeEnemyMaterial;
    private Vector3 _runtimeEnemyScale = Vector3.one;

    [Header("Gem Settings")]
    [SerializeField] private Mesh gemMesh;
    [SerializeField] private Material gemMaterial;
    [SerializeField] private float gemScale = 0.4f;

    [Header("Bullet Settings")]
    [SerializeField] private Mesh bulletMesh;
    [SerializeField] private Material bulletMaterial;
    [SerializeField] private float bulletScale = 0.5f;

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
    /// 通常敵の表示設定をステージから適用する。null の項目は既存の値を維持する。
    /// </summary>
    public void SetEnemyDisplay(Mesh mesh, Material material, Vector3 scale)
    {
        _runtimeEnemyMesh = mesh;
        _runtimeEnemyMaterial = material;
        _runtimeEnemyScale = scale;
    }

    /// <summary>
    /// 敵を座標・回転リストで描画。回転は Job で計算済み（プレイヤー方向）。
    /// </summary>
    /// <param name="count">描画する敵の数。省略時は positions.Count を使用。</param>
    public void RenderEnemies(IList<Vector3> positions, IList<Quaternion> rotations, IList<float> flashTimers, IList<bool> activeFlags, int? count = null)
    {
        Mesh mesh = _runtimeEnemyMesh;
        Material mat = _runtimeEnemyMaterial;
        if (mesh == null || mat == null) return;

        Vector3 scale = _runtimeEnemyScale;
        int drawCount = count ?? positions.Count;
        int batchIndex = 0;

        for (int i = 0; i < drawCount; i++)
        {
            if (activeFlags[i] == false)
            {
                continue;
            }

            _matrices[batchIndex] = Matrix4x4.TRS(
                positions[i],
                rotations[i],
                scale
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

            // バッチが満タンなら描画実行
            if (batchIndex >= BATCH_SIZE)
            {
                ExecuteDrawEnemies(mesh, mat, batchIndex);
                batchIndex = 0;
            }
        }

        if (batchIndex > 0)
        {
            ExecuteDrawEnemies(mesh, mat, batchIndex);
        }
    }

    private void ExecuteDrawEnemies(Mesh mesh, Material mat, int count)
    {
        // プロパティブロックに配列をセット
        _mpb.SetVectorArray(_propertyID_EmissionColor, _emissionColors);

        // 描画発行
        // count引数には「実際に配列に入れた数」を渡す（配列全部ではない）
        Graphics.DrawMeshInstanced(
            mesh,
            0,
            mat,
            _matrices,
            count,
            _mpb,
            UnityEngine.Rendering.ShadowCastingMode.On,
            true // Receive Shadows
        );
    }


    /// <summary>
    /// ジェムを座標・アクティブリストで描画（敵と同様に DrawMeshInstanced、Instantiate なし）。
    /// </summary>
    public void RenderGems(IList<Vector3> positions, IList<bool> activeFlags)
    {
        if (gemMesh == null || gemMaterial == null)
        {
            return;
        }

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
                Vector3.one * gemScale
            );
            batchIndex++;

            if (batchIndex >= BATCH_SIZE)
            {
                ExecuteDrawGems(batchIndex);

                batchIndex = 0;
            }
        }

        if (batchIndex > 0)
        {
            ExecuteDrawGems(batchIndex);
        }
    }

    /// <summary>
    /// 弾を座標・回転・アクティブリストで描画（敵・ジェムと同様に DrawMeshInstanced、Instantiate なし）。
    /// </summary>
    public void RenderBullets(IList<Vector3> positions, IList<Quaternion> rotations, IList<bool> activeFlags)
    {
        if (bulletMesh == null || bulletMaterial == null)
        {
            return;
        }

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
                rotations[i],
                Vector3.one * bulletScale
            );
            batchIndex++;

            if (batchIndex >= BATCH_SIZE)
            {
                Graphics.DrawMeshInstanced(
                    bulletMesh,
                    0,
                    bulletMaterial,
                    _matrices,
                    batchIndex,
                    null,
                    UnityEngine.Rendering.ShadowCastingMode.On,
                    true
                );
                batchIndex = 0;
            }
        }

        if (batchIndex > 0)
        {
            Graphics.DrawMeshInstanced(
                bulletMesh,
                0,
                bulletMaterial,
                _matrices,
                batchIndex,
                null,
                UnityEngine.Rendering.ShadowCastingMode.On,
                true
            );
        }
    }

    private void ExecuteDrawGems(int count)
    {
        Graphics.DrawMeshInstanced(
            gemMesh,
            0,
            gemMaterial,
            _matrices,
            count,
            null,
            UnityEngine.Rendering.ShadowCastingMode.On,
            true
        );
    }
}
