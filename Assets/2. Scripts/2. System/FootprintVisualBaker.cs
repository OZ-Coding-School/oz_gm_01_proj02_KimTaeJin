using UnityEngine;

[DisallowMultipleComponent]
public sealed class FootprintVisualBaker : MonoBehaviour
{
    [Header("Mask")]
    [SerializeField] private FootprintMaskSO mask;
    [SerializeField] private bool useGridCellSize = true;
    [SerializeField] private GridSystem grid;
    [SerializeField] private float cellSize = 1f;

    [Header("Base Plate")]
    [SerializeField] private Transform basePlateRoot;
    [SerializeField] private GameObject baseTilePrefab;
    [SerializeField] private Vector3 baseTileOffset = Vector3.zero;
    [SerializeField] private bool normalizeTileToCell = true;
    [SerializeField] private bool centerTileToCell = true;
    [SerializeField] private float tileScaleMultiplier = 1f;

    [Header("Grid Anchor (Auto Center)")]
    [SerializeField] private Transform gridAnchor;
    [SerializeField] private bool autoPlaceGridAnchor = true;
    [SerializeField] private bool createGridAnchorIfMissing = true;
    [SerializeField] private Vector3 gridAnchorOffset = Vector3.zero;

    [Header("Bounds Tiles (Optional)")]
    [SerializeField] private bool buildBoundsTiles = false;
    [SerializeField] private Transform boundsRoot;
    [SerializeField] private Vector3 boundsTileOffset = Vector3.zero;
    [SerializeField] private float boundsTileHeight = 0.02f;
    [SerializeField] private bool hideBoundsTiles = true;

