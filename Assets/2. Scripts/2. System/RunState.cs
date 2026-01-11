using UnityEngine;

public sealed class RunState : IGameState
{
    private readonly GameLoopStateMachine _sm;
    private RunScope _scope;

    public RunState(GameLoopStateMachine sm) => _sm = sm;

    public void Enter(AppServicesRoot app)
    {
        Debug.Log("[RunState] Enter");

        _scope = Object.FindObjectOfType<RunScope>();
        if (_scope == null)
        {
            Debug.LogError("[RunState] RunScope not found in scene. Add RunScope to the scene and assign required components.");
            return;
        }

        _scope.Initialize(app);
        _scope.GameManager.StartRun();
    }

    public void Tick() { }

    public void Exit()
    {
        _scope = null;
    }
}
