using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class GridDataService : MonoBehaviour
{
    [Serializable]
    public struct TowerData
    {
        public string towerId;
        public int level;
        public int order;
    }

    public struct PlacementResult
    {
        public Vector3Int cell;
        public bool canPlace;
        public bool isUpgrade;
        public bool hidePlaced;
        public TowerDefinitionSO previewDef;
        public TowerDefinitionSO existingDef;
    }

    [Header("Grid")]
    [SerializeField] private Grid worldGrid;
    [SerializeField] private GridSystem gridSystem;
    [SerializeField] private BaseFootprintReserver baseFootprint;
    [SerializeField] private bool autoSyncWorldGrid = true;

    [Header("Catalog")]
    [SerializeField] private TowerDefinitionSO[] towerCatalog;

    public event Action<Vector3Int> OnDataChanged;
    public event Action OnGridReset;

    private RunScope _scope;
    private readonly Dictionary<Vector3Int, TowerData> _data = new();
    private readonly Dictionary<Vector3Int, Vector2Int[]> _occupiedByTower = new();
    private readonly Dictionary<string, TowerDefinitionSO> _defsById = new();
    private readonly HashSet<Vector2Int> _roadCells = new();
    private readonly List<Vector2Int> _cells = new();
    private readonly List<Vector2Int> _upgradeCells = new();
    private readonly List<Vector3Int> _changedCells = new();
    private readonly List<GridRoadUtility.RoadTower> _roadTowers = new();
    private int _nextOrder;

    public IReadOnlyDictionary<Vector3Int, TowerData> Data => _data;
    public Grid WorldGrid => worldGrid;
    public GridSystem GridSystem => gridSystem;

    public void Construct(RunScope scope)
    {
        _scope = scope;
        if (gridSystem == null && scope != null) gridSystem = scope.Grid;
        if (baseFootprint == null && scope != null) baseFootprint = scope.BaseFootprintReserver;
        if (worldGrid == null && scope != null) worldGrid = scope.GetComponent<Grid>();
        BuildDefinitionCache();
        EnsureWorldGridSync();
        ResetOrder();
    }

    private void Awake()
    {
        BuildDefinitionCache();
        EnsureWorldGridSync();
        ResetOrder();
    }

    public bool TryGet(Vector3Int cell, out TowerData data) => _data.TryGetValue(cell, out data);

    public bool TryGetDefinition(string towerId, out TowerDefinitionSO def)
    {
        BuildDefinitionCache();
        return _defsById.TryGetValue(towerId, out def);
    }

    public void CollectRoadTowers(List<GridRoadUtility.RoadTower> results)
    {
        if (results == null) return;
        results.Clear();
        if (_data.Count == 0) return;

        foreach (var kvp in _data)
        {
            if (!TryGetDefinition(kvp.Value.towerId, out TowerDefinitionSO def)) continue;
            Vector2Int anchor = ToCell2D(kvp.Key);
            results.Add(new GridRoadUtility.RoadTower(anchor, def, kvp.Value.order));
        }

        results.Sort((a, b) =>
        {
            int o = a.Order.CompareTo(b.Order);
            if (o != 0) return o;
            int x = a.Anchor.x.CompareTo(b.Anchor.x);
            return x != 0 ? x : a.Anchor.y.CompareTo(b.Anchor.y);
        });
    }

    public Vector3Int GetAnchorCell()
    {
        Vector2Int anchor = GetAnchorCell2D();
        return new Vector3Int(anchor.x, 0, anchor.y);
    }

    public void RequestGridReset()
    {
        OnGridReset?.Invoke();
    }

    public void ClearAll()
    {
        _changedCells.Clear();
        foreach (var kvp in _data)
            _changedCells.Add(kvp.Key);

        if (gridSystem != null)
        {
            foreach (var kvp in _occupiedByTower)
                gridSystem.ReleaseAll(kvp.Value);
        }
        _occupiedByTower.Clear();
        _data.Clear();
        ResetOrder();
        for (int i = 0; i < _changedCells.Count; i++)
            OnDataChanged?.Invoke(_changedCells[i]);
        OnGridReset?.Invoke();
    }

    public PlacementResult EvaluatePlacement(TowerDefinitionSO selected, Vector3Int cell)
    {
        var result = new PlacementResult
        {
            cell = cell,
            canPlace = false,
            isUpgrade = false,
            hidePlaced = false,
            previewDef = selected,
            existingDef = null
        };

        if (selected == null) return result;

        if (_data.TryGetValue(cell, out TowerData existingData) && TryGetDefinition(existingData.towerId, out TowerDefinitionSO existingDef))
        {
            result.existingDef = existingDef;

            if (IsSameUpgradeChainById(selected, existingDef))
            {
                TowerDefinitionSO nextDef = existingDef.upgradeNext;
                if (nextDef != null && nextDef.prefab != null)
                {
                    result.previewDef = nextDef;
                    result.isUpgrade = true;
                    result.hidePlaced = true;
                    result.canPlace = CanUpgradeAtCell(selected, existingDef, cell);
                    return result;
                }
            }

            return result;
        }

        result.previewDef = selected;
        result.canPlace = CanPlaceAtCell(selected, cell);
        return result;
    }

    public bool TryApplyPlacement(TowerDefinitionSO selected, Vector3Int cell, out PlacementResult result)
    {
        result = EvaluatePlacement(selected, cell);
        if (!result.canPlace) return false;

        if (result.isUpgrade)
            return CommitUpgrade(cell, result.existingDef, result.previewDef);

        return CommitPlacement(cell, selected);
    }

    private bool CommitPlacement(Vector3Int cell, TowerDefinitionSO def)
    {
        if (gridSystem == null || def == null || string.IsNullOrEmpty(def.id)) return false;
        if (_data.ContainsKey(cell)) return false;

        _cells.Clear();
        FootprintMaskUtility.GetFootprintData(def, out FootprintMaskSO mask, out Vector2Int size, out Vector2Int pivot);
        FootprintMaskUtility.GetFootprintCells(mask, size, pivot, ToCell2D(cell), _cells);
        if (!gridSystem.TryOccupyAll(_cells)) return false;

        _occupiedByTower[cell] = _cells.ToArray();
        _data[cell] = new TowerData { towerId = def.id, level = 1, order = _nextOrder++ };
        OnDataChanged?.Invoke(cell);
        return true;
    }

    private bool CommitUpgrade(Vector3Int cell, TowerDefinitionSO existingDef, TowerDefinitionSO nextDef)
    {
        if (gridSystem == null || existingDef == null || nextDef == null || string.IsNullOrEmpty(nextDef.id)) return false;
        if (!_data.TryGetValue(cell, out TowerData existing)) return false;

        _upgradeCells.Clear();
        if (_occupiedByTower.TryGetValue(cell, out Vector2Int[] ownedCells))
        {
            _upgradeCells.AddRange(ownedCells);
        }
        else
        {
            FootprintMaskUtility.GetFootprintData(existingDef, out FootprintMaskSO curMask, out Vector2Int curSize, out Vector2Int curPivot);
            FootprintMaskUtility.GetFootprintCells(curMask, curSize, curPivot, ToCell2D(cell), _upgradeCells);
        }

        _cells.Clear();
        FootprintMaskUtility.GetFootprintData(nextDef, out FootprintMaskSO nextMask, out Vector2Int nextSize, out Vector2Int nextPivot);
        FootprintMaskUtility.GetFootprintCells(nextMask, nextSize, nextPivot, ToCell2D(cell), _cells);
        if (!gridSystem.TryReplaceAll(_upgradeCells, _cells)) return false;

        _occupiedByTower[cell] = _cells.ToArray();
        existing.towerId = nextDef.id;
        existing.level = Mathf.Max(1, existing.level + 1);
        _data[cell] = existing;
        OnDataChanged?.Invoke(cell);
        return true;
    }

    public bool TryRemove(Vector3Int cell)
    {
        if (!_data.TryGetValue(cell, out TowerData removed)) return false;
        _data.Remove(cell);
        if (_occupiedByTower.TryGetValue(cell, out Vector2Int[] occupiedCells))
        {
            gridSystem?.ReleaseAll(occupiedCells);
            _occupiedByTower.Remove(cell);
        }
        else if (gridSystem != null && TryGetDefinition(removed.towerId, out TowerDefinitionSO removedDef))
        {
            _cells.Clear();
            FootprintMaskUtility.GetFootprintData(removedDef, out FootprintMaskSO mask, out Vector2Int size, out Vector2Int pivot);
            FootprintMaskUtility.GetFootprintCells(mask, size, pivot, ToCell2D(cell), _cells);
            gridSystem.ReleaseAll(_cells);
        }
        OnDataChanged?.Invoke(cell);
        return true;
    }

    private Vector2Int GetAnchorCell2D()
    {
        if (gridSystem == null) return Vector2Int.zero;

        if (baseFootprint != null && baseFootprint.UseFixedFootprint)
        {
            FootprintMaskSO mask = baseFootprint.UseFootprintMask ? baseFootprint.FixedFootprintMask : null;
            if (mask != null && mask.IsValid)
                return FootprintMaskUtility.GetCenteredAnchor(gridSystem, mask.Size, mask.Pivot, baseFootprint.EvenFootprintBiasPositive);

            return FootprintMaskUtility.GetCenteredAnchor(gridSystem, baseFootprint.FixedFootprintSize, Vector2Int.zero,
                baseFootprint.EvenFootprintBiasPositive);
        }

        return gridSystem.CenterCell;
    }

    private bool CanPlaceAtCell(TowerDefinitionSO def, Vector3Int cell)
    {
        if (gridSystem == null || def == null || def.prefab == null) return false;

        Vector2Int anchor = ToCell2D(cell);
        FootprintMaskUtility.GetFootprintData(def, out FootprintMaskSO mask, out Vector2Int size, out Vector2Int pivot);
        RefreshRoadCells();
        return BuildGridRules.CanPlaceFootprint(gridSystem, mask, size, pivot, anchor, _roadCells);
    }

    private bool CanUpgradeAtCell(TowerDefinitionSO selected, TowerDefinitionSO existing, Vector3Int cell)
    {
        if (gridSystem == null || selected == null || existing == null) return false;
        if (!IsSameUpgradeChainById(selected, existing)) return false;

        TowerDefinitionSO nextDef = existing.upgradeNext;
        if (nextDef == null || nextDef.prefab == null) return false;

        Vector2Int anchor = ToCell2D(cell);

        _upgradeCells.Clear();
        FootprintMaskUtility.GetFootprintData(existing, out FootprintMaskSO curMask, out Vector2Int curSize, out Vector2Int curPivot);
        FootprintMaskUtility.GetFootprintCells(curMask, curSize, curPivot, anchor, _upgradeCells);

        _cells.Clear();
        FootprintMaskUtility.GetFootprintData(nextDef, out FootprintMaskSO nextMask, out Vector2Int nextSize, out Vector2Int nextPivot);
        FootprintMaskUtility.GetFootprintCells(nextMask, nextSize, nextPivot, anchor, _cells);

        for (int i = 0; i < _cells.Count; i++)
        {
            Vector2Int c = _cells[i];
            if (!gridSystem.IsInBounds(c)) return false;
            if (gridSystem.IsOccupied(c) && !ListContainsCell(_upgradeCells, c)) return false;
        }

        return true;
    }

    private void RefreshRoadCells()
    {
        _roadCells.Clear();
        if (gridSystem == null) return;

        CollectRoadTowers(_roadTowers);
        GridRoadUtility.BuildRoadCells(gridSystem, GetAnchorCell2D(), baseFootprint, _roadTowers, _roadCells);
    }

    private void BuildDefinitionCache()
    {
        if (_defsById.Count > 0) return;
        AddDefinitions(towerCatalog);
        if (GameRoot.Instance != null) AddDefinitions(GameRoot.Instance.TowerCatalog);
    }

    private void AddDefinitions(IEnumerable<TowerDefinitionSO> defs)
    {
        if (defs == null) return;
        foreach (TowerDefinitionSO def in defs)
            AddDefinitionChain(def);
    }

    private void AddDefinitionChain(TowerDefinitionSO def)
    {
        if (def == null) return;
        var cur = def;
        int guard = 0;
        while (cur != null && guard++ < 32)
        {
            if (!string.IsNullOrEmpty(cur.id) && !_defsById.ContainsKey(cur.id))
                _defsById.Add(cur.id, cur);
            cur = cur.upgradeNext;
        }
    }

    private static Vector2Int ToCell2D(Vector3Int cell) => new Vector2Int(cell.x, cell.z);

    private static bool IsSameUpgradeChainById(TowerDefinitionSO a, TowerDefinitionSO b)
    {
        if (a == null || b == null) return false;
        if (IsUpgradeChainMatchById(a, b)) return true;
        return IsUpgradeChainMatchById(b, a);
    }

    private static bool IsUpgradeChainMatchById(TowerDefinitionSO root, TowerDefinitionSO target)
    {
        if (root == null || target == null) return false;
        for (TowerDefinitionSO cur = root; cur != null; cur = cur.upgradeNext)
        {
            if (string.Equals(cur.id, target.id, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private void EnsureWorldGridSync()
    {
        if (!autoSyncWorldGrid) return;
        if (worldGrid == null || gridSystem == null) return;

        GridWorldSync sync = worldGrid.GetComponent<GridWorldSync>();
        if (sync == null) sync = worldGrid.gameObject.AddComponent<GridWorldSync>();
        sync.Configure(gridSystem, worldGrid);
    }

    private void ResetOrder()
    {
        _nextOrder = 0;
        if (_data.Count == 0) return;

        foreach (var kvp in _data)
        {
            if (kvp.Value.order >= _nextOrder)
                _nextOrder = kvp.Value.order + 1;
        }
    }

    private static bool ListContainsCell(List<Vector2Int> cells, Vector2Int cell)
    {
        for (int i = 0; i < cells.Count; i++)
            if (cells[i] == cell) return true;
        return false;
    }
}
