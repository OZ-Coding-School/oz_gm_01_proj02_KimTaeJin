using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(200)]
public sealed partial class PlacementVisualizer : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GridDataService dataService;
    [SerializeField] private TowerPlacementController controller;
    [SerializeField] private Transform root;
    [SerializeField] private Grid grid;
    [SerializeField] private RunScope scope;

    [Header("Mode")]
    [SerializeField] private bool isWorldVisualizer = true;

    [Header("Panel Render")]
    [SerializeField] private Camera panelCamera;
    [SerializeField] private RenderTexture panelTexture;
    [SerializeField] private RawImage panelTargetImage;
    [SerializeField] private string panelLayerName = "PanelPreview";
    [SerializeField] private bool autoConfigurePanelCamera = true;
    [SerializeField] private bool ignorePlayAreaFogOnPanelCamera = true;

    [Header("Panel Grid")]
    [SerializeField] private GameObject gridPlanePrefab;
    [SerializeField] private Transform gridPlaneRoot;
    [SerializeField] private bool matchGridCellSize = true;
    [SerializeField] private Vector3 gridPlaneScale = Vector3.one;
    [SerializeField] private float gridPlaneY = 0f;

    [Header("Panel Grid Lines")]
    [SerializeField] private bool drawPanelGridLines = true;
    [SerializeField] private Material gridLineMaterial;
    [SerializeField] private float gridLineWidth = 0.03f;
    [SerializeField] private float gridLineY = 0.02f;
    [SerializeField] private Color gridLineColor = new Color(1f, 1f, 1f, 0.6f);

    [Header("Panel Grid Plane Tint")]
    [SerializeField] private bool tintGridPlanesOnHover = true;
    [SerializeField] private Color gridPlaneNeutralColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color gridPlaneCanPlaceColor = new Color(0.3f, 0.9f, 0.3f, 0.9f);
    [SerializeField] private Color gridPlaneCannotPlaceColor = new Color(1f, 0.3f, 0.3f, 0.9f);

    [Header("Panel Grid Overlay")]
    [SerializeField] private bool showBuildableOverlay = true;
    [SerializeField] private bool showRoadOverlay = true;
    [SerializeField] private Color gridPlaneBuildableColor = new Color(0.8f, 0.95f, 0.8f, 0.9f);
    [SerializeField] private Color gridPlaneUnbuildableColor = new Color(0.75f, 0.75f, 0.75f, 0.9f);
    [SerializeField] private Color gridPlaneRoadColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);

    [Header("Panel Road Tiles")]
    [SerializeField] private bool spawnPanelRoadTiles = true;
    [SerializeField] private GameObject panelRoadTilePrefab;
    [SerializeField] private Transform panelRoadRoot;
    [SerializeField] private Vector3 panelRoadRotation = Vector3.zero;
    [SerializeField] private float panelRoadTileYOffset = 0.01f;
    [SerializeField] private Vector2 panelRoadTileGridOffset = Vector2.zero;
    [SerializeField] private bool normalizePanelRoadToCell = true;
    [SerializeField] private bool centerPanelRoadToCell = true;
    [SerializeField] private float panelRoadTileScaleMultiplier = 1f;
    [SerializeField] private bool matchPanelRoadToGridPlaneScale = true;
    [SerializeField] private bool usePanelRoadBottomOffset = true;
    [SerializeField] private bool useBuildCellPrefabFallback = true;

    [Header("Panel Grid Projection")]
    [SerializeField] private bool compensatePanelPitch = true;

    [Header("Panel Camera Align")]
    [SerializeField] private bool autoAlignPanelCamera = true;
    [SerializeField] private bool autoApplyPanelCameraPitch = true;
    [SerializeField, Range(10f, 89f)] private float panelCameraPitch = 70f;

    [Header("Panel Center")]
    [SerializeField] private GameObject centerPrefab;
    [SerializeField] private Vector3 centerOffset = Vector3.zero;

    [Header("Preview")]
    [SerializeField] private Color canPlaceColor = new Color(0.2f, 1f, 0.2f, 0.6f);
    [SerializeField] private Color cannotPlaceColor = new Color(1f, 0.2f, 0.2f, 0.6f);

    [Header("Panel Upgrade Tween")]
    [SerializeField] private bool playPanelUpgradeTween = true;
    [SerializeField] private float panelUpgradeDropHeight = 1.2f;
    [SerializeField] private float panelUpgradeTweenDuration = 0.18f;
    [SerializeField] private Ease panelUpgradeTweenEase = Ease.OutQuad;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    public bool IsWorldVisualizer => isWorldVisualizer;

    private readonly Dictionary<Vector3Int, PlacedView> _placed = new();
    private readonly List<Vector2Int> _releaseCells = new();
    private readonly List<GameObject> _gridPlanes = new();
    private readonly List<LineRenderer> _gridLines = new();
    private readonly Dictionary<Vector2Int, Renderer> _gridPlaneRenderers = new();
    private readonly Dictionary<Vector2Int, Color> _gridPlaneBaseColors = new();
    private readonly List<Vector2Int> _hoverCells = new();
    private readonly HashSet<Vector2Int> _roadCells = new();
    private readonly List<GridRoadUtility.RoadTower> _roadTowerBuffer = new();
    private readonly Dictionary<Vector2Int, GameObject> _panelRoadTiles = new();
    private readonly List<Vector2Int> _panelRoadRemove = new();
    private MaterialPropertyBlock _gridPlaneMpb;
    private Vector3Int _hiddenCell;
    private bool _hasHidden;
    private GameObject _previewInstance;
    private Renderer[] _previewRenderers;
    private MaterialPropertyBlock _previewMpb;
    private string _previewTowerId;
    private GameObject _centerInstance;
    private int _panelLayer = -2;
    private string _panelLayerNameCache;
    private bool _invalidLayerWarned;
    private bool _invalidParentWarned;
    private Transform _gridLineRoot;
    private float _panelBaseCellZ;
    private float _panelBaseCenterOffsetZ;
    private bool _panelBaseCaptured;
    private float _panelCompensatedCellX;
    private float _panelCompensatedCellZ;
    private bool _panelCompensatedSizeValid;
    private bool[,] _buildable;
    private bool _overlayDirty;
    private bool _panelRoadTileMetricsCached;
    private GameObject _panelRoadTilePrefabCache;
    private Vector3 _panelRoadTileBaseScale;
    private Vector3 _panelRoadTileCenter;
    private Bounds _panelRoadTileBounds;
    private bool _panelRoadTileHasBounds;
    private float _panelRoadTileBottomOffset;
    private readonly Dictionary<Transform, Vector3> _panelBasePlateScaleCache = new();
    private readonly Dictionary<Vector2Int, AimSnapshot> _aimSnapshots = new();

    public struct AimSnapshot
    {
        public Quaternion yawWorldRot;
        public Quaternion pitchLocalRot;
        public bool hasPitch;
    }

    private sealed class PlacedView
    {
        public GameObject instance;
        public Renderer[] renderers;
        public TowerEntity tower;
        public TowerDefinitionSO def;
    }

    private void Awake()
    {
        if (root == null) root = transform;
        if (dataService == null) dataService = FindObjectOfType<GridDataService>();
        if (controller == null) controller = FindObjectOfType<TowerPlacementController>();
        if (scope == null) scope = RunScopeLocator.Current;
        if (grid == null && dataService != null) grid = dataService.WorldGrid;
        _previewMpb = new MaterialPropertyBlock();
        _gridPlaneMpb = new MaterialPropertyBlock();
        ResolvePanelLayer();
        ConfigurePanelRender();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        if (dataService != null)
        {
            dataService.OnDataChanged += HandleDataChanged;
            dataService.OnGridReset += HandleGridReset;
        }
        if (controller != null)
        {
            controller.OnCellHoverChanged += HandleCellHoverChanged;
            controller.OnPlacementCanceled += HandlePlacementCanceled;
        }
        if (!isWorldVisualizer)
        {
            ConfigurePanelRender();
            ApplyPanelGridCompensation();
            AlignPanelCameraToGrid();
            RebuildGridPlanes();
            RebuildGridLines();
            EnsureCenterObject();
            MarkGridPlaneOverlayDirty();
        }
        if (dataService != null && _placed.Count == 0)
        {
            foreach (var kvp in dataService.Data)
                RebuildCell(kvp.Key);
        }
        if (debugLogs)
            Debug.Log($"[PlacementVisualizer] Enabled name={name} world={isWorldVisualizer}");
    }

    private void OnDisable()
    {
        if (dataService != null)
        {
            dataService.OnDataChanged -= HandleDataChanged;
            dataService.OnGridReset -= HandleGridReset;
        }
        if (controller != null)
        {
            controller.OnCellHoverChanged -= HandleCellHoverChanged;
            controller.OnPlacementCanceled -= HandlePlacementCanceled;
        }
    }

    private void LateUpdate()
    {
        if (isWorldVisualizer) return;
        ApplyPanelGridCompensation();
        if (!_overlayDirty) return;
        _overlayDirty = false;
        UpdateGridPlaneBaseColors();
    }
}
