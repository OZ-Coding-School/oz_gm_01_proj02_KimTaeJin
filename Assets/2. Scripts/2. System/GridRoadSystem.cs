using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GridRoadSystem : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GridSystem grid;
    [SerializeField] private BaseFootprintReserver baseFootprint;
    [SerializeField] private GameObject roadTilePrefab;
    [SerializeField] private Transform roadRoot;

    [Header("Tuning")]
    [SerializeField] private Vector3 roadRotation = Vector3.zero;
    [SerializeField] private float roadTileYOffset = 0f;
    [SerializeField] private Vector2 roadTileGridOffset = Vector2.zero;
    [SerializeField] private bool normalizeTileToCell = true;
    [SerializeField] private bool centerTileToCell = true;
    [SerializeField] private float tileScaleMultiplier = 1f;
    [SerializeField] private bool useBottomOffset = true;
    [SerializeField] private bool useBuildCellPrefabFallback = true;
    [SerializeField] private float rebuildInterval = 0.25f;

    private RunScope _scope;
    private float _timer;
    private int _lastHash;
    private GameObject _lastPrefab;
    private bool _tileMetricsCached;
    private Vector3 _tileBaseScale;
    private Vector3 _tileCenter;
    private Bounds _tileBounds;
    private bool _hasTileBounds;
    private float _tileBottomOffset;
    private bool _warnedMissingPrefab;

    private readonly HashSet<Vector2Int> _roadCells = new();
    private readonly Dictionary<Vector2Int, GameObject> _instances = new();
    private readonly List<Vector2Int> _remove = new();

    private void OnEnable()
    {
        RunScopeLocator.Changed += OnScopeChanged;
        TryBind();
    }

    private void OnDisable()
    {
        RunScopeLocator.Changed -= OnScopeChanged;
        ClearRoadTiles();
        _scope = null;
    }

    private void OnScopeChanged(RunScope scope)
    {
        TryBind();
    }

    private void TryBind()
    {
        _scope = RunScopeLocator.Current;
        if (_scope == null) return;

        if (grid == null) grid = _scope.Grid;
        if (baseFootprint == null) baseFootprint = _scope.BaseFootprintReserver;

        _timer = 0f;
        _lastHash = 0;
        _lastPrefab = null;
    }

    private void Update()
    {
        if (_scope == null || grid == null) return;

        TryResolveRoadPrefab();
        if (roadTilePrefab == null)
        {
            if (_instances.Count > 0) ClearRoadTiles();
            _lastPrefab = null;
            return;
        }

        _timer -= Time.deltaTime;
        if (_timer > 0f) return;
        _timer = Mathf.Max(0.05f, rebuildInterval);

        int hash = ComputeStateHash();
        bool prefabChanged = roadTilePrefab != _lastPrefab;
        if (hash == _lastHash && !prefabChanged) return;

        if (prefabChanged)
        {
            ClearRoadTiles();
            _tileMetricsCached = false;
        }

        _lastHash = hash;
        _lastPrefab = roadTilePrefab;
        RebuildRoads();
    }

    private int ComputeStateHash()
    {
        if (grid == null) return 0;

        int hash = 17;
        Vector2Int center = GetCenterCell();
        hash = hash * 31 + center.GetHashCode();
        hash = hash * 31 + grid.Width;
        hash = hash * 31 + grid.Height;

        var towers = _scope.Entities != null ? _scope.Entities.Towers : null;
        if (towers != null)
        {
            for (int i = 0; i < towers.Count; i++)
            {
                var t = towers[i];
                if (t == null) continue;
                hash = hash * 31 + t.Cell.GetHashCode();
            }
        }

        return hash;
    }

    private Vector2Int GetCenterCell()
    {
        if (_scope != null && _scope.TowerBuild != null)
            return _scope.TowerBuild.GetAnchorCell();
        return grid != null ? grid.CenterCell : Vector2Int.zero;
    }

    private void RebuildRoads()
    {
        _roadCells.Clear();
        var towers = _scope.Entities != null ? _scope.Entities.Towers : null;
        GridRoadUtility.BuildRoadCells(grid, GetCenterCell(), baseFootprint, towers, _roadCells);
        SyncRoadTiles();
    }

    private void SyncRoadTiles()
    {
        if (_roadCells.Count == 0)
        {
            ClearRoadTiles();
            return;
        }

        CacheTileMetrics();
        GetTileTransform(out Vector3 tileScale, out Vector3 tileOffset);
        Quaternion rot = Quaternion.Euler(roadRotation);
        Vector3 rotatedOffset = rot * tileOffset;
        EnsureRoadRoot();

        _remove.Clear();
        foreach (var kvp in _instances)
        {
            if (!_roadCells.Contains(kvp.Key))
                _remove.Add(kvp.Key);
        }

        for (int i = 0; i < _remove.Count; i++)
        {
            Vector2Int cell = _remove[i];
            if (!_instances.TryGetValue(cell, out GameObject go)) continue;
            if (go != null) Destroy(go);
            _instances.Remove(cell);
        }

        foreach (var cell in _roadCells)
        {
            if (_instances.ContainsKey(cell)) continue;
            Vector3 pos = grid.CellToWorldCenter(cell);
            pos += rotatedOffset + new Vector3(roadTileGridOffset.x, 0f, roadTileGridOffset.y);
            pos.y += roadTileYOffset + (useBottomOffset ? _tileBottomOffset : 0f);
            var go = Instantiate(roadTilePrefab, pos, rot, roadRoot);
            go.transform.localScale = tileScale;
            go.name = $"RoadTile_{cell.x}_{cell.y}";
            _instances.Add(cell, go);
        }
    }

    private void EnsureRoadRoot()
    {
        if (roadRoot != null) return;
        var root = new GameObject("[GridRoadTiles]").transform;
        Transform parent = (grid != null && grid.Anchor != null) ? grid.Anchor : transform;
        root.SetParent(parent, true);
        roadRoot = root;
    }

    private void ClearRoadTiles()
    {
        foreach (var kvp in _instances)
        {
            if (kvp.Value != null) Destroy(kvp.Value);
        }
        _instances.Clear();
        _remove.Clear();
    }

    private void TryResolveRoadPrefab()
    {
        if (roadTilePrefab != null) return;
        if (!useBuildCellPrefabFallback) return;

        var root = GameRoot.Instance;
        if (root != null && root.BuildCellSizePrefab != null)
            roadTilePrefab = root.BuildCellSizePrefab;

        if (roadTilePrefab == null && !_warnedMissingPrefab)
        {
            _warnedMissingPrefab = true;
            Debug.LogWarning("[GridRoadSystem] roadTilePrefab is missing. Assign TileReal (or set GameRoot.BuildCellSizePrefab).");
        }
    }

    private void CacheTileMetrics()
    {
        if (_tileMetricsCached) return;
        _tileMetricsCached = true;
        _tileBottomOffset = 0f;
        _hasTileBounds = false;
        _tileCenter = Vector3.zero;
        _tileBounds = default;
        _tileBaseScale = roadTilePrefab != null ? roadTilePrefab.transform.localScale : Vector3.one;

        if (roadTilePrefab == null) return;

        _tileBounds = GetPrefabBounds(roadTilePrefab);
        _hasTileBounds = _tileBounds.size.x > 0.0001f && _tileBounds.size.z > 0.0001f;
        if (_hasTileBounds && centerTileToCell)
            _tileCenter = _tileBounds.center;

        if (useBottomOffset && _tileBounds.size.y > 0.0001f)
            _tileBottomOffset = -_tileBounds.min.y * Mathf.Max(0.01f, tileScaleMultiplier);
    }

    private void GetTileTransform(out Vector3 scale, out Vector3 offset)
    {
        scale = _tileBaseScale;
        offset = Vector3.zero;

        float mul = Mathf.Max(0.01f, tileScaleMultiplier);

        if (normalizeTileToCell && _hasTileBounds && grid != null)
        {
            float sx = grid.CellSizeX / Mathf.Max(0.0001f, _tileBounds.size.x);
            float sz = grid.CellSizeZ / Mathf.Max(0.0001f, _tileBounds.size.z);
            scale = new Vector3(_tileBaseScale.x * sx, _tileBaseScale.y, _tileBaseScale.z * sz) * mul;
            if (centerTileToCell)
                offset = new Vector3(-_tileCenter.x * sx, 0f, -_tileCenter.z * sz);
            return;
        }

        scale = _tileBaseScale * mul;
        if (centerTileToCell && _hasTileBounds)
            offset = new Vector3(-_tileCenter.x * _tileBaseScale.x, 0f, -_tileCenter.z * _tileBaseScale.z);
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
}
