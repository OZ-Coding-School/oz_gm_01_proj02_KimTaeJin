using System;

public sealed class GameLoopStateMachine : IDisposable
{
    private IGameState _current;
    private AppServicesRoot _app;

    public void Boot(AppServicesRoot app)
    {
        _app = app;
        ChangeState(new BootState(this));
    }

    public void ChangeState(IGameState next)
    {
        _current?.Exit();
        _current = next;
        _current?.Enter(_app);
    }

    public void Tick() => _current?.Tick();

    public void Dispose()
    {
        _current?.Exit();
        _current = null;
    }
}
