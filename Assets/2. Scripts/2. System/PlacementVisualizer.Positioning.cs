using UnityEngine;

public sealed partial class PlacementVisualizer : MonoBehaviour
{
    private Vector3 GetPlacementPosition(TowerDefinitionSO def, Vector3Int cell)
    {
        if (grid == null) return Vector3.zero;
        Vector3 center = GetCellCenterWorld(cell);
        center += GetFootprintOffset(def);

        if (!isWorldVisualizer) return center;

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
        if (!hasBasePlate && def != null && def.prefab != null)
        {
            var col = def.prefab.GetComponentInChildren<Collider>(true);
            if (col != null) bottomOffset = GetColliderBottomOffset(col, def.prefab.transform);
        }

        return new Vector3(center.x, groundY + bottomOffset + extra, center.z);
    }

    private Vector3 GetCellCenterWorld(Vector3Int cell)
    {
        Vector3 origin = grid.CellToWorld(cell);
        Vector3 size = grid.cellSize;
        return new Vector3(origin.x + size.x * 0.5f, origin.y, origin.z + size.z * 0.5f);
    }

    private Vector3 GetFootprintOffset(TowerDefinitionSO def)
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

    private bool HasBasePlate(TowerDefinitionSO def)
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

    private static Vector2Int ToCell2D(Vector3Int cell) => new Vector2Int(cell.x, cell.z);
}
