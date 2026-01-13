using DG.Tweening;
using UnityEngine;

public sealed class TowerBuildSystem : MonoBehaviour
{
    private RunScope _scope;
    [SerializeField] private float dropHeight = 1.8f;
    [SerializeField] private float dropDuration = 0.22f;
    private readonly System.Collections.Generic.List<Vector2Int> _cells = new();
    private readonly System.Collections.Generic.Dictionary<TowerDefinitionSO, bool> _hasBasePlate = new();

    public void Construct(RunScope scope) => _scope = scope;

    public Vector2Int GetAnchorCell()
    {
        if (_scope == null || _scope.Grid == null) return Vector2Int.zero;

        var baseFootprint = _scope.BaseFootprintReserver;
        if (baseFootprint != null && baseFootprint.UseFixedFootprint)
        {
            FootprintMaskSO mask = baseFootprint.UseFootprintMask ? baseFootprint.FixedFootprintMask : null;
            if (mask != null && mask.IsValid)
                return FootprintMaskUtility.GetCenteredAnchor(_scope.Grid, mask.Size, mask.Pivot, baseFootprint.EvenFootprintBiasPositive);

            return FootprintMaskUtility.GetCenteredAnchor(_scope.Grid, baseFootprint.FixedFootprintSize, Vector2Int.zero,
                baseFootprint.EvenFootprintBiasPositive);
        }

        return _scope.Grid.CenterCell;
    }

    public bool CanPlace(TowerDefinitionSO def, Vector2Int cell)
    {
        if (_scope == null || def == null || def.prefab == null) return false;
        if (_scope.Grid == null) return false;
        FootprintMaskUtility.GetFootprintData(def, out FootprintMaskSO mask, out Vector2Int size, out Vector2Int pivot);
        return BuildGridRules.CanPlaceFootprint(_scope.Grid, mask, size, pivot, cell);
    }

    public bool CanPlaceOffsetDetailed(TowerDefinitionSO def, Vector2Int offset, out string reason)
    {
        reason = "";
        if (_scope == null)
        {
            reason = "RunScope is null";
            return false;
        }
        if (def == null)
        {
            reason = "TowerDefinitionSO is null";
            return false;
        }
        if (def.prefab == null)
        {
            reason = "TowerDefinitionSO.prefab is null";
            return false;
        }
        if (_scope.Grid == null)
        {
            reason = "GridSystem is null";
            return false;
        }

        Vector2Int cell = GetAnchorCell() + offset;
        FootprintMaskUtility.GetFootprintData(def, out FootprintMaskSO mask, out Vector2Int size, out Vector2Int pivot);
        FootprintMaskUtility.GetFootprintCells(mask, size, pivot, cell, _cells);
        for (int i = 0; i < _cells.Count; i++)
        {
            Vector2Int c = _cells[i];
            if (!_scope.Grid.IsInBounds(c))
            {
                reason = $"Out of bounds cell={c}";
                return false;
            }
            if (_scope.Grid.IsOccupied(c))
            {
                reason = $"Cell occupied cell={c}";
                return false;
            }
        }

        if (!BuildGridRules.CanPlaceFootprint(_scope.Grid, mask, size, pivot, cell))
        {
            reason = "Not connected to build lines";
            return false;
        }

        return true;
    }

    public bool CanPlaceOffset(TowerDefinitionSO def, Vector2Int offset)
    {
        if (_scope == null || _scope.Grid == null) return false;
        Vector2Int cell = GetAnchorCell() + offset;
        return CanPlace(def, cell);
    }

