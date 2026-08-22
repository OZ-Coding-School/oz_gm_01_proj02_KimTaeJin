using System.Collections.Generic;
using UnityEngine;

public sealed class GridSystem : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] private float cellSize = 2f;
    [SerializeField] private float cellSizeZScale = 1f;
    [SerializeField] private int width = 9;
    [SerializeField] private int height = 10;

    [Header("Anchor (중심건물/빌드보드)")]
    [SerializeField] private Transform anchor;
    [SerializeField] private Vector3 anchorOffset = Vector3.zero;
    [SerializeField] private bool centerOnAnchor = true;

    [Header("Debug")]
    [SerializeField] private Vector3 origin = Vector3.zero;
    [SerializeField] private bool logOccupy = false;

    [Header("Gizmos")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private bool drawGizmosSelectedOnly = false;
    [SerializeField] private bool drawGizmosInGame = false;
    [SerializeField] private float gizmoY = 0.02f;
    [SerializeField] private Color gizmoLineColor = new Color(0.1f, 0.9f, 1f, 0.6f);
    [SerializeField] private Color gizmoCenterColor = new Color(1f, 0.6f, 0f, 0.9f);
    [SerializeField] private bool drawCenterCell = true;
    [SerializeField] private bool drawAnchor = true;

    private readonly HashSet<Vector2Int> _occupied = new();
    private readonly HashSet<Vector2Int> _transactionCells = new();
    private readonly HashSet<Vector2Int> _replaceableCells = new();

    public float CellSize => cellSize;
    public float CellSizeX => cellSize;
    public float CellSizeZ => cellSize * cellSizeZScale;
    public float CellSizeZScale => cellSizeZScale;
    public int Width => width;
    public int Height => height;
    public Vector2Int CenterCell => new Vector2Int(width / 2, height / 2);

    public Transform Anchor => anchor;
    public Vector3 Origin => origin;
    public int OccupiedCount => _occupied.Count;

    public void Configure(float newCellSize, Vector3 newOrigin)
    {
        Configure(newCellSize, cellSizeZScale, newOrigin);
    }

    public void Configure(float newCellSize, float newCellSizeZScale, Vector3 newOrigin)
    {
        cellSize = Mathf.Max(0.25f, newCellSize);
        cellSizeZScale = Mathf.Max(0.01f, newCellSizeZScale);
        origin = newOrigin;
        centerOnAnchor = false;
    }

    public void Configure(float newCellSize, Transform newAnchor, int newWidth, int newHeight, Vector3 newOffset, bool center = true)
    {
        Configure(newCellSize, cellSizeZScale, newAnchor, newWidth, newHeight, newOffset, center);
    }

    public void Configure(float newCellSize, float newCellSizeZScale, Transform newAnchor, int newWidth, int newHeight, Vector3 newOffset, bool center = true)
    {
        cellSize = Mathf.Max(0.25f, newCellSize);
        cellSizeZScale = Mathf.Max(0.01f, newCellSizeZScale);
        anchor = newAnchor;
        width = Mathf.Max(1, newWidth);
        height = Mathf.Max(1, newHeight);
        anchorOffset = newOffset;
        centerOnAnchor = center;

        RecalcOrigin();
    }

    private void LateUpdate()
    {
        if (centerOnAnchor && anchor != null)
            RecalcOrigin();
    }

    private void RecalcOrigin()
    {
        Vector3 a = anchor.position + anchorOffset;

        float sizeX = cellSize;
        float sizeZ = CellSizeZ;
        float totalW = width * sizeX;
        float totalH = height * sizeZ;

        origin = new Vector3(
            a.x - totalW * 0.5f,
            a.y,
            a.z - totalH * 0.5f
        );
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos || drawGizmosSelectedOnly) return;
        if (Application.isPlaying && !drawGizmosInGame) return;
        DrawGridGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos || !drawGizmosSelectedOnly) return;
        if (Application.isPlaying && !drawGizmosInGame) return;
        DrawGridGizmos();
    }

    private void DrawGridGizmos()
    {
        float sizeX = Mathf.Max(0.0001f, cellSize);
        float sizeZ = Mathf.Max(0.0001f, CellSizeZ);
        int w = Mathf.Max(1, width);
        int h = Mathf.Max(1, height);
        Vector3 o = GetOriginForGizmos();

        float x0 = o.x;
        float z0 = o.z;
        float x1 = x0 + w * sizeX;
        float z1 = z0 + h * sizeZ;

        Gizmos.color = gizmoLineColor;
        for (int i = 0; i <= w; i++)
        {
            float x = x0 + i * sizeX;
            Gizmos.DrawLine(new Vector3(x, o.y + gizmoY, z0), new Vector3(x, o.y + gizmoY, z1));
        }

        for (int j = 0; j <= h; j++)
        {
            float z = z0 + j * sizeZ;
            Gizmos.DrawLine(new Vector3(x0, o.y + gizmoY, z), new Vector3(x1, o.y + gizmoY, z));
        }

        if (drawCenterCell)
        {
            Vector2Int center = new Vector2Int(w / 2, h / 2);
            Vector3 c = o + new Vector3((center.x + 0.5f) * sizeX, 0f, (center.y + 0.5f) * sizeZ);
            Gizmos.color = gizmoCenterColor;
            Gizmos.DrawWireCube(new Vector3(c.x, o.y + gizmoY, c.z), new Vector3(sizeX, 0f, sizeZ));
        }

        if (drawAnchor && anchor != null)
        {
            Gizmos.color = gizmoCenterColor;
            Vector3 a = anchor.position + anchorOffset;
            float r = Mathf.Min(sizeX, sizeZ) * 0.15f;
            Gizmos.DrawLine(a + Vector3.left * r, a + Vector3.right * r);
            Gizmos.DrawLine(a + Vector3.forward * r, a + Vector3.back * r);
        }
    }

    private Vector3 GetOriginForGizmos()
    {
        if (!centerOnAnchor || anchor == null) return origin;

        Vector3 a = anchor.position + anchorOffset;
        float sizeX = cellSize;
        float sizeZ = CellSizeZ;
        float totalW = width * sizeX;
        float totalH = height * sizeZ;
        return new Vector3(
            a.x - totalW * 0.5f,
            a.y,
            a.z - totalH * 0.5f
        );
    }

    public bool IsInBounds(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < width && cell.y >= 0 && cell.y < height;
    }

    public Vector2Int WorldToCell(Vector3 world)
    {
        Vector3 local = world - origin;
        float sizeX = Mathf.Max(0.0001f, cellSize);
        float sizeZ = Mathf.Max(0.0001f, CellSizeZ);
        int x = Mathf.FloorToInt(local.x / sizeX);
        int y = Mathf.FloorToInt(local.z / sizeZ);
        return new Vector2Int(x, y);
    }

    public Vector3 CellToWorldCenter(Vector2Int cell)
    {
        float sizeX = cellSize;
        float sizeZ = CellSizeZ;
        return origin + new Vector3((cell.x + 0.5f) * sizeX, 0f, (cell.y + 0.5f) * sizeZ);
    }

    public bool IsOccupied(Vector2Int cell) => _occupied.Contains(cell);
    public bool TryOccupy(Vector2Int cell)
    {
        if (!IsInBounds(cell)) return false;
        bool added = _occupied.Add(cell);
        if (logOccupy && added)
            Debug.Log($"[GridSystem] Occupy cell={cell} by {new System.Diagnostics.StackTrace(1, false).GetFrame(0)?.GetMethod()?.DeclaringType?.Name}");
        return added;
    }

    public bool CanOccupyAll(IReadOnlyList<Vector2Int> cells)
    {
        return ValidateNewCells(cells, null);
    }

    public bool TryOccupyAll(IReadOnlyList<Vector2Int> cells)
    {
        if (!ValidateNewCells(cells, null)) return false;

        for (int i = 0; i < cells.Count; i++)
            _occupied.Add(cells[i]);

        if (logOccupy)
            Debug.Log($"[GridSystem] Occupied transaction cells={cells.Count}");
        return true;
    }

    public bool TryReplaceAll(IReadOnlyList<Vector2Int> currentCells, IReadOnlyList<Vector2Int> nextCells)
    {
        if (!ValidateCurrentCells(currentCells)) return false;
        if (!ValidateNewCells(nextCells, _replaceableCells)) return false;

        for (int i = 0; i < currentCells.Count; i++)
            _occupied.Remove(currentCells[i]);
        for (int i = 0; i < nextCells.Count; i++)
            _occupied.Add(nextCells[i]);

        if (logOccupy)
            Debug.Log($"[GridSystem] Replaced transaction current={currentCells.Count} next={nextCells.Count}");
        return true;
    }

    public void ReleaseAll(IReadOnlyList<Vector2Int> cells)
    {
        if (cells == null) return;
        for (int i = 0; i < cells.Count; i++)
            _occupied.Remove(cells[i]);
    }

    private bool ValidateCurrentCells(IReadOnlyList<Vector2Int> cells)
    {
        if (cells == null || cells.Count == 0) return false;

        _replaceableCells.Clear();
        for (int i = 0; i < cells.Count; i++)
        {
            Vector2Int cell = cells[i];
            if (!IsInBounds(cell) || !_occupied.Contains(cell) || !_replaceableCells.Add(cell))
                return false;
        }
        return true;
    }

    private bool ValidateNewCells(IReadOnlyList<Vector2Int> cells, HashSet<Vector2Int> allowedOccupied)
    {
        if (cells == null || cells.Count == 0) return false;

        _transactionCells.Clear();
        for (int i = 0; i < cells.Count; i++)
        {
            Vector2Int cell = cells[i];
            if (!IsInBounds(cell) || !_transactionCells.Add(cell)) return false;
            if (_occupied.Contains(cell) && (allowedOccupied == null || !allowedOccupied.Contains(cell)))
                return false;
        }
        return true;
    }

    public void Release(Vector2Int cell) => _occupied.Remove(cell);
    public void ClearAll() => _occupied.Clear();
}
