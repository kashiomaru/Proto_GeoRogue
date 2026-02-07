using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using System.Collections.Generic;

public class RenderManager : InitializeMonobehaviour
{
    [Header("Enemy Settings")]
    [SerializeField] private float flashIntensity = 0.8f;

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

    private Vector4[] _emissionColors = new Vector4[BATCH_SIZE];

    private MaterialPropertyBlock _mpb;
    private int _propertyID_EmissionColor;

    private RenderParams _rpGem;
    private RenderParams _rpBullet;

    protected override void InitializeInternal()
    {
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
    }

    /// <summary>ヒットフラッシュの Emission 強度。敵の Matrix Job で使用する。</summary>
    public float FlashIntensity => flashIntensity;

    /// <summary>
    /// 敵を Job で詰めた Matrix4x4 と Emission 配列で描画する。
    /// rpEnemy と mesh は呼び出し元（EnemyGroup）が保持する RenderParams と Mesh を渡す。
    /// count が BATCH_SIZE（1023）を超える場合は警告し、先頭 1023 件のみ描画する。
    /// </summary>
    public void RenderEnemies(RenderParams rpEnemy, Mesh mesh, NativeArray<Matrix4x4> matrices, NativeArray<Vector4> emissionColors, int count)
    {
        if (mesh == null || rpEnemy.material == null || count <= 0) return;

        if (count > BATCH_SIZE)
        {
            Debug.LogWarning($"[RenderManager] 敵の描画数が {BATCH_SIZE} を超えています。先頭 {BATCH_SIZE} 件のみ描画します。");
            count = BATCH_SIZE;
        }
        for (int i = 0; i < count; i++)
            _emissionColors[i] = emissionColors[i];
        _mpb.SetVectorArray(_propertyID_EmissionColor, _emissionColors);
        rpEnemy.matProps = _mpb;
        Graphics.RenderMeshInstanced(rpEnemy, mesh, 0, matrices, count);
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
