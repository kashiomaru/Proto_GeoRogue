using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Jobs;

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

    private CopyEmissionToManagedJob _copyEmissionJob;

    private RenderParams _rpGem;
    private RenderParams _rpPlayerBullet;
    private RenderParams _rpEnemyBullet;

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
        if (playerBulletMaterial != null)
        {
            _rpPlayerBullet = new RenderParams(playerBulletMaterial)
            {
                shadowCastingMode = ShadowCastingMode.On,
                receiveShadows = true
            };
        }
        if (enemyBulletMaterial != null)
        {
            _rpEnemyBullet = new RenderParams(enemyBulletMaterial)
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
    public unsafe void RenderEnemies(RenderParams rpEnemy, Mesh mesh, NativeArray<Matrix4x4> matrices, NativeArray<Vector4> emissionColors, int count)
    {
        if (mesh == null || rpEnemy.material == null || count <= 0) return;

        if (count > BATCH_SIZE)
        {
            Debug.LogWarning($"[RenderManager] 敵の描画数が {BATCH_SIZE} を超えています。先頭 {BATCH_SIZE} 件のみ描画します。");
            count = BATCH_SIZE;
        }
        fixed (Vector4* ptr = _emissionColors)
        {
            _copyEmissionJob.emissionColors = emissionColors;
            _copyEmissionJob.targetColors = ptr;
            _copyEmissionJob.Schedule(count, 64).Complete();
        }
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
        RenderBulletsInternal(_rpPlayerBullet, playerBulletMesh, matrices, count);
    }

    /// <summary>
    /// 敵弾を Job で詰めた Matrix4x4 配列で描画する。count が BATCH_SIZE（1023）を超える場合は警告し、先頭 1023 件のみ描画する。
    /// </summary>
    public void RenderEnemyBullets(NativeArray<Matrix4x4> matrices, int count)
    {
        RenderBulletsInternal(_rpEnemyBullet, enemyBulletMesh, matrices, count);
    }

    private void RenderBulletsInternal(RenderParams rp, Mesh mesh, NativeArray<Matrix4x4> matrices, int count)
    {
        if (mesh == null || rp.material == null || count <= 0)
            return;

        if (count > BATCH_SIZE)
        {
            Debug.LogWarning($"[RenderManager] 弾の描画数が {BATCH_SIZE} を超えています。先頭 {BATCH_SIZE} 件のみ描画します。");
            count = BATCH_SIZE;
        }
        Graphics.RenderMeshInstanced(rp, mesh, 0, matrices, count);
    }
}
