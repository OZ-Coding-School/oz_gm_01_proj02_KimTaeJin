using System.Collections.Generic;
using UnityEngine;

public sealed class BaseFootprintReserver : MonoBehaviour
{
    [SerializeField] private Collider boundsCollider;
    [SerializeField] private Renderer boundsRenderer;
    [SerializeField] private float boundsPadding = 0.05f;
    [SerializeField] private bool useFixedFootprint = false;
    [SerializeField] private Vector2Int fixedFootprintSize = new Vector2Int(2, 2);
    [SerializeField] private bool useFootprintMask = false;
    [SerializeField] private FootprintMaskSO fixedFootprintMask;
    [SerializeField] private bool evenFootprintBiasPositive = true;

    private RunScope _scope;
    private bool _hasRect;
    private Vector2Int _minCell;
    private Vector2Int _maxCell;
    private readonly List<Vector2Int> _occupiedCells = new();

    public bool UseFixedFootprint => useFixedFootprint;
    public Vector2Int FixedFootprintSize => fixedFootprintSize;
    public bool UseFootprintMask => useFootprintMask;
    public FootprintMaskSO FixedFootprintMask => fixedFootprintMask;
    public bool EvenFootprintBiasPositive => evenFootprintBiasPositive;

    public bool TryGetCellRect(out Vector2Int minCell, out Vector2Int maxCell)
    {
        minCell = _minCell;
        maxCell = _maxCell;
        return _hasRect;
    }

    public bool TryGetOccupiedCells(List<Vector2Int> results)
    {
        if (results == null) return false;
        if (!_hasRect) return false;
        results.Clear();
        results.AddRange(_occupiedCells);
        return results.Count > 0;
    }

    public void Construct(RunScope scope)
    {
        _scope = scope;
        Debug.Log($"[BaseFootprintReserver] Construct on {gameObject.name} useFixedFootprint={useFixedFootprint}");
        TryReserve();
    }

    private void TryReserve()
    {
        if (_scope == null || _scope.Grid == null) return;
        if (useFixedFootprint)
        {
            ReserveFixedFootprint();
            return;
        }

        var anchor = _scope.Grid.Anchor;
        if (anchor == null) return;

        if (boundsCollider == null) boundsCollider = anchor.GetComponentInChildren<Collider>();
        if (boundsRenderer == null) boundsRenderer = anchor.GetComponentInChildren<Renderer>();

        Bounds b;
        if (boundsCollider != null) b = boundsCollider.bounds;
        else if (boundsRenderer != null) b = boundsRenderer.bounds;
        else b = new Bounds(anchor.position, Vector3.one * 0.01f);

        if (boundsPadding > 0f) b.Expand(boundsPadding * 2f);

        const float eps = 0.0001f;
        Vector2Int min = _scope.Grid.WorldToCell(new Vector3(b.min.x + eps, 0f, b.min.z + eps));
        Vector2Int max = _scope.Grid.WorldToCell(new Vector3(b.max.x - eps, 0f, b.max.z - eps));

        min.x = Mathf.Clamp(min.x, 0, _scope.Grid.Width - 1);
        min.y = Mathf.Clamp(min.y, 0, _scope.Grid.Height - 1);
        max.x = Mathf.Clamp(max.x, 0, _scope.Grid.Width - 1);
        max.y = Mathf.Clamp(max.y, 0, _scope.Grid.Height - 1);

        _minCell = min;
        _maxCell = max;
        _hasRect = true;
        _occupiedCells.Clear();

        for (int y = min.y; y <= max.y; y++)
            for (int x = min.x; x <= max.x; x++)
            {
                var cell = new Vector2Int(x, y);
                _scope.Grid.TryOccupy(cell);
                _occupiedCells.Add(cell);
            }
    }

    private void ReserveFixedFootprint()
    {
        if (useFootprintMask && fixedFootprintMask != null && fixedFootprintMask.IsValid)
        {
            ReserveFootprintMask(fixedFootprintMask);
            return;
        }

        int w = Mathf.Max(1, fixedFootprintSize.x);
        int h = Mathf.Max(1, fixedFootprintSize.y);

        Vector2Int center = _scope.Grid.CenterCell;
        int minX = center.x - (w / 2);
        int minY = center.y - (h / 2);
        if (evenFootprintBiasPositive && (w % 2 == 0)) minX += 1;
        if (evenFootprintBiasPositive && (h % 2 == 0)) minY += 1;

        Vector2Int min = new Vector2Int(minX, minY);
        Vector2Int max = new Vector2Int(minX + w - 1, minY + h - 1);

        min.x = Mathf.Clamp(min.x, 0, _scope.Grid.Width - 1);
        min.y = Mathf.Clamp(min.y, 0, _scope.Grid.Height - 1);
        max.x = Mathf.Clamp(max.x, 0, _scope.Grid.Width - 1);
        max.y = Mathf.Clamp(max.y, 0, _scope.Grid.Height - 1);

        _minCell = min;
        _maxCell = max;
        _hasRect = true;
        _occupiedCells.Clear();

        for (int y = min.y; y <= max.y; y++)
            for (int x = min.x; x <= max.x; x++)
            {
                var cell = new Vector2Int(x, y);
                _scope.Grid.TryOccupy(cell);
                _occupiedCells.Add(cell);
            }

        Debug.Log($"[BaseFootprintReserver] Fixed footprint {w}x{h} center={center} min={min} max={max}");
    }

    private void ReserveFootprintMask(FootprintMaskSO mask)
    {
        if (_scope == null || _scope.Grid == null || mask == null) return;

        Vector2Int size = mask.Size;
        Vector2Int pivot = mask.Pivot;

        Vector2Int anchor = FootprintMaskUtility.GetCenteredAnchor(_scope.Grid, size, pivot, evenFootprintBiasPositive);
        var temp = new List<Vector2Int>();
        FootprintMaskUtility.GetFootprintCells(mask, size, pivot, anchor, temp);

        _occupiedCells.Clear();

        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int maxX = int.MinValue;
        int maxY = int.MinValue;

        for (int i = 0; i < temp.Count; i++)
        {
            var cell = temp[i];
            if (!_scope.Grid.IsInBounds(cell)) continue;
            _scope.Grid.TryOccupy(cell);
            _occupiedCells.Add(cell);
            minX = Mathf.Min(minX, cell.x);
            minY = Mathf.Min(minY, cell.y);
            maxX = Mathf.Max(maxX, cell.x);
            maxY = Mathf.Max(maxY, cell.y);
        }

        if (_occupiedCells.Count == 0)
        {
            _hasRect = false;
            return;
        }

        _minCell = new Vector2Int(minX, minY);
        _maxCell = new Vector2Int(maxX, maxY);
        _hasRect = true;
        Debug.Log($"[BaseFootprintReserver] Mask footprint size={size} pivot={pivot} anchor={anchor} cells={_occupiedCells.Count}");
    }
}
