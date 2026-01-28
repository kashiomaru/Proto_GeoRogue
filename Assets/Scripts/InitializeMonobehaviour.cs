using UnityEngine;

/// <summary>
/// MonoBehaviourの代わりに継承する基底クラス。
/// Initialize()を実装させ、Start()で自動的に呼び出します。
/// 初期化フラグにより、複数回の初期化処理を防ぎます。
/// </summary>
public abstract class InitializeMonobehaviour : MonoBehaviour
{
    // 初期化フラグ
    private bool _initialized = false;
    
    /// <summary>
    /// 初期化済みかどうかを取得します。
    /// </summary>
    protected bool IsInitialized => _initialized;
    
    void Start()
    {
        InitializeNotYet();
    }

    protected void InitializeNotYet()
    {
        if (!_initialized)
        {
            try
            {
                InitializeInternal();
                _initialized = true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[{GetType().Name}] InitializeInternal()で例外が発生しました: {ex.Message}\n{ex.StackTrace}");
                // 例外を再スロー（初期化が失敗した状態を維持）
                throw;
            }
        }
    }
    
    /// <summary>
    /// 初期化処理を実装します。
    /// このメソッドはStart()で自動的に呼び出されます。
    /// </summary>
    protected abstract void InitializeInternal();

    /// <summary>
    /// 初期化を実行します。
    /// </summary>
    public void Initialize()
    {
        InitializeNotYet();
    }
    
    void OnDestroy()
    {
        FinalizeIfInitialized();
    }

    /// <summary>
    /// 初期化済みの場合にファイナライズを実行します。
    /// </summary>
    protected void FinalizeIfInitialized()
    {
        if (_initialized)
        {
            try
            {
                FinalizeInternal();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[{GetType().Name}] FinalizeInternal()で例外が発生しました: {ex.Message}\n{ex.StackTrace}");
                // ファイナライズ時の例外は再スローしない（破棄処理中なので）
            }
            finally
            {
                _initialized = false;
            }
        }
    }
    
    /// <summary>
    /// ファイナライズ処理を実装します。
    /// このメソッドはOnDestroy()で自動的に呼び出されます。
    /// </summary>
    protected abstract void FinalizeInternal();
}