    public bool TryGetPlacementPos(TowerDefinitionSO def, Vector2Int cell, out Vector3 pos)
    {
        pos = default;
        if (_scope == null || def == null || def.prefab == null) return false;
        if (_scope.Grid == null) return false;

        FootprintMaskUtility.GetFootprintData(def, out FootprintMaskSO mask, out Vector2Int size, out Vector2Int pivot);
        Vector3 center = _scope.Grid.CellToWorldCenter(cell);
        Vector3 offset = GetFootprintOffset(_scope.Grid, size, pivot);
        center += offset;

        bool hasBasePlate = HasBasePlate(def);

        float groundY = center.y;
        if (!hasBasePlate && GameRoot.Instance != null)
        {
            float rayH = GameRoot.Instance.GroundRayHeight;
            var origin = new Vector3(center.x, rayH, center.z);

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayH * 2f,
                    GameRoot.Instance.GroundMask, QueryTriggerInteraction.Ignore))
            {
                groundY = hit.point.y;
            }
        }

        float extra = (GameRoot.Instance != null) ? GameRoot.Instance.GroundExtraOffset : 0.02f;
        if (hasBasePlate)
            extra = 0f;

        float bottomOffset = 0.0f;
        if (!hasBasePlate)
        {
            var prefabCol = def.prefab.GetComponentInChildren<Collider>(true);
            if (prefabCol != null)
                bottomOffset = GetColliderBottomOffset(prefabCol, def.prefab.transform);
        }

        pos = new Vector3(center.x, groundY + bottomOffset + extra, center.z);
        return true;
    }

    public bool TryPlaceTower(TowerDefinitionSO def, Vector2Int cell, Quaternion rot)
    {
        return TryPlaceTower(def, cell, rot, out _);
    }

    public bool TryPlaceTower(TowerDefinitionSO def, Vector2Int cell, Quaternion rot, out TowerEntity towerEntity)
    {
        towerEntity = null;
        if (!CanPlace(def, cell)) return false;

        if (!TryGetPlacementPos(def, cell, out Vector3 pos)) return false;

        Transform parent = (_scope.Grid != null) ? _scope.Grid.Anchor : null;
        var tower = Object.Instantiate(def.prefab, pos, rot);
        if (parent != null)
            tower.transform.SetParent(parent, true);

        tower.name = $"{def.id}_Tower";
        tower.SetCell(cell);
        FootprintMaskUtility.GetFootprintData(def, out FootprintMaskSO mask, out Vector2Int size, out Vector2Int pivot);
        tower.SetFootprint(size);
        tower.Construct(_scope, def);

        _scope.Entities.RegisterTower(tower);
        FootprintMaskUtility.GetFootprintCells(mask, size, pivot, cell, _cells);
        tower.SetOccupiedCells(_cells);
        OccupyCells(_cells);

        PlayDropTween(tower.transform, pos);

        towerEntity = tower;
        return true;
    }

    public bool TryPlaceTowerOffset(TowerDefinitionSO def, Vector2Int offset, Quaternion rot)
    {
        if (_scope == null || _scope.Grid == null) return false;
        Vector2Int cell = GetAnchorCell() + offset;
        if (!TryPlaceTower(def, cell, rot, out TowerEntity tower)) return false;
        if (tower != null) tower.SetOffsetFromCenter(offset);
        return true;
    }

    private void OccupyCells(System.Collections.Generic.List<Vector2Int> cells)
    {
        if (_scope == null || _scope.Grid == null) return;
        for (int i = 0; i < cells.Count; i++)
            _scope.Grid.TryOccupy(cells[i]);
    }

    private Vector3 GetFootprintOffset(GridSystem grid, Vector2Int size, Vector2Int pivot)
    {
        if (grid == null) return Vector3.zero;
        size.x = Mathf.Max(1, size.x);
        size.y = Mathf.Max(1, size.y);
        pivot.x = Mathf.Clamp(pivot.x, 0, size.x - 1);
        pivot.y = Mathf.Clamp(pivot.y, 0, size.y - 1);

        float ox = ((size.x - 1) * 0.5f - pivot.x) * grid.CellSizeX;
        float oz = ((size.y - 1) * 0.5f - pivot.y) * grid.CellSizeZ;
        return new Vector3(ox, 0f, oz);
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

    private bool HasBasePlate(TowerDefinitionSO def)
    {
        if (def == null || def.prefab == null) return false;
        if (_hasBasePlate.TryGetValue(def, out bool has))
            return has;

        has = def.prefab.GetComponentInChildren<FootprintVisualBaker>(true) != null;
        if (!has)
        {
            var list = def.prefab.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < list.Length; i++)
            {
                var t = list[i];
                if (t != null && t.name == "BasePlate")
                {
                    has = true;
                    break;
                }
            }
        }

        _hasBasePlate[def] = has;
        return has;
    }

    private void PlayDropTween(Transform t, Vector3 targetPos)
    {
        if (t == null) return;
        if (dropHeight <= 0f || dropDuration <= 0f) return;

        if (t.parent != null)
        {
            Vector3 localTarget = t.localPosition;
            t.localPosition = localTarget + Vector3.up * dropHeight;
            t.DOLocalMove(localTarget, dropDuration).SetEase(Ease.OutQuad);
            return;
        }

        t.position = targetPos + Vector3.up * dropHeight;
        t.DOMove(targetPos, dropDuration).SetEase(Ease.OutQuad);
    }
}
