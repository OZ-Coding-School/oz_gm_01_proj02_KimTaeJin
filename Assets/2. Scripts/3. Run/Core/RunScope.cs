using UnityEngine;

public sealed class RunScope : MonoBehaviour
{
    public AppServicesRoot App { get; private set; }

    public RunEventBus Events { get; private set; }
    public EntityManager Entities { get; private set; }
    public CombatSystem Combat { get; private set; }

    public GameManager GameManager { get; private set; }
    public EnemySpawnSystem Spawner { get; private set; }

    public void Initialize(AppServicesRoot app)
    {
        App = app;

        Events = new RunEventBus();
        Entities = new EntityManager();
        Combat = new CombatSystem();

        GameManager = new GameManager(this);

        Spawner = gameObject.AddComponent<EnemySpawnSystem>();
        Spawner.Construct(this);
    }

    private void OnDestroy()
    {
        Events?.Dispose();
        Entities?.Dispose();
    }
}
