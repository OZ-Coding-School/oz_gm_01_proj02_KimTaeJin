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

    [Header("Required")]
    [SerializeField] private GridSystem grid;
    [SerializeField] private GridVisualizer gridVisualizer;
    [SerializeField] private TowerBuildSystem towerBuild;

    [Header("Optional (Auto)")]
    [SerializeField] private BuildModeController buildMode;
    [SerializeField] private BuildModePauseSystem buildModePause;
    [SerializeField] private BaseFootprintReserver baseFootprintReserver;
    [SerializeField] private BaseFootprintOverlay baseFootprintOverlay;
    [SerializeField] private PlacementSystem placement;
    [SerializeField] private EnemySpawnSystem spawner;

    private bool _initialized;

    public void Initialize(AppServicesRoot app)
    {
        if (_initialized) return;
        _initialized = true;

        App = app;

        RunScopeLocator.SetCurrent(this);

        Events = new RunEventBus();

        grid = grid != null ? grid : GetComponent<GridSystem>();
        gridVisualizer = gridVisualizer != null ? gridVisualizer : GetComponent<GridVisualizer>();
        towerBuild = towerBuild != null ? towerBuild : GetComponent<TowerBuildSystem>();

        if (grid == null)
            Debug.LogError("[RunScope] GridSystem is missing.");
        if (gridVisualizer == null)
            Debug.LogError("[RunScope] GridVisualizer is missing.");
        if (towerBuild == null)
            Debug.LogError("[RunScope] TowerBuildSystem is missing.");

        buildMode = buildMode != null ? buildMode : GetComponent<BuildModeController>();
        buildModePause = buildModePause != null ? buildModePause : GetComponent<BuildModePauseSystem>();
        baseFootprintReserver = baseFootprintReserver != null ? baseFootprintReserver : GetComponent<BaseFootprintReserver>();
        baseFootprintOverlay = baseFootprintOverlay != null ? baseFootprintOverlay : GetComponent<BaseFootprintOverlay>();
        placement = placement != null ? placement : GetComponent<PlacementSystem>();
        spawner = spawner != null ? spawner : GetComponent<EnemySpawnSystem>();

        Grid = grid;
        TowerBuild = towerBuild;
        Placement = placement;
        Spawner = spawner;

        if (grid != null && GameRoot.Instance != null)
        {
            if (GameRoot.Instance.BuildAnchor != null)
            {
                grid.Configure(GameRoot.Instance.BuildCellSize, GameRoot.Instance.BuildAnchor,
                    GameRoot.Instance.BuildWidth, GameRoot.Instance.BuildHeight, GameRoot.Instance.BuildAnchorOffset, GameRoot.Instance.BuildCenter);
            }
            else
            {
                grid.Configure(GameRoot.Instance.BuildCellSize, GameRoot.Instance.BuildGridOrigin);
            }
        }

        buildMode?.Construct(this);
        buildModePause?.Construct(this);
        gridVisualizer?.Construct(this);
        baseFootprintReserver?.Construct(this);
        baseFootprintOverlay?.Construct(this);
        towerBuild?.Construct(this);
        placement?.Construct(this);

        Entities = new EntityManager();
        Combat = new CombatSystem();

        int startGold = (GameRoot.Instance != null) ? GameRoot.Instance.StartGold : 50;
        Economy = new RunEconomy(startGold);

        GameManager = new GameManager(this);

        Spawner?.Construct(this);
    }

    private void OnDestroy()
    {
        if (RunScopeLocator.Current == this)
            RunScopeLocator.SetCurrent(null);

        if (Entities != null)
        {
            if (Entities.Player != null)
                Destroy(Entities.Player.gameObject);

            for (int i = Entities.Towers.Count - 1; i >= 0; i--)
            {
                var t = Entities.Towers[i];
                if (t != null) Destroy(t.gameObject);
            }

            // Enemy Pool Despawn
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
