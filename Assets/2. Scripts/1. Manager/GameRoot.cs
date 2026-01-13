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
    [SerializeField] private float buildCellSizeZScale = 1f;
    [SerializeField] private bool autoBuildCellSize = false;
    [SerializeField] private GameObject buildCellSizePrefab;
    [SerializeField] private Vector2Int buildCellSizeFootprint = new Vector2Int(1, 1);
    [SerializeField] private bool buildCellSizeUseMaxAxis = true;
    [SerializeField] private float buildCellSizePadding = 0f;
    [SerializeField] private float buildCellSizeScale = 1f;
    [SerializeField] private Vector3 buildGridOrigin = Vector3.zero;
    [SerializeField] private Transform buildAnchor;
    [SerializeField] private Vector3 buildAnchorOffset = Vector3.zero;
    [SerializeField] private int buildWidth = 9;
    [SerializeField] private int buildHeight = 10;
    [SerializeField] private bool buildCenter = true;

    public TowerDefinitionSO[] TowerCatalog => towerCatalog;
    public int StartGold => startGold;
    public float BuildCellSize => buildCellSize;
    public float BuildCellSizeZScale => buildCellSizeZScale;
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

        if (autoBuildCellSize)
        {
            float computed = ComputeBuildCellSize();
            if (!Mathf.Approximately(buildCellSize, computed))
                buildCellSize = computed;
        }

        _app = new AppServicesRoot();

        var poolRoot = new GameObject("[PoolRoot]").transform;
        poolRoot.SetParent(transform, false);

        _app.Initialize(poolRoot);


        _loop = new GameLoopStateMachine();
        _loop.Boot(_app);
        Debug.Log($"Physics.gravity = {Physics.gravity}");

    }

#if UNITY_EDITOR
    [ContextMenu("Recompute Build Cell Size (Editor)")]
    private void RecomputeBuildCellSizeEditor()
    {
        if (Application.isPlaying) return;
        if (!autoBuildCellSize) return;
        if (buildCellSizePrefab == null) return;
        buildCellSize = ComputeBuildCellSize();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    private float ComputeBuildCellSize()
    {
        if (!autoBuildCellSize) return buildCellSize;
        if (buildCellSizePrefab == null) return buildCellSize;

        Bounds b = GetPrefabBounds(buildCellSizePrefab);
        if (b.size.x <= 0.0001f || b.size.z <= 0.0001f) return buildCellSize;

        int fx = Mathf.Max(1, buildCellSizeFootprint.x);
        int fy = Mathf.Max(1, buildCellSizeFootprint.y);

        float sizeX = b.size.x / fx;
        float sizeZ = b.size.z / fy;
        float size = buildCellSizeUseMaxAxis ? Mathf.Max(sizeX, sizeZ) : (sizeX + sizeZ) * 0.5f;

        size += buildCellSizePadding;
        size *= Mathf.Max(0.01f, buildCellSizeScale);
        return Mathf.Max(0.25f, size);
    }

    private static Bounds GetPrefabBounds(GameObject prefab)
    {
        if (prefab == null) return new Bounds(Vector3.zero, Vector3.zero);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            string path = UnityEditor.AssetDatabase.GetAssetPath(prefab);
            if (!string.IsNullOrEmpty(path))
            {
                var root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
                var renderers = root.GetComponentsInChildren<Renderer>(true);
                Bounds b = GetBounds(renderers);
                UnityEditor.PrefabUtility.UnloadPrefabContents(root);
                return b;
            }
        }
#endif

        var temp = Instantiate(prefab);
        temp.hideFlags = HideFlags.HideAndDontSave;
        temp.transform.position = Vector3.zero;
        temp.transform.rotation = Quaternion.identity;
        temp.transform.localScale = prefab.transform.localScale;

        var tempRenderers = temp.GetComponentsInChildren<Renderer>(true);
        Bounds tempBounds = GetBounds(tempRenderers);

        Destroy(temp);
        return tempBounds;
    }

    private static Bounds GetBounds(Renderer[] renderers)
    {
        bool has = false;
        Bounds b = new Bounds(Vector3.zero, Vector3.zero);
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            if (!has)
            {
                b = r.bounds;
                has = true;
            }
            else
            {
                b.Encapsulate(r.bounds);
            }
        }
        return b;
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
