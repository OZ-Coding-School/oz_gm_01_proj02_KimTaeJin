using System.Collections.Generic;
using UnityEngine;

public static class GridRoadUtility
{
    private static readonly Vector2Int[] Neighbors =
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1)
    };

    public readonly struct RoadTower
    {
        public readonly Vector2Int Anchor;
        public readonly TowerDefinitionSO Def;
        public readonly int Order;

        public RoadTower(Vector2Int anchor, TowerDefinitionSO def, int order)
        {
            Anchor = anchor;
            Def = def;
            Order = order;
        }
    }

    public static void BuildRoadCells(
        GridSystem grid,
        Vector2Int centerAnchor,
        BaseFootprintReserver baseFootprint,
        IReadOnlyList<TowerEntity> towers,
        HashSet<Vector2Int> results)
    {
        if (results == null) return;
        results.Clear();
        if (grid == null || towers == null || towers.Count == 0) return;

        var nodes = new List<TowerNode>();
        for (int i = 0; i < towers.Count; i++)
        {
            var tower = towers[i];
            if (tower == null) continue;
            var def = tower.Definition;
            if (def == null) continue;

            Vector2Int anchor = tower.Cell;
            if (!grid.IsInBounds(anchor)) continue;

            FootprintMaskUtility.GetFootprintData(def, out FootprintMaskSO mask, out Vector2Int size, out Vector2Int pivot);
            var footprint = new List<Vector2Int>();
            FootprintMaskUtility.GetFootprintCells(mask, size, pivot, anchor, footprint);
            nodes.Add(new TowerNode(anchor, footprint));
        }

        BuildRoadCellsInternal(grid, centerAnchor, baseFootprint, nodes, results);
    }

    public static void BuildRoadCells(
        GridSystem grid,
        Vector2Int centerAnchor,
        BaseFootprintReserver baseFootprint,
        IReadOnlyList<RoadTower> towers,
        HashSet<Vector2Int> results)
    {
        if (results == null) return;
        results.Clear();
        if (grid == null || towers == null || towers.Count == 0) return;

        var ordered = new List<RoadTower>(towers);
        ordered.Sort((a, b) =>
        {
            int o = a.Order.CompareTo(b.Order);
            if (o != 0) return o;
            int x = a.Anchor.x.CompareTo(b.Anchor.x);
            return x != 0 ? x : a.Anchor.y.CompareTo(b.Anchor.y);
        });

        var nodes = new List<TowerNode>();
        for (int i = 0; i < ordered.Count; i++)
        {
            RoadTower tower = ordered[i];
            if (tower.Def == null) continue;
            if (!grid.IsInBounds(tower.Anchor)) continue;

            FootprintMaskUtility.GetFootprintData(tower.Def, out FootprintMaskSO mask, out Vector2Int size, out Vector2Int pivot);
            var footprint = new List<Vector2Int>();
            FootprintMaskUtility.GetFootprintCells(mask, size, pivot, tower.Anchor, footprint);
            nodes.Add(new TowerNode(tower.Anchor, footprint));
        }

        BuildRoadCellsInternal(grid, centerAnchor, baseFootprint, nodes, results);
    }

    private static void BuildRoadCellsInternal(
        GridSystem grid,
        Vector2Int centerAnchor,
        BaseFootprintReserver baseFootprint,
        List<TowerNode> nodes,
        HashSet<Vector2Int> results)
    {
        if (results == null) return;
        results.Clear();

        if (grid == null || nodes == null || nodes.Count == 0) return;
        if (!grid.IsInBounds(centerAnchor)) return;

        var centerFootprint = new HashSet<Vector2Int>();
        var cellBuffer = new List<Vector2Int>();

        if (baseFootprint != null && baseFootprint.TryGetOccupiedCells(cellBuffer))
        {
            for (int i = 0; i < cellBuffer.Count; i++)
            {
                if (grid.IsInBounds(cellBuffer[i]))
                    centerFootprint.Add(cellBuffer[i]);
            }
        }
        else if (baseFootprint != null && baseFootprint.UseFixedFootprint)
        {
            FootprintMaskUtility.GetFootprintData(baseFootprint, out FootprintMaskSO mask, out Vector2Int size, out Vector2Int pivot);
            FootprintMaskUtility.GetFootprintCells(mask, size, pivot, centerAnchor, cellBuffer);
            for (int i = 0; i < cellBuffer.Count; i++)
            {
                if (grid.IsInBounds(cellBuffer[i]))
                    centerFootprint.Add(cellBuffer[i]);
            }
        }

        if (centerFootprint.Count == 0)
            centerFootprint.Add(centerAnchor);

        var centerFootprintList = new List<Vector2Int>(centerFootprint);

        var passable = new HashSet<Vector2Int>();
        var priorTowerCells = new HashSet<Vector2Int>();
        var exclude = new HashSet<Vector2Int>();
        var pathA = new List<Vector2Int>();
        var pathB = new List<Vector2Int>();
        var candidatePath = new List<Vector2Int>();
        var bestPath = new List<Vector2Int>();

        for (int i = 0; i < nodes.Count; i++)
        {
            TowerNode node = nodes[i];
            Vector2Int start = node.Anchor;
            List<Vector2Int> startFootprint = node.Footprint;

            int bestDist = int.MaxValue;
            int bestBlocked = int.MaxValue;
            List<Vector2Int> bestTargetFootprint = null;
            bestPath.Clear();

            EvaluateCandidate(grid, start, startFootprint, centerAnchor, centerFootprintList, priorTowerCells,
                passable, pathA, pathB, candidatePath,
                ref bestDist, ref bestBlocked, ref bestTargetFootprint, bestPath);

            for (int t = 0; t < i; t++)
            {
                TowerNode other = nodes[t];
                EvaluateCandidate(grid, start, startFootprint, other.Anchor, other.Footprint, priorTowerCells,
                    passable, pathA, pathB, candidatePath,
                    ref bestDist, ref bestBlocked, ref bestTargetFootprint, bestPath);
            }

            if (bestPath.Count == 0 || bestTargetFootprint == null) continue;

            exclude.Clear();
            AddFootprint(exclude, startFootprint);
            AddFootprint(exclude, bestTargetFootprint);

            for (int p = 0; p < bestPath.Count; p++)
            {
                var cell = bestPath[p];
                if (exclude.Contains(cell)) continue;
                if (grid.IsOccupied(cell)) continue;
                results.Add(cell);
            }

            AddFootprint(priorTowerCells, startFootprint);
        }
    }

    private sealed class TowerNode
    {
        public readonly Vector2Int Anchor;
        public readonly List<Vector2Int> Footprint;

        public TowerNode(Vector2Int anchor, List<Vector2Int> footprint)
        {
            Anchor = anchor;
            Footprint = footprint;
        }
    }

    private static void EvaluateCandidate(
        GridSystem grid,
        Vector2Int start,
        List<Vector2Int> startFootprint,
        Vector2Int end,
        List<Vector2Int> endFootprint,
        HashSet<Vector2Int> extraPassable,
        HashSet<Vector2Int> passable,
        List<Vector2Int> pathA,
        List<Vector2Int> pathB,
        List<Vector2Int> candidatePath,
        ref int bestDist,
        ref int bestBlocked,
        ref List<Vector2Int> bestTargetFootprint,
        List<Vector2Int> bestPath)
    {
        int dist = Manhattan(start, end);
        if (dist > bestDist) return;

        passable.Clear();
        AddFootprint(passable, startFootprint);
        AddFootprint(passable, endFootprint);
        passable.Add(start);
        passable.Add(end);
        AddCells(passable, extraPassable);

        int blocked = BuildBestLPath(grid, start, end, passable, pathA, pathB, candidatePath);
        if (dist < bestDist || (dist == bestDist && blocked < bestBlocked))
        {
            bestDist = dist;
            bestBlocked = blocked;
            bestTargetFootprint = endFootprint;
            bestPath.Clear();
            bestPath.AddRange(candidatePath);
        }
    }

    private static int BuildBestLPath(
        GridSystem grid,
        Vector2Int start,
        Vector2Int end,
        HashSet<Vector2Int> passable,
        List<Vector2Int> pathA,
        List<Vector2Int> pathB,
        List<Vector2Int> bestPath)
    {
        BuildLPath(start, end, true, pathA);
        int blockedA = CountBlocked(grid, pathA, passable);
        BuildLPath(start, end, false, pathB);
        int blockedB = CountBlocked(grid, pathB, passable);

        if (blockedA <= blockedB)
        {
            bestPath.Clear();
            bestPath.AddRange(pathA);
            return blockedA;
        }

        bestPath.Clear();
        bestPath.AddRange(pathB);
        return blockedB;
    }

    private static void BuildLPath(Vector2Int start, Vector2Int end, bool horizontalFirst, List<Vector2Int> path)
    {
        if (path == null) return;
        path.Clear();
        path.Add(start);

        Vector2Int corner = horizontalFirst
            ? new Vector2Int(end.x, start.y)
            : new Vector2Int(start.x, end.y);

        AppendLine(start, corner, path, true);
        AppendLine(corner, end, path, true);
    }

    private static void AppendLine(Vector2Int from, Vector2Int to, List<Vector2Int> path, bool skipFirst)
    {
        int dx = Mathf.Clamp(to.x - from.x, -1, 1);
        int dy = Mathf.Clamp(to.y - from.y, -1, 1);

        int x = from.x;
        int y = from.y;
        bool first = true;

        while (true)
        {
            if (!(skipFirst && first))
                path.Add(new Vector2Int(x, y));

            if (x == to.x && y == to.y) break;

            x += dx;
            y += dy;
            first = false;
        }
    }

    private static int CountBlocked(GridSystem grid, List<Vector2Int> path, HashSet<Vector2Int> passable)
    {
        if (grid == null || path == null) return 0;
        int blocked = 0;
        for (int i = 0; i < path.Count; i++)
        {
            if (IsBlocked(grid, path[i], passable))
                blocked++;
        }
        return blocked;
    }

    private static int Manhattan(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private static void AddFootprint(HashSet<Vector2Int> set, List<Vector2Int> cells)
    {
        if (set == null || cells == null) return;
        for (int i = 0; i < cells.Count; i++)
            set.Add(cells[i]);
    }

    private static void AddCells(HashSet<Vector2Int> set, HashSet<Vector2Int> cells)
    {
        if (set == null || cells == null) return;
        foreach (var cell in cells)
            set.Add(cell);
    }

    public static bool TryFindShortestPath(
        GridSystem grid,
        Vector2Int start,
        Vector2Int goal,
        HashSet<Vector2Int> passable,
        List<Vector2Int> path)
    {
        if (path == null) return false;
        path.Clear();

        if (grid == null) return false;
        if (!grid.IsInBounds(start) || !grid.IsInBounds(goal)) return false;

        if (start == goal)
        {
            path.Add(start);
            return true;
        }

        int w = grid.Width;
        int h = grid.Height;
        var visited = new bool[w, h];
        var prev = new Vector2Int[w, h];
        var hasPrev = new bool[w, h];
        var queue = new Queue<Vector2Int>();

        visited[start.x, start.y] = true;
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (cur == goal) break;

            for (int i = 0; i < Neighbors.Length; i++)
            {
                Vector2Int next = cur + Neighbors[i];
                if (!grid.IsInBounds(next)) continue;
                if (visited[next.x, next.y]) continue;
                if (IsBlocked(grid, next, passable)) continue;

                visited[next.x, next.y] = true;
                prev[next.x, next.y] = cur;
                hasPrev[next.x, next.y] = true;
                queue.Enqueue(next);
            }
        }

        if (!visited[goal.x, goal.y]) return false;

        var step = goal;
        path.Add(step);
        while (step != start)
        {
            if (!hasPrev[step.x, step.y]) break;
            step = prev[step.x, step.y];
            path.Add(step);
        }

        path.Reverse();
        return true;
    }

    private static bool IsBlocked(GridSystem grid, Vector2Int cell, HashSet<Vector2Int> passable)
    {
        if (grid == null) return true;
        if (!grid.IsOccupied(cell)) return false;
        if (passable == null) return true;
        return !passable.Contains(cell);
    }
}