    [Header("Visual Root (Auto Height)")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private bool autoPlaceVisualRoot = true;
    [SerializeField] private float visualRootYOffset = 0f;
    [SerializeField] private bool useTileTopForVisualRoot = true;

    [Header("Anchors")]
    [SerializeField] private Transform anchorRoot;
    [SerializeField] private AnchorVisual[] anchorVisuals;

    [Header("Rebuild")]
    [SerializeField] private bool clearBeforeRebuild = true;

    [System.Serializable]
    public struct AnchorVisual
    {
        public FootprintAnchorType type;
        public GameObject prefab;
        public Vector3 localOffset;
        public Vector3 localEuler;
        public bool suppressBaseTile;
    }

    private static Mesh _boundsMesh;
    private static Material _boundsMaterial;

    public void Rebuild()
    {
        if (mask == null || !mask.IsValid) return;
        if (baseTilePrefab == null) return;

        EnsureRoots();
        EnsureBoundsRoot();

        if (clearBeforeRebuild)
        {
            ClearChildren(basePlateRoot);
            ClearChildren(anchorRoot);
            if (boundsRoot != null)
                ClearChildren(boundsRoot);
        }

        Vector2Int pivot = mask.Pivot;
        float size = GetCellSize();
        float tileTop = 0f;
        if (useTileTopForVisualRoot)
            tileTop = GetPrefabTopY(baseTilePrefab);
        
        Vector3 baseScale = baseTilePrefab.transform.localScale;
        Bounds tileBounds = default;
        Vector3 tileCenter = Vector3.zero;
        bool hasTileBounds = false;
        if (normalizeTileToCell)
        {
            tileBounds = GetPrefabBounds(baseTilePrefab);
            hasTileBounds = tileBounds.size.x > 0.0001f && tileBounds.size.z > 0.0001f;
            if (centerTileToCell && hasTileBounds)
                tileCenter = tileBounds.center;
        }

        for (int y = 0; y < mask.Height; y++)
        {
            for (int x = 0; x < mask.Width; x++)
            {
                if (!mask.GetCell(x, y)) continue;
                FootprintAnchorType anchorType = mask.GetAnchor(x, y);
                if (ShouldSuppressBaseTile(anchorType)) continue;
                Vector3 pos = CellToLocal(x, y, pivot, size) + baseTileOffset;
                var tile = Instantiate(baseTilePrefab, basePlateRoot);
                tile.name = $"Tile_{x}_{y}";
                tile.transform.localRotation = Quaternion.identity;
                Vector3 offset = Vector3.zero;
                if (normalizeTileToCell && hasTileBounds)
                {
                    float sx = size / Mathf.Max(0.0001f, tileBounds.size.x);
                    float sz = size / Mathf.Max(0.0001f, tileBounds.size.z);
                    Vector3 scale = new Vector3(baseScale.x * sx, baseScale.y, baseScale.z * sz);
                    scale *= Mathf.Max(0.01f, tileScaleMultiplier);
                    tile.transform.localScale = scale;
                    if (centerTileToCell)
                        offset = new Vector3(-tileCenter.x * sx, 0f, -tileCenter.z * sz);
                }
                else
                {
                    Vector3 scale = baseScale * Mathf.Max(0.01f, tileScaleMultiplier);
                    tile.transform.localScale = scale;
                    if (centerTileToCell && hasTileBounds)
                        offset = new Vector3(-tileCenter.x * baseScale.x, 0f, -tileCenter.z * baseScale.z);
                }
                tile.transform.localPosition = pos + offset;
            }
        }

        if (buildBoundsTiles && boundsRoot != null)
        {
            for (int y = 0; y < mask.Height; y++)
            {
                for (int x = 0; x < mask.Width; x++)
                {
                    if (!mask.GetCell(x, y)) continue;
                    Vector3 pos = CellToLocal(x, y, pivot, size) + boundsTileOffset;
                    var tile = CreateBoundsTile(hideBoundsTiles);
                    tile.name = $"Bounds_{x}_{y}";
                    tile.transform.SetParent(boundsRoot, false);
                    tile.transform.localPosition = pos + new Vector3(0f, boundsTileHeight * 0.5f, 0f);
                    tile.transform.localRotation = Quaternion.identity;
                    tile.transform.localScale = new Vector3(size, Mathf.Max(0.001f, boundsTileHeight), size);
                }
            }
        }

        if (autoPlaceVisualRoot && visualRoot != null)
        {
            Vector3 lp = visualRoot.localPosition;
            lp.y = baseTileOffset.y + tileTop + visualRootYOffset;
            visualRoot.localPosition = lp;
        }

        AutoPlaceGridAnchor(pivot, size);

        if (anchorVisuals == null || anchorVisuals.Length == 0) return;

        for (int y = 0; y < mask.Height; y++)
        {
            for (int x = 0; x < mask.Width; x++)
            {
                FootprintAnchorType type = mask.GetAnchor(x, y);
                if (type == FootprintAnchorType.None) continue;
                if (!TryGetAnchorVisual(type, out AnchorVisual visual) || visual.prefab == null) continue;

                Vector3 pos = CellToLocal(x, y, pivot, size) + visual.localOffset;
                var go = Instantiate(visual.prefab, anchorRoot);
                go.name = $"{type}_{x}_{y}";
                go.transform.localPosition = pos;
                go.transform.localRotation = Quaternion.Euler(visual.localEuler);
                go.transform.localScale = visual.prefab.transform.localScale;
            }
        }
    }

    private float GetCellSize()
    {
        if (useGridCellSize && grid != null)
            return Mathf.Max(0.001f, grid.CellSize);
        return Mathf.Max(0.001f, cellSize);
    }

    private static Vector3 CellToLocal(int x, int y, Vector2Int pivot, float size)
    {
        float lx = (x - pivot.x + 0.5f) * size;
        float lz = (y - pivot.y + 0.5f) * size;
        return new Vector3(lx, 0f, lz);
    }

    private void EnsureRoots()
    {
        if (basePlateRoot == null)
        {
            var go = new GameObject("BasePlate");
            go.transform.SetParent(transform, false);
            basePlateRoot = go.transform;
        }

        if (anchorRoot == null)
        {
            var go = new GameObject("BaseAnchors");
            go.transform.SetParent(transform, false);
            anchorRoot = go.transform;
        }

        if (gridAnchor == null && autoPlaceGridAnchor && createGridAnchorIfMissing)
        {
            var go = new GameObject("GridAnchor");
            go.transform.SetParent(transform, false);
            gridAnchor = go.transform;
        }
    }

    private void EnsureBoundsRoot()
    {
        if (!buildBoundsTiles) return;
        if (boundsRoot != null) return;
        var go = new GameObject("BasePlateBounds");
        go.transform.SetParent(transform, false);
        boundsRoot = go.transform;
    }

    private static float GetPrefabTopY(GameObject prefab)
    {
        if (prefab == null) return 0f;
        var temp = Instantiate(prefab);
        temp.hideFlags = HideFlags.HideAndDontSave;
        temp.transform.position = Vector3.zero;
        temp.transform.rotation = Quaternion.identity;
        temp.transform.localScale = prefab.transform.localScale;

        float top = 0f;
        var renderers = temp.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);
            top = b.max.y;
        }

