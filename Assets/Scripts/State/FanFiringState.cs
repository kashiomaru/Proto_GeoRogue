/// <summary>
/// 発射モード「Fan（扇形）」：プレイヤー前方を基準に、SpreadAngle で扇形に広がって発射する。
/// </summary>
public class FanFiringState : IState<Player>
{
    public void OnEnter(Player context) { }

    public void OnUpdate(Player context)
    {
        context.TryFire();
    }

    public void OnExit(Player context) { }
}
