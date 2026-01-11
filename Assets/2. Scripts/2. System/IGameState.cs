public interface IGameState
{
    void Enter(AppServicesRoot app);
    void Tick();
    void Exit();
}