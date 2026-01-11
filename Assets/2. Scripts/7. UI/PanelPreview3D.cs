using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class PanelPreview3D : MonoBehaviour
{
    [Header("Render")]
    [SerializeField] private Camera previewCamera;
    [SerializeField] private RawImage targetImage;
    [SerializeField] private RenderTexture renderTexture;
    [SerializeField] private LayerMask previewLayer;
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

    [Header("Grid")]
    [SerializeField] private int gridWidth = 9;
    [SerializeField] private int gridHeight = 10;
    [SerializeField] private float cellWorldWidth = 1f;
    [SerializeField] private float cellWorldHeight = 1f;
    [SerializeField] private float gridWorldScale = 1f;
    [SerializeField] private float previewScale = 1f;

    [Header("Grid Lines")]
    [SerializeField] private bool useWorldGridLines = true;
    [SerializeField] private Material lineMaterial;
    [SerializeField] private float lineWidth = 0.03f;
    [SerializeField] private float lineYOffset = 0.01f;
    [SerializeField] private Color lineColor = new Color(1f, 1f, 1f, 0.8f);

    [Header("Center Building")]
    [SerializeField] private GameObject centerPrefab;
    [SerializeField] private Vector2Int centerFootprint = new Vector2Int(2, 2);
    [SerializeField] private Vector3 centerRotation = Vector3.zero;
    [SerializeField] private Vector3 placementRotation = Vector3.zero;

    [Header("Placement Move")]
    [SerializeField] private float placementMoveDuration = 0.12f;
    [SerializeField] private Ease placementMoveEase = Ease.OutQuad;

    private GameObject _centerInstance;
    private GameObject _placementInstance;
    private Renderer[] _placementRenderers;
    private MaterialPropertyBlock _mpb;
    private Vector2Int _placementFootprint = Vector2Int.one;
    private Tween _placementMoveTween;
    private Transform _runtimeRoot;
    private Vector2Int _rtSize;
    private Transform _lineRoot;

    private void Awake()
    {
        EnsurePreviewRoot();
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        SetupRenderTarget();
        ApplyCameraSettings();
    }
    
    private void LateUpdate()
    {
        if (autoResizeRenderTexture)
            UpdateRenderTextureFromTarget();
    }

    public void SyncFromGridView(PanelGridView grid)
    {
        if (grid == null) return;
        gridWidth = grid.Width;
        gridHeight = grid.Height;

        float cw = Mathf.Max(0.01f, grid.CellWidth);
        float ch = Mathf.Max(0.01f, grid.CellHeight);
        float baseScale = Mathf.Max(0.01f, gridWorldScale);
        cellWorldWidth = baseScale;
        cellWorldHeight = baseScale * (ch / cw);

        FitRawImageToGrid();
        if (useWorldGridLines)
            RebuildGridLines();
        ApplyCameraSettings();
    }

    public void ShowCenter()
    {
        if (centerPrefab == null) return;
        if (_centerInstance != null) Destroy(_centerInstance);

        _centerInstance = Instantiate(centerPrefab, previewRoot);
        _centerInstance.name = "[CenterPreview]";
        SetLayerRecursive(_centerInstance, GetPreviewLayerIndex());

        _centerInstance.transform.localRotation = Quaternion.Euler(centerRotation);

        FitToFootprint(_centerInstance, centerFootprint);
        PlaceAtGridCenter(_centerInstance.transform);
    }

    public void SetPlacementPrefab(GameObject prefab)
    {
        if (_placementInstance != null) Destroy(_placementInstance);
        _placementRenderers = null;

        if (prefab == null) return;

        _placementInstance = Instantiate(prefab, previewRoot);
        _placementInstance.name = "[PlacementPreview]";
        SetLayerRecursive(_placementInstance, GetPreviewLayerIndex());
        _placementInstance.transform.localRotation = Quaternion.Euler(placementRotation);

        _placementRenderers = _placementInstance.GetComponentsInChildren<Renderer>(true);
        FitToFootprint(_placementInstance, _placementFootprint);
        _placementInstance.SetActive(false);
    }

    public void SetPlacementRotation(Vector3 euler)
    {
        placementRotation = euler;
        if (_placementInstance != null)
            _placementInstance.transform.localRotation = Quaternion.Euler(placementRotation);
    }

    public void SetPlacementFootprint(Vector2Int footprint)
    {
        _placementFootprint = footprint;
        if (_placementInstance != null)
            FitToFootprint(_placementInstance, _placementFootprint);
    }

    public void SetPlacementActive(bool on)
    {
        if (_placementInstance != null)
            _placementInstance.SetActive(on);
    }

    public void SetPlacementCell(Vector2Int cell, bool smooth = true)
    {
        if (_placementInstance == null) return;
        Vector3 target = GetCellWorldPosition(cell, _placementInstance.transform);
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
        if (_placementRenderers == null || _placementRenderers.Length == 0) return;

        int baseColor = Shader.PropertyToID("_BaseColor");
        int c = Shader.PropertyToID("_Color");

        for (int i = 0; i < _placementRenderers.Length; i++)
        {
            var r = _placementRenderers[i];
            if (r == null) continue;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(baseColor, color);
            _mpb.SetColor(c, color);
            r.SetPropertyBlock(_mpb);
        }
    }

    private Vector2Int GetCenterCell()
        => new Vector2Int(gridWidth / 2, gridHeight / 2);

    private void PlaceAtCell(Transform t, Vector2Int cell)
    {
        t.localPosition = GetCellWorldPosition(cell, t);
    }

    private void PlaceAtGridCenter(Transform t)
    {
        Vector3 pos = new Vector3(0f, 0f, 0f) + sceneOffset;

        var renderers = t.GetComponentsInChildren<Renderer>(true);
        Bounds b = GetBounds(renderers);
        float bottomOffset = -b.min.y;

        t.localPosition = pos + new Vector3(0f, bottomOffset, 0f);
    }

    private Vector3 CellToWorld(Vector2Int cell)
    {
        float totalW = gridWidth * cellWorldWidth;
        float totalH = gridHeight * cellWorldHeight;

        float x = -totalW * 0.5f + (cell.x + 0.5f) * cellWorldWidth;
        float z = -totalH * 0.5f + (cell.y + 0.5f) * cellWorldHeight;
        return new Vector3(x, 0f, z);
    }

    private Vector3 GetCellWorldPosition(Vector2Int cell, Transform t)
    {
        Vector3 pos = CellToWorld(cell);

        var renderers = t.GetComponentsInChildren<Renderer>(true);
        Bounds b = GetBounds(renderers);
        float bottomOffset = -b.min.y;

        return pos + new Vector3(0f, bottomOffset, 0f) + sceneOffset;
    }

    private void FitToFootprint(GameObject go, Vector2Int footprint)
    {
        if (go == null) return;
        if (footprint.x <= 0) footprint.x = 1;
        if (footprint.y <= 0) footprint.y = 1;

        var renderers = go.GetComponentsInChildren<Renderer>(true);
        Bounds b = GetBounds(renderers);

        if (b.size.x <= 0.0001f || b.size.z <= 0.0001f) return;

        float targetX = footprint.x * cellWorldWidth;
        float targetZ = footprint.y * cellWorldHeight;
        float scale = Mathf.Min(targetX / b.size.x, targetZ / b.size.z);

        float finalScale = scale * Mathf.Max(0.01f, previewScale);
        go.transform.localScale = go.transform.localScale * finalScale;
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
        if (previewCamera == null) return;
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
        previewCamera.cullingMask = previewLayer;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);

        if (autoSetupCamera && previewRoot != null)
        {
            previewCamera.transform.SetParent(previewRoot, false);
            if (lockCameraTopDown)
            {
                previewCamera.transform.localPosition = new Vector3(0f, Mathf.Max(0.1f, topDownHeight), 0f);
                previewCamera.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            else
            {
                previewCamera.transform.localPosition = cameraOffset;
                if (cameraEuler != Vector3.zero)
                    previewCamera.transform.localRotation = Quaternion.Euler(cameraEuler);
                else
                    previewCamera.transform.LookAt(previewRoot.position + cameraLookAtOffset);
            }
        }
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

    private void EnsureLineRoot()
    {
        if (_lineRoot != null) return;
        var go = new GameObject("[PreviewGridLines]");
        _lineRoot = go.transform;
        _lineRoot.SetParent(previewRoot, false);
        SetLayerRecursive(_lineRoot.gameObject, GetPreviewLayerIndex());
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
        bool needDetached = useDetachedPreviewRoot;
        if (previewRoot != null && previewRoot is RectTransform)
            needDetached = true;

        if (needDetached)
        {
            if (_runtimeRoot == null)
            {
                var go = new GameObject("[PanelPreview3D_Root]");
                _runtimeRoot = go.transform;
            }
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

    private void OnDestroy()
    {
        if (_runtimeRoot != null)
            Destroy(_runtimeRoot.gameObject);
    }

    private int GetPreviewLayerIndex()
    {
        int mask = previewLayer.value;
        if (mask == 0) return 0;
        for (int i = 0; i < 32; i++)
            if ((mask & (1 << i)) != 0) return i;
        return 0;
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        if (go == null) return;
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
    }
}
