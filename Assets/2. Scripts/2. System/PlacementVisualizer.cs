using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(200)]
public sealed class PlacementVisualizer : MonoBehaviour
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

    private readonly Dictionary<Vector3Int, PlacedView> _placed = new();
    private readonly List<Vector2Int> _releaseCells = new();
    private readonly List<GameObject> _gridPlanes = new();
    private readonly List<LineRenderer> _gridLines = new();
    private readonly Dictionary<Vector2Int, Renderer> _gridPlaneRenderers = new();
    private readonly Dictionary<Vector2Int, Color> _gridPlaneBaseColors = new();
    private readonly List<Vector2Int> _hoverCells = new();
    private readonly HashSet<Vector2Int> _roadCells = new();
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
    private bool _panelRoadTileMetricsLogged;
    private GameObject _panelRoadTilePrefabCache;
    private Vector3 _panelRoadTileBaseScale;
    private Vector3 _panelRoadTileCenter;
    private Bounds _panelRoadTileBounds;
    private bool _panelRoadTileHasBounds;
    private float _panelRoadTileBottomOffset;

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

    private void HandleGridReset()
    {
        if (isWorldVisualizer) return;

        DestroyAllPlaced();
        ClearPreview();
        ClearGridPlanes();
        ClearCenterObject();
        ConfigurePanelRender();

        ApplyPanelGridCompensation();
        AlignPanelCameraToGrid();
        RebuildGridPlanes();
        RebuildGridLines();
        EnsureCenterObject();

        if (dataService == null) return;
        foreach (var kvp in dataService.Data)
            RebuildCell(kvp.Key);
        MarkGridPlaneOverlayDirty();
        if (debugLogs)
            Debug.Log($"[PlacementVisualizer] GridReset name={name} placed={_placed.Count}");
    }

    private void HandleDataChanged(Vector3Int cell)
    {
        RebuildCell(cell);
        MarkGridPlaneOverlayDirty();
        if (debugLogs)
            Debug.Log($"[PlacementVisualizer] DataChanged cell={cell} name={name}");
    }

    private void HandlePlacementCanceled()
    {
        ClearPreview();
        RestoreHiddenPlaced();
    }

    private void HandleCellHoverChanged(Vector3Int cell)
    {
        if (dataService == null || controller == null) return;

        RestoreHiddenPlaced();

        GridDataService.PlacementResult result = dataService.EvaluatePlacement(controller.Selected, cell);
        if (result.previewDef == null)
        {
            ClearPreview();
            ResetGridPlaneColors();
            return;
        }

        EnsurePreview(result.previewDef);
        SetPreviewTint(result.canPlace);
        SetPreviewPosition(cell, result.previewDef);
        UpdateGridPlaneColors(cell, result);

        if (result.hidePlaced)
            HidePlacedAt(cell);
        if (debugLogs)
            Debug.Log($"[PlacementVisualizer] Hover cell={cell} can={result.canPlace} name={name}");
    }

    private void RebuildCell(Vector3Int cell)
    {
        if (dataService == null) return;

        if (!_placed.TryGetValue(cell, out PlacedView existing))
        {
            if (dataService.TryGet(cell, out GridDataService.TowerData data) && dataService.TryGetDefinition(data.towerId, out TowerDefinitionSO def))
                SpawnPlaced(cell, def);
            return;
        }

        if (!dataService.TryGet(cell, out GridDataService.TowerData currentData))
        {
            DespawnPlaced(cell);
            return;
        }

        if (dataService.TryGetDefinition(currentData.towerId, out TowerDefinitionSO newDef))
            ReplacePlaced(cell, newDef, existing != null ? existing.def : null);
        else
            DespawnPlaced(cell);
    }

    private void SpawnPlaced(Vector3Int cell, TowerDefinitionSO def)
    {
        if (def == null || def.prefab == null || root == null) return;
        if (_placed.ContainsKey(cell)) return;

        if (grid == null) return;
        Vector3 pos = GetPlacementPosition(def, cell);
        var tower = Instantiate(def.prefab, pos, Quaternion.identity);
        AttachToParent(tower.gameObject, root);
        tower.name = $"{def.id}_Tower";
        ApplyPanelLayer(tower.gameObject);

        Vector2Int cell2 = new Vector2Int(cell.x, cell.z);
        tower.SetCell(cell2);
        Vector2Int anchor = dataService != null ? ToCell2D(dataService.GetAnchorCell()) : Vector2Int.zero;
        tower.SetOffsetFromCenter(cell2 - anchor);

        FootprintMaskUtility.GetFootprintData(def, out FootprintMaskSO mask, out Vector2Int size, out Vector2Int pivot);
        tower.SetFootprint(size);
        var footprint = new List<Vector2Int>();
        FootprintMaskUtility.GetFootprintCells(mask, size, pivot, cell2, footprint);
        tower.SetOccupiedCells(footprint);

        if (isWorldVisualizer)
        {
            var resolvedScope = scope != null ? scope : RunScopeLocator.Current;
            tower.Construct(resolvedScope, def);
            resolvedScope?.Entities?.RegisterTower(tower);
            OccupyCells(footprint);
        }
        else
        {
            DisableGameplay(tower.gameObject);
        }

        _placed[cell] = new PlacedView
        {
            instance = tower.gameObject,
            renderers = tower.GetComponentsInChildren<Renderer>(true),
            tower = tower,
            def = def
        };
    }

    private void ReplacePlaced(Vector3Int cell, TowerDefinitionSO def, TowerDefinitionSO prevDef)
    {
        bool playTween = !isWorldVisualizer && playPanelUpgradeTween && IsDirectUpgrade(prevDef, def);
        DespawnPlaced(cell);
        SpawnPlaced(cell, def);
        if (playTween && _placed.TryGetValue(cell, out PlacedView view) && view != null)
            PlayPanelUpgradeTween(view.instance);
    }

    private void PlayPanelUpgradeTween(GameObject instance)
    {
        if (instance == null) return;
        float duration = Mathf.Max(0.01f, panelUpgradeTweenDuration);
        Transform target = instance.transform;
        Vector3 basePos = target.position;
        target.DOKill();
        float drop = Mathf.Max(0f, panelUpgradeDropHeight);
        target.position = basePos + Vector3.up * drop;
        target.DOMove(basePos, duration).SetEase(panelUpgradeTweenEase).SetUpdate(true);
    }

    private static bool IsDirectUpgrade(TowerDefinitionSO prevDef, TowerDefinitionSO nextDef)
    {
        if (prevDef == null || nextDef == null) return false;
        if (prevDef.upgradeNext == null) return false;
        return string.Equals(prevDef.upgradeNext.id, nextDef.id, StringComparison.Ordinal);
    }

    private void DespawnPlaced(Vector3Int cell)
    {
        if (!_placed.TryGetValue(cell, out PlacedView view)) return;
        _placed.Remove(cell);

        if (view != null && view.tower != null)
        {
            if (isWorldVisualizer)
            {
                ReleaseOccupiedCells(view.def, cell);
                view.tower.SuppressGridRelease();
                var resolvedScope = scope != null ? scope : RunScopeLocator.Current;
                resolvedScope?.Entities?.UnregisterTower(view.tower);
            }
            view.tower.gameObject.SetActive(false);
            Destroy(view.tower.gameObject);
            return;
        }

        if (view != null && view.instance != null)
        {
            view.instance.SetActive(false);
            Destroy(view.instance);
        }
    }

    private void DestroyAllPlaced()
    {
        foreach (var kvp in _placed)
        {
            if (kvp.Value != null && kvp.Value.instance != null)
            {
                if (isWorldVisualizer)
                {
                    ReleaseOccupiedCells(kvp.Value.def, kvp.Key);
                    if (kvp.Value.tower != null)
                    {
                        kvp.Value.tower.SuppressGridRelease();
                        var resolvedScope = scope != null ? scope : RunScopeLocator.Current;
                        resolvedScope?.Entities?.UnregisterTower(kvp.Value.tower);
                    }
                }
                kvp.Value.instance.SetActive(false);
                Destroy(kvp.Value.instance);
            }
        }
        _placed.Clear();
        _hasHidden = false;
    }

    private void EnsurePreview(TowerDefinitionSO def)
    {
        if (def == null || def.prefab == null) return;

        if (_previewInstance != null && _previewTowerId == def.id) return;

        ClearPreview();

        _previewInstance = Instantiate(def.prefab.gameObject);
        AttachToParent(_previewInstance, root);
        _previewInstance.name = "[PlacementPreview]";
        DisableGameplay(_previewInstance);
        _previewRenderers = _previewInstance.GetComponentsInChildren<Renderer>(true);
        _previewTowerId = def.id;
    }

    private void SetPreviewPosition(Vector3Int cell, TowerDefinitionSO def)
    {
        if (_previewInstance == null || def == null) return;
        Vector3 pos = GetPlacementPosition(def, cell);
        _previewInstance.transform.SetPositionAndRotation(pos, Quaternion.identity);
    }

    private void SetPreviewTint(bool canPlace)
    {
        if (_previewRenderers == null) return;
        Color c = canPlace ? canPlaceColor : cannotPlaceColor;

        int baseColor = Shader.PropertyToID("_BaseColor");
        int color = Shader.PropertyToID("_Color");

        for (int i = 0; i < _previewRenderers.Length; i++)
        {
            Renderer r = _previewRenderers[i];
            if (r == null) continue;
            r.GetPropertyBlock(_previewMpb);
            _previewMpb.SetColor(baseColor, c);
            _previewMpb.SetColor(color, c);
            r.SetPropertyBlock(_previewMpb);
        }
    }

    private void ClearPreview()
    {
        if (_previewInstance != null) Destroy(_previewInstance);
        _previewInstance = null;
        _previewRenderers = null;
        _previewTowerId = null;
        ResetGridPlaneColors();
    }

    private void HidePlacedAt(Vector3Int cell)
    {
        if (!_placed.TryGetValue(cell, out PlacedView view)) return;
        if (view == null || view.renderers == null) return;
        SetRenderersEnabled(view.renderers, false);
        _hiddenCell = cell;
        _hasHidden = true;
    }

    private void RestoreHiddenPlaced()
    {
        if (!_hasHidden) return;
        if (_placed.TryGetValue(_hiddenCell, out PlacedView view) && view != null && view.renderers != null)
            SetRenderersEnabled(view.renderers, true);
        _hasHidden = false;
    }

    private void SetRenderersEnabled(Renderer[] renderers, bool on)
    {
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].enabled = on;
    }

    private Vector3 GetPlacementPosition(TowerDefinitionSO def, Vector3Int cell)
    {
        if (grid == null) return Vector3.zero;
        Vector3 center = GetCellCenterWorld(cell);
        center += GetFootprintOffset(def);

        if (!isWorldVisualizer) return center;

        bool hasBasePlate = HasBasePlate(def);
        float groundY = center.y;
        if (!hasBasePlate && GameRoot.Instance != null)
        {
            float rayH = GameRoot.Instance.GroundRayHeight;
            Vector3 origin = new Vector3(center.x, rayH, center.z);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayH * 2f,
                    GameRoot.Instance.GroundMask, QueryTriggerInteraction.Ignore))
                groundY = hit.point.y;
        }

        float extra = GameRoot.Instance != null ? GameRoot.Instance.GroundExtraOffset : 0.02f;
        if (hasBasePlate) extra = 0f;

        float bottomOffset = 0f;
        if (!hasBasePlate && def != null && def.prefab != null)
        {
            var col = def.prefab.GetComponentInChildren<Collider>(true);
            if (col != null) bottomOffset = GetColliderBottomOffset(col, def.prefab.transform);
        }

        return new Vector3(center.x, groundY + bottomOffset + extra, center.z);
    }

    private Vector3 GetCellCenterWorld(Vector3Int cell)
    {
        Vector3 origin = grid.CellToWorld(cell);
        Vector3 size = grid.cellSize;
        return new Vector3(origin.x + size.x * 0.5f, origin.y, origin.z + size.z * 0.5f);
    }

    private Vector3 GetFootprintOffset(TowerDefinitionSO def)
    {
        if (def == null) return Vector3.zero;
        FootprintMaskUtility.GetFootprintData(def, out _, out Vector2Int size, out Vector2Int pivot);
        size.x = Mathf.Max(1, size.x);
        size.y = Mathf.Max(1, size.y);
        pivot.x = Mathf.Clamp(pivot.x, 0, size.x - 1);
        pivot.y = Mathf.Clamp(pivot.y, 0, size.y - 1);

        float ox = ((size.x - 1) * 0.5f - pivot.x) * grid.cellSize.x;
        float oz = ((size.y - 1) * 0.5f - pivot.y) * grid.cellSize.z;
        return new Vector3(ox, 0f, oz);
    }

    private void OccupyCells(List<Vector2Int> cells)
    {
        if (dataService == null || dataService.GridSystem == null) return;
        for (int i = 0; i < cells.Count; i++)
            dataService.GridSystem.TryOccupy(cells[i]);
    }

    private void ConfigurePanelRender()
    {
        if (isWorldVisualizer) return;
        if (!autoConfigurePanelCamera) return;

        int layer = GetPanelLayer();
        if (panelCamera != null)
        {
            if (panelTexture != null)
                panelCamera.targetTexture = panelTexture;
            if (layer >= 0)
                panelCamera.cullingMask = 1 << layer;
        }

        if (panelTargetImage != null && panelTexture != null)
            panelTargetImage.texture = panelTexture;
    }

    private void ApplyPanelGridCompensation()
    {
        if (isWorldVisualizer) return;
        if (!compensatePanelPitch) return;
        if (grid == null || panelCamera == null) return;

        float baseCellX = grid.cellSize.x;
        float baseCellZ = grid.cellSize.z;
        int w = 1;
        int h = 1;
        if (dataService != null && dataService.GridSystem != null)
        {
            baseCellX = dataService.GridSystem.CellSizeX;
            baseCellZ = dataService.GridSystem.CellSizeZ;
            w = dataService.GridSystem.Width;
            h = dataService.GridSystem.Height;
        }

        if (!_panelBaseCaptured || !Mathf.Approximately(_panelBaseCellZ, baseCellZ))
        {
            _panelBaseCellZ = baseCellZ;
            _panelBaseCenterOffsetZ = centerOffset.z;
            _panelBaseCaptured = true;
        }

        float panelAspect = panelCamera.aspect;
        bool hasRectAspect = false;
        float rectW = 0f;
        float rectH = 0f;
        Vector3 rectScale = Vector3.one;
        if (panelTargetImage != null)
        {
            Rect rect = panelTargetImage.rectTransform.rect;
            Vector3 scale = panelTargetImage.rectTransform.lossyScale;
            rectScale = scale;
            rectW = rect.width * Mathf.Abs(scale.x);
            rectH = rect.height * Mathf.Abs(scale.y);
            if (rectW > 0.01f && rectH > 0.01f)
            {
                panelAspect = rectW / rectH;
                hasRectAspect = true;
            }
        }
        if (!hasRectAspect && panelTexture != null && panelTexture.width > 0 && panelTexture.height > 0)
            panelAspect = panelTexture.width / (float)panelTexture.height;

        float aspectSafe = Mathf.Max(0.01f, panelAspect);
        float widthSafe = Mathf.Max(1, w);
        float heightSafe = Mathf.Max(1, h);
        float screenCellRatio = (1f / aspectSafe) * (widthSafe / heightSafe);
        PanelGridView panelGridView = controller != null ? controller.PanelGrid : null;
        if (panelGridView != null)
        {
            float cellW = panelGridView.CellWidth;
            float cellH = panelGridView.CellHeight;
            if (cellW > 0.01f && cellH > 0.01f)
                screenCellRatio = cellH / cellW;
        }

        Vector3 zInCam = panelCamera.transform.InverseTransformVector(Vector3.forward);
        Vector3 xInCam = panelCamera.transform.InverseTransformVector(Vector3.right);

        float zScale = Mathf.Abs(zInCam.y);
        float xScale = Mathf.Abs(xInCam.x);
        zScale = Mathf.Clamp(zScale, 0.01f, 1f);
        xScale = Mathf.Clamp(xScale, 0.01f, 1f);

        float newCellX = baseCellX;
        float newCellZ = baseCellX * screenCellRatio * (xScale / zScale);
        float factor = baseCellZ > 0.0001f ? newCellZ / baseCellZ : 1f;

        Vector3 size = grid.cellSize;
        size.x = newCellX;
        size.z = newCellZ;
        grid.cellSize = size;

        centerOffset.z = _panelBaseCenterOffsetZ * factor;

        if (panelCamera.orthographic)
        {
            float projectedHeight = heightSafe * newCellZ * zScale;
            float projectedWidth = widthSafe * newCellX * xScale;
            float targetOrtho = 0.5f * Mathf.Max(projectedHeight, projectedWidth / aspectSafe);
            if (!Mathf.Approximately(panelCamera.orthographicSize, targetOrtho))
                panelCamera.orthographicSize = targetOrtho;
        }

        bool changed = !_panelCompensatedSizeValid
                       || !Mathf.Approximately(_panelCompensatedCellX, newCellX)
                       || !Mathf.Approximately(_panelCompensatedCellZ, newCellZ);

        if (changed)
        {
            _panelCompensatedCellX = newCellX;
            _panelCompensatedCellZ = newCellZ;
            _panelCompensatedSizeValid = true;
            if (debugLogs)
            {
                string rectInfo = hasRectAspect
                    ? $"rect={rectW:F3}x{rectH:F3} scale={rectScale}"
                    : "rect=none";
                string panelGridInfo = panelGridView != null
                    ? $"panelCell={panelGridView.CellWidth:F3}x{panelGridView.CellHeight:F3}"
                    : "panelCell=none";
                Debug.Log(
                    $"[패널보정] aspect={panelAspect:F4} ratio={screenCellRatio:F4} base={baseCellX:F4},{baseCellZ:F4} new={newCellX:F4},{newCellZ:F4} grid={grid.cellSize} planeScale={gridPlaneScale} {rectInfo} {panelGridInfo}",
                    this);
            }
            RebuildGridPlanes();
            RebuildGridLines();
            MarkGridPlaneOverlayDirty();
        }
    }

    private void AlignPanelCameraToGrid()
    {
        if (isWorldVisualizer) return;
        if (!autoAlignPanelCamera) return;
        if (panelCamera == null || grid == null) return;

        int w = dataService != null && dataService.GridSystem != null ? dataService.GridSystem.Width : 1;
        int h = dataService != null && dataService.GridSystem != null ? dataService.GridSystem.Height : 1;

        Vector3 origin = grid.transform.position;
        Vector3 size = grid.cellSize;
        Vector3 center = origin + new Vector3(size.x * w * 0.5f, 0f, size.z * h * 0.5f);

        Vector3 forward = panelCamera.transform.forward;
        if (Mathf.Abs(forward.y) < 0.0001f) return;

        float y = panelCamera.transform.position.y;
        float t = (center.y - y) / forward.y;
        Vector3 pos = center - forward * t;
        pos.y = y;
        panelCamera.transform.position = pos;
    }

    private void CapturePanelBase()
    {
        if (_panelBaseCaptured) return;
        if (grid == null) return;
        _panelBaseCellZ = grid.cellSize.z;
        _panelBaseCenterOffsetZ = centerOffset.z;
        _panelBaseCaptured = true;
    }

    private void ResolvePanelLayer()
    {
        string resolvedName = panelLayerName != null ? panelLayerName.Trim() : null;
        if (string.Equals(_panelLayerNameCache, resolvedName, StringComparison.Ordinal) && _panelLayer >= 0)
            return;

        _panelLayerNameCache = resolvedName;
        _panelLayer = string.IsNullOrEmpty(resolvedName) ? -1 : LayerMask.NameToLayer(resolvedName);

        if (_panelLayer < 0 && !_invalidLayerWarned && !string.IsNullOrEmpty(resolvedName))
        {
            _invalidLayerWarned = true;
            Debug.LogWarning($"[PlacementVisualizer] PanelPreview 레이어를 찾을 수 없음: {resolvedName}", this);
        }
    }

    private int GetPanelLayer()
    {
        if (_panelLayer < 0 || !string.Equals(_panelLayerNameCache, panelLayerName, StringComparison.Ordinal))
            ResolvePanelLayer();
        return _panelLayer;
    }

    private void ApplyPanelLayer(GameObject go)
    {
        if (isWorldVisualizer) return;
        if (go == null) return;
        int layer = GetPanelLayer();
        if (layer < 0 && root != null)
            layer = root.gameObject.layer;
        if (layer < 0) return;
        SetLayerRecursively(go.transform, layer);
    }

    private void SetLayerRecursively(Transform rootTransform, int layer)
    {
        if (rootTransform == null) return;
        rootTransform.gameObject.layer = layer;
        for (int i = 0; i < rootTransform.childCount; i++)
            SetLayerRecursively(rootTransform.GetChild(i), layer);
    }

    private void RebuildGridPlanes()
    {
        if (isWorldVisualizer) return;
        ClearGridPlanes();
        if (gridPlanePrefab == null || grid == null) return;

        int w = dataService != null && dataService.GridSystem != null ? dataService.GridSystem.Width : 1;
        int h = dataService != null && dataService.GridSystem != null ? dataService.GridSystem.Height : 1;
        Transform parent = gridPlaneRoot != null ? gridPlaneRoot : root;
        if (parent == null) return;

        _gridPlaneRenderers.Clear();

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Vector3Int cell = new Vector3Int(x, 0, y);
                Vector3 pos = GetCellCenterWorld(cell);
                pos.y += gridPlaneY;
                GameObject tile = Instantiate(gridPlanePrefab, pos, Quaternion.identity);
                AttachToParent(tile, parent);
                tile.name = $"GridPlane_{x}_{y}";
                Vector3 baseScale = tile.transform.localScale;
                Vector3 scale = baseScale;
                if (matchGridCellSize)
                    scale = new Vector3(baseScale.x * grid.cellSize.x, baseScale.y, baseScale.z * grid.cellSize.z);
                scale = Vector3.Scale(scale, gridPlaneScale);
                tile.transform.localScale = scale;
                DisableGameplay(tile);
                _gridPlanes.Add(tile);

                Renderer renderer = tile.GetComponentInChildren<Renderer>(true);
                if (renderer != null)
                {
                    _gridPlaneRenderers[new Vector2Int(x, y)] = renderer;
                    ApplyGridPlaneColor(renderer, gridPlaneNeutralColor);
                }
            }
        }
        MarkGridPlaneOverlayDirty();
    }

    private void RebuildGridLines()
    {
        if (isWorldVisualizer) return;
        ClearGridLines();
        if (!drawPanelGridLines || grid == null) return;

        int w = dataService != null && dataService.GridSystem != null ? dataService.GridSystem.Width : 1;
        int h = dataService != null && dataService.GridSystem != null ? dataService.GridSystem.Height : 1;

        Transform parent = gridPlaneRoot != null ? gridPlaneRoot : root;
        EnsureGridLineRoot(parent);
        if (_gridLineRoot == null) return;

        Material mat = GetGridLineMaterial();
        if (mat == null) return;

        Vector3 origin = grid.transform.position;
        float sizeX = grid.cellSize.x;
        float sizeZ = grid.cellSize.z;
        float y = origin.y + gridLineY;

        for (int i = 0; i <= w; i++)
        {
            float x = origin.x + i * sizeX;
            AddGridLine(new Vector3(x, y, origin.z), new Vector3(x, y, origin.z + h * sizeZ), mat);
        }

        for (int j = 0; j <= h; j++)
        {
            float z = origin.z + j * sizeZ;
            AddGridLine(new Vector3(origin.x, y, z), new Vector3(origin.x + w * sizeX, y, z), mat);
        }
    }

    private void EnsureGridLineRoot(Transform parent)
    {
        if (_gridLineRoot != null) return;
        Transform resolved = ResolveParent(parent);
        if (resolved == null) return;

        var go = new GameObject("GridLines3D");
        _gridLineRoot = go.transform;
        _gridLineRoot.SetParent(resolved, false);
        ApplyPanelLayer(go);
    }

    private Material GetGridLineMaterial()
    {
        if (gridLineMaterial != null)
        {
            ApplyLineMaterialColor(gridLineMaterial);
            return gridLineMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) return null;

        gridLineMaterial = new Material(shader);
        gridLineMaterial.name = "PanelGridLine (Runtime)";
        ApplyLineMaterialColor(gridLineMaterial);
        return gridLineMaterial;
    }

    private void ApplyLineMaterialColor(Material mat)
    {
        if (mat == null) return;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", gridLineColor);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", gridLineColor);
    }

    private void AddGridLine(Vector3 a, Vector3 b, Material mat)
    {
        if (_gridLineRoot == null || mat == null) return;

        var go = new GameObject("GridLine");
        go.transform.SetParent(_gridLineRoot, false);
        ApplyPanelLayer(go);

        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.material = mat;
        lr.widthMultiplier = gridLineWidth;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.startColor = gridLineColor;
        lr.endColor = gridLineColor;
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);

        _gridLines.Add(lr);
    }

    private void ClearGridPlanes()
    {
        for (int i = 0; i < _gridPlanes.Count; i++)
        {
            if (_gridPlanes[i] != null)
                Destroy(_gridPlanes[i]);
        }
        _gridPlanes.Clear();
        _gridPlaneRenderers.Clear();
        _gridPlaneBaseColors.Clear();
        _roadCells.Clear();
        ClearPanelRoadTiles();
        ClearGridLines();
    }

    private void ClearGridLines()
    {
        for (int i = 0; i < _gridLines.Count; i++)
        {
            if (_gridLines[i] != null)
                Destroy(_gridLines[i].gameObject);
        }
        _gridLines.Clear();

        if (_gridLineRoot != null)
        {
            Destroy(_gridLineRoot.gameObject);
            _gridLineRoot = null;
        }
    }

    private void UpdateGridPlaneColors(Vector3Int cell, GridDataService.PlacementResult result)
    {
        if (isWorldVisualizer) return;
        if (!tintGridPlanesOnHover) return;
        if (result.previewDef == null) return;

        ResetGridPlaneColors();

        _hoverCells.Clear();
        FootprintMaskUtility.GetFootprintData(result.previewDef, out FootprintMaskSO mask, out Vector2Int size, out Vector2Int pivot);
        FootprintMaskUtility.GetFootprintCells(mask, size, pivot, ToCell2D(cell), _hoverCells);

        Color c = result.canPlace ? gridPlaneCanPlaceColor : gridPlaneCannotPlaceColor;
        for (int i = 0; i < _hoverCells.Count; i++)
        {
            if (_gridPlaneRenderers.TryGetValue(_hoverCells[i], out Renderer renderer))
                ApplyGridPlaneColor(renderer, c);
        }
    }

    private void ResetGridPlaneColors()
    {
        if (isWorldVisualizer) return;
        if (!tintGridPlanesOnHover) return;

        foreach (var kvp in _gridPlaneRenderers)
        {
            if (_gridPlaneBaseColors.TryGetValue(kvp.Key, out Color baseColor))
                ApplyGridPlaneColor(kvp.Value, baseColor);
            else
                ApplyGridPlaneColor(kvp.Value, gridPlaneNeutralColor);
        }
    }

    private void ApplyGridPlaneColor(Renderer renderer, Color color)
    {
        if (renderer == null) return;
        renderer.GetPropertyBlock(_gridPlaneMpb);
        if (renderer.sharedMaterial != null)
        {
            if (renderer.sharedMaterial.HasProperty("_BaseColor"))
                _gridPlaneMpb.SetColor("_BaseColor", color);
            if (renderer.sharedMaterial.HasProperty("_Color"))
                _gridPlaneMpb.SetColor("_Color", color);
        }
        renderer.SetPropertyBlock(_gridPlaneMpb);
    }

    private void EnsureCenterObject()
    {
        if (isWorldVisualizer) return;
        if (centerPrefab == null || grid == null) return;
        if (_centerInstance != null) return;
        if (root == null) return;

        Vector3Int anchor = dataService != null ? dataService.GetAnchorCell() : Vector3Int.zero;
        Vector3 target = GetCellCenterWorld(anchor);
        _centerInstance = Instantiate(centerPrefab, target, Quaternion.identity);
        AttachToParent(_centerInstance, root);
        _centerInstance.name = "[CenterObject]";
        DisableGameplay(_centerInstance);
        AlignCenterToGridAnchor(_centerInstance, target);
    }

    private void ClearCenterObject()
    {
        if (_centerInstance == null) return;
        Destroy(_centerInstance);
        _centerInstance = null;
    }

    private void AttachToParent(GameObject instance, Transform parent)
    {
        if (instance == null) return;

        Transform resolved = ResolveParent(parent);
        if (resolved == null) return;

        Scene parentScene = resolved.gameObject.scene;
        if (parentScene.IsValid() && instance.scene != parentScene)
            SceneManager.MoveGameObjectToScene(instance, parentScene);
        instance.transform.SetParent(resolved, true);
    }

    private Transform ResolveParent(Transform parent)
    {
        if (parent != null && parent.gameObject.scene.IsValid())
            return parent;

        if (transform != null && transform.gameObject.scene.IsValid())
        {
            WarnInvalidParent(parent);
            return transform;
        }

        WarnInvalidParent(parent);
        return null;
    }

    private void WarnInvalidParent(Transform parent)
    {
        if (_invalidParentWarned) return;
        _invalidParentWarned = true;
        string parentName = parent != null ? parent.name : "null";
        Debug.LogWarning(
            $"[PlacementVisualizer] 부모가 프리팹 에셋이라 연결할 수 없음. Hierarchy의 씬 오브젝트로 Root/GridPlaneRoot를 다시 지정하세요. parent={parentName}",
            this);
    }

    private void ReleaseOccupiedCells(TowerDefinitionSO def, Vector3Int cell)
    {
        if (dataService == null || dataService.GridSystem == null || def == null) return;
        _releaseCells.Clear();
        FootprintMaskUtility.GetFootprintData(def, out FootprintMaskSO mask, out Vector2Int size, out Vector2Int pivot);
        FootprintMaskUtility.GetFootprintCells(mask, size, pivot, ToCell2D(cell), _releaseCells);
        for (int i = 0; i < _releaseCells.Count; i++)
            dataService.GridSystem.Release(_releaseCells[i]);
    }

    private bool HasBasePlate(TowerDefinitionSO def)
    {
        if (def == null || def.prefab == null) return false;
        if (def.prefab.GetComponentInChildren<FootprintVisualBaker>(true) != null) return true;
        var list = def.prefab.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < list.Length; i++)
        {
            Transform t = list[i];
            if (t != null && t.name == "BasePlate")
                return true;
        }
        return false;
    }

    private static float GetColliderBottomOffset(Collider col, Transform tr)
    {
        float sy = tr.lossyScale.y;

        switch (col)
        {
            case CapsuleCollider cap:
                return (cap.height * 0.5f - cap.center.y) * sy;
            case BoxCollider box:
                return (box.size.y * 0.5f - box.center.y) * sy;
            case SphereCollider sph:
                return (sph.radius - sph.center.y) * sy;
            default:
                return col.bounds.extents.y;
        }
    }

    private static Vector2Int ToCell2D(Vector3Int cell) => new Vector2Int(cell.x, cell.z);

    private void MarkGridPlaneOverlayDirty()
    {
        if (isWorldVisualizer) return;
        _overlayDirty = true;
    }

    private void UpdateGridPlaneBaseColors()
    {
        if (isWorldVisualizer) return;
        bool hasGridPlanes = _gridPlaneRenderers.Count > 0;
        if (!hasGridPlanes && !spawnPanelRoadTiles) return;

        GridSystem gridSystem = dataService != null ? dataService.GridSystem : null;
        if (gridSystem == null)
        {
            if (hasGridPlanes)
            {
                _gridPlaneBaseColors.Clear();
                foreach (var kvp in _gridPlaneRenderers)
                {
                    _gridPlaneBaseColors[kvp.Key] = gridPlaneNeutralColor;
                    ApplyGridPlaneColor(kvp.Value, gridPlaneNeutralColor);
                }
            }
            ClearPanelRoadTiles();
            return;
        }

        int w = gridSystem.Width;
        int h = gridSystem.Height;

        if (showBuildableOverlay)
            EnsureOverlayBuffers(w, h);

        bool needRoadCells = showBuildableOverlay || showRoadOverlay || spawnPanelRoadTiles;
        _roadCells.Clear();
        if (needRoadCells)
        {
            RunScope resolvedScope = scope != null ? scope : RunScopeLocator.Current;
            IReadOnlyList<TowerEntity> towers = resolvedScope != null && resolvedScope.Entities != null ? resolvedScope.Entities.Towers : null;
            BaseFootprintReserver baseFootprint = resolvedScope != null ? resolvedScope.BaseFootprintReserver : null;
            Vector2Int anchor = dataService != null ? ToCell2D(dataService.GetAnchorCell()) : Vector2Int.zero;
            GridRoadUtility.BuildRoadCells(gridSystem, anchor, baseFootprint, towers, _roadCells);
        }

        if (showBuildableOverlay)
            BuildGridRules.ComputeBuildable(gridSystem, _buildable, null, _roadCells);

        if (hasGridPlanes)
        {
            _gridPlaneBaseColors.Clear();
            foreach (var kvp in _gridPlaneRenderers)
            {
                Vector2Int cell = kvp.Key;
                Color c = gridPlaneNeutralColor;

                if (showBuildableOverlay && _buildable != null)
                {
                    if (cell.x >= 0 && cell.x < w && cell.y >= 0 && cell.y < h)
                        c = _buildable[cell.x, cell.y] ? gridPlaneBuildableColor : gridPlaneUnbuildableColor;
                }

                if (showRoadOverlay && _roadCells.Contains(cell))
                    c = gridPlaneRoadColor;

                _gridPlaneBaseColors[cell] = c;
                ApplyGridPlaneColor(kvp.Value, c);
            }
        }

        UpdatePanelRoadTiles();
    }

    private void EnsureOverlayBuffers(int w, int h)
    {
        if (_buildable == null || _buildable.GetLength(0) != w || _buildable.GetLength(1) != h)
            _buildable = new bool[w, h];
    }

    private void AlignCenterToGridAnchor(GameObject instance, Vector3 target)
    {
        if (instance == null) return;
        Transform anchor = FindChildByName(instance.transform, "GridAnchor");
        if (anchor == null)
        {
            instance.transform.position = target + centerOffset;
            return;
        }

        Vector3 offset = anchor.position - instance.transform.position;
        instance.transform.position = target - offset + centerOffset;
    }

    private static Transform FindChildByName(Transform rootTransform, string targetName)
    {
        if (rootTransform == null || string.IsNullOrEmpty(targetName)) return null;
        Transform direct = rootTransform.Find(targetName);
        if (direct != null) return direct;

        var list = rootTransform.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < list.Length; i++)
        {
            Transform t = list[i];
            if (t == null || t == rootTransform) continue;
            if (t.name == targetName) return t;
        }

        return null;
    }

    private void UpdatePanelRoadTiles()
    {
        if (isWorldVisualizer) return;
        if (!spawnPanelRoadTiles)
        {
            ClearPanelRoadTiles();
            return;
        }
        if (grid == null)
        {
            ClearPanelRoadTiles();
            return;
        }

        if (_roadCells.Count == 0)
        {
            ClearPanelRoadTiles();
            return;
        }

        GameObject prefab = ResolvePanelRoadPrefab();
        if (prefab == null)
        {
            ClearPanelRoadTiles();
            return;
        }

        if (_panelRoadTilePrefabCache != prefab)
        {
            ClearPanelRoadTiles();
            _panelRoadTileMetricsCached = false;
            _panelRoadTileMetricsLogged = false;
            _panelRoadTilePrefabCache = prefab;
        }

        CachePanelRoadTileMetrics(prefab);
        GetPanelRoadTileTransform(out Vector3 tileScale, out Vector3 tileOffset);
        if (debugLogs && !_panelRoadTileMetricsLogged)
        {
            _panelRoadTileMetricsLogged = true;
            Debug.Log(
                $"[패널도로] 스케일={tileScale} 오프셋={tileOffset} 셀={grid.cellSize} 정규화={normalizePanelRoadToCell} 격자스케일={gridPlaneScale}",
                this);
        }
        Quaternion rot = Quaternion.Euler(panelRoadRotation);
        Vector3 rotatedOffset = rot * tileOffset;
        EnsurePanelRoadRoot();

        _panelRoadRemove.Clear();
        foreach (var kvp in _panelRoadTiles)
        {
            if (!_roadCells.Contains(kvp.Key))
                _panelRoadRemove.Add(kvp.Key);
        }

        for (int i = 0; i < _panelRoadRemove.Count; i++)
        {
            Vector2Int cell = _panelRoadRemove[i];
            if (_panelRoadTiles.TryGetValue(cell, out GameObject go))
            {
                if (go != null) Destroy(go);
                _panelRoadTiles.Remove(cell);
            }
        }

        foreach (var cell in _roadCells)
        {
            Vector3 pos = GetCellCenterWorld(new Vector3Int(cell.x, 0, cell.y));
            pos += rotatedOffset + new Vector3(panelRoadTileGridOffset.x, 0f, panelRoadTileGridOffset.y);
            pos.y += panelRoadTileYOffset + (usePanelRoadBottomOffset ? _panelRoadTileBottomOffset : 0f);

            if (_panelRoadTiles.TryGetValue(cell, out GameObject existing))
            {
                if (existing != null)
                {
                    existing.transform.SetPositionAndRotation(pos, rot);
                    existing.transform.localScale = tileScale;
                    continue;
                }
                _panelRoadTiles.Remove(cell);
            }

            var go = Instantiate(prefab, pos, rot);
            AttachToParent(go, panelRoadRoot != null ? panelRoadRoot : (gridPlaneRoot != null ? gridPlaneRoot : root));
            go.transform.localScale = tileScale;
            go.name = $"PanelRoad_{cell.x}_{cell.y}";
            DisableGameplay(go);
            _panelRoadTiles[cell] = go;
        }
    }

    private void ClearPanelRoadTiles()
    {
        foreach (var kvp in _panelRoadTiles)
        {
            if (kvp.Value != null) Destroy(kvp.Value);
        }
        _panelRoadTiles.Clear();
        _panelRoadRemove.Clear();
    }

    private void EnsurePanelRoadRoot()
    {
        if (panelRoadRoot != null) return;
        Transform parent = gridPlaneRoot != null ? gridPlaneRoot : root;
        Transform resolved = ResolveParent(parent);
        if (resolved == null) return;

        var go = new GameObject("PanelRoadTiles");
        panelRoadRoot = go.transform;
        panelRoadRoot.SetParent(resolved, false);
        ApplyPanelLayer(go);
    }

    private GameObject ResolvePanelRoadPrefab()
    {
        if (panelRoadTilePrefab != null) return panelRoadTilePrefab;
        if (!useBuildCellPrefabFallback) return null;
        return GameRoot.Instance != null ? GameRoot.Instance.BuildCellSizePrefab : null;
    }

    private void CachePanelRoadTileMetrics(GameObject prefab)
    {
        if (_panelRoadTileMetricsCached) return;
        _panelRoadTileMetricsCached = true;
        _panelRoadTileBottomOffset = 0f;
        _panelRoadTileHasBounds = false;
        _panelRoadTileCenter = Vector3.zero;
        _panelRoadTileBounds = default;
        _panelRoadTileBaseScale = prefab != null ? prefab.transform.localScale : Vector3.one;

        if (prefab == null) return;

        _panelRoadTileBounds = GetPrefabBounds(prefab);
        _panelRoadTileHasBounds = _panelRoadTileBounds.size.x > 0.0001f && _panelRoadTileBounds.size.z > 0.0001f;
        if (_panelRoadTileHasBounds && centerPanelRoadToCell)
            _panelRoadTileCenter = _panelRoadTileBounds.center;

        if (usePanelRoadBottomOffset && _panelRoadTileBounds.size.y > 0.0001f)
            _panelRoadTileBottomOffset = -_panelRoadTileBounds.min.y * Mathf.Max(0.01f, panelRoadTileScaleMultiplier);

        if (debugLogs)
        {
            string prefabName = prefab != null ? prefab.name : "null";
            Debug.Log(
                $"[패널도로] 프리팹={prefabName} baseScale={_panelRoadTileBaseScale} bounds={_panelRoadTileBounds.size} hasBounds={_panelRoadTileHasBounds} center={_panelRoadTileCenter} bottomOffset={_panelRoadTileBottomOffset:F4}",
                this);
        }
    }

    private void GetPanelRoadTileTransform(out Vector3 scale, out Vector3 offset)
    {
        scale = _panelRoadTileBaseScale;
        offset = Vector3.zero;

        float mul = Mathf.Max(0.01f, panelRoadTileScaleMultiplier);
        if (normalizePanelRoadToCell && _panelRoadTileHasBounds && grid != null)
        {
            float cellX = grid.cellSize.x;
            float cellZ = grid.cellSize.z;
            GridSystem gridSystem = dataService != null ? dataService.GridSystem : null;
            if (gridSystem != null)
            {
                cellX = gridSystem.CellSizeX;
                cellZ = gridSystem.CellSizeZ;
            }
            float sx = cellX / Mathf.Max(0.0001f, _panelRoadTileBounds.size.x);
            float sz = cellZ / Mathf.Max(0.0001f, _panelRoadTileBounds.size.z);
            scale = new Vector3(_panelRoadTileBaseScale.x * sx, _panelRoadTileBaseScale.y, _panelRoadTileBaseScale.z * sz) * mul;
            if (centerPanelRoadToCell)
                offset = new Vector3(-_panelRoadTileCenter.x * sx, 0f, -_panelRoadTileCenter.z * sz);
        }
        else
        {
            scale = _panelRoadTileBaseScale * mul;
            if (centerPanelRoadToCell && _panelRoadTileHasBounds)
                offset = new Vector3(-_panelRoadTileCenter.x * _panelRoadTileBaseScale.x, 0f, -_panelRoadTileCenter.z * _panelRoadTileBaseScale.z);
        }

        if (matchPanelRoadToGridPlaneScale)
        {
            scale = new Vector3(scale.x * gridPlaneScale.x, scale.y, scale.z * gridPlaneScale.z);
            offset = new Vector3(offset.x * gridPlaneScale.x, offset.y, offset.z * gridPlaneScale.z);
        }
    }

    private static Bounds GetPrefabBounds(GameObject prefab)
    {
        if (prefab == null) return new Bounds(Vector3.zero, Vector3.zero);
        var temp = Instantiate(prefab);
        temp.hideFlags = HideFlags.HideAndDontSave;
        temp.transform.position = Vector3.zero;
        temp.transform.rotation = Quaternion.identity;
        temp.transform.localScale = prefab.transform.localScale;

        var colliders = temp.GetComponentsInChildren<Collider>(true);
        Bounds b = GetBounds(colliders);
        if (b.size.sqrMagnitude < 0.0001f)
        {
            var renderers = temp.GetComponentsInChildren<Renderer>(true);
            b = GetBounds(renderers);
        }

        if (Application.isPlaying)
            Destroy(temp);
        else
            DestroyImmediate(temp);

        return b;
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
            else b.Encapsulate(r.bounds);
        }
        return b;
    }

    private static Bounds GetBounds(Collider[] colliders)
    {
        bool has = false;
        Bounds b = new Bounds(Vector3.zero, Vector3.zero);
        for (int i = 0; i < colliders.Length; i++)
        {
            var c = colliders[i];
            if (c == null) continue;
            if (!has)
            {
                b = c.bounds;
                has = true;
            }
            else b.Encapsulate(c.bounds);
        }
        return b;
    }

    private void DisableGameplay(GameObject go)
    {
        foreach (var t in go.GetComponentsInChildren<TowerEntity>(true))
            t.SuppressGridRelease();

        foreach (var c in go.GetComponentsInChildren<Collider>(true))
            c.enabled = false;

        foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            mb.enabled = false;

        if (!isWorldVisualizer)
        {
            ApplyPanelLayer(go);
            return;
        }

        int ignore = LayerMask.NameToLayer("Ignore Raycast");
        if (ignore >= 0)
            SetLayerRecursively(go.transform, ignore);
    }
}
