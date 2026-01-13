using UnityEngine;

[DisallowMultipleComponent]
public sealed class FootprintVisualBaker : MonoBehaviour
{
    [Header("Mask")]
    [SerializeField] private FootprintMaskSO mask;
    [SerializeField] private bool useGridCellSize = true;
    [SerializeField] private GridSystem grid;
    [SerializeField] private float cellSize = 1f;
    [SerializeField] private float cellSizeZScale = 1f;

    [Header("Base Plate")]
    [SerializeField] private Transform basePlateRoot;
    [SerializeField] private GameObject baseTilePrefab;
    [SerializeField] private Vector3 baseTileOffset = Vector3.zero;
    [SerializeField] private bool centerFootprintOnRoot = false;
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
    [SerializeField] private bool normalizeAnchorToCell = false;
    [SerializeField] private float anchorScaleMultiplier = 1f;

    [Header("Rebuild")]
    [SerializeField] private bool clearBeforeRebuild = true;

    [System.Serializable]
    public struct AnchorVisual
    {
        public FootprintAnchorType type;
        public GameObject prefab;
        public Vector3 localOffset;
        public Vector3 localEuler;
        public bool centerToCell;
        public bool rotateOffset;
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
        Vector2 size = GetCellSize();
        float sizeX = size.x;
        float sizeZ = size.y;
        float tileTop = 0f;
        if (useTileTopForVisualRoot)
            tileTop = GetPrefabTopY(baseTilePrefab);

