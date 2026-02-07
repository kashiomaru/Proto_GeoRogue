using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using System.Collections.Generic;

public class RenderManager : InitializeMonobehaviour
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

    [Header("Player Bullet Settings")]
    [SerializeField] private Mesh playerBulletMesh;
    [SerializeField] private Material playerBulletMaterial;

    [Header("Enemy Bullet Settings")]
    [SerializeField] private Mesh enemyBulletMesh;
    [SerializeField] private Material enemyBulletMaterial;

    private const int BATCH_SIZE = 1023;

    private NativeArray<Matrix4x4> _matrices;
    private Vector4[] _emissionColors = new Vector4[BATCH_SIZE];

    private MaterialPropertyBlock _mpb;
    private int _propertyID_EmissionColor;

    // 毎フレーム new を避けるためキャッシュ（敵は mat / matProps を毎回設定、弾は mat を毎回設定）
    private RenderParams _rpEnemy;
    private RenderParams _rpGem;
    private RenderParams _rpBullet;

    protected override void InitializeInternal()
    {
        _matrices = new NativeArray<Matrix4x4>(BATCH_SIZE, Allocator.Persistent);
        _mpb = new MaterialPropertyBlock();
        _propertyID_EmissionColor = Shader.PropertyToID("_EmissionColor");

        if (gemMaterial != null)
        {
            _rpGem = new RenderParams(gemMaterial)
            {
                shadowCastingMode = ShadowCastingMode.On,
                receiveShadows = true
            };
        }
    }

    protected override void FinalizeInternal()
    {
        if (_matrices.IsCreated)
        {
            _matrices.Dispose();
        }
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

    /// <summary>ヒットフラッシュの Emission 強度。敵の Matrix Job で使用する。</summary>
    public float FlashIntensity => flashIntensity;

    /// <summary>
    /// 敵を Job で詰めた Matrix4x4 と Emission 配列で描画する。
    /// SetEnemyDisplay で設定した Mesh/Material を使用。count が BATCH_SIZE（1023）を超える場合は警告し、先頭 1023 件のみ描画する。
    /// </summary>
    public void RenderEnemies(NativeArray<Matrix4x4> matrices, NativeArray<Vector4> emissionColors, int count)
    {
        Mesh mesh = _runtimeEnemyMesh;
        Material mat = _runtimeEnemyMaterial;
        if (mesh == null || mat == null || count <= 0) return;

        if (count > BATCH_SIZE)
        {
            Debug.LogWarning($"[RenderManager] 敵の描画数が {BATCH_SIZE} を超えています。先頭 {BATCH_SIZE} 件のみ描画します。");
            count = BATCH_SIZE;
        }
        for (int i = 0; i < count; i++)
        {
            _matrices[i] = matrices[i];
            _emissionColors[i] = emissionColors[i];
        }
        ExecuteDrawEnemies(mesh, mat, count);
    }

    private void ExecuteDrawEnemies(Mesh mesh, Material mat, int count)
    {
        _mpb.SetVectorArray(_propertyID_EmissionColor, _emissionColors);

        if (_rpEnemy.material == null)
        {
            _rpEnemy = new RenderParams(mat)
            {
                matProps = _mpb,
                shadowCastingMode = ShadowCastingMode.On,
                receiveShadows = true
            };
        }
        else
        {
            _rpEnemy.material = mat;
            _rpEnemy.matProps = _mpb;
        }
        Graphics.RenderMeshInstanced(_rpEnemy, mesh, 0, _matrices, count);
    }


    /// <summary>
    /// ジェムを Job で詰めた Matrix4x4 配列で描画する。配列は詰まっており、先頭 count 件を描画する。
    /// count が BATCH_SIZE（1023）を超える場合は警告を出し、先頭 1023 件のみ描画する。
    /// </summary>
    public void RenderGems(NativeArray<Matrix4x4> matrices, int count)
    {
        if (gemMesh == null || gemMaterial == null || count <= 0)
            return;

        if (count > BATCH_SIZE)
        {
            Debug.LogWarning($"[RenderManager] ジェムの描画数が {BATCH_SIZE} を超えています。先頭 {BATCH_SIZE} 件のみ描画します。");
            count = BATCH_SIZE;
        }
        Graphics.RenderMeshInstanced(_rpGem, gemMesh, 0, matrices, count);
    }

    /// <summary>
    /// プレイヤー弾を Job で詰めた Matrix4x4 配列で描画する。count が BATCH_SIZE（1023）を超える場合は警告し、先頭 1023 件のみ描画する。
    /// </summary>
    public void RenderPlayerBullets(NativeArray<Matrix4x4> matrices, int count)
    {
        RenderBulletsInternal(playerBulletMesh, playerBulletMaterial, matrices, count);
    }

    /// <summary>
    /// 敵弾を Job で詰めた Matrix4x4 配列で描画する。count が BATCH_SIZE（1023）を超える場合は警告し、先頭 1023 件のみ描画する。
    /// </summary>
    public void RenderEnemyBullets(NativeArray<Matrix4x4> matrices, int count)
    {
        RenderBulletsInternal(enemyBulletMesh, enemyBulletMaterial, matrices, count);
    }

    private void RenderBulletsInternal(Mesh mesh, Material mat, NativeArray<Matrix4x4> matrices, int count)
    {
        if (mesh == null || mat == null || count <= 0)
            return;

        if (count > BATCH_SIZE)
        {
            Debug.LogWarning($"[RenderManager] 弾の描画数が {BATCH_SIZE} を超えています。先頭 {BATCH_SIZE} 件のみ描画します。");
            count = BATCH_SIZE;
        }

        if (_rpBullet.material == null)
        {
            _rpBullet = new RenderParams(mat)
            {
                shadowCastingMode = ShadowCastingMode.On,
                receiveShadows = true
            };
        }
        else
        {
            _rpBullet.material = mat;
        }
        Graphics.RenderMeshInstanced(_rpBullet, mesh, 0, matrices, count);
    }
}
