using System.Collections.Generic;
using UnityEngine;

public sealed class GridSystem : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] private float cellSize = 2f;
    [SerializeField] private int width = 9;   
    [SerializeField] private int height = 10; 

    [Header("Anchor (중심건물/빌드보드)")]
    [SerializeField] private Transform anchor;
    [SerializeField] private Vector3 anchorOffset = Vector3.zero;
    [SerializeField] private bool centerOnAnchor = true;

    [Header("Debug")]
    [SerializeField] private Vector3 origin = Vector3.zero;

    private readonly HashSet<Vector2Int> _occupied = new();

    public float CellSize => cellSize;
    public int Width => width;
    public int Height => height;

    public Transform Anchor => anchor;
    public Vector3 Origin => origin;

    public void Configure(float newCellSize, Vector3 newOrigin)
    {
        cellSize = Mathf.Max(0.25f, newCellSize);
        origin = newOrigin;
        centerOnAnchor = false;
    }

    public void Configure(float newCellSize, Transform newAnchor, int newWidth, int newHeight, Vector3 newOffset, bool center = true)
    {
        cellSize = Mathf.Max(0.25f, newCellSize);
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

        float totalW = width * cellSize;
        float totalH = height * cellSize;

        origin = new Vector3(
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
        int x = Mathf.FloorToInt(local.x / cellSize);
        int y = Mathf.FloorToInt(local.z / cellSize);
        return new Vector2Int(x, y);
    }

    public Vector3 CellToWorldCenter(Vector2Int cell)
    {
        return origin + new Vector3((cell.x + 0.5f) * cellSize, 0f, (cell.y + 0.5f) * cellSize);
    }

    public bool IsOccupied(Vector2Int cell) => _occupied.Contains(cell);
    public bool TryOccupy(Vector2Int cell) => _occupied.Add(cell);
    public void Release(Vector2Int cell) => _occupied.Remove(cell);
    public void ClearAll() => _occupied.Clear();
}
