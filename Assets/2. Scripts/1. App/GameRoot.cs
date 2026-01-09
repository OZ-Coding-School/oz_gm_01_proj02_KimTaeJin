using UnityEngine;

public sealed class GameRoot : MonoBehaviour
{
    public static GameRoot Instance { get; private set; }

    private AppServicesRoot _app;
    private GameLoopStateMachine _loop;
    [SerializeField] private PlayerEntity playerPrefab;
    [SerializeField] private EnemyEntity enemyPrefab;

    [Header("Build/Tower Catalog")]
    [SerializeField] private TowerDefinitionSO[] towerCatalog;
    [SerializeField] private int startGold = 50;

    [Header("Build/Grid")]
    [SerializeField] private float buildCellSize = 2f;
    [SerializeField] private Vector3 buildGridOrigin = Vector3.zero;
    [SerializeField] private Transform buildAnchor;
    [SerializeField] private Vector3 buildAnchorOffset = Vector3.zero;
    [SerializeField] private int buildWidth = 9;
    [SerializeField] private int buildHeight = 10;
    [SerializeField] private bool buildCenter = true;

    public TowerDefinitionSO[] TowerCatalog => towerCatalog;
    public int StartGold => startGold;
    public float BuildCellSize => buildCellSize;
    public Vector3 BuildGridOrigin => buildGridOrigin;
    public Transform BuildAnchor => buildAnchor;
    public Vector3 BuildAnchorOffset => buildAnchorOffset;
    public int BuildWidth => buildWidth;
    public int BuildHeight => buildHeight;
    public bool BuildCenter => buildCenter;


    [SerializeField] private float spawnInterval = 1.5f;
    [SerializeField] private float spawnRadius = 10f;

    public PlayerEntity PlayerPrefab => playerPrefab;
    public EnemyEntity EnemyPrefab => enemyPrefab;
    public float SpawnInterval => spawnInterval;
    public float SpawnRadius => spawnRadius;

    [Header("Ground Snap")]
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float groundRayHeight = 30f;
    [SerializeField] private float groundExtraOffset = 0.02f;

    public LayerMask GroundMask => groundMask;
    public float GroundRayHeight => groundRayHeight;
    public float GroundExtraOffset => groundExtraOffset;
    //==================================================
    [SerializeField] private int maxEnemiesAlive = 60;
    public int MaxEnemiesAlive => maxEnemiesAlive;
    public PoolService Pool => _app.Pool;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Debug.Log("[GameRoot] Awake");

        _app = new AppServicesRoot();

        var poolRoot = new GameObject("[PoolRoot]").transform;
        poolRoot.SetParent(transform, false);

        _app.Initialize(poolRoot);


        _loop = new GameLoopStateMachine();
        _loop.Boot(_app);
        Debug.Log($"Physics.gravity = {Physics.gravity}");

    }

    private void Update()
    {
        _loop?.Tick();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;

        _loop?.Dispose();
        _app?.Dispose();

        Instance = null;
    }
}
