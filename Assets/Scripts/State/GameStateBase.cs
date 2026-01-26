/// <summary>
/// ゲームステートの基底クラス
/// </summary>
public abstract class GameStateBase : IState<GameManager>
{
    /// <summary>
    /// ステートに入った時に呼ばれる
    /// </summary>
    public abstract void OnEnter(GameManager context);
    
    /// <summary>
    /// ステートの更新処理
    /// </summary>
    public abstract void OnUpdate(GameManager context);
    
    /// <summary>
    /// ステートから出る時に呼ばれる
    /// </summary>
    public abstract void OnExit(GameManager context);
}
