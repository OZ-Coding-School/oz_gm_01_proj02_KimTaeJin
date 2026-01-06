using UnityEngine;

public sealed class GameRoot : MonoBehaviour
{
    public static GameRoot Instance { get; private set; }

    private AppServicesRoot _app;
    private GameLoopStateMachine _loop;
    [SerializeField] private PlayerEntity playerPrefab;
    [SerializeField] private EnemyEntity enemyPrefab;

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
    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Debug.Log("[GameRoot] Awake");

        _app = new AppServicesRoot();
        _app.Initialize();

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
