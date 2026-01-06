using UnityEngine;

public sealed class BootState : IGameState
{
    private readonly GameLoopStateMachine _sm;

    public BootState(GameLoopStateMachine sm) => _sm = sm;

    public void Enter(AppServicesRoot app)
    {
        Debug.Log("[BootState] Enter -> Go Run");
        _sm.ChangeState(new RunState(_sm));
    }

    public void Tick() { }
    public void Exit() { }
}
