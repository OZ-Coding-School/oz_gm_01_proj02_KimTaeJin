using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public sealed class PanelPreview3D : MonoBehaviour
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
    [SerializeField] private Vector3 previewWorldOffset = new Vector3(10000f, 10000f, 10000f);
    [SerializeField] private float previewNearClip = 0.05f;
    [SerializeField] private float previewFarClip = 200f;

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

    [Header("Placed Parent")]
    [SerializeField] private bool useCenterGridAnchorForPlaced = true;
    [SerializeField] private string centerGridAnchorName = "GridAnchor";

    private GameObject _centerInstance;
    private Transform _centerGridAnchor;
    private GameObject _placementInstance;
    private Renderer[] _placementRenderers;
    private MaterialPropertyBlock _mpb;
    private Vector2Int _placementFootprint = Vector2Int.one;
    private Vector2Int _placementPivot = Vector2Int.zero;
    private Tween _placementMoveTween;
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
    private Vector2Int _placementCell;
    private bool _hasPlacementCell;
    private float _baseCellAspect = 1f;
    private Renderer[,] _tileRenderers;
    private MaterialPropertyBlock _tileMpb;
    private Vector2Int _centerPivot = Vector2Int.zero;
    private LayerMask _cachedPreviewLayerMask;
    private int _cachedPreviewLayerIndex = -1;
    private bool _previewLayerCached;

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
    }

    public void SyncFromGridView(PanelGridView grid)
    {
        if (grid == null) return;
        gridWidth = grid.Width;
        gridHeight = grid.Height;

        float cw = Mathf.Max(0.01f, grid.CellWidth);
        float ch = Mathf.Max(0.01f, grid.CellHeight);
        if (useGridViewCellAspect && useGridRootRectForAspect && gridRootRect != null)
        {
            float rectW = gridRootRect.rect.width;
            float rectH = gridRootRect.rect.height;
            if (rectW > 0f && rectH > 0f && gridWidth > 0 && gridHeight > 0)
            {
                cw = Mathf.Max(0.01f, rectW / gridWidth);
                ch = Mathf.Max(0.01f, rectH / gridHeight);
            }
        }
        float baseScale = Mathf.Max(0.01f, gridWorldScale);
        if (useGridCellSize && gridSystem != null)
            baseScale = Mathf.Max(0.01f, gridSystem.CellSizeX);
        cellWorldWidth = baseScale;
        if (useGridViewCellAspect)
        {
            _baseCellAspect = Mathf.Max(0.01f, ch / cw);
            cellWorldHeight = baseScale * _baseCellAspect * Mathf.Max(0.01f, cellHeightScale);
        }
        else
        {
            _baseCellAspect = 1f;
            cellWorldHeight = baseScale * Mathf.Max(0.01f, cellHeightScale);
        }
        ApplyCellAspectCompensation();

        FitRawImageToGrid();
        if (useWorldGridLines)
            RebuildGridLines();
        if (autoBuildTiles)
            RebuildTiles();
        ApplyCameraSettings();
    }

    public bool TryScreenToCell(Vector2 screen, Canvas canvas, Camera uiCamera, out Vector2Int cell)
    {
        cell = default;
        if (previewCamera == null || targetImage == null || canvas == null) return false;

        RectTransform rt = targetImage.rectTransform;
        if (rt == null) return false;

        Camera cam = (canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : uiCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screen, cam, out Vector2 local);

        Rect rect = rt.rect;
        if (rect.width <= 0f || rect.height <= 0f) return false;

        float u = (local.x - rect.xMin) / rect.width;
        float v = (local.y - rect.yMin) / rect.height;
        if (u < 0f || u > 1f || v < 0f || v > 1f) return false;

        Ray ray = previewCamera.ViewportPointToRay(new Vector3(u, v, 0f));
        Transform root = previewRoot != null ? previewRoot : transform;
        Vector3 planePoint = root.TransformPoint(new Vector3(0f, sceneOffset.y, 0f));
        Vector3 planeNormal = root.TransformDirection(Vector3.up);
        Plane plane = new Plane(planeNormal, planePoint);
        if (!plane.Raycast(ray, out float enter)) return false;

        Vector3 hit = ray.GetPoint(enter);
        Vector3 localHit = root.InverseTransformPoint(hit) - sceneOffset;
        return TryLocalToCell(localHit, out cell);
    }

    public void ShowCenter()
    {
        if (centerPrefab == null) return;
        if (_centerInstance != null)
        {
            RemoveCachedMetrics(_centerInstance.transform);
            Destroy(_centerInstance);
        }

        _centerInstance = Instantiate(centerPrefab, previewRoot);
        _centerInstance.name = "[CenterPreview]";
        PreparePreviewObject(_centerInstance);

        _centerInstance.transform.localRotation = Quaternion.Euler(centerRotation);
        CacheCenterGridAnchor();

        CacheBaseMetrics(_centerInstance);
        FitToFootprint(_centerInstance, centerFootprint);
        PlaceAtGridCenter(_centerInstance.transform);
        ApplyCenterPreviewOffset(_centerInstance.transform);
    }

    public void SyncPlacedTowers(IReadOnlyList<TowerEntity> towers)
    {
        ClearPlacementPreview();
        SetPlacementActive(false);
        if (towers == null || previewRoot == null)
        {
            ClearPlacedTowers();
            return;
        }

        if (debugPreviewPlacements)
            Debug.Log($"[PanelPreview3D] SyncPlacedTowers count={towers.Count} center={GetCenterCell()} mask={GetEffectivePreviewLayerMask().value}");

        _placedToRemove.Clear();

        for (int i = 0; i < towers.Count; i++)
        {
            var t = towers[i];
            if (t == null) continue;
            var def = t.Definition;
            if (def == null || def.prefab == null) continue;

            if (!_placedByTower.TryGetValue(t, out GameObject go) || go == null)
            {
                go = Instantiate(def.prefab.gameObject, previewRoot);
                go.name = $"[PlacedPreview]{def.id}";
                PreparePreviewObject(go);
                _placedByTower[t] = go;
                _placedInstances.Add(go);
            }

            var towerEntity = go.GetComponentInChildren<TowerEntity>();
            if (towerEntity != null) towerEntity.enabled = false;

            go.transform.localRotation = Quaternion.Euler(placementRotation);
            FootprintMaskUtility.GetFootprintData(def, out FootprintMaskSO mask, out Vector2Int size, out Vector2Int pivot);
            Vector2Int cell = GetCenterCell() + t.OffsetFromCenter;
            if (debugPreviewPlacements)
                Debug.Log($"[PanelPreview3D] {t.name} def={def.id} cell={t.Cell} offset={t.OffsetFromCenter} previewCell={cell}");
            CacheBaseMetrics(go);
            FitToFootprint(go, size);
            PlaceAtCell(go.transform, cell, size, pivot, GetPlacedYOffset());
            ReparentPlacedInstance(go);
            _placedCells[go.transform] = cell;
            _placedFootprints[go.transform] = size;
            _placedPivots[go.transform] = pivot;

            go.SetActive(true);
        }

        foreach (var kvp in _placedByTower)
        {
            if (kvp.Key == null || kvp.Value == null)
            {
                _placedToRemove.Add(kvp.Key);
                continue;
            }

            bool stillExists = false;
            for (int i = 0; i < towers.Count; i++)
            {
                if (towers[i] == kvp.Key)
                {
                    stillExists = true;
                    break;
                }
            }
            if (!stillExists)
                _placedToRemove.Add(kvp.Key);
        }

        for (int i = 0; i < _placedToRemove.Count; i++)
        {
            TowerEntity t = _placedToRemove[i];
            if (!_placedByTower.TryGetValue(t, out GameObject go)) continue;
            if (go != null)
            {
                go.SetActive(false);
                RemoveCachedMetrics(go.transform);
                _placedCells.Remove(go.transform);
                _placedFootprints.Remove(go.transform);
                _placedPivots.Remove(go.transform);
                _placedInstances.Remove(go);
                Destroy(go);
            }
            _placedByTower.Remove(t);
        }
    }

    public void ClearPlacedTowers()
    {
        for (int i = 0; i < _placedInstances.Count; i++)
        {
            var go = _placedInstances[i];
            if (go == null) continue;
            // Hide immediately to avoid one-frame overlap before Destroy completes.
            go.SetActive(false);
            RemoveCachedMetrics(go.transform);
            Destroy(go);
        }
        _placedInstances.Clear();
        _placedCells.Clear();
        _placedFootprints.Clear();
        _placedPivots.Clear();
        _placedByTower.Clear();
        _placedToRemove.Clear();
    }

    public void SetPlacementPrefab(GameObject prefab)
    {
        if (!showPlacementPreview)
        {
            ClearPlacementPreview();
            return;
        }
        if (_placementInstance != null)
        {
            RemoveCachedMetrics(_placementInstance.transform);
            Destroy(_placementInstance);
        }
        _placementRenderers = null;
        _placementMoveTween?.Kill();

        if (prefab == null) return;

        _placementInstance = Instantiate(prefab, previewRoot);
        _placementInstance.name = "[PlacementPreview]";
        PreparePreviewObject(_placementInstance);
        _placementInstance.transform.localRotation = Quaternion.Euler(placementRotation);
        ReparentPlacedInstance(_placementInstance);

        _placementRenderers = _placementInstance.GetComponentsInChildren<Renderer>(true);
        CacheBaseMetrics(_placementInstance);
        FitToFootprint(_placementInstance, _placementFootprint);
        _placementInstance.SetActive(false);
    }

    public void ClearPlacementPreview()
    {
        if (_placementInstance != null)
        {
            RemoveCachedMetrics(_placementInstance.transform);
            Destroy(_placementInstance);
        }
        _placementInstance = null;
        _placementRenderers = null;
        _placementMoveTween?.Kill();
        _hasPlacementCell = false;
    }

    public void SetPlacementRotation(Vector3 euler)
    {
        placementRotation = euler;
        if (_placementInstance != null)
            _placementInstance.transform.localRotation = Quaternion.Euler(placementRotation);
    }

    public void SetPlacementFootprint(FootprintMaskSO mask, Vector2Int size, Vector2Int pivot)
    {
        _placementFootprint = size;
        _placementPivot = pivot;
        if (_placementInstance != null)
            FitToFootprint(_placementInstance, _placementFootprint);
    }

    public void SetCenterFootprint(Vector2Int footprint, bool biasPositive)
    {
        centerFootprint = new Vector2Int(Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y));
        evenFootprintBiasPositive = biasPositive;
        if (_centerInstance != null)
        {
            _centerPivot = Vector2Int.zero;
            FitToFootprint(_centerInstance, centerFootprint);
            PlaceAtGridCenter(_centerInstance.transform);
            ApplyCenterPreviewOffset(_centerInstance.transform);
        }
    }

    public void SetCenterFootprint(FootprintMaskSO mask, bool biasPositive)
    {
        if (mask == null || !mask.IsValid)
        {
            SetCenterFootprint(Vector2Int.one, biasPositive);
            return;
        }

        centerFootprint = mask.Size;
        _centerPivot = mask.Pivot;
        evenFootprintBiasPositive = biasPositive;

        if (_centerInstance != null)
        {
            FitToFootprint(_centerInstance, centerFootprint);
            PlaceAtGridCenter(_centerInstance.transform);
            ApplyCenterPreviewOffset(_centerInstance.transform);
        }
    }

    public void SetPlacementActive(bool on)
    {
        if (!showPlacementPreview) on = false;
        if (_placementInstance != null)
            _placementInstance.SetActive(on);
    }

    public void SetPlacementCell(Vector2Int cell, bool smooth = true)
    {
        if (!showPlacementPreview || _placementInstance == null) return;
        _placementCell = cell;
        _hasPlacementCell = true;
        Vector3 target = GetCellLocalPosition(cell, _placementFootprint, _placementPivot, _placementInstance.transform);
        _placementMoveTween?.Kill();
        if (smooth)
        {
            _placementMoveTween = _placementInstance.transform.DOLocalMove(target, placementMoveDuration)
                .SetEase(placementMoveEase)
                .SetUpdate(true);
        }
        else
        {
            _placementInstance.transform.localPosition = target;
        }
    }

    public void SetPlacementTint(Color color)
    {
        if (!showPlacementPreview) return;
        if (_placementRenderers == null || _placementRenderers.Length == 0) return;

        for (int i = 0; i < _placementRenderers.Length; i++)
        {
            var r = _placementRenderers[i];
            if (r == null) continue;
            ApplyColor(r, _mpb, color);
        }
    }

    public bool TryCommitPlacementPreview(TowerEntity tower, Vector2Int cell, Vector2Int footprint, Vector2Int pivot)
    {
        if (_placementInstance == null || tower == null || previewRoot == null) return false;

        _placementMoveTween?.Kill();
        _placementMoveTween = null;

        if (_placementRenderers != null)
        {
            for (int i = 0; i < _placementRenderers.Length; i++)
            {
                var r = _placementRenderers[i];
                if (r == null) continue;
                r.SetPropertyBlock(null);
            }
        }

        GameObject go = _placementInstance;
        _placementInstance = null;
        _placementRenderers = null;
        _hasPlacementCell = false;

        if (_placedByTower.TryGetValue(tower, out GameObject existing) && existing != null && existing != go)
        {
            existing.SetActive(false);
            RemoveCachedMetrics(existing.transform);
            _placedCells.Remove(existing.transform);
            _placedFootprints.Remove(existing.transform);
            _placedPivots.Remove(existing.transform);
            _placedInstances.Remove(existing);
            Destroy(existing);
        }

        _placedByTower[tower] = go;
        if (!_placedInstances.Contains(go))
            _placedInstances.Add(go);

        string id = tower.Definition != null ? tower.Definition.id : tower.name;
        go.name = $"[PlacedPreview]{id}";
        go.transform.localRotation = Quaternion.Euler(placementRotation);
        CacheBaseMetrics(go);
        FitToFootprint(go, footprint);
        PlaceAtCell(go.transform, cell, footprint, pivot, GetPlacedYOffset());
        ReparentPlacedInstance(go);

        _placedCells[go.transform] = cell;
        _placedFootprints[go.transform] = footprint;
        _placedPivots[go.transform] = pivot;

        go.SetActive(true);
        return true;
    }

    private static void ApplyColor(Renderer r, MaterialPropertyBlock mpb, Color color)
    {
        if (r == null || mpb == null) return;
        int baseColor = Shader.PropertyToID("_BaseColor");
        int c = Shader.PropertyToID("_Color");
        r.GetPropertyBlock(mpb);
        mpb.SetColor(baseColor, color);
        mpb.SetColor(c, color);
        r.SetPropertyBlock(mpb);
    }

    private Vector2Int GetCenterCell()
        => CenterCell;

    private void PlaceAtCell(Transform t, Vector2Int cell, Vector2Int footprint)
    {
        PlaceAtCell(t, cell, footprint, Vector2Int.zero, 0f);
    }

    private void PlaceAtCell(Transform t, Vector2Int cell, Vector2Int footprint, Vector2Int pivot)
    {
        PlaceAtCell(t, cell, footprint, pivot, 0f);
    }

    private void PlaceAtCell(Transform t, Vector2Int cell, Vector2Int footprint, Vector2Int pivot, float yOffset)
    {
        Vector3 pos = GetCellLocalPosition(cell, footprint, pivot, t);
        if (!Mathf.Approximately(yOffset, 0f))
            pos.y += yOffset;
        t.localPosition = pos;
    }

    private void PlaceAtGridCenter(Transform t)
    {
        Vector2Int anchor = GetCenteredFootprintAnchor(centerFootprint, _centerPivot);
        PlaceAtCell(t, anchor, centerFootprint, _centerPivot, 0f);
    }

    private void ApplyCenterPreviewOffset(Transform t)
    {
        if (t == null) return;
        float yOffset = centerPreviewYOffset;
        if (matchCenterToRoadHeight)
            yOffset += GetRoadTileLift();
        Vector3 offset = centerPreviewOffset;
        if (!Mathf.Approximately(yOffset, 0f))
            offset += Vector3.up * yOffset;
        if (offset == Vector3.zero) return;
        t.localPosition += offset;
    }

    private float GetPlacedYOffset()
    {
        float yOffset = placedPreviewYOffset;
        if (matchPlacedToRoadHeight)
            yOffset += GetRoadTileLift();
        return yOffset;
    }

    private float GetRoadTileLift()
    {
        GameObject prefab = roadTilePrefab != null ? roadTilePrefab : tilePrefab;
        if (prefab == null) return 0f;

        float yOffset = roadMatchGridTileSettings ? tileYOffset : roadTileYOffset;
        if (!roadUseBottomOffset) return yOffset;

        Bounds b = GetPrefabBounds(prefab);
        if (b.size.y <= 0.0001f) return yOffset;
        return yOffset + (-b.min.y);
    }

    private void CacheCenterGridAnchor()
    {
        _centerGridAnchor = null;
        if (_centerInstance == null || string.IsNullOrEmpty(centerGridAnchorName)) return;
        _centerGridAnchor = FindChildByName(_centerInstance.transform, centerGridAnchorName);
    }

    private Transform GetPlacedParent()
    {
        if (useCenterGridAnchorForPlaced && _centerGridAnchor != null)
            return _centerGridAnchor;
        return previewRoot;
    }

    private void ReparentPlacedInstance(GameObject go)
    {
        if (go == null) return;
        Transform parent = GetPlacedParent();
        if (parent == null || go.transform.parent == parent) return;
        go.transform.SetParent(parent, true);
    }

    private Vector3 CellToWorld(Vector2Int cell)
    {
        float totalW = gridWidth * cellWorldWidth;
        float totalH = gridHeight * cellWorldHeight;

        float x = -totalW * 0.5f + (cell.x + 0.5f) * cellWorldWidth;
        float z = -totalH * 0.5f + (cell.y + 0.5f) * cellWorldHeight;
        return new Vector3(x, 0f, z);
    }

    private Vector3 CellToWorld(Vector2Int cell, float cellWidth, float cellHeight)
    {
        float totalW = gridWidth * cellWidth;
        float totalH = gridHeight * cellHeight;

        float x = -totalW * 0.5f + (cell.x + 0.5f) * cellWidth;
        float z = -totalH * 0.5f + (cell.y + 0.5f) * cellHeight;
        return new Vector3(x, 0f, z);
    }

    private Vector3 GetCellWorldPosition(Vector2Int cell, Vector2Int footprint, Vector2Int pivot, Transform t)
    {
        Vector3 pos = CellToWorld(cell);
        Vector3 footprintOffset = GetFootprintOffset(footprint, pivot);

        var renderers = GetRenderersForBounds(t, out _);
        Bounds b = GetBounds(renderers);
        Vector3 localCenter = t.InverseTransformPoint(b.center);
        Vector3 localExtents = t.InverseTransformVector(b.extents);
        float bottomOffset = -(localCenter.y - localExtents.y);
        Vector3 centerOffset = new Vector3(-localCenter.x, 0f, -localCenter.z);

        return pos + footprintOffset + centerOffset + new Vector3(0f, bottomOffset, 0f) + sceneOffset;
    }

    private Vector3 GetCellLocalPosition(Vector2Int cell, Vector2Int footprint, Vector2Int pivot, Transform t)
    {
        Vector3 previewLocal = GetCellWorldPosition(cell, footprint, pivot, t);
        if (t == null) return previewLocal;

        Transform parent = t.parent;
        if (parent == null || previewRoot == null || parent == previewRoot)
            return previewLocal;

        Vector3 world = previewRoot.TransformPoint(previewLocal);
        return parent.InverseTransformPoint(world);
    }

    private Vector3 GetFootprintOffset(Vector2Int footprint, Vector2Int pivot)
    {
        if (footprint.x < 1) footprint.x = 1;
        if (footprint.y < 1) footprint.y = 1;
        pivot.x = Mathf.Clamp(pivot.x, 0, footprint.x - 1);
        pivot.y = Mathf.Clamp(pivot.y, 0, footprint.y - 1);
        float ox = ((footprint.x - 1) * 0.5f - pivot.x) * cellWorldWidth;
        float oz = ((footprint.y - 1) * 0.5f - pivot.y) * cellWorldHeight;
        return new Vector3(ox, 0f, oz);
    }

    private Vector2Int GetCenteredFootprintAnchor(Vector2Int footprint, Vector2Int pivot)
    {
        if (footprint.x < 1) footprint.x = 1;
        if (footprint.y < 1) footprint.y = 1;
        pivot.x = Mathf.Clamp(pivot.x, 0, footprint.x - 1);
        pivot.y = Mathf.Clamp(pivot.y, 0, footprint.y - 1);

        int maxX = Mathf.Max(0, gridWidth - footprint.x);
        int maxY = Mathf.Max(0, gridHeight - footprint.y);
        int x = Mathf.FloorToInt((gridWidth - footprint.x) * 0.5f);
        int y = Mathf.FloorToInt((gridHeight - footprint.y) * 0.5f);
        if (evenFootprintBiasPositive && (footprint.x % 2 == 0)) x += 1;
        if (evenFootprintBiasPositive && (footprint.y % 2 == 0)) y += 1;
        x = Mathf.Clamp(x, 0, maxX);
        y = Mathf.Clamp(y, 0, maxY);
        int ax = Mathf.Clamp(x + pivot.x, 0, gridWidth - 1);
        int ay = Mathf.Clamp(y + pivot.y, 0, gridHeight - 1);
        return new Vector2Int(ax, ay);
    }

    private void ApplyCellAspectCompensation()
    {
        if (!compensateTiltForSquareCells || !useOrthographic || lockCameraTopDown) return;
        if (cellWorldWidth <= 0.0001f) return;

        float pitch = Mathf.Abs(cameraEuler.x);
        if (previewCamera != null && !autoSetupCamera)
            pitch = Mathf.Abs(previewCamera.transform.localEulerAngles.x);

        float cos = Mathf.Cos(pitch * Mathf.Deg2Rad);
        if (cos < 0.0001f) return;

        float newHeight = cellWorldWidth * (_baseCellAspect / cos) * Mathf.Max(0.01f, cellHeightScale);
        if (Mathf.Approximately(newHeight, cellWorldHeight)) return;

        cellWorldHeight = newHeight;
        if (useWorldGridLines)
            RebuildGridLines();
        if (autoBuildTiles)
            RebuildTiles();
        RefreshInstances();
    }

    private void RefreshInstances()
    {
        if (_centerInstance != null)
        {
            FitToFootprint(_centerInstance, centerFootprint);
            PlaceAtGridCenter(_centerInstance.transform);
            ApplyCenterPreviewOffset(_centerInstance.transform);
        }

        if (_placementInstance != null)
        {
            FitToFootprint(_placementInstance, _placementFootprint);
            if (_hasPlacementCell)
                SetPlacementCell(_placementCell, false);
        }

        if (_placedCells.Count == 0) return;
        foreach (var kvp in _placedCells)
        {
            var t = kvp.Key;
            if (t == null) continue;
            Vector2Int footprint = Vector2Int.one;
            if (_placedFootprints.TryGetValue(t, out Vector2Int fp))
                footprint = fp;
            Vector2Int pivot = Vector2Int.zero;
            if (_placedPivots.TryGetValue(t, out Vector2Int p))
                pivot = p;
            FitToFootprint(t.gameObject, footprint);
            PlaceAtCell(t, kvp.Value, footprint, pivot, GetPlacedYOffset());
        }
    }

    private bool TryLocalToCell(Vector3 local, out Vector2Int cell)
    {
        cell = default;
        float totalW = gridWidth * cellWorldWidth;
        float totalH = gridHeight * cellWorldHeight;

        float lx = local.x + totalW * 0.5f;
        float lz = local.z + totalH * 0.5f;

        if (lx < 0f || lx >= totalW || lz < 0f || lz >= totalH)
            return false;

        int x = Mathf.FloorToInt(lx / cellWorldWidth);
        int y = Mathf.FloorToInt(lz / cellWorldHeight);
        cell = new Vector2Int(x, y);
        return true;
    }

    private void FitToFootprint(GameObject go, Vector2Int footprint)
    {
        if (!autoScaleToFootprint) return;
        if (go == null) return;
        if (footprint.x <= 0) footprint.x = 1;
        if (footprint.y <= 0) footprint.y = 1;

        Transform boundsRoot;
        var renderers = GetRenderersForBounds(go.transform, out boundsRoot);
        Bounds b = GetBaseBounds(boundsRoot, renderers);

        if (b.size.x <= 0.0001f || b.size.z <= 0.0001f) return;

        Transform scaleTarget = go.transform;
        bool scaleFootprint = false;
        if (scaleFootprintNode && TryGetFootprintNode(go.transform, out Transform node))
        {
            scaleTarget = node;
            scaleFootprint = true;
        }

        if (!_baseScales.TryGetValue(scaleTarget, out Vector3 baseScale))
            baseScale = scaleTarget.localScale;

        float targetX = footprint.x * cellWorldWidth;
        float targetZ = footprint.y * cellWorldHeight;

        if (scaleFootprint)
        {
            // Non-uniform scale keeps rectangular cell aspect aligned to the grid.
            float sx = targetX / b.size.x;
            float sz = targetZ / b.size.z;
            Vector3 scaled = new Vector3(baseScale.x * sx, baseScale.y, baseScale.z * sz);
            scaled *= Mathf.Max(0.01f, previewScale);
            scaleTarget.localScale = scaled;
        }
        else
        {
            float scale = Mathf.Min(targetX / b.size.x, targetZ / b.size.z);
            float finalScale = scale * Mathf.Max(0.01f, previewScale);
            Vector3 scaled = baseScale * finalScale;
            if (scaleTarget == go.transform && !Mathf.Approximately(previewHeightScale, 1f))
                scaled.y *= previewHeightScale;
            scaleTarget.localScale = scaled;
        }

        if (scaleTarget != go.transform && !Mathf.Approximately(previewHeightScale, 1f))
        {
            Transform root = go.transform;
            if (_baseScales.TryGetValue(root, out Vector3 rootBase))
            {
                Vector3 rootScale = root.localScale;
                rootScale.y = rootBase.y * previewHeightScale;
                root.localScale = rootScale;
            }
        }
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

    private static Bounds GetPrefabBounds(GameObject prefab)
    {
        if (prefab == null) return new Bounds(Vector3.zero, Vector3.zero);
        var temp = Instantiate(prefab);
        temp.hideFlags = HideFlags.HideAndDontSave;
        temp.transform.position = Vector3.zero;
        temp.transform.rotation = Quaternion.identity;
        temp.transform.localScale = prefab.transform.localScale;

        var renderers = temp.GetComponentsInChildren<Renderer>(true);
        Bounds b = GetBounds(renderers);

        if (Application.isPlaying)
            Destroy(temp);
        else
            DestroyImmediate(temp);

        return b;
    }

    private void CacheBaseMetrics(GameObject go)
    {
        if (go == null) return;
        Transform t = go.transform;
        if (!_baseScales.ContainsKey(t))
            _baseScales[t] = t.localScale;
        if (!_baseBounds.ContainsKey(t))
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            _baseBounds[t] = GetBounds(renderers);
        }

        if (TryGetFootprintNode(t, out Transform node))
        {
            if (!_baseScales.ContainsKey(node))
                _baseScales[node] = node.localScale;
            if (!_baseBounds.ContainsKey(node))
            {
                var renderers = node.GetComponentsInChildren<Renderer>(true);
                _baseBounds[node] = GetBounds(renderers);
            }
        }
    }

    private Bounds GetBaseBounds(Transform t, Renderer[] renderers)
    {
        if (t != null && _baseBounds.TryGetValue(t, out Bounds b))
            return b;

        b = GetBounds(renderers);
        if (t != null)
            _baseBounds[t] = b;
        return b;
    }

    private void RemoveCachedMetrics(Transform t)
    {
        if (t == null) return;
        _baseScales.Remove(t);
        _baseBounds.Remove(t);
    }

    private bool TryGetFootprintNode(Transform root, out Transform node)
    {
        node = null;
        if (!useFootprintNodeForBounds || root == null) return false;
        if (string.IsNullOrWhiteSpace(footprintNodeName)) return false;

        if (TryFindNodeByName(root, footprintNodeName, out node))
            return true;

        if (!string.Equals(footprintNodeName, "BasePlate", System.StringComparison.OrdinalIgnoreCase)
            && TryFindNodeByName(root, "BasePlate", out node))
            return true;

        return false;
    }

    private static bool TryFindNodeByName(Transform root, string name, out Transform node)
    {
        node = null;
        if (root == null || string.IsNullOrWhiteSpace(name)) return false;

        var list = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < list.Length; i++)
        {
            var t = list[i];
            if (t != null && t.name == name)
            {
                node = t;
                return true;
            }
        }

        return false;
    }

    private Renderer[] GetRenderersForBounds(Transform root, out Transform boundsRoot)
    {
        if (TryGetFootprintNode(root, out Transform node))
        {
            boundsRoot = node;
            return node.GetComponentsInChildren<Renderer>(true);
        }

        boundsRoot = root;
        return root != null ? root.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
    }

    private void SetupRenderTarget()
    {
        if (previewCamera == null || targetImage == null) return;

        if (renderTexture == null)
            renderTexture = new RenderTexture(1024, 1024, 16, RenderTextureFormat.ARGB32);

        previewCamera.targetTexture = renderTexture;
        targetImage.texture = renderTexture;
        if (renderTexture != null)
            previewCamera.aspect = (float)renderTexture.width / Mathf.Max(1f, renderTexture.height);
    }

    private void FitRawImageToGrid()
    {
        if (!autoFitRawImageToGrid || targetImage == null || gridRootRect == null) return;
        RectTransform rt = targetImage.rectTransform;
        if (rt == null) return;

        if (rt.parent != gridRootRect)
            rt.SetParent(gridRootRect, false);

        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.SetSiblingIndex(0);
    }

    private void UpdateRenderTextureFromTarget()
    {
        if (previewCamera == null || targetImage == null) return;
        var rt = targetImage.rectTransform;
        Vector2 size = rt.rect.size;
        if (size.x < 1f || size.y < 1f) return;

        int w = Mathf.Clamp(Mathf.RoundToInt(size.x * Mathf.Max(0.1f, renderTextureScale)), renderTextureMinSize, renderTextureMaxSize);
        int h = Mathf.Clamp(Mathf.RoundToInt(size.y * Mathf.Max(0.1f, renderTextureScale)), renderTextureMinSize, renderTextureMaxSize);
        if (_rtSize.x == w && _rtSize.y == h && renderTexture != null) return;

        _rtSize = new Vector2Int(w, h);
        if (renderTexture != null)
            renderTexture.Release();

        renderTexture = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32);
        previewCamera.targetTexture = renderTexture;
        targetImage.texture = renderTexture;
        previewCamera.aspect = (float)w / Mathf.Max(1f, h);
        ApplyCameraSettings();
    }

    private void ApplyCameraSettings()
    {
        EnsurePreviewRoot();
        if (previewCamera == null) return;
        if (previewCamera.GetComponent<PlayAreaFogIgnore>() == null)
            previewCamera.gameObject.AddComponent<PlayAreaFogIgnore>();
        if (isolatePreviewWorld)
        {
            previewCamera.nearClipPlane = Mathf.Max(0.01f, previewNearClip);
            previewCamera.farClipPlane = Mathf.Max(previewCamera.nearClipPlane + 1f, previewFarClip);
        }
        previewCamera.orthographic = useOrthographic;
        if (useOrthographic)
        {
            if (autoFitToGrid)
            {
                float halfH = (gridHeight * cellWorldHeight) * 0.5f;
                float halfW = (gridWidth * cellWorldWidth) * 0.5f;
                float aspect = Mathf.Max(0.01f, previewCamera.aspect);
                float fit = Mathf.Max(halfH, halfW / aspect) * Mathf.Max(1f, fitPadding);
                previewCamera.orthographicSize = fit;
            }
            else
            {
                previewCamera.orthographicSize = (gridHeight * cellWorldHeight) * 0.5f;
            }
        }
        else
        {
            previewCamera.fieldOfView = Mathf.Clamp(perspectiveFov, 10f, 80f);
        }
        previewCamera.cullingMask = GetPreviewLayerMask();
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        bool useTransparent = transparentBackground && !ShouldForceOpaqueBackground();
        if (useTransparent)
        {
            previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        }
        else
        {
            Color bg = previewBackgroundColor;
            bg.a = 1f;
            previewCamera.backgroundColor = bg;
        }
        ApplyTargetImageTint(useTransparent);
        ApplyPreviewLayerExclusion();

        bool forceRig = isolatePreviewWorld;
        if ((autoSetupCamera || forceRig) && previewRoot != null)
        {
            previewCamera.transform.SetParent(previewRoot, false);
            if (autoSetupCamera && lockCameraTopDown)
            {
                previewCamera.transform.localPosition = new Vector3(0f, Mathf.Max(0.1f, topDownHeight), 0f);
                previewCamera.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            else if (autoSetupCamera)
            {
                previewCamera.transform.localPosition = cameraOffset;
                if (cameraEuler != Vector3.zero)
                    previewCamera.transform.localRotation = Quaternion.Euler(cameraEuler);
                else
                    previewCamera.transform.LookAt(previewRoot.position + cameraLookAtOffset);
            }
        }

        if ((autoSetupCamera || forceRig) && previewLight != null && previewRoot != null)
        {
            previewLight.transform.SetParent(previewRoot, false);
            if (autoSetupCamera && lockCameraTopDown)
            {
                previewLight.transform.localPosition = new Vector3(0f, Mathf.Max(0.1f, topDownHeight), 0f);
                previewLight.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            else if (autoSetupCamera)
            {
                previewLight.transform.localPosition = cameraOffset;
                if (cameraEuler != Vector3.zero)
                    previewLight.transform.localRotation = Quaternion.Euler(cameraEuler);
                else
                    previewLight.transform.LookAt(previewRoot.position + cameraLookAtOffset);
            }
        }

        ApplyPerspectiveFitToGrid();
        ApplyCellAspectCompensation();
    }

    private void ApplyPerspectiveFitToGrid()
    {
        if (previewCamera == null || previewRoot == null) return;
        if (!autoSetupCamera || useOrthographic || !autoFitToGrid || lockCameraTopDown) return;

        if (!TryGetPreviewBounds(out Bounds bounds)) return;

        float padding = Mathf.Max(1f, fitPadding);
        bounds.extents *= padding;

        float fov = Mathf.Deg2Rad * Mathf.Clamp(previewCamera.fieldOfView, 10f, 80f);
        float tanHalfV = Mathf.Tan(fov * 0.5f);
        float aspect = Mathf.Max(0.01f, previewCamera.aspect);
        float tanHalfH = tanHalfV * aspect;

        Vector3[] corners = GetBoundsCorners(bounds);
        float maxDelta = 0f;
        float near = Mathf.Max(0.01f, previewCamera.nearClipPlane);

        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 p = previewCamera.transform.InverseTransformPoint(corners[i]);
            float reqZ = Mathf.Max(Mathf.Abs(p.x) / tanHalfH, Mathf.Abs(p.y) / tanHalfV);
            maxDelta = Mathf.Max(maxDelta, reqZ - p.z);
            maxDelta = Mathf.Max(maxDelta, near - p.z);
        }

        if (maxDelta > 0.001f)
        {
            Vector3 back = previewCamera.transform.forward * maxDelta;
            previewCamera.transform.position -= back;
            if (previewLight != null)
                previewLight.transform.position -= back;
        }
    }

    private float GetMaxPreviewHeight()
    {
        if (previewRoot == null) return 0f;
        float rootY = previewRoot.position.y;
        float max = 0f;

        if (_placementInstance != null)
            AccumulateHeight(_placementInstance, rootY, ref max);

        if (_centerInstance != null)
            AccumulateHeight(_centerInstance, rootY, ref max);

        for (int i = 0; i < _placedInstances.Count; i++)
        {
            var go = _placedInstances[i];
            if (go == null) continue;
            AccumulateHeight(go, rootY, ref max);
        }

        return Mathf.Max(0f, max);
    }

    private static void AccumulateHeight(GameObject go, float rootY, ref float maxHeight)
    {
        if (go == null) return;
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0) return;
        Bounds b = GetBounds(renderers);
        float h = b.max.y - rootY;
        if (h > maxHeight)
            maxHeight = h;
    }

    private void RebuildGridLines()
    {
        if (!useWorldGridLines) return;
        if (previewRoot == null) return;

        EnsureLineRoot();
        if (_lineRoot == null) return;

        for (int i = _lineRoot.childCount - 1; i >= 0; i--)
            Destroy(_lineRoot.GetChild(i).gameObject);

        float totalW = gridWidth * cellWorldWidth;
        float totalH = gridHeight * cellWorldHeight;
        float halfW = totalW * 0.5f;
        float halfH = totalH * 0.5f;
        float y = lineYOffset;

        for (int x = 0; x <= gridWidth; x++)
        {
            float px = -halfW + x * cellWorldWidth;
            Vector3 a = new Vector3(px, y, -halfH);
            Vector3 b = new Vector3(px, y, halfH);
            CreateLine(a, b);
        }

        for (int yIdx = 0; yIdx <= gridHeight; yIdx++)
        {
            float pz = -halfH + yIdx * cellWorldHeight;
            Vector3 a = new Vector3(-halfW, y, pz);
            Vector3 b = new Vector3(halfW, y, pz);
            CreateLine(a, b);
        }
    }

    public void SetTileStates(bool[,] buildable, bool[,] occupied)
    {
        if (_tileRenderers == null) return;

        int w = _tileRenderers.GetLength(0);
        int h = _tileRenderers.GetLength(1);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var r = _tileRenderers[x, y];
                if (r == null) continue;

                bool isOccupied = occupied != null && occupied[x, y];
                bool isBuildable = buildable != null && buildable[x, y];

                Color c = isOccupied ? tileOccupiedColor : (isBuildable ? tileNormalColor : tileBlockedColor);
                ApplyColor(r, _tileMpb, c);
            }
        }
    }

    public void SetRoadCells(IReadOnlyCollection<Vector2Int> cells)
    {
        ClearRoadTiles();
        if (cells == null || cells.Count == 0) return;
        if (roadTilePrefab == null || previewRoot == null) return;

        EnsureRoadRoot();
        if (roadRoot == null) return;

        Vector3 baseScale = roadTilePrefab.transform.localScale;
        Bounds tileBounds = default;
        Vector3 tileCenter = Vector3.zero;
        bool hasTileBounds = false;

        float scale = roadMatchGridTileSettings ? tileGridScale : roadTileScale;
        Vector2 gridOffset = roadMatchGridTileSettings ? tileGridOffset : roadTileGridOffset;
        float yOffset = roadMatchGridTileSettings ? tileYOffset : roadTileYOffset;

        float tileScale = Mathf.Max(0.01f, scale);
        float tileCellWidth = cellWorldWidth * tileScale;
        float tileCellHeight = cellWorldHeight * tileScale;

        if (normalizeTileToCell)
        {
            tileBounds = GetPrefabBounds(roadTilePrefab);
            hasTileBounds = tileBounds.size.x > 0.0001f && tileBounds.size.z > 0.0001f;
            if (centerTileToCell && hasTileBounds)
                tileCenter = tileBounds.center;
        }
        float bottomOffset = (roadUseBottomOffset && hasTileBounds) ? -tileBounds.min.y : 0f;

        foreach (var cell in cells)
        {
            var go = Instantiate(roadTilePrefab, roadRoot);
            go.name = $"[RoadTile]{cell.x}_{cell.y}";
            PreparePreviewObject(go);

            Vector3 pos = CellToWorld(cell, tileCellWidth, tileCellHeight) + sceneOffset;
            Vector3 offset = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            if (normalizeTileToCell && hasTileBounds)
            {
                float sx = tileCellWidth / Mathf.Max(0.0001f, tileBounds.size.x);
                float sz = tileCellHeight / Mathf.Max(0.0001f, tileBounds.size.z);
                go.transform.localScale = new Vector3(baseScale.x * sx, baseScale.y, baseScale.z * sz);
                if (centerTileToCell)
                    offset = new Vector3(-tileCenter.x * sx, 0f, -tileCenter.z * sz);
            }
            else
            {
                go.transform.localScale = new Vector3(baseScale.x * tileCellWidth, baseScale.y, baseScale.z * tileCellHeight);
                if (centerTileToCell && hasTileBounds)
                    offset = new Vector3(-tileCenter.x * baseScale.x, 0f, -tileCenter.z * baseScale.z);
            }

            go.transform.localPosition = pos + new Vector3(gridOffset.x, yOffset + bottomOffset, gridOffset.y) + offset;
            _roadInstances.Add(go);
        }
    }

    public void ClearRoadTiles()
    {
        if (_roadInstances.Count == 0) return;

        for (int i = 0; i < _roadInstances.Count; i++)
        {
            var go = _roadInstances[i];
            if (go == null) continue;
            go.SetActive(false);
            Destroy(go);
        }
        _roadInstances.Clear();
    }

    private void RebuildTiles()
    {
        if (!autoBuildTiles || tilePrefab == null || previewRoot == null) return;
        EnsureTileRoot();
        ClearTiles();

        _tileRenderers = new Renderer[gridWidth, gridHeight];
        Vector3 baseScale = tilePrefab.transform.localScale;
        Bounds tileBounds = default;
        Vector3 tileCenter = Vector3.zero;
        bool hasTileBounds = false;
        float tileScale = Mathf.Max(0.01f, tileGridScale);
        float tileCellWidth = cellWorldWidth * tileScale;
        float tileCellHeight = cellWorldHeight * tileScale;
        if (normalizeTileToCell)
        {
            tileBounds = GetPrefabBounds(tilePrefab);
            hasTileBounds = tileBounds.size.x > 0.0001f && tileBounds.size.z > 0.0001f;
            if (centerTileToCell && hasTileBounds)
                tileCenter = tileBounds.center;
        }

        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                var go = Instantiate(tilePrefab, tileRoot);
                go.name = $"[Tile]{x}_{y}";
                PreparePreviewObject(go);

                Vector3 pos = CellToWorld(new Vector2Int(x, y), tileCellWidth, tileCellHeight) + sceneOffset;
                Vector3 offset = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                if (normalizeTileToCell && hasTileBounds)
                {
                    float sx = tileCellWidth / Mathf.Max(0.0001f, tileBounds.size.x);
                    float sz = tileCellHeight / Mathf.Max(0.0001f, tileBounds.size.z);
                    go.transform.localScale = new Vector3(baseScale.x * sx, baseScale.y, baseScale.z * sz);
                    if (centerTileToCell)
                        offset = new Vector3(-tileCenter.x * sx, 0f, -tileCenter.z * sz);
                }
                else
                {
                    go.transform.localScale = new Vector3(baseScale.x * tileCellWidth, baseScale.y, baseScale.z * tileCellHeight);
                    if (centerTileToCell && hasTileBounds)
                        offset = new Vector3(-tileCenter.x * baseScale.x, 0f, -tileCenter.z * baseScale.z);
                }
                go.transform.localPosition = pos + new Vector3(tileGridOffset.x, tileYOffset, tileGridOffset.y) + offset;

                var renderer = go.GetComponentInChildren<Renderer>();
                _tileRenderers[x, y] = renderer;
                if (renderer != null)
                    ApplyColor(renderer, _tileMpb, tileNormalColor);
            }
        }
    }

    private void ClearTiles()
    {
        if (tileRoot == null) return;
        for (int i = tileRoot.childCount - 1; i >= 0; i--)
            Destroy(tileRoot.GetChild(i).gameObject);
        _tileRenderers = null;
    }

    private void EnsureTileRoot()
    {
        if (tileRoot != null) return;
        var go = new GameObject("[PreviewTiles]");
        tileRoot = go.transform;
        tileRoot.SetParent(previewRoot, false);
        PreparePreviewObject(tileRoot.gameObject);
    }

    private void EnsureRoadRoot()
    {
        if (roadRoot != null) return;
        var go = new GameObject("[PreviewRoadTiles]");
        roadRoot = go.transform;
        roadRoot.SetParent(previewRoot, false);
        PreparePreviewObject(roadRoot.gameObject);
    }

    private void EnsureLineRoot()
    {
        if (_lineRoot != null) return;
        var go = new GameObject("[PreviewGridLines]");
        _lineRoot = go.transform;
        _lineRoot.SetParent(previewRoot, false);
        PreparePreviewObject(_lineRoot.gameObject);
    }

    private void CreateLine(Vector3 a, Vector3 b)
    {
        var go = new GameObject("L");
        go.transform.SetParent(_lineRoot, false);
        go.layer = GetPreviewLayerIndex();

        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.useWorldSpace = false;
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.numCapVertices = 0;
        lr.numCornerVertices = 0;

        if (lineMaterial == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            lineMaterial = new Material(shader);
        }

        lr.material = lineMaterial;
        lr.startColor = lineColor;
        lr.endColor = lineColor;
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);
    }

    private void EnsurePreviewRoot()
    {
        bool needDetached = useDetachedPreviewRoot || isolatePreviewWorld;
        if (previewRoot != null && previewRoot is RectTransform)
            needDetached = true;

        if (needDetached)
        {
            if (_runtimeRoot == null)
            {
                var go = new GameObject("[PanelPreview3D_Root]");
                _runtimeRoot = go.transform;
            }
            if (isolatePreviewWorld)
                _runtimeRoot.position = previewWorldOffset;
            previewRoot = _runtimeRoot;
            return;
        }

        if (previewRoot == null)
        {
            var go = new GameObject("PreviewRoot");
            go.transform.SetParent(transform, false);
            previewRoot = go.transform;
        }
    }

    private void ApplyPreviewLayerExclusion()
    {
        if (!excludePreviewLayerFromMainCamera) return;
        LayerMask mask = GetPreviewLayerMask();
        if (mask.value == 0) return;

        Camera cam = Camera.main;
        if (cam == null || cam == previewCamera) return;
        cam.cullingMask &= ~mask.value;
    }

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        if (!excludePreviewLayerFromMainCamera) return;
        if (cam == null || cam == previewCamera) return;
        LayerMask mask = GetPreviewLayerMask();
        if (mask.value == 0) return;
        cam.cullingMask &= ~mask.value;
    }

    private void OnDestroy()
    {
        if (_runtimeRoot != null)
            Destroy(_runtimeRoot.gameObject);
    }

    private void PreparePreviewObject(GameObject go)
    {
        if (go == null) return;
        SetLayerRecursive(go, GetPreviewLayerIndex());
        if (!stripPreviewComponents) return;
        StripPreviewComponents(go);
    }

    private static void StripPreviewComponents(GameObject root)
    {
        if (root == null) return;

        var monos = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < monos.Length; i++)
        {
            var mb = monos[i];
            if (mb == null) continue;
            if (Application.isPlaying)
                Destroy(mb);
            else
                DestroyImmediate(mb);
        }

        var colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            var col = colliders[i];
            if (col == null) continue;
            if (Application.isPlaying)
                Destroy(col);
            else
                DestroyImmediate(col);
        }

        var rbs = root.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rbs.Length; i++)
        {
            var rb = rbs[i];
            if (rb == null) continue;
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }

        var audios = root.GetComponentsInChildren<AudioSource>(true);
        for (int i = 0; i < audios.Length; i++)
        {
            var audio = audios[i];
            if (audio != null) audio.enabled = false;
        }

        var agents = root.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true);
        for (int i = 0; i < agents.Length; i++)
        {
            var agent = agents[i];
            if (agent == null) continue;
            if (Application.isPlaying)
                Destroy(agent);
            else
                DestroyImmediate(agent);
        }
    }

    private int GetPreviewLayerIndex()
    {
        EnsurePreviewLayerCache();
        return _cachedPreviewLayerIndex;
    }

    private LayerMask GetPreviewLayerMask()
    {
        EnsurePreviewLayerCache();
        return _cachedPreviewLayerMask;
    }

    private void EnsurePreviewLayerCache()
    {
        if (_previewLayerCached) return;
        _cachedPreviewLayerMask = GetEffectivePreviewLayerMask();
        _cachedPreviewLayerIndex = GetFirstLayerIndex(_cachedPreviewLayerMask.value);
        _previewLayerCached = true;
    }

    private LayerMask GetEffectivePreviewLayerMask()
    {
        if (!autoIsolatePreviewLayer) return previewLayer;

        int stashLayer = LayerMask.NameToLayer("Stash");
        if (stashLayer >= 0 && stashLayer < 32)
            return 1 << stashLayer;

        int mask = previewLayer.value;
        int worldTowerMask = GetWorldTowerLayerMask();

        if (mask != 0)
        {
            int isolatedIndex = GetIsolatedPreviewLayerIndex(mask);
            if (isolatedIndex >= 1 && (worldTowerMask & (1 << isolatedIndex)) == 0)
                return 1 << isolatedIndex;
        }

        int fallback = Mathf.Clamp(fallbackPreviewLayerIndex, 1, 31);
        if ((worldTowerMask & (1 << fallback)) == 0)
            return 1 << fallback;

        for (int i = 31; i >= 1; i--)
        {
            if ((worldTowerMask & (1 << i)) == 0)
                return 1 << i;
        }

        return 1 << fallback;
    }

    private bool ShouldForceOpaqueBackground()
    {
        int stashLayer = LayerMask.NameToLayer("Stash");
        if (stashLayer < 0 || stashLayer > 31) return false;
        int mask = GetPreviewLayerMask().value;
        return (mask & (1 << stashLayer)) != 0;
    }

    private bool TryGetPreviewBounds(out Bounds bounds)
    {
        bounds = new Bounds();
        if (previewRoot == null) return false;

        var renderers = previewRoot.GetComponentsInChildren<Renderer>(true);
        if (renderers != null && renderers.Length > 0)
        {
            bounds = GetBounds(renderers);
            return bounds.size.sqrMagnitude > 0.0001f;
        }

        float totalW = gridWidth * cellWorldWidth;
        float totalH = gridHeight * cellWorldHeight;
        Vector3 size = new Vector3(Mathf.Max(0.1f, totalW), 0.1f, Mathf.Max(0.1f, totalH));
        bounds = new Bounds(previewRoot.position + sceneOffset, size);
        return true;
    }

    private static Vector3[] GetBoundsCorners(Bounds b)
    {
        Vector3 c = b.center;
        Vector3 e = b.extents;
        return new[]
        {
            c + new Vector3(-e.x, -e.y, -e.z),
            c + new Vector3(-e.x, -e.y,  e.z),
            c + new Vector3(-e.x,  e.y, -e.z),
            c + new Vector3(-e.x,  e.y,  e.z),
            c + new Vector3( e.x, -e.y, -e.z),
            c + new Vector3( e.x, -e.y,  e.z),
            c + new Vector3( e.x,  e.y, -e.z),
            c + new Vector3( e.x,  e.y,  e.z),
        };
    }

    private void ApplyTargetImageTint(bool useTransparent)
    {
        if (targetImage == null || useTransparent) return;
        Color c = targetImage.color;
        if (c.a >= 0.999f) return;
        c.a = 1f;
        targetImage.color = c;
    }

    private static int GetIsolatedPreviewLayerIndex(int mask)
    {
        for (int i = 31; i >= 1; i--)
        {
            if ((mask & (1 << i)) != 0)
                return i;
        }
        return -1;
    }

    private static int GetFirstLayerIndex(int mask)
    {
        if (mask == 0) return 0;
        for (int i = 0; i < 32; i++)
            if ((mask & (1 << i)) != 0) return i;
        return 0;
    }

    private int GetWorldTowerLayerMask()
    {
        int mask = 0;
        var towers = FindObjectsOfType<TowerEntity>(true);
        for (int i = 0; i < towers.Length; i++)
        {
            var tower = towers[i];
            if (tower == null) continue;
            if (previewRoot != null && tower.transform.IsChildOf(previewRoot)) continue;
            mask |= 1 << tower.gameObject.layer;
        }
        return mask;
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        if (go == null) return;
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
    }

    private static Transform FindChildByName(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name)) return null;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t != null && string.Equals(t.name, name, System.StringComparison.OrdinalIgnoreCase))
                return t;
        }
        return null;
    }
}
