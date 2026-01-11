using DG.Tweening;
using UnityEngine;

public sealed class TowerBuildSystem : MonoBehaviour
{
    private RunScope _scope;
    [SerializeField] private float dropHeight = 1.8f;
    [SerializeField] private float dropDuration = 0.22f;

    public void Construct(RunScope scope) => _scope = scope;

    public bool CanPlace(TowerDefinitionSO def, Vector2Int cell)
    {
        if (_scope == null || def == null || def.prefab == null) return false;
        if (_scope.Grid == null) return false;
        if (!_scope.Grid.IsInBounds(cell)) return false;
        if (_scope.Grid.IsOccupied(cell)) return false;
        return true;
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

        Vector2Int cell = _scope.Grid.CenterCell + offset;
        if (!_scope.Grid.IsInBounds(cell))
        {
            reason = $"Out of bounds cell={cell}";
            return false;
        }
        if (_scope.Grid.IsOccupied(cell))
        {
            reason = $"Cell occupied cell={cell}";
            return false;
        }

        return true;
    }

    public bool CanPlaceOffset(TowerDefinitionSO def, Vector2Int offset)
    {
        if (_scope == null || _scope.Grid == null) return false;
        Vector2Int cell = _scope.Grid.CenterCell + offset;
        return CanPlace(def, cell);
    }

    public bool TryGetPlacementPos(TowerDefinitionSO def, Vector2Int cell, out Vector3 pos)
    {
        pos = default;
        if (_scope == null || def == null || def.prefab == null) return false;
        if (_scope.Grid == null) return false;

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
        tower.Construct(_scope, def);

        _scope.Entities.RegisterTower(tower);
        _scope.Grid.TryOccupy(cell);

        PlayDropTween(tower.transform, pos);

        towerEntity = tower;
        return true;
    }

    public bool TryPlaceTowerOffset(TowerDefinitionSO def, Vector2Int offset, Quaternion rot)
    {
        if (_scope == null || _scope.Grid == null) return false;
        Vector2Int cell = _scope.Grid.CenterCell + offset;
        if (!TryPlaceTower(def, cell, rot, out TowerEntity tower)) return false;
        if (tower != null) tower.SetOffsetFromCenter(offset);
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

    private void PlayDropTween(Transform t, Vector3 targetPos)
    {
        if (t == null) return;
        if (dropHeight <= 0f || dropDuration <= 0f) return;

        t.position = targetPos + Vector3.up * dropHeight;
        t.DOMove(targetPos, dropDuration).SetEase(Ease.OutQuad);
    }
}
