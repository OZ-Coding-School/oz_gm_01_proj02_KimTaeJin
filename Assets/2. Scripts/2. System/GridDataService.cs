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
    private readonly Dictionary<string, TowerDefinitionSO> _defsById = new();
    private readonly HashSet<Vector2Int> _roadCells = new();
    private readonly List<Vector2Int> _cells = new();
    private readonly List<Vector2Int> _upgradeCells = new();

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
    }

    private void Awake()
    {
        BuildDefinitionCache();
        EnsureWorldGridSync();
    }

    public bool TryGet(Vector3Int cell, out TowerData data) => _data.TryGetValue(cell, out data);

    public bool TryGetDefinition(string towerId, out TowerDefinitionSO def)
    {
        BuildDefinitionCache();
        return _defsById.TryGetValue(towerId, out def);
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
        _data.Clear();
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
            return TryUpgrade(cell, result.previewDef);

        return TryPlace(cell, selected);
    }

    public bool TryPlace(Vector3Int cell, TowerDefinitionSO def)
    {
        if (def == null || string.IsNullOrEmpty(def.id)) return false;
        if (_data.ContainsKey(cell)) return false;

        _data[cell] = new TowerData { towerId = def.id, level = 1 };
        OnDataChanged?.Invoke(cell);
        return true;
    }

    public bool TryUpgrade(Vector3Int cell, TowerDefinitionSO nextDef)
    {
        if (nextDef == null || string.IsNullOrEmpty(nextDef.id)) return false;
        if (!_data.TryGetValue(cell, out TowerData existing)) return false;

        existing.towerId = nextDef.id;
        existing.level = Mathf.Max(1, existing.level + 1);
        _data[cell] = existing;
        OnDataChanged?.Invoke(cell);
        return true;
    }

    public bool TryRemove(Vector3Int cell)
    {
        if (!_data.Remove(cell)) return false;
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

        IReadOnlyList<TowerEntity> towers = _scope != null && _scope.Entities != null ? _scope.Entities.Towers : null;
        GridRoadUtility.BuildRoadCells(gridSystem, GetAnchorCell2D(), baseFootprint, towers, _roadCells);
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

    private static bool ListContainsCell(List<Vector2Int> cells, Vector2Int cell)
    {
        for (int i = 0; i < cells.Count; i++)
            if (cells[i] == cell) return true;
        return false;
    }
}
