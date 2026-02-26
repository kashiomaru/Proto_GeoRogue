/// <summary>
/// ゲームステートの基底クラス
/// </summary>
public abstract class GameStateBase : IState<GameManager>
{
    /// <summary>
    /// プレイ中（弾・敵の処理を行う）かどうか。Normal / Boss のとき true。
    /// </summary>
    public abstract bool IsPlaying { get; }

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
