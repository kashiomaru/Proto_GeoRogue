/// <summary>
/// ステートのインターフェース
/// </summary>
/// <typeparam name="T">ステートマシンのコンテキスト型</typeparam>
public interface IState<T>
{
    /// <summary>
    /// ステートに入った時に呼ばれる
    /// </summary>
    void OnEnter(T context);
    
    /// <summary>
    /// ステートの更新処理
    /// </summary>
    void OnUpdate(T context);
    
    /// <summary>
    /// ステートから出る時に呼ばれる
    /// </summary>
    void OnExit(T context);
}
