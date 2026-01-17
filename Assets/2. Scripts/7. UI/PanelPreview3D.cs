using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public sealed partial class PanelPreview3D : MonoBehaviour
{
    [Header("Render")]
    [SerializeField] private Camera previewCamera;
    [SerializeField] private RawImage targetImage;
    [SerializeField] private RenderTexture renderTexture;
    [SerializeField] private LayerMask previewLayer;
    [SerializeField] private bool autoIsolatePreviewLayer = true;
    [SerializeField] private int fallbackPreviewLayerIndex = 30;
    [SerializeField] private bool debugPreviewPlacements = false;
    [SerializeField] private bool transparentBackground = true;
    [SerializeField] private Color previewBackgroundColor = new Color(0f, 0f, 0f, 1f);
    [SerializeField] private bool excludePreviewLayerFromMainCamera = true;
    [SerializeField] private bool autoResizeRenderTexture = true;
    [SerializeField] private float renderTextureScale = 1f;
    [SerializeField] private int renderTextureMinSize = 256;
    [SerializeField] private int renderTextureMaxSize = 2048;
    [SerializeField] private RectTransform gridRootRect;
    [SerializeField] private bool autoFitRawImageToGrid = true;

    [Header("Scene")]
    [SerializeField] private Transform previewRoot;
    [SerializeField] private Light previewLight;
    [SerializeField] private bool useDetachedPreviewRoot = true;
    [SerializeField] private bool stripPreviewComponents = true;
    [SerializeField] private bool isolatePreviewWorld = true;
    [SerializeField] private Vector3 previewWorldOffset = new Vector3(1000f, 1000f, 1000f);
    [SerializeField] private float previewNearClip = 0.05f;
    [SerializeField] private float previewFarClip = 200f;

    [Header("Preview Scripts")]
    [SerializeField] private bool applyPreviewScriptFilter = true;
    [SerializeField] private string[] previewScriptWhitelist;
    [SerializeField] private string[] previewScriptBlacklist;

    [Header("Camera Rig")]
    [SerializeField] private bool autoSetupCamera = true;
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 6f, -6f);
    [SerializeField] private Vector3 cameraEuler = new Vector3(45f, 0f, 0f);
    [SerializeField] private Vector3 cameraLookAtOffset = Vector3.zero;
    [SerializeField] private Vector3 sceneOffset = Vector3.zero;
    [SerializeField] private bool autoFitToGrid = true;
    [SerializeField] private float fitPadding = 1.08f;
    [SerializeField] private bool lockCameraTopDown = false;
    [SerializeField] private float topDownHeight = 8f;
    [SerializeField] private bool useOrthographic = false;
    [SerializeField] private float perspectiveFov = 35f;
    [SerializeField] private bool compensateTiltForSquareCells = false;

    [Header("Grid")]
    [SerializeField] private int gridWidth = 9;
    [SerializeField] private int gridHeight = 10;
    [SerializeField] private float cellWorldWidth = 1f;
    [SerializeField] private float cellWorldHeight = 1f;
    [SerializeField] private float gridWorldScale = 1f;
    [SerializeField] private bool useGridCellSize = true;
    [SerializeField] private GridSystem gridSystem;
    [SerializeField] private bool useGridViewCellAspect = true;
    [SerializeField] private bool useGridRootRectForAspect = false;
    [SerializeField] private float cellHeightScale = 1f;
    [SerializeField] private float previewScale = 1f;
    [SerializeField] private float previewHeightScale = 1f;

    [Header("Grid Lines")]
    [SerializeField] private bool useWorldGridLines = true;
    [SerializeField] private Material lineMaterial;
    [SerializeField] private float lineWidth = 0.03f;
    [SerializeField] private float lineYOffset = 0.01f;
    [SerializeField] private Color lineColor = new Color(1f, 1f, 1f, 0.8f);

    [Header("Grid Tiles")]
    [SerializeField] private GameObject tilePrefab;
    [SerializeField] private Transform tileRoot;
    [SerializeField] private bool autoBuildTiles = true;
    [SerializeField] private bool normalizeTileToCell = true;
    [SerializeField] private bool centerTileToCell = true;
    [SerializeField] private float tileYOffset = -0.02f;
    [SerializeField] private Vector2 tileGridOffset = Vector2.zero;
    [SerializeField] private float tileGridScale = 1f;
    [SerializeField] private Color tileNormalColor = new Color(0.55f, 0.55f, 0.55f, 1f);
    [SerializeField] private Color tileBlockedColor = new Color(0.25f, 0.25f, 0.25f, 1f);
    [SerializeField] private Color tileOccupiedColor = new Color(0.2f, 0.2f, 0.2f, 1f);

    [Header("Road Tiles")]
    [SerializeField] private GameObject roadTilePrefab;
    [SerializeField] private Transform roadRoot;
    [SerializeField] private bool roadMatchGridTileSettings = true;
    [SerializeField] private bool roadUseBottomOffset = true;
    [SerializeField] private float roadTileYOffset = -0.015f;
    [SerializeField] private float roadTileScale = 1f;
    [SerializeField] private Vector2 roadTileGridOffset = Vector2.zero;

    [Header("Placed Preview Height")]
    [SerializeField] private bool matchPlacedToRoadHeight = true;
    [SerializeField] private float placedPreviewYOffset = 0f;
    [SerializeField] private bool matchCenterToRoadHeight = true;

    [Header("Center Building")]
    [SerializeField] private GameObject centerPrefab;
    [SerializeField] private Vector2Int centerFootprint = new Vector2Int(2, 2);
    [SerializeField] private bool evenFootprintBiasPositive = true;
    [SerializeField] private Vector3 centerRotation = Vector3.zero;
    [SerializeField] private Vector3 placementRotation = Vector3.zero;
    [SerializeField] private Vector3 centerPreviewOffset = Vector3.zero;
    [SerializeField] private float centerPreviewYOffset = 0f;

    [Header("Footprint Align")]
    [SerializeField] private bool autoScaleToFootprint = true;
    [SerializeField] private bool useFootprintNodeForBounds = true;
    [SerializeField] private bool scaleFootprintNode = true;
    [SerializeField] private string footprintNodeName = "BasePlate";

    [Header("Placement Move")]
    [SerializeField] private float placementMoveDuration = 0.12f;
    [SerializeField] private Ease placementMoveEase = Ease.OutQuad;
    [SerializeField] private bool showPlacementPreview = true;

    [Header("Placement Drop")]
    [SerializeField] private float placementDropHeight = 1.2f;
    [SerializeField] private float placementDropDuration = 0.18f;
    [SerializeField] private Ease placementDropEase = Ease.OutCubic;

    [Header("Placed Parent")]
    [SerializeField] private bool useCenterGridAnchorForPlaced = true;
    [SerializeField] private string centerGridAnchorName = "GridAnchor";

    private GameObject _centerInstance;
    private Transform _centerGridAnchor;
    private GameObject _placementInstance;
    private Renderer[] _placementRenderers;
    private GameObject _placementPrefabSource;
    private GameObject _upgradePreviewInstance;
    private Renderer[] _upgradePreviewRenderers;
    private GameObject _upgradePreviewPrefabSource;
    private bool _usingUpgradePreview;
    private Color _placementTintColor = Color.white;
    private MaterialPropertyBlock _mpb;
    private Vector2Int _placementFootprint = Vector2Int.one;
    private Vector2Int _placementPivot = Vector2Int.zero;
    private Tween _placementMoveTween;
    private Tween _placementDropTween;
    private Transform _runtimeRoot;
    private Vector2Int _rtSize;
    private Transform _lineRoot;
    private readonly List<GameObject> _placedInstances = new();
    private readonly List<GameObject> _roadInstances = new();
    private readonly Dictionary<TowerEntity, GameObject> _placedByTower = new();
    private readonly List<TowerEntity> _placedToRemove = new();
    private readonly Dictionary<Transform, Vector3> _baseScales = new();
    private readonly Dictionary<Transform, Bounds> _baseBounds = new();
    private readonly Dictionary<Transform, Vector2Int> _placedCells = new();
    private readonly Dictionary<Transform, Vector2Int> _placedFootprints = new();
    private readonly Dictionary<Transform, Vector2Int> _placedPivots = new();
    private readonly List<Transform> _hiddenPlacedList = new();
    private readonly List<Transform> _placedTransformsBuffer = new();
    private Transform _hiddenPlaced;
    private Vector2Int _hiddenPlacedCell;
    private bool _hasHiddenPlaced;
    private Vector2Int _placementCell;
    private bool _hasPlacementCell;
    private bool _placementCanPlace;
    private bool _loggedPreviewRoot;
    private float _baseCellAspect = 1f;
    private Renderer[,] _tileRenderers;
    private MaterialPropertyBlock _tileMpb;
    private Vector2Int _centerPivot = Vector2Int.zero;
    private LayerMask _cachedPreviewLayerMask;
    private int _cachedPreviewLayerIndex = -1;
    private bool _previewLayerCached;
    private bool _loggedPlacementPreviewState;
    private bool _loggedUpgradePreviewState;

    public int GridWidth => gridWidth;
    public int GridHeight => gridHeight;
    public Vector2Int CenterCell => GetCenteredFootprintAnchor(centerFootprint, _centerPivot);

    public void SetGridSystem(GridSystem grid)
    {
        gridSystem = grid;
    }

    private void Awake()
    {
        EnsurePreviewRoot();
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        if (_tileMpb == null) _tileMpb = new MaterialPropertyBlock();
        SetupRenderTarget();
        ApplyCameraSettings();
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
    }
    
    private void LateUpdate()
    {
        if (autoResizeRenderTexture)
            UpdateRenderTextureFromTarget();
        ApplyPreviewLayerExclusion();
        SyncPlacedTowerRotations();
    }

    public void ShowCenter()
    {
        if (centerPrefab == null) return;
        if (_centerInstance != null)
        {
            DetachPreviewsFromCenter();
            RemoveCachedMetrics(_centerInstance.transform);
            Destroy(_centerInstance);
        }

        RefreshPreviewLayer();
        _centerInstance = Instantiate(centerPrefab, previewRoot);
        _centerInstance.name = "[CenterPreview]";
        PreparePreviewObject(_centerInstance);

        _centerInstance.transform.localRotation = Quaternion.Euler(centerRotation);
        CacheCenterGridAnchor();

        CacheBaseMetrics(_centerInstance);
        FitToFootprint(_centerInstance, centerFootprint);
        PlaceAtGridCenter(_centerInstance.transform);
        ApplyCenterPreviewOffset(_centerInstance.transform);
        ReparentPlacedInstances();
    }

}
