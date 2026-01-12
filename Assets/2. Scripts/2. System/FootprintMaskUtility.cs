using System.Collections.Generic;
using UnityEngine;

public static class FootprintMaskUtility
{
    public static void GetFootprintData(
        TowerDefinitionSO def,
        out FootprintMaskSO mask,
        out Vector2Int size,
        out Vector2Int pivot)
    {
        mask = def != null ? def.footprintMask : null;
        if (mask != null && mask.IsValid)
        {
            size = mask.Size;
            pivot = mask.Pivot;
        }
        else
        {
            size = def != null ? def.footprint : Vector2Int.one;
            pivot = Vector2Int.zero;
            mask = null;
        }

        ClampSizePivot(ref size, ref pivot);
    }

    public static void GetFootprintData(
        BaseFootprintReserver baseFootprint,
        out FootprintMaskSO mask,
        out Vector2Int size,
        out Vector2Int pivot)
    {
        mask = baseFootprint != null && baseFootprint.UseFootprintMask ? baseFootprint.FixedFootprintMask : null;
        if (baseFootprint != null && baseFootprint.UseFixedFootprint)
        {
            if (mask != null && mask.IsValid)
            {
                size = mask.Size;
                pivot = mask.Pivot;
            }
            else
            {
                size = baseFootprint.FixedFootprintSize;
                pivot = Vector2Int.zero;
                mask = null;
            }
        }
        else
        {
            size = Vector2Int.one;
            pivot = Vector2Int.zero;
            mask = null;
        }

        ClampSizePivot(ref size, ref pivot);
    }

    public static void GetAnchorRange(GridSystem grid, Vector2Int size, Vector2Int pivot, out Vector2Int min, out Vector2Int max)
    {
        if (grid == null)
        {
            min = Vector2Int.zero;
            max = Vector2Int.zero;
            return;
        }

        ClampSizePivot(ref size, ref pivot);

        min = new Vector2Int(pivot.x, pivot.y);
        max = new Vector2Int(
            Mathf.Max(min.x, grid.Width - size.x + pivot.x),
            Mathf.Max(min.y, grid.Height - size.y + pivot.y));
    }

    public static void GetFootprintCells(
        FootprintMaskSO mask,
        Vector2Int size,
        Vector2Int pivot,
        Vector2Int anchor,
        List<Vector2Int> results)
    {
        if (results == null) return;
        results.Clear();

        ClampSizePivot(ref size, ref pivot);

        if (mask != null && mask.IsValid && mask.HasAnyFilled())
        {
            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    if (!mask.GetCell(x, y)) continue;
                    results.Add(new Vector2Int(anchor.x + x - pivot.x, anchor.y + y - pivot.y));
                }
            }
            return;
        }

        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
                results.Add(new Vector2Int(anchor.x + x - pivot.x, anchor.y + y - pivot.y));
        }
    }

    public static Vector2Int GetCenteredAnchor(GridSystem grid, Vector2Int size, Vector2Int pivot, bool evenBiasPositive)
    {
        if (grid == null) return Vector2Int.zero;

        ClampSizePivot(ref size, ref pivot);

        int baseX = Mathf.FloorToInt((grid.Width - size.x) * 0.5f);
        int baseY = Mathf.FloorToInt((grid.Height - size.y) * 0.5f);
        if (evenBiasPositive && (size.x % 2 == 0)) baseX += 1;
        if (evenBiasPositive && (size.y % 2 == 0)) baseY += 1;

        return new Vector2Int(baseX + pivot.x, baseY + pivot.y);
    }

    private static void ClampSizePivot(ref Vector2Int size, ref Vector2Int pivot)
    {
        size.x = Mathf.Max(1, size.x);
        size.y = Mathf.Max(1, size.y);
        pivot.x = Mathf.Clamp(pivot.x, 0, size.x - 1);
        pivot.y = Mathf.Clamp(pivot.y, 0, size.y - 1);
    }
}
