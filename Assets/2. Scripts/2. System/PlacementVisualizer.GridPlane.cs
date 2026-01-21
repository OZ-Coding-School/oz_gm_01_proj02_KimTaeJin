using System.Collections.Generic;
using UnityEngine;

public sealed partial class PlacementVisualizer : MonoBehaviour
{
    private void RebuildGridPlanes()
    {
        if (isWorldVisualizer) return;
        ClearGridPlanes();
        if (gridPlanePrefab == null || grid == null) return;

        int w = dataService != null && dataService.GridSystem != null ? dataService.GridSystem.Width : 1;
        int h = dataService != null && dataService.GridSystem != null ? dataService.GridSystem.Height : 1;
        Transform parent = gridPlaneRoot != null ? gridPlaneRoot : root;
        if (parent == null) return;

        _gridPlaneRenderers.Clear();

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Vector3Int cell = new Vector3Int(x, 0, y);
                Vector3 pos = GetCellCenterWorld(cell);
                pos.y += gridPlaneY;
                GameObject tile = Instantiate(gridPlanePrefab, pos, Quaternion.identity);
                AttachToParent(tile, parent);
                tile.name = $"GridPlane_{x}_{y}";
                Vector3 baseScale = tile.transform.localScale;
                Vector3 scale = baseScale;
                if (matchGridCellSize)
                    scale = new Vector3(baseScale.x * grid.cellSize.x, baseScale.y, baseScale.z * grid.cellSize.z);
                scale = Vector3.Scale(scale, gridPlaneScale);
                tile.transform.localScale = scale;
                DisableGameplay(tile);
                _gridPlanes.Add(tile);

                Renderer renderer = tile.GetComponentInChildren<Renderer>(true);
                if (renderer != null)
                {
                    _gridPlaneRenderers[new Vector2Int(x, y)] = renderer;
                    ApplyGridPlaneColor(renderer, gridPlaneNeutralColor);
                }
            }
        }
        MarkGridPlaneOverlayDirty();
    }

    private void RebuildGridLines()
    {
        if (isWorldVisualizer) return;
        ClearGridLines();
        if (!drawPanelGridLines || grid == null) return;

        int w = dataService != null && dataService.GridSystem != null ? dataService.GridSystem.Width : 1;
        int h = dataService != null && dataService.GridSystem != null ? dataService.GridSystem.Height : 1;

        Transform parent = gridPlaneRoot != null ? gridPlaneRoot : root;
        EnsureGridLineRoot(parent);
        if (_gridLineRoot == null) return;

        Material mat = GetGridLineMaterial();
        if (mat == null) return;

        Vector3 origin = grid.transform.position;
        float sizeX = grid.cellSize.x;
        float sizeZ = grid.cellSize.z;
        float y = origin.y + gridLineY;

        for (int i = 0; i <= w; i++)
        {
            float x = origin.x + i * sizeX;
            AddGridLine(new Vector3(x, y, origin.z), new Vector3(x, y, origin.z + h * sizeZ), mat);
        }

        for (int j = 0; j <= h; j++)
        {
            float z = origin.z + j * sizeZ;
            AddGridLine(new Vector3(origin.x, y, z), new Vector3(origin.x + w * sizeX, y, z), mat);
        }
    }

    private void EnsureGridLineRoot(Transform parent)
    {
        if (_gridLineRoot != null) return;
        Transform resolved = ResolveParent(parent);
        if (resolved == null) return;

        var go = new GameObject("GridLines3D");
        _gridLineRoot = go.transform;
        _gridLineRoot.SetParent(resolved, false);
        ApplyPanelLayer(go);
    }

    private Material GetGridLineMaterial()
    {
        if (gridLineMaterial != null)
        {
            ApplyLineMaterialColor(gridLineMaterial);
            return gridLineMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) return null;

        gridLineMaterial = new Material(shader);
        gridLineMaterial.name = "PanelGridLine (Runtime)";
        ApplyLineMaterialColor(gridLineMaterial);
        return gridLineMaterial;
    }

    private void ApplyLineMaterialColor(Material mat)
    {
        if (mat == null) return;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", gridLineColor);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", gridLineColor);
    }

    private void AddGridLine(Vector3 a, Vector3 b, Material mat)
    {
        if (_gridLineRoot == null || mat == null) return;

        var go = new GameObject("GridLine");
        go.transform.SetParent(_gridLineRoot, false);
        ApplyPanelLayer(go);

        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.material = mat;
        lr.widthMultiplier = gridLineWidth;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.startColor = gridLineColor;
        lr.endColor = gridLineColor;
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);

        _gridLines.Add(lr);
    }

    private void ClearGridPlanes()
    {
        for (int i = 0; i < _gridPlanes.Count; i++)
        {
            if (_gridPlanes[i] != null)
                Destroy(_gridPlanes[i]);
        }
        _gridPlanes.Clear();
        _gridPlaneRenderers.Clear();
        _gridPlaneBaseColors.Clear();
        _roadCells.Clear();
        ClearPanelRoadTiles();
        ClearGridLines();
    }

    private void ClearGridLines()
    {
        for (int i = 0; i < _gridLines.Count; i++)
        {
            if (_gridLines[i] != null)
                Destroy(_gridLines[i].gameObject);
        }
        _gridLines.Clear();

        if (_gridLineRoot != null)
        {
            Destroy(_gridLineRoot.gameObject);
            _gridLineRoot = null;
        }
    }

    private void UpdateGridPlaneColors(Vector3Int cell, GridDataService.PlacementResult result)
    {
        if (isWorldVisualizer) return;
        if (!tintGridPlanesOnHover) return;
        if (result.previewDef == null) return;

        ResetGridPlaneColors();

        _hoverCells.Clear();
        FootprintMaskUtility.GetFootprintData(result.previewDef, out FootprintMaskSO mask, out Vector2Int size, out Vector2Int pivot);
        FootprintMaskUtility.GetFootprintCells(mask, size, pivot, ToCell2D(cell), _hoverCells);

        Color c = result.canPlace ? gridPlaneCanPlaceColor : gridPlaneCannotPlaceColor;
        for (int i = 0; i < _hoverCells.Count; i++)
        {
            if (_gridPlaneRenderers.TryGetValue(_hoverCells[i], out Renderer renderer))
                ApplyGridPlaneColor(renderer, c);
        }
    }

    private void ResetGridPlaneColors()
    {
        if (isWorldVisualizer) return;
        if (!tintGridPlanesOnHover) return;

        foreach (var kvp in _gridPlaneRenderers)
        {
            if (_gridPlaneBaseColors.TryGetValue(kvp.Key, out Color baseColor))
                ApplyGridPlaneColor(kvp.Value, baseColor);
            else
                ApplyGridPlaneColor(kvp.Value, gridPlaneNeutralColor);
        }
    }

    private void ApplyGridPlaneColor(Renderer renderer, Color color)
    {
        if (renderer == null) return;
        renderer.GetPropertyBlock(_gridPlaneMpb);
        if (renderer.sharedMaterial != null)
        {
            if (renderer.sharedMaterial.HasProperty("_BaseColor"))
                _gridPlaneMpb.SetColor("_BaseColor", color);
            if (renderer.sharedMaterial.HasProperty("_Color"))
                _gridPlaneMpb.SetColor("_Color", color);
        }
        renderer.SetPropertyBlock(_gridPlaneMpb);
    }

    private void EnsureCenterObject()
    {
        if (isWorldVisualizer) return;
        if (centerPrefab == null || grid == null) return;
        if (_centerInstance != null) return;
        if (root == null) return;

        Vector3Int anchor = dataService != null ? dataService.GetAnchorCell() : Vector3Int.zero;
        Vector3 target = GetCellCenterWorld(anchor);
        _centerInstance = Instantiate(centerPrefab, target, Quaternion.identity);
        AttachToParent(_centerInstance, root);
        _centerInstance.name = "[CenterObject]";
        DisableGameplay(_centerInstance);
        ApplyPanelBasePlateScale(_centerInstance);
        AlignCenterToGridAnchor(_centerInstance, target);
    }

    private void ClearCenterObject()
    {
        if (_centerInstance == null) return;
        ClearPanelBasePlateScaleCache(_centerInstance);
        Destroy(_centerInstance);
        _centerInstance = null;
    }

    private void MarkGridPlaneOverlayDirty()
    {
        if (isWorldVisualizer) return;
        _overlayDirty = true;
    }

    private void UpdateGridPlaneBaseColors()
    {
        if (isWorldVisualizer) return;
        bool hasGridPlanes = _gridPlaneRenderers.Count > 0;
        if (!hasGridPlanes && !spawnPanelRoadTiles) return;

        GridSystem gridSystem = dataService != null ? dataService.GridSystem : null;
        if (gridSystem == null)
        {
            if (hasGridPlanes)
            {
                _gridPlaneBaseColors.Clear();
                foreach (var kvp in _gridPlaneRenderers)
                {
                    _gridPlaneBaseColors[kvp.Key] = gridPlaneNeutralColor;
                    ApplyGridPlaneColor(kvp.Value, gridPlaneNeutralColor);
                }
            }
            ClearPanelRoadTiles();
            return;
        }

        int w = gridSystem.Width;
        int h = gridSystem.Height;

        if (showBuildableOverlay)
            EnsureOverlayBuffers(w, h);

        bool needRoadCells = showBuildableOverlay || showRoadOverlay || spawnPanelRoadTiles;
        _roadCells.Clear();
        if (needRoadCells)
        {
            RunScope resolvedScope = scope != null ? scope : RunScopeLocator.Current;
            IReadOnlyList<TowerEntity> towers = resolvedScope != null && resolvedScope.Entities != null ? resolvedScope.Entities.Towers : null;
            BaseFootprintReserver baseFootprint = resolvedScope != null ? resolvedScope.BaseFootprintReserver : null;
            Vector2Int anchor = dataService != null ? ToCell2D(dataService.GetAnchorCell()) : Vector2Int.zero;
            GridRoadUtility.BuildRoadCells(gridSystem, anchor, baseFootprint, towers, _roadCells);
        }

        if (showBuildableOverlay)
            BuildGridRules.ComputeBuildable(gridSystem, _buildable, null, _roadCells);

        if (hasGridPlanes)
        {
            _gridPlaneBaseColors.Clear();
            foreach (var kvp in _gridPlaneRenderers)
            {
                Vector2Int cell = kvp.Key;
                Color c = gridPlaneNeutralColor;

                if (showBuildableOverlay && _buildable != null)
                {
                    if (cell.x >= 0 && cell.x < w && cell.y >= 0 && cell.y < h)
                        c = _buildable[cell.x, cell.y] ? gridPlaneBuildableColor : gridPlaneUnbuildableColor;
                }

                if (showRoadOverlay && _roadCells.Contains(cell))
                    c = gridPlaneRoadColor;

                _gridPlaneBaseColors[cell] = c;
                ApplyGridPlaneColor(kvp.Value, c);
            }
        }

        UpdatePanelRoadTiles();
    }

    private void EnsureOverlayBuffers(int w, int h)
    {
        if (_buildable == null || _buildable.GetLength(0) != w || _buildable.GetLength(1) != h)
            _buildable = new bool[w, h];
    }

    private void AlignCenterToGridAnchor(GameObject instance, Vector3 target)
    {
        if (instance == null) return;
        Transform anchor = FindChildByName(instance.transform, "GridAnchor");
        if (anchor == null)
        {
            instance.transform.position = target + centerOffset;
            return;
        }

        Vector3 offset = anchor.position - instance.transform.position;
        instance.transform.position = target - offset + centerOffset;
    }
}
