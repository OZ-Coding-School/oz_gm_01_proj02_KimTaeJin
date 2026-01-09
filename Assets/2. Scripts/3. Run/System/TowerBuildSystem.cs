using UnityEngine;

public sealed class TowerBuildSystem : MonoBehaviour
{
    private RunScope _scope;

    public void Construct(RunScope scope) => _scope = scope;

    public bool CanPlace(TowerDefinitionSO def, Vector2Int cell)
    {
        if (_scope == null || def == null || def.prefab == null) return false;
        if (!_scope.Grid.IsInBounds(cell)) return false;
        if (_scope.Grid.IsOccupied(cell)) return false;
        if (!_scope.Economy.CanAfford(def.cost)) return false;
        return true;
    }


    public bool TryGetPlacementPos(TowerDefinitionSO def, Vector2Int cell, out Vector3 pos)
    {
        pos = default;
        if (_scope == null || def == null || def.prefab == null) return false;

        Vector3 center = _scope.Grid.CellToWorldCenter(cell);

        float groundY = center.y;
        if (GameRoot.Instance != null)
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

        float bottomOffset = 0.0f;
        var prefabCol = def.prefab.GetComponentInChildren<Collider>(true);
        if (prefabCol != null)
            bottomOffset = GetColliderBottomOffset(prefabCol, def.prefab.transform);

        pos = new Vector3(center.x, groundY + bottomOffset + extra, center.z);
        return true;
    }

    public bool TryPlaceTower(TowerDefinitionSO def, Vector2Int cell, Quaternion rot)
    {
        if (!CanPlace(def, cell)) return false;
        if (!_scope.Economy.Spend(def.cost)) return false;

        if (!TryGetPlacementPos(def, cell, out Vector3 pos)) return false;

        Transform parent = (_scope.Grid != null) ? _scope.Grid.Anchor : null;
        var tower = Object.Instantiate(def.prefab, pos, rot);
        if (parent != null)
            tower.transform.SetParent(parent, true);

        tower.name = $"{def.id}_Tower";
        tower.SetCell(cell);
        tower.Construct(_scope, def);

        _scope.Entities.RegisterTower(tower);
        _scope.Grid.TryOccupy(cell);

        return true;
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
