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
    [SerializeField] private float gemScale = 0.4f;

    [Header("Player Bullet Settings")]
    [SerializeField] private Mesh playerBulletMesh;
    [SerializeField] private Material playerBulletMaterial;
    [SerializeField] private float playerBulletScale = 0.5f;

    [Header("Enemy Bullet Settings")]
    [SerializeField] private Mesh enemyBulletMesh;
    [SerializeField] private Material enemyBulletMaterial;
    [SerializeField] private float enemyBulletScale = 0.5f;

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

    /// <summary>
    /// 敵を座標・回転リストで描画。回転は Job で計算済み（プレイヤー方向）。
    /// 描画数が BATCH_SIZE（1023）を超える場合は警告を出し、先頭 1023 件のみ描画する。
    /// </summary>
    /// <param name="count">描画する敵の数。省略時は positions.Count を使用。</param>
    public void RenderEnemies(IList<Vector3> positions, IList<Quaternion> rotations, IList<float> flashTimers, IList<bool> activeFlags, int? count = null)
    {
        Mesh mesh = _runtimeEnemyMesh;
        Material mat = _runtimeEnemyMaterial;
        if (mesh == null || mat == null) return;

        Vector3 scale = _runtimeEnemyScale;
        int drawCount = count ?? positions.Count;
        int writeIndex = 0;

        for (int i = 0; i < drawCount; i++)
        {
            if (activeFlags[i] == false)
            {
                continue;
            }

            if (writeIndex >= BATCH_SIZE)
            {
                Debug.LogWarning($"[RenderManager] 敵の描画数が {BATCH_SIZE} を超えています。先頭 {BATCH_SIZE} 件のみ描画します。");
                break;
            }

            _matrices[writeIndex] = Matrix4x4.TRS(
                positions[i],
                rotations[i],
                scale
            );

            if (flashTimers[i] > 0f)
            {
                _emissionColors[writeIndex] = new Vector4(flashIntensity, flashIntensity, flashIntensity, 1f);
            }
            else
            {
                _emissionColors[writeIndex] = Vector4.zero;
            }

            writeIndex++;
        }

        if (writeIndex > 0)
        {
            ExecuteDrawEnemies(mesh, mat, writeIndex);
        }
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
    /// ジェムを座標・アクティブリストで描画（敵と同様に RenderMeshInstanced、Instantiate なし）。
    /// 描画数が BATCH_SIZE（1023）を超える場合は警告を出し、先頭 1023 件のみ描画する。
    /// </summary>
    public void RenderGems(IList<Vector3> positions, IList<bool> activeFlags)
    {
        if (gemMesh == null || gemMaterial == null)
        {
            return;
        }

        int count = positions.Count;
        int writeIndex = 0;

        for (int i = 0; i < count; i++)
        {
            if (activeFlags[i] == false)
            {
                continue;
            }

            if (writeIndex >= BATCH_SIZE)
            {
                Debug.LogWarning($"[RenderManager] ジェムの描画数が {BATCH_SIZE} を超えています。先頭 {BATCH_SIZE} 件のみ描画します。");
                break;
            }

            _matrices[writeIndex] = Matrix4x4.TRS(
                positions[i],
                Quaternion.identity,
                Vector3.one * gemScale
            );
            writeIndex++;
        }

        if (writeIndex > 0)
        {
            ExecuteDrawGems(writeIndex);
        }
    }

    /// <summary>
    /// プレイヤー弾を座標・回転・アクティブリストで描画。
    /// </summary>
    public void RenderPlayerBullets(IList<Vector3> positions, IList<Quaternion> rotations, IList<bool> activeFlags)
    {
        RenderBulletsInternal(playerBulletMesh, playerBulletMaterial, playerBulletScale, positions, rotations, activeFlags);
    }

    /// <summary>
    /// 敵弾を座標・回転・アクティブリストで描画。
    /// </summary>
    public void RenderEnemyBullets(IList<Vector3> positions, IList<Quaternion> rotations, IList<bool> activeFlags)
    {
        RenderBulletsInternal(enemyBulletMesh, enemyBulletMaterial, enemyBulletScale, positions, rotations, activeFlags);
    }

    /// <summary>
    /// 弾を座標・回転・アクティブリストで描画。描画数が BATCH_SIZE（1023）を超える場合は警告を出し、先頭 1023 件のみ描画する。
    /// </summary>
    private void RenderBulletsInternal(Mesh mesh, Material mat, float scale, IList<Vector3> positions, IList<Quaternion> rotations, IList<bool> activeFlags)
    {
        if (mesh == null || mat == null)
        {
            return;
        }

        int count = positions.Count;
        int writeIndex = 0;
        Vector3 scaleVec = Vector3.one * scale;

        for (int i = 0; i < count; i++)
        {
            if (activeFlags[i] == false)
            {
                continue;
            }

            if (writeIndex >= BATCH_SIZE)
            {
                Debug.LogWarning($"[RenderManager] 弾の描画数が {BATCH_SIZE} を超えています。先頭 {BATCH_SIZE} 件のみ描画します。");
                break;
            }

            _matrices[writeIndex] = Matrix4x4.TRS(
                positions[i],
                rotations[i],
                scaleVec
            );
            writeIndex++;
        }

        if (writeIndex > 0)
        {
            ExecuteDrawBullets(mesh, mat, writeIndex);
        }
    }

    private void ExecuteDrawBullets(Mesh mesh, Material mat, int count)
    {
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
        Graphics.RenderMeshInstanced(_rpBullet, mesh, 0, _matrices, count);
    }

    private void ExecuteDrawGems(int count)
    {
        Graphics.RenderMeshInstanced(_rpGem, gemMesh, 0, _matrices, count);
    }
}
