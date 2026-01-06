using UnityEngine;

public sealed class RunState : IGameState
{
    private readonly GameLoopStateMachine _sm;
    private RunScope _scope;

    public RunState(GameLoopStateMachine sm) => _sm = sm;

    public void Enter(AppServicesRoot app)
    {
        Debug.Log("[RunState] Enter");

        var go = new GameObject("RunScope");
        _scope = go.AddComponent<RunScope>();
        _scope.Initialize(app);

        _scope.GameManager.StartRun();
    }

    public void Tick() { }

    public void Exit()
    {
        if (_scope != null)
        {
            Object.Destroy(_scope.gameObject);
            _scope = null;
        }
    }
}
