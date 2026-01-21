using UnityEngine;

public sealed class TowerBuildSystem : MonoBehaviour
{
    [SerializeField] private GridDataService dataService;
    [SerializeField] private Grid worldGrid;

    public void Construct(RunScope scope)
    {
        if (dataService == null && scope != null) dataService = scope.GridData;
        if (worldGrid == null && dataService != null) worldGrid = dataService.WorldGrid;
    }

    public Vector2Int GetAnchorCell()
    {
        if (dataService == null) return Vector2Int.zero;
        Vector3Int anchor = dataService.GetAnchorCell();
        return new Vector2Int(anchor.x, anchor.z);
    }

    public bool CanPlace(TowerDefinitionSO def, Vector2Int cell)
    {
        if (dataService == null) return false;
        var result = dataService.EvaluatePlacement(def, new Vector3Int(cell.x, 0, cell.y));
        return result.canPlace;
    }

    public bool CanPlaceOffset(TowerDefinitionSO def, Vector2Int offset)
    {
        Vector2Int cell = GetAnchorCell() + offset;
        return CanPlace(def, cell);
    }

    public bool TryGetUpgradePreview(TowerDefinitionSO def, Vector2Int cell, out TowerDefinitionSO nextDef)
    {
        nextDef = null;
        if (dataService == null) return false;
        var result = dataService.EvaluatePlacement(def, new Vector3Int(cell.x, 0, cell.y));
        if (!result.isUpgrade || result.previewDef == null) return false;
        nextDef = result.previewDef;
        return true;
    }

    public bool TryGetUpgradePreviewOffset(TowerDefinitionSO def, Vector2Int offset, out TowerDefinitionSO nextDef)
    {
        Vector2Int cell = GetAnchorCell() + offset;
        return TryGetUpgradePreview(def, cell, out nextDef);
    }

    public bool TryGetPlacementPos(TowerDefinitionSO def, Vector2Int cell, out Vector3 pos)
    {
        pos = default;
        if (def == null || def.prefab == null) return false;

        Grid g = worldGrid != null ? worldGrid : dataService != null ? dataService.WorldGrid : null;
        if (g == null) return false;

        Vector3Int c = new Vector3Int(cell.x, 0, cell.y);
        Vector3 center = GetCellCenterWorld(g, c);
        center += GetFootprintOffset(def, g);

        bool hasBasePlate = HasBasePlate(def);
        float groundY = center.y;
        if (!hasBasePlate && GameRoot.Instance != null)
        {
            float rayH = GameRoot.Instance.GroundRayHeight;
            Vector3 origin = new Vector3(center.x, rayH, center.z);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayH * 2f,
                    GameRoot.Instance.GroundMask, QueryTriggerInteraction.Ignore))
                groundY = hit.point.y;
        }

        float extra = GameRoot.Instance != null ? GameRoot.Instance.GroundExtraOffset : 0.02f;
        if (hasBasePlate) extra = 0f;

        float bottomOffset = 0f;
        var col = def.prefab.GetComponentInChildren<Collider>(true);
        if (!hasBasePlate && col != null)
            bottomOffset = GetColliderBottomOffset(col, def.prefab.transform);

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
        if (dataService == null) return false;
        return dataService.TryApplyPlacement(def, new Vector3Int(cell.x, 0, cell.y), out _);
    }

    public bool TryPlaceTowerOffset(TowerDefinitionSO def, Vector2Int offset, Quaternion rot)
    {
        return TryPlaceTowerOffset(def, offset, rot, out _);
    }

    public bool TryPlaceTowerOffset(TowerDefinitionSO def, Vector2Int offset, Quaternion rot, out TowerEntity tower)
    {
        tower = null;
        Vector2Int cell = GetAnchorCell() + offset;
        return TryPlaceTower(def, cell, rot, out _);
    }

    private static Vector3 GetCellCenterWorld(Grid grid, Vector3Int cell)
    {
        Vector3 origin = grid.CellToWorld(cell);
        Vector3 size = grid.cellSize;
        return new Vector3(origin.x + size.x * 0.5f, origin.y, origin.z + size.z * 0.5f);
    }

    private static Vector3 GetFootprintOffset(TowerDefinitionSO def, Grid grid)
    {
        if (def == null) return Vector3.zero;
        FootprintMaskUtility.GetFootprintData(def, out _, out Vector2Int size, out Vector2Int pivot);
        size.x = Mathf.Max(1, size.x);
        size.y = Mathf.Max(1, size.y);
        pivot.x = Mathf.Clamp(pivot.x, 0, size.x - 1);
        pivot.y = Mathf.Clamp(pivot.y, 0, size.y - 1);

        float ox = ((size.x - 1) * 0.5f - pivot.x) * grid.cellSize.x;
        float oz = ((size.y - 1) * 0.5f - pivot.y) * grid.cellSize.z;
        return new Vector3(ox, 0f, oz);
    }

    private static bool HasBasePlate(TowerDefinitionSO def)
    {
        if (def == null || def.prefab == null) return false;
        if (def.prefab.GetComponentInChildren<FootprintVisualBaker>(true) != null) return true;
        var list = def.prefab.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < list.Length; i++)
        {
            Transform t = list[i];
            if (t != null && t.name == "BasePlate")
                return true;
        }
        return false;
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
}
