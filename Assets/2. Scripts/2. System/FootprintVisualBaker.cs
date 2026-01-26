using System.Collections.Generic;
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
    [SerializeField] private bool useBaseTileBottomOffset = false;

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

    [Header("Hierarchy Auto Setup (Optional)")]
    [SerializeField] private bool autoSetupHierarchy = true;
    [SerializeField] private Transform[] bodyParts;
    [SerializeField] private Transform gun;
    [SerializeField] private Transform shootPoint;

    [Header("Auto Assign (Optional)")]
    [SerializeField] private bool autoAssignByName = true;

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
        TryRebuild(out _);
    }

    public bool TryRebuild(out string reason)
    {
        return TryRebuildInternal(out reason, false);
    }

    public void AutoSetupAndRebuild()
    {
        TryAutoSetupAndRebuild(out _);
    }

    public bool TryAutoSetupAndRebuild(out string reason)
    {
        return TryRebuildInternal(out reason, true);
    }

    public bool HasRequiredInputs(out string reason)
    {
        return HasRequiredInputsInternal(out reason, false);
    }

    public bool HasRequiredInputsForAutoSetup(out string reason)
    {
        return HasRequiredInputsInternal(out reason, true);
    }

    public bool TryAutoAssignReferences(out string reason, bool overwriteExisting)
    {
        return AutoAssignReferencesInternal(out reason, overwriteExisting);
    }

    public bool GetValidationReport(out string report)
    {
        List<string> missing = null;
        List<string> optional = null;

        if (mask == null || !mask.IsValid)
            AppendMissing(ref missing, "Mask");
        if (baseTilePrefab == null)
            AppendMissing(ref missing, "BaseTilePrefab");
        if (autoPlaceVisualRoot && visualRoot == null)
            AppendMissing(ref missing, "VisualRoot");

        if (autoSetupHierarchy)
        {
            if (bodyParts == null || bodyParts.Length == 0)
                AppendMissing(ref optional, "BodyParts");
            if (gun == null)
                AppendMissing(ref optional, "Gun");
            if (shootPoint == null)
                AppendMissing(ref optional, "ShootPoint");
        }

        if (missing == null && optional == null)
        {
            report = "OK";
            return true;
        }

        if (missing != null)
            report = $"Missing: {string.Join(", ", missing)}";
        else
            report = string.Empty;

        if (optional != null)
        {
            if (!string.IsNullOrEmpty(report)) report += " | ";
            report += $"Optional: {string.Join(", ", optional)}";
        }

        return missing == null;
    }

    public void EnsureStructure()
    {
        EnsureRoots();
        EnsureBoundsRoot();
    }

    public void AutoAssignDefaults()
    {
        if (!normalizeAnchorToCell) normalizeAnchorToCell = true;
        if (!centerFootprintOnRoot) centerFootprintOnRoot = true;
    }

    private bool TryRebuildInternal(out string reason, bool includeAutoSetup)
    {
        if (!HasRequiredInputsInternal(out reason, includeAutoSetup)) return false;

        EnsureStructure();

        if (includeAutoSetup)
            AutoSetupHierarchy();

        AutoAssignDefaults();

        RebuildInternal();
        return true;
    }

    private void RebuildInternal()
    {

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
        if (normalizeTileToCell || useBaseTileBottomOffset)
        {
            tileBounds = GetPrefabBounds(baseTilePrefab);
            hasTileBounds = tileBounds.size.x > 0.0001f && tileBounds.size.z > 0.0001f;
            if (centerTileToCell && hasTileBounds)
                tileCenter = tileBounds.center;
        }
        float bottomOffset = 0f;
        if (useBaseTileBottomOffset && hasTileBounds)
            bottomOffset = -tileBounds.min.y * Mathf.Max(0.01f, tileScaleMultiplier);

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
                tile.transform.localPosition = pos + offset + new Vector3(0f, bottomOffset, 0f);
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
            lp.y = baseTileOffset.y + bottomOffset + tileTop + visualRootYOffset;
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
            basePlateRoot = FindChildByName("BasePlate");
            if (basePlateRoot == null)
            {
                var go = new GameObject("BasePlate");
                go.transform.SetParent(transform, false);
                basePlateRoot = go.transform;
            }
        }

        if (anchorRoot == null)
        {
            anchorRoot = FindChildByName("BaseAnchors");
            if (anchorRoot == null)
            {
                var go = new GameObject("BaseAnchors");
                go.transform.SetParent(transform, false);
                anchorRoot = go.transform;
            }
        }

        if (gridAnchor == null && autoPlaceGridAnchor && createGridAnchorIfMissing)
        {
            gridAnchor = FindChildByName("GridAnchor");
            if (gridAnchor == null)
            {
                var go = new GameObject("GridAnchor");
                go.transform.SetParent(transform, false);
                gridAnchor = go.transform;
            }
        }
    }

    private void EnsureBoundsRoot()
    {
        if (boundsRoot == null)
            boundsRoot = FindChildByName("BasePlateBounds");
        if (boundsRoot != null) return;
        var go = new GameObject("BasePlateBounds");
        go.transform.SetParent(transform, false);
        boundsRoot = go.transform;
    }

    private Transform FindChildByName(string targetName)
    {
        if (string.IsNullOrEmpty(targetName)) return null;

        Transform direct = transform.Find(targetName);
        if (direct != null) return direct;

        var list = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < list.Length; i++)
        {
            Transform t = list[i];
            if (t == null || t == transform) continue;
            if (t.name == targetName) return t;
        }

        return null;
    }

    private bool HasRequiredInputsInternal(out string reason, bool includeAutoSetup)
    {
        List<string> missing = null;

        if (mask == null || !mask.IsValid)
            AppendMissing(ref missing, "Mask");
        if (baseTilePrefab == null)
            AppendMissing(ref missing, "BaseTilePrefab");
        if (autoPlaceVisualRoot && visualRoot == null && !(includeAutoSetup && autoSetupHierarchy))
            AppendMissing(ref missing, "VisualRoot");

        if (missing == null)
        {
            reason = string.Empty;
            return true;
        }

        reason = string.Join(", ", missing);
        return false;
    }

    private static void AppendMissing(ref List<string> missing, string item)
    {
        if (missing == null) missing = new List<string>();
        missing.Add(item);
    }

    private void AutoSetupHierarchy()
    {
        if (!autoSetupHierarchy) return;
        if (Application.isPlaying) return;
        if (!CanEditHierarchy())
        {
            Debug.LogWarning($"[{nameof(FootprintVisualBaker)}] Auto setup requires editing the prefab asset.", this);
            return;
        }

        EnsureVisualRoot();
        AutoSetupBodyParts();
        AutoSetupGun();
    }

    private void EnsureVisualRoot()
    {
        if (visualRoot != null) return;

        string preferred = $"{name}_VisualRoot";
        visualRoot = FindChildByName(preferred);
        if (visualRoot == null)
            visualRoot = FindChildByName("VisualRoot");
        if (visualRoot == null)
        {
            var go = new GameObject(preferred);
            go.transform.SetParent(transform, false);
            visualRoot = go.transform;
        }
    }

    private void AutoSetupBodyParts()
    {
        if (visualRoot == null || bodyParts == null) return;

        for (int i = 0; i < bodyParts.Length; i++)
        {
            Transform part = bodyParts[i];
            if (part == null) continue;
            if (part == visualRoot || part.IsChildOf(visualRoot)) continue;
            part.SetParent(visualRoot, true);
        }
    }

    private void AutoSetupGun()
    {
        if (gun == null) return;

        Transform yawPivot = gun;
        Transform pitchPivot = FindChildByName(yawPivot, "PitchPivot")
            ?? FindChildByName(yawPivot, "PitchPivotOrigin")
            ?? FindChildByName(yawPivot, "Pitch");
        Transform gunVisual = null;
        bool wrapped = false;

        if (pitchPivot == null)
        {
            if (HasRendererOnSelf(gun))
            {
                Transform oldGun = gun;
                string baseName = oldGun.name;
                yawPivot = new GameObject(baseName).transform;
                yawPivot.SetParent(oldGun.parent, false);
                yawPivot.localPosition = oldGun.localPosition;
                yawPivot.localRotation = Quaternion.identity;
                yawPivot.localScale = Vector3.one;

                oldGun.SetParent(yawPivot, true);
                oldGun.name = MakeVisualName(baseName);
                gunVisual = oldGun;
                gun = yawPivot;
                wrapped = true;
            }

            pitchPivot = EnsureChild(yawPivot, "PitchPivotOrigin");
        }

        Transform gunVisualRoot = EnsureChild(pitchPivot, "GunVisualRoot");
        Transform gunMeshOffset = EnsureChild(gunVisualRoot, "GunMeshOffset");

        if (!wrapped)
            gunVisual = FindGunVisualCandidate(yawPivot, pitchPivot);

        if (gunVisual != null && !gunVisual.IsChildOf(gunMeshOffset))
            gunVisual.SetParent(gunMeshOffset, true);

        if (shootPoint != null && !shootPoint.IsChildOf(yawPivot))
            shootPoint.SetParent(yawPivot, true);

        AssignTowerEntityPivots(yawPivot, pitchPivot, shootPoint);
    }

    private static bool HasRendererOnSelf(Transform t)
    {
        if (t == null) return false;
        return t.GetComponent<Renderer>() != null;
    }

    private bool AutoAssignReferencesInternal(out string reason, bool overwriteExisting)
    {
        if (!autoAssignByName)
        {
            reason = "Auto assign disabled";
            return true;
        }

        int assigned = 0;
        List<string> missing = null;
        var sb = new System.Text.StringBuilder();

        if (overwriteExisting || bodyParts == null || bodyParts.Length == 0)
        {
            var parts = FindBodyPartsByName();
            if (parts.Count > 0)
            {
                bodyParts = parts.ToArray();
                assigned += parts.Count;
                sb.Append($"BodyParts={parts.Count}");
            }
            else
            {
                AppendMissing(ref missing, "BodyParts");
            }
        }

        if (overwriteExisting || gun == null)
        {
            var foundGun = FindByNameTokens(GunTokens, GunExcludeTokens);
            if (foundGun != null)
            {
                gun = foundGun;
                assigned++;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append("Gun=1");
            }
            else
            {
                AppendMissing(ref missing, "Gun");
            }
        }

        if (overwriteExisting || shootPoint == null)
        {
            var foundShoot = FindByNameTokens(ShootTokens, ShootExcludeTokens);
            if (foundShoot != null)
            {
                shootPoint = foundShoot;
                assigned++;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append("ShootPoint=1");
            }
            else
            {
                AppendMissing(ref missing, "ShootPoint");
            }
        }

        if (sb.Length == 0)
            sb.Append("No changes");

        if (missing != null)
        {
            sb.Append(" | Missing: ");
            sb.Append(string.Join(", ", missing));
            reason = sb.ToString();
            return false;
        }

        reason = sb.ToString();
        return true;
    }

    private List<Transform> FindBodyPartsByName()
    {
        var result = new List<Transform>();
        var list = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < list.Length; i++)
        {
            Transform t = list[i];
            if (t == null || t == transform) continue;
            if (IsUnderReservedRoot(t)) continue;
            if (ContainsAny(t.name, BodyExcludeTokens)) continue;
            if (!ContainsAny(t.name, BodyTokens)) continue;
            if (ContainsAny(t.name, GunTokens) || ContainsAny(t.name, ShootTokens)) continue;
            result.Add(t);
        }
        return result;
    }

    private Transform FindByNameTokens(string[] tokens, string[] excludeTokens)
    {
        var list = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < list.Length; i++)
        {
            Transform t = list[i];
            if (t == null || t == transform) continue;
            if (IsUnderReservedRoot(t)) continue;
            if (!ContainsAny(t.name, tokens)) continue;
            if (excludeTokens != null && ContainsAny(t.name, excludeTokens)) continue;
            return t;
        }
        return null;
    }

    private static bool ContainsAny(string name, string[] tokens)
    {
        if (string.IsNullOrEmpty(name) || tokens == null || tokens.Length == 0) return false;
        for (int i = 0; i < tokens.Length; i++)
        {
            if (name.IndexOf(tokens[i], System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    private static bool IsReservedRootName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return string.Equals(name, "BasePlate", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "BaseAnchors", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "BasePlateBounds", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "GridAnchor", System.StringComparison.OrdinalIgnoreCase);
    }

    private bool IsUnderReservedRoot(Transform t)
    {
        if (t == null) return false;
        Transform cur = t;
        while (cur != null && cur != transform)
        {
            if (IsReservedRootName(cur.name)) return true;
            cur = cur.parent;
        }
        return false;
    }

    private bool CanEditHierarchy()
    {
#if UNITY_EDITOR
        if (!UnityEditor.PrefabUtility.IsPartOfPrefabInstance(gameObject)) return true;
        var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && stage.IsPartOfPrefabContents(gameObject)) return true;
        return false;
#else
        return true;
#endif
    }

    private static readonly string[] BodyTokens = { "base", "mid" };
    private static readonly string[] BodyExcludeTokens = { "baseplate", "bounds", "anchor", "grid", "gun", "shoot", "muzzle" };
    private static readonly string[] GunTokens = { "gun" };
    private static readonly string[] GunExcludeTokens = { "gunvisualroot", "gunmeshoffset", "visualroot", "pitch" };
    private static readonly string[] ShootTokens = { "shoot", "muzzle" };
    private static readonly string[] ShootExcludeTokens = { "vfx", "effect" };

    private static string MakeVisualName(string baseName)
    {
        if (string.IsNullOrEmpty(baseName)) return "Gun_Visual";
        if (baseName.EndsWith("_Visual"))
            return baseName;
        return $"{baseName}_Visual";
    }

    private Transform FindGunVisualCandidate(Transform yawPivot, Transform pitchPivot)
    {
        if (yawPivot == null) return null;

        for (int i = 0; i < yawPivot.childCount; i++)
        {
            Transform child = yawPivot.GetChild(i);
            if (child == null || child == pitchPivot) continue;
            if (shootPoint != null && child == shootPoint) continue;
            if (child.GetComponentInChildren<Renderer>(true) != null)
                return child;
        }

        return null;
    }

    private static Transform EnsureChild(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrEmpty(childName)) return null;
        var existing = FindChildByName(parent, childName);
        if (existing != null) return existing;
        var go = new GameObject(childName);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static Transform FindChildByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName)) return null;

        Transform direct = root.Find(targetName);
        if (direct != null) return direct;

        var list = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < list.Length; i++)
        {
            Transform t = list[i];
            if (t == null || t == root) continue;
            if (t.name == targetName) return t;
        }

        return null;
    }

    private void AssignTowerEntityPivots(Transform yawPivot, Transform pitchPivot, Transform muzzle)
    {
#if UNITY_EDITOR
        var tower = GetComponent<TowerEntity>();
        if (tower == null) return;

        var so = new UnityEditor.SerializedObject(tower);
        so.FindProperty("yawPivot").objectReferenceValue = yawPivot;
        so.FindProperty("pitchPivot").objectReferenceValue = pitchPivot;
        so.FindProperty("muzzle").objectReferenceValue = muzzle;
        so.ApplyModifiedPropertiesWithoutUndo();
#endif
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
