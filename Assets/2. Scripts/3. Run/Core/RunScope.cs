using UnityEngine;

public sealed class RunScope : MonoBehaviour
{
    public AppServicesRoot App { get; private set; }

    public RunEventBus Events { get; private set; }
    public EntityManager Entities { get; private set; }
    public CombatSystem Combat { get; private set; }

    public RunEconomy Economy { get; private set; }

    public GridSystem Grid { get; private set; }
    public TowerBuildSystem TowerBuild { get; private set; }
    public PlacementSystem Placement { get; private set; }

    public GameManager GameManager { get; private set; }
    public EnemySpawnSystem Spawner { get; private set; }

    public void Initialize(AppServicesRoot app)
    {
        App = app;

        RunScopeLocator.Current = this;

        Events = new RunEventBus();
        var buildMode = gameObject.AddComponent<BuildModeController>();
        var pause = gameObject.AddComponent<BuildModePauseSystem>();
        pause.Construct(this);
        var gridViz = gameObject.AddComponent<GridVisualizer>();
        gridViz.Construct(this);

        buildMode.Construct(this);

        Entities = new EntityManager();
        Combat = new CombatSystem();

        int startGold = (GameRoot.Instance != null) ? GameRoot.Instance.StartGold : 50;
        Economy = new RunEconomy(startGold);

        // Run systems (MonoBehaviour)
        Grid = gameObject.AddComponent<GridSystem>();
        if (GameRoot.Instance != null)
            if (GameRoot.Instance != null && GameRoot.Instance.BuildAnchor != null)
            {
                Grid.Configure(GameRoot.Instance.BuildCellSize, GameRoot.Instance.BuildAnchor,
                    GameRoot.Instance.BuildWidth, GameRoot.Instance.BuildHeight, GameRoot.Instance.BuildAnchorOffset, GameRoot.Instance.BuildCenter);
            }
            else if (GameRoot.Instance != null)
            {
                Grid.Configure(GameRoot.Instance.BuildCellSize, GameRoot.Instance.BuildGridOrigin);
            }

        TowerBuild = gameObject.AddComponent<TowerBuildSystem>();
        TowerBuild.Construct(this);

        Placement = gameObject.AddComponent<PlacementSystem>();
        Placement.Construct(this);

        GameManager = new GameManager(this);

        Spawner = gameObject.AddComponent<EnemySpawnSystem>();
        Spawner.Construct(this);
    }

    private void OnDestroy()
    {
        if (RunScopeLocator.Current == this)
            RunScopeLocator.Current = null;

        if (Entities != null)
        {
            if (Entities.Player != null)
                Destroy(Entities.Player.gameObject);

            for (int i = Entities.Towers.Count - 1; i >= 0; i--)
            {
                var t = Entities.Towers[i];
                if (t != null) Destroy(t.gameObject);
            }

            // Enemy Ǯ  ̸ Despawn
            if (App != null && App.Pool != null && GameRoot.Instance != null && GameRoot.Instance.EnemyPrefab != null)
            {
                for (int i = Entities.Enemies.Count - 1; i >= 0; i--)
                {
                    var e = Entities.Enemies[i];
                    if (e == null) continue;
                    App.Pool.Despawn(e.gameObject, GameRoot.Instance.EnemyPrefab.gameObject);
                }
            }
            else
            {
                for (int i = Entities.Enemies.Count - 1; i >= 0; i--)
                {
                    var e = Entities.Enemies[i];
                    if (e != null) Destroy(e.gameObject);
                }
            }
        }

        Economy?.Dispose();
        Events?.Dispose();
        Entities?.Dispose();
    }
}
