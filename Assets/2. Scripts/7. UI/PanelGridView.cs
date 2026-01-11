using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class PanelGridView : MonoBehaviour
{
    [SerializeField] private RectTransform gridRoot;
    [SerializeField] private int width = 9;
    [SerializeField] private int height = 10;
    [SerializeField] private float cellWidth = 64f;
    [SerializeField] private float cellHeight = 64f;
    [SerializeField] private bool autoResize = true;
    [SerializeField] private bool autoFitCellSizeFromRect = false;
    [SerializeField] private float minCellSize = 8f;

    [Header("Lines (Optional)")]
    [SerializeField] private Image linePrefab;
    [SerializeField] private float lineThickness = 2f;
    [SerializeField] private Color lineColor = new Color(1f, 1f, 1f, 0.75f);

    private readonly List<RectTransform> _lines = new();

    public int Width => width;
    public int Height => height;
    public float CellWidth => cellWidth;
    public float CellHeight => cellHeight;

    public Vector2Int CenterCell => new Vector2Int(width / 2, height / 2);

    private void Awake()
    {
        if (gridRoot == null) gridRoot = (RectTransform)transform;
        if (autoFitCellSizeFromRect)
        {
            UpdateCellSizeFromRect();
        }
        else if (autoResize)
        {
            gridRoot.sizeDelta = new Vector2(width * cellWidth, height * cellHeight);
        }

        RebuildLines();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (!autoFitCellSizeFromRect) return;
        UpdateCellSizeFromRect();
        RebuildLines();
    }

    public void RebuildLines()
    {
        if (linePrefab == null || gridRoot == null) return;

        for (int i = 0; i < _lines.Count; i++)
            if (_lines[i] != null) Destroy(_lines[i].gameObject);
        _lines.Clear();

        float totalW = width * cellWidth;
        float totalH = height * cellHeight;
        float halfW = totalW * 0.5f;
        float halfH = totalH * 0.5f;

        for (int i = 0; i <= width; i++)
        {
            var line = Instantiate(linePrefab, gridRoot);
            line.name = $"V{i}";
            var rt = line.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(lineThickness, totalH);
            rt.anchoredPosition = new Vector2(-halfW + i * cellWidth, 0f);
            line.color = lineColor;
            _lines.Add(rt);
        }

        for (int j = 0; j <= height; j++)
        {
            var line = Instantiate(linePrefab, gridRoot);
            line.name = $"H{j}";
            var rt = line.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(totalW, lineThickness);
            rt.anchoredPosition = new Vector2(0f, -halfH + j * cellHeight);
            line.color = lineColor;
            _lines.Add(rt);
        }
    }

    private void UpdateCellSizeFromRect()
    {
        if (gridRoot == null) return;
        float w = gridRoot.rect.width;
        float h = gridRoot.rect.height;
        if (w <= 0f || h <= 0f) return;

        cellWidth = Mathf.Max(minCellSize, w / Mathf.Max(1, width));
        cellHeight = Mathf.Max(minCellSize, h / Mathf.Max(1, height));
    }

    public Vector2 CellToLocalCenter(Vector2Int cell)
    {
        float totalW = width * cellWidth;
        float totalH = height * cellHeight;

        float x = -totalW * 0.5f + (cell.x + 0.5f) * cellWidth;
        float y = -totalH * 0.5f + (cell.y + 0.5f) * cellHeight;
        return new Vector2(x, y);
    }

    public bool TryScreenToCell(Vector2 screen, Canvas canvas, Camera cam, out Vector2Int cell)
    {
        cell = default;
        if (gridRoot == null || canvas == null) return false;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            gridRoot,
            screen,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam,
            out Vector2 local);

        float totalW = width * cellWidth;
        float totalH = height * cellHeight;

        float lx = local.x + totalW * 0.5f;
        float ly = local.y + totalH * 0.5f;

        if (lx < 0f || lx >= totalW || ly < 0f || ly >= totalH)
            return false;

        int x = Mathf.FloorToInt(lx / cellWidth);
        int y = Mathf.FloorToInt(ly / cellHeight);
        cell = new Vector2Int(x, y);
        return true;
    }
}
