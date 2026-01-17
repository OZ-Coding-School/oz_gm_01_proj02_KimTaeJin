using System.Collections.Generic;
using UnityEngine;

public static class BuildGridRules
{
    public static void ComputeBuildable(GridSystem grid, bool[,] buildable, bool[,] occupied)
    {
        ComputeBuildable(grid, buildable, occupied, null);
    }

    public static void ComputeBuildable(GridSystem grid, bool[,] buildable, bool[,] occupied, HashSet<Vector2Int> extraOccupied)
    {
        if (grid == null) return;
        int w = grid.Width;
        int h = grid.Height;

        if (buildable != null && (buildable.GetLength(0) != w || buildable.GetLength(1) != h))
            return;
        if (occupied != null && (occupied.GetLength(0) != w || occupied.GetLength(1) != h))
            return;

        bool[] rowHas = new bool[h];
        bool[] colHas = new bool[w];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                bool occ = grid.IsOccupied(cell);
                bool extra = extraOccupied != null && extraOccupied.Contains(cell);
                if (occupied != null) occupied[x, y] = occ;
                if (occ || extra)
                {
                    rowHas[y] = true;
                    colHas[x] = true;
                }
            }
        }

        if (buildable == null) return;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
                buildable[x, y] = rowHas[y] || colHas[x];
        }
    }

    public static bool CanPlaceFootprint(GridSystem grid, Vector2Int anchor, Vector2Int footprint)
    {
        return CanPlaceFootprint(grid, null, footprint, Vector2Int.zero, anchor);
    }

    public static bool CanPlaceFootprint(GridSystem grid, FootprintMaskSO mask, Vector2Int size, Vector2Int pivot, Vector2Int anchor)
    {
        return CanPlaceFootprint(grid, mask, size, pivot, anchor, null);
    }

    public static bool CanPlaceFootprint(GridSystem grid, FootprintMaskSO mask, Vector2Int size, Vector2Int pivot, Vector2Int anchor,
        HashSet<Vector2Int> extraOccupied)
    {
        if (grid == null) return false;
        int w = grid.Width;
        int h = grid.Height;

        bool[] rowHas = new bool[h];
        bool[] colHas = new bool[w];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                bool occ = grid.IsOccupied(cell);
                bool extra = extraOccupied != null && extraOccupied.Contains(cell);
                if (!occ && !extra) continue;
                rowHas[y] = true;
                colHas[x] = true;
            }
        }

        var cells = new List<Vector2Int>();
        FootprintMaskUtility.GetFootprintCells(mask, size, pivot, anchor, cells);

        for (int i = 0; i < cells.Count; i++)
        {
            Vector2Int cell = cells[i];
            if (!grid.IsInBounds(cell)) return false;
            if (grid.IsOccupied(cell)) return false;
            if (!(rowHas[cell.y] || colHas[cell.x])) return false;
        }
        return true;
    }

}