        if (Application.isPlaying)
            Destroy(temp);
        else
            DestroyImmediate(temp);

        return top;
    }

    private static Bounds GetPrefabBounds(GameObject prefab)
    {
        if (prefab == null) return new Bounds(Vector3.zero, Vector3.zero);
        var temp = Instantiate(prefab);
        temp.hideFlags = HideFlags.HideAndDontSave;
        temp.transform.position = Vector3.zero;
        temp.transform.rotation = Quaternion.identity;
        temp.transform.localScale = prefab.transform.localScale;

        Bounds b = new Bounds(Vector3.zero, Vector3.zero);
        var renderers = temp.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);
        }

        if (Application.isPlaying)
            Destroy(temp);
        else
            DestroyImmediate(temp);

        return b;
    }

    private void AutoPlaceGridAnchor(Vector2Int pivot, float size)
    {
        if (!autoPlaceGridAnchor || gridAnchor == null || mask == null || !mask.IsValid) return;
        if (size <= 0.0001f) return;

        float half = size * 0.5f;
        bool has = false;
        float minX = 0f;
        float maxX = 0f;
        float minZ = 0f;
        float maxZ = 0f;

        for (int y = 0; y < mask.Height; y++)
        {
            for (int x = 0; x < mask.Width; x++)
            {
                if (!mask.GetCell(x, y)) continue;
                Vector3 c = CellToLocal(x, y, pivot, size) + baseTileOffset;
                float lx = c.x - half;
                float rx = c.x + half;
                float bz = c.z - half;
                float fz = c.z + half;
                if (!has)
                {
                    minX = lx;
                    maxX = rx;
                    minZ = bz;
                    maxZ = fz;
                    has = true;
                }
                else
                {
                    minX = Mathf.Min(minX, lx);
                    maxX = Mathf.Max(maxX, rx);
                    minZ = Mathf.Min(minZ, bz);
                    maxZ = Mathf.Max(maxZ, fz);
                }
            }
        }

        if (!has) return;

        Vector3 pos = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
        pos += gridAnchorOffset;
        gridAnchor.localPosition = pos;
    }

    private static void ClearChildren(Transform root)
    {
        if (root == null) return;
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            var child = root.GetChild(i);
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
    }

    private bool TryGetAnchorVisual(FootprintAnchorType type, out AnchorVisual visual)
    {
        if (anchorVisuals != null)
        {
            for (int i = 0; i < anchorVisuals.Length; i++)
            {
                if (anchorVisuals[i].type == type)
                {
                    visual = anchorVisuals[i];
                    return true;
                }
            }
        }

        visual = default;
        return false;
    }

    private bool ShouldSuppressBaseTile(FootprintAnchorType type)
    {
        if (type == FootprintAnchorType.None) return false;
        if (TryGetAnchorVisual(type, out AnchorVisual visual))
            return visual.suppressBaseTile;
        return false;
    }

    private static GameObject CreateBoundsTile(bool hide)
    {
        var go = new GameObject("BoundsTile");
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = GetDefaultBoundsMesh();
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = GetBoundsMaterial();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.enabled = !hide;
        return go;
    }

    private static Mesh GetDefaultBoundsMesh()
    {
        if (_boundsMesh != null) return _boundsMesh;
        _boundsMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        if (_boundsMesh == null)
            _boundsMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        return _boundsMesh;
    }

    private static Material GetBoundsMaterial()
    {
        if (_boundsMaterial != null) return _boundsMaterial;
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        _boundsMaterial = new Material(shader);
        _boundsMaterial.color = new Color(0f, 0f, 0f, 0f);
        return _boundsMaterial;
    }
}
