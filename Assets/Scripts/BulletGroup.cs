using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 弾 1 種類分の BulletPool と描画用リストをまとめる。
/// プレイヤー弾用・敵弾用の共通処理を吸収し、BulletManager から利用する。
/// </summary>
public class BulletGroup
{
    private BulletPool _pool;
    private List<Vector3> _positionList;
    private List<Quaternion> _rotationList;
    private List<bool> _activeList;

    /// <summary>内部の BulletPool。Spawn / ScheduleMoveJob / Positions / Active 等はここから参照する。</summary>
    public BulletPool Pool => _pool;

    /// <summary>描画用。CopyToRenderLists 後に RenderManager に渡す。</summary>
    public IList<Vector3> PositionList => _positionList;
    /// <summary>描画用。CopyToRenderLists 後に RenderManager に渡す。</summary>
    public IList<Quaternion> RotationList => _rotationList;
    /// <summary>描画用。CopyToRenderLists 後に RenderManager に渡す。</summary>
    public IList<bool> ActiveList => _activeList;

    /// <summary>
    /// 初期化。最大弾数とダメージ配列の有無を指定する。
    /// </summary>
    public void Initialize(int maxCount, bool useDamageArray = false)
    {
        _pool = new BulletPool();
        _pool.Initialize(maxCount, useDamageArray);

        _positionList = new List<Vector3>(maxCount);
        _rotationList = new List<Quaternion>(maxCount);
        _activeList = new List<bool>(maxCount);
        for (int i = 0; i < maxCount; i++)
        {
            _positionList.Add(default);
            _rotationList.Add(default);
            _activeList.Add(false);
        }
    }

    /// <summary>
    /// 描画用に座標・回転・アクティブをリストへコピーする。RenderManager に渡す前に呼ぶ。
    /// </summary>
    public void CopyToRenderLists()
    {
        _pool?.CopyToRenderLists(_positionList, _rotationList, _activeList);
    }

    /// <summary>
    /// すべての弾を無効化する。
    /// </summary>
    public void Reset()
    {
        _pool?.Reset();
    }

    /// <summary>
    /// ネイティブ配列を解放する。以降このグループは使用しないこと。
    /// </summary>
    public void Dispose()
    {
        _pool?.Dispose();
        _pool = null;
    }
}
