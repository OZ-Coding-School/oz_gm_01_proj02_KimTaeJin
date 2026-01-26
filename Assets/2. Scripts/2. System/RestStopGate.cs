using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RestStopGate : MonoBehaviour
{
    public enum GateType
    {
        Enter = 0,
        Exit = 1
    }

    public enum TriggerEvent
    {
        Enter = 0,
        Exit = 1
    }

    [SerializeField] private GateType gateType = GateType.Enter;
    [SerializeField] private TriggerEvent triggerEvent = TriggerEvent.Enter;
    [SerializeField] private RestStopSystem restStopSystem;
    [SerializeField] private Transform houseStopTarget;
    [SerializeField] private bool requirePlayer = true;
    [SerializeField] private bool requireHouseDrift = false;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string houseTag = "";
    [SerializeField] private bool waitForAllTargetExits = true;
    [SerializeField] private string roadTilePrefix = "RoadTile_";
    [SerializeField] private string panelRoadTilePrefix = "PanelRoad_";

    [Header("House Tail Delay")]
    [SerializeField] private bool delayByHouseTail = true;
    [SerializeField] private bool includeBaseFootprint = false;
    [SerializeField] private float tailExtraPadding = 0.05f;

    private readonly HashSet<Collider> _inside = new();
    private readonly List<Collider> _insideCleanup = new();
    private readonly List<Vector2Int> _tailCells = new();
    private readonly List<Vector2Int> _tailCellsTemp = new();
    private Coroutine _tailWaitRoutine;

    private void Awake()
    {
        if (restStopSystem == null)
            restStopSystem = FindObjectOfType<RestStopSystem>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsTarget(other)) return;

        if (triggerEvent == TriggerEvent.Enter)
            TryApply();
        else if (waitForAllTargetExits)
        {
            CleanupInside();
            _inside.Add(other);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (triggerEvent != TriggerEvent.Exit) return;
        if (!IsTarget(other)) return;

        if (waitForAllTargetExits)
        {
            CleanupInside();
            _inside.Remove(other);
            if (_inside.Count > 0) return;
        }

        if (TryDelayByHouseTail(other)) return;
        TryApply();
    }

    private void OnDisable()
    {
        _inside.Clear();
        StopTailWait();
    }

    private void TryApply()
    {
        if (restStopSystem == null) return;

        if (gateType == GateType.Enter)
            restStopSystem.EnterRestStop(houseStopTarget);
        else
            restStopSystem.ExitRestStop();
    }

    private bool IsTarget(Collider other)
    {
        if (other == null) return false;

        bool isPlayer = IsPlayer(other);
        bool isHouse = IsHouse(other);

        if (!requirePlayer && !requireHouseDrift)
            return isPlayer || isHouse;
        if (requirePlayer && requireHouseDrift)
            return isPlayer || isHouse;
        if (requirePlayer)
            return isPlayer;
        if (requireHouseDrift)
            return isHouse;
        return true;
    }

    private bool IsPlayer(Collider other)
    {
        if (other == null) return false;
        if (!string.IsNullOrEmpty(playerTag) && other.CompareTag(playerTag)) return true;
        return other.GetComponentInParent<PlayerController>() != null;
    }

    private bool IsHouse(Collider other)
    {
        if (other == null) return false;
        if (other.GetComponentInParent<TowerEntity>() != null) return false;
        if (IsRoadTile(other)) return false;
        if (!string.IsNullOrEmpty(houseTag) && HasTagInParents(other.transform, houseTag))
            return true;
        return other.GetComponentInParent<HouseDrift>() != null;
    }

    private bool IsRoadTile(Collider other)
    {
        if (other == null) return false;
        Transform current = other.transform;
        int guard = 0;
        while (current != null && guard++ < 6)
        {
            if (!string.IsNullOrEmpty(roadTilePrefix) && current.name.StartsWith(roadTilePrefix))
                return true;
            if (!string.IsNullOrEmpty(panelRoadTilePrefix) && current.name.StartsWith(panelRoadTilePrefix))
                return true;
            current = current.parent;
        }
        return false;
    }

    private bool TryDelayByHouseTail(Collider other)
    {
        if (!delayByHouseTail) return false;
        if (gateType != GateType.Enter) return false;
        if (other == null) return false;
        if (_tailWaitRoutine != null) return true;

        var house = other.GetComponentInParent<HouseDrift>();
        if (house == null) return false;

        Vector3 dir = house.Direction;
        if (dir.sqrMagnitude <= 0.0001f) return false;
        dir.Normalize();

        float distance = ComputeTailDistance(other.transform.position, dir);
        if (distance <= 0.001f) return false;

        _tailWaitRoutine = StartCoroutine(WaitForHouseAdvance(other.transform, dir, distance));
        return true;
    }

    private IEnumerator WaitForHouseAdvance(Transform target, Vector3 dir, float distance)
    {
        Vector3 start = target != null ? target.position : Vector3.zero;
        float targetDistance = Mathf.Max(0f, distance);

        while (target != null)
        {
            float moved = Vector3.Dot(target.position - start, dir);
            if (moved >= targetDistance) break;
            if (restStopSystem != null && restStopSystem.IsResting) break;
            yield return null;
        }

        _tailWaitRoutine = null;
        TryApply();
    }

    private void StopTailWait()
    {
        if (_tailWaitRoutine == null) return;
        StopCoroutine(_tailWaitRoutine);
        _tailWaitRoutine = null;
    }

    private float ComputeTailDistance(Vector3 origin, Vector3 dir)
    {
        RunScope scope = RunScopeLocator.Current;
        GridSystem grid = scope != null ? scope.Grid : null;
        GridDataService data = scope != null ? scope.GridData : null;
        if (grid == null || data == null) return 0f;

        float halfExtent = GetHalfCellExtent(dir, grid);
        float minDot = 0f;
        bool has = false;

        foreach (var kvp in data.Data)
        {
            if (!data.TryGetDefinition(kvp.Value.towerId, out TowerDefinitionSO def)) continue;
            FootprintMaskUtility.GetFootprintData(def, out FootprintMaskSO mask, out Vector2Int size, out Vector2Int pivot);
            _tailCells.Clear();
            FootprintMaskUtility.GetFootprintCells(mask, size, pivot, new Vector2Int(kvp.Key.x, kvp.Key.z), _tailCells);
            UpdateMinDot(_tailCells, origin, dir, halfExtent, grid, ref minDot, ref has);
        }

        if (includeBaseFootprint)
        {
            var baseReserver = scope.BaseFootprintReserver;
            if (baseReserver != null && baseReserver.TryGetOccupiedCells(_tailCellsTemp))
                UpdateMinDot(_tailCellsTemp, origin, dir, halfExtent, grid, ref minDot, ref has);
        }

        if (!has) return 0f;

        float distance = Mathf.Max(0f, -minDot);
        if (tailExtraPadding > 0f) distance += tailExtraPadding;
        return distance;
    }

    private void UpdateMinDot(List<Vector2Int> cells, Vector3 origin, Vector3 dir, float halfExtent, GridSystem grid,
        ref float minDot, ref bool has)
    {
        if (cells == null || cells.Count == 0) return;
        for (int i = 0; i < cells.Count; i++)
        {
            Vector2Int cell = cells[i];
            if (grid != null && !grid.IsInBounds(cell)) continue;
            Vector3 pos = grid.CellToWorldCenter(cell);
            float dot = Vector3.Dot(pos - origin, dir) - halfExtent;
            if (!has || dot < minDot)
            {
                minDot = dot;
                has = true;
            }
        }
    }

    private float GetHalfCellExtent(Vector3 dir, GridSystem grid)
    {
        if (grid == null) return 0f;
        float ax = Mathf.Abs(dir.x);
        float az = Mathf.Abs(dir.z);
        return 0.5f * (grid.CellSizeX * ax + grid.CellSizeZ * az);
    }

    private void CleanupInside()
    {
        if (_inside.Count == 0) return;
        _insideCleanup.Clear();
        foreach (var col in _inside)
        {
            if (col == null)
                _insideCleanup.Add(col);
        }
        for (int i = 0; i < _insideCleanup.Count; i++)
            _inside.Remove(_insideCleanup[i]);
        _insideCleanup.Clear();
    }

    private bool HasTagInParents(Transform tr, string tag)
    {
        if (tr == null || string.IsNullOrEmpty(tag)) return false;
        Transform current = tr;
        int guard = 0;
        while (current != null && guard++ < 32)
        {
            if (current.CompareTag(tag))
                return true;
            current = current.parent;
        }
        return false;
    }
}