        Vector3 footprintCenter = Vector3.zero;
        bool hasFootprint = TryGetFootprintCenter(pivot, sizeX, sizeZ, baseTileOffset, out footprintCenter);
        Vector3 layoutOffset = (centerFootprintOnRoot && hasFootprint) ? -footprintCenter : Vector3.zero;
        
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
                Vector3 pos = CellToLocal(x, y, pivot, sizeX, sizeZ) + baseTileOffset + layoutOffset;
                var tile = Instantiate(baseTilePrefab, basePlateRoot);
                tile.name = $"Tile_{x}_{y}";
                tile.transform.localRotation = Quaternion.identity;
                Vector3 offset = Vector3.zero;
                if (normalizeTileToCell && hasTileBounds)
                {
                    float sx = sizeX / Mathf.Max(0.0001f, tileBounds.size.x);
                    float sz = sizeZ / Mathf.Max(0.0001f, tileBounds.size.z);
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
                Vector3 pos = CellToLocal(x, y, pivot, sizeX, sizeZ) + baseTileOffset + boundsTileOffset + layoutOffset;
                var tile = CreateBoundsTile(hideBoundsTiles);
                tile.name = $"Bounds_{x}_{y}";
                tile.transform.SetParent(boundsRoot, false);
                tile.transform.localPosition = pos + new Vector3(0f, boundsTileHeight * 0.5f, 0f);
                tile.transform.localRotation = Quaternion.identity;
                tile.transform.localScale = new Vector3(sizeX, Mathf.Max(0.001f, boundsTileHeight), sizeZ);
            }
        }
        }

        if (autoPlaceVisualRoot && visualRoot != null)
        {
            Vector3 lp = visualRoot.localPosition;
            lp.y = baseTileOffset.y + tileTop + visualRootYOffset;
            visualRoot.localPosition = lp;
        }

        AutoPlaceGridAnchor(pivot, sizeX, sizeZ, footprintCenter, hasFootprint, layoutOffset);

        if (anchorVisuals == null || anchorVisuals.Length == 0) return;

        for (int y = 0; y < mask.Height; y++)
        {
            for (int x = 0; x < mask.Width; x++)
            {
                FootprintAnchorType type = mask.GetAnchor(x, y);
                if (type == FootprintAnchorType.None) continue;
                if (!TryGetAnchorVisual(type, out AnchorVisual visual) || visual.prefab == null) continue;

                Quaternion rot = Quaternion.Euler(visual.localEuler);
                Vector3 localOffset = visual.localOffset;
                if (visual.rotateOffset)
                    localOffset = rot * localOffset;

                Bounds anchorBounds = default;
                bool hasAnchorBounds = false;
                if (normalizeAnchorToCell || visual.centerToCell)
                {
                    anchorBounds = GetPrefabBounds(visual.prefab);
                    hasAnchorBounds = anchorBounds.size.x > 0.0001f && anchorBounds.size.z > 0.0001f;
                }

                float sx = 1f;
                float sz = 1f;
                float scaleMul = 1f;
                Vector3 anchorBaseScale = visual.prefab.transform.localScale;

                Vector3 scaleToApply = anchorBaseScale;
                if (normalizeAnchorToCell)
                {
                    if (hasAnchorBounds)
                    {
                        float targetX = sizeX;
                        float targetZ = sizeZ;
                        int yaw = Mathf.RoundToInt(visual.localEuler.y / 90f);
                        if ((Mathf.Abs(yaw) % 2) == 1)
                        {
                            targetX = sizeZ;
                            targetZ = sizeX;
                        }

                        sx = targetX / anchorBounds.size.x;
                        sz = targetZ / anchorBounds.size.z;
                        scaleMul = Mathf.Max(0.01f, anchorScaleMultiplier);
                        scaleToApply = new Vector3(anchorBaseScale.x * sx, anchorBaseScale.y, anchorBaseScale.z * sz);
                        scaleToApply *= scaleMul;
                    }
                    else
                    {
                        scaleToApply = anchorBaseScale * Mathf.Max(0.01f, anchorScaleMultiplier);
                    }
                }

                Vector3 centerOffset = Vector3.zero;
                if (visual.centerToCell && hasAnchorBounds)
                {
                    centerOffset = new Vector3(-anchorBounds.center.x * sx, 0f, -anchorBounds.center.z * sz);
                    if (normalizeAnchorToCell)
                        centerOffset *= scaleMul;
                    centerOffset = rot * centerOffset;
                }

                Vector3 pos = CellToLocal(x, y, pivot, sizeX, sizeZ) + baseTileOffset + layoutOffset + localOffset + centerOffset;
                var go = Instantiate(visual.prefab, anchorRoot);
                go.name = $"{type}_{x}_{y}";
                go.transform.localPosition = pos;
                go.transform.localRotation = rot;
                go.transform.localScale = scaleToApply;
            }
        }
    }

    private Vector2 GetCellSize()
    {
        if (useGridCellSize)
        {
            GridSystem resolved = ResolveGrid();
            if (resolved != null)
                return new Vector2(Mathf.Max(0.001f, resolved.CellSizeX), Mathf.Max(0.001f, resolved.CellSizeZ));
        }
        float size = Mathf.Max(0.001f, cellSize);
        float zScale = Mathf.Max(0.01f, cellSizeZScale);
        return new Vector2(size, size * zScale);
    }

    private GridSystem ResolveGrid()
    {
        if (grid != null) return grid;
        var scopeGrid = RunScopeLocator.Current?.Grid;
        if (scopeGrid != null) return scopeGrid;
#if UNITY_EDITOR
        return FindObjectOfType<GridSystem>();
#else
        return null;
#endif
    }

    private static Vector3 CellToLocal(int x, int y, Vector2Int pivot, float sizeX, float sizeZ)
    {
        float lx = (x - pivot.x + 0.5f) * sizeX;
        float lz = (y - pivot.y + 0.5f) * sizeZ;
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

    private void AutoPlaceGridAnchor(Vector2Int pivot, float sizeX, float sizeZ, Vector3 footprintCenter, bool hasFootprint, Vector3 layoutOffset)
    {
        if (!autoPlaceGridAnchor || gridAnchor == null || mask == null || !mask.IsValid) return;
        if (!hasFootprint) return;

        Vector3 pos = footprintCenter + gridAnchorOffset + layoutOffset;
        gridAnchor.localPosition = pos;
    }

    private bool TryGetFootprintCenter(Vector2Int pivot, float sizeX, float sizeZ, Vector3 cellOffset, out Vector3 center)
    {
        center = Vector3.zero;
        if (mask == null || !mask.IsValid) return false;
        if (sizeX <= 0.0001f || sizeZ <= 0.0001f) return false;

        float halfX = sizeX * 0.5f;
        float halfZ = sizeZ * 0.5f;
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
                Vector3 c = CellToLocal(x, y, pivot, sizeX, sizeZ) + cellOffset;
                float lx = c.x - halfX;
                float rx = c.x + halfX;
                float bz = c.z - halfZ;
                float fz = c.z + halfZ;
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

        if (!has) return false;

        center = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
        return true;
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
