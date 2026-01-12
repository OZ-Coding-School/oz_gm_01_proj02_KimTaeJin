using System.Collections.Generic;
using UnityEngine;

public enum FootprintAnchorType : byte
{
    None = 0,
    Gate = 1,
    Wall = 2,
    Deco = 3,
    Custom1 = 4,
    Custom2 = 5
}

[CreateAssetMenu(menuName = "Game/Footprint Mask", fileName = "FootprintMask_")]
public sealed class FootprintMaskSO : ScriptableObject
{
    [SerializeField] private int width = 1;
    [SerializeField] private int height = 1;
    [SerializeField] private Vector2Int pivot = Vector2Int.zero;
    [SerializeField] private bool[] cells = new bool[1];
    [SerializeField] private FootprintAnchorType[] anchors = new FootprintAnchorType[1];

    public int Width => width;
    public int Height => height;
    public Vector2Int Pivot => pivot;
    public Vector2Int Size => new Vector2Int(width, height);
    public bool IsValid => width > 0 && height > 0 && cells != null && cells.Length == width * height;

    public bool HasAnyFilled()
    {
        if (!IsValid) return false;
        for (int i = 0; i < cells.Length; i++)
            if (cells[i]) return true;
        return false;
    }

    public bool GetCell(int x, int y)
    {
        if (!IsValid) return false;
        if (x < 0 || y < 0 || x >= width || y >= height) return false;
        return cells[y * width + x];
    }

    public void SetCell(int x, int y, bool on)
    {
        if (!IsValid) return;
        if (x < 0 || y < 0 || x >= width || y >= height) return;
        int idx = y * width + x;
        cells[idx] = on;
        if (!on && anchors != null && idx < anchors.Length)
            anchors[idx] = FootprintAnchorType.None;
    }

    public FootprintAnchorType GetAnchor(int x, int y)
    {
        if (!IsValid) return FootprintAnchorType.None;
        if (anchors == null || anchors.Length != cells.Length) return FootprintAnchorType.None;
        if (x < 0 || y < 0 || x >= width || y >= height) return FootprintAnchorType.None;
        return anchors[y * width + x];
    }

    public void SetAnchor(int x, int y, FootprintAnchorType type)
    {
        if (!IsValid) return;
        if (anchors == null || anchors.Length != cells.Length) return;
        if (x < 0 || y < 0 || x >= width || y >= height) return;
        int idx = y * width + x;
        anchors[idx] = type;
        if (type != FootprintAnchorType.None)
            cells[idx] = true;
    }

    public void ClearAnchors()
    {
        if (!IsValid) return;
        if (anchors == null || anchors.Length != cells.Length) return;
        for (int i = 0; i < anchors.Length; i++)
            anchors[i] = FootprintAnchorType.None;
    }

    public void Resize(int newWidth, int newHeight)
    {
        newWidth = Mathf.Max(1, newWidth);
        newHeight = Mathf.Max(1, newHeight);

        bool[] next = new bool[newWidth * newHeight];
        FootprintAnchorType[] nextAnchors = new FootprintAnchorType[newWidth * newHeight];
        if (IsValid)
        {
            int copyW = Mathf.Min(width, newWidth);
            int copyH = Mathf.Min(height, newHeight);
            for (int y = 0; y < copyH; y++)
            {
                for (int x = 0; x < copyW; x++)
                {
                    int oldIdx = y * width + x;
                    int newIdx = y * newWidth + x;
                    next[newIdx] = cells[oldIdx];
                    if (anchors != null && anchors.Length == cells.Length)
                        nextAnchors[newIdx] = anchors[oldIdx];
                }
            }
        }

        width = newWidth;
        height = newHeight;
        cells = next;
        anchors = nextAnchors;

        pivot.x = Mathf.Clamp(pivot.x, 0, width - 1);
        pivot.y = Mathf.Clamp(pivot.y, 0, height - 1);

        EnsurePivotFilled();
        ClampAnchorsToFilled();
    }

    public void Clear()
    {
        if (!IsValid) return;
        for (int i = 0; i < cells.Length; i++)
            cells[i] = false;
        ClearAnchors();
        EnsurePivotFilled();
    }

    public void Fill()
    {
        if (!IsValid) return;
        for (int i = 0; i < cells.Length; i++)
            cells[i] = true;
    }

    public void Invert()
    {
        if (!IsValid) return;
        for (int i = 0; i < cells.Length; i++)
            cells[i] = !cells[i];
        EnsurePivotFilled();
        ClampAnchorsToFilled();
    }

    public void GetFilledCells(List<Vector2Int> results)
    {
        if (results == null) return;
        results.Clear();
        if (!IsValid) return;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (GetCell(x, y))
                    results.Add(new Vector2Int(x, y));
            }
        }
    }

    public void GetAnchorCells(FootprintAnchorType type, List<Vector2Int> results)
    {
        if (results == null) return;
        results.Clear();
        if (!IsValid) return;
        if (anchors == null || anchors.Length != cells.Length) return;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;
                if (anchors[idx] == type && cells[idx])
                    results.Add(new Vector2Int(x, y));
            }
        }
    }

    private void EnsurePivotFilled()
    {
        if (!IsValid) return;
        int idx = pivot.y * width + pivot.x;
        if (idx >= 0 && idx < cells.Length)
            cells[idx] = true;
    }

    private void ClampAnchorsToFilled()
    {
        if (!IsValid) return;
        if (anchors == null || anchors.Length != cells.Length) return;
        for (int i = 0; i < anchors.Length; i++)
            if (!cells[i]) anchors[i] = FootprintAnchorType.None;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
        if (cells == null || cells.Length != width * height)
            Resize(width, height);
        if (anchors == null || anchors.Length != width * height)
            anchors = new FootprintAnchorType[width * height];
        pivot.x = Mathf.Clamp(pivot.x, 0, width - 1);
        pivot.y = Mathf.Clamp(pivot.y, 0, height - 1);
        EnsurePivotFilled();
        ClampAnchorsToFilled();
    }
#endif
}
