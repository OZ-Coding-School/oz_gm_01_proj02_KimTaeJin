using UnityEngine;

public sealed partial class PlacementVisualizer : MonoBehaviour
{
    private void UpdatePanelRoadTiles()
    {
        if (isWorldVisualizer) return;
        if (!spawnPanelRoadTiles)
        {
            ClearPanelRoadTiles();
            return;
        }
        if (grid == null)
        {
            ClearPanelRoadTiles();
            return;
        }

        if (_roadCells.Count == 0)
        {
            ClearPanelRoadTiles();
            return;
        }

        GameObject prefab = ResolvePanelRoadPrefab();
        if (prefab == null)
        {
            ClearPanelRoadTiles();
            return;
        }

        if (_panelRoadTilePrefabCache != prefab)
        {
            ClearPanelRoadTiles();
            _panelRoadTileMetricsCached = false;
            _panelRoadTilePrefabCache = prefab;
        }

        CachePanelRoadTileMetrics(prefab);
        GetPanelRoadTileTransform(out Vector3 tileScale, out Vector3 tileOffset);
        Quaternion rot = Quaternion.Euler(panelRoadRotation);
        Vector3 rotatedOffset = rot * tileOffset;
        EnsurePanelRoadRoot();

        _panelRoadRemove.Clear();
        foreach (var kvp in _panelRoadTiles)
        {
            if (!_roadCells.Contains(kvp.Key))
                _panelRoadRemove.Add(kvp.Key);
        }

        for (int i = 0; i < _panelRoadRemove.Count; i++)
        {
            Vector2Int cell = _panelRoadRemove[i];
            if (_panelRoadTiles.TryGetValue(cell, out GameObject go))
            {
                if (go != null) Destroy(go);
                _panelRoadTiles.Remove(cell);
            }
        }

        foreach (var cell in _roadCells)
        {
            Vector3 pos = GetCellCenterWorld(new Vector3Int(cell.x, 0, cell.y));
            pos += rotatedOffset + new Vector3(panelRoadTileGridOffset.x, 0f, panelRoadTileGridOffset.y);
            float surfaceY = pos.y + gridPlaneY;
            if (_gridPlaneRenderers.TryGetValue(cell, out Renderer renderer) && renderer != null)
                surfaceY = renderer.bounds.max.y;
            pos.y = surfaceY + panelRoadTileYOffset + (usePanelRoadBottomOffset ? _panelRoadTileBottomOffset : 0f);

            if (_panelRoadTiles.TryGetValue(cell, out GameObject existing))
            {
                if (existing != null)
                {
                    existing.transform.SetPositionAndRotation(pos, rot);
                    existing.transform.localScale = tileScale;
                    continue;
                }
                _panelRoadTiles.Remove(cell);
            }

            var go = Instantiate(prefab, pos, rot);
            AttachToParent(go, panelRoadRoot != null ? panelRoadRoot : (gridPlaneRoot != null ? gridPlaneRoot : root));
            go.transform.localScale = tileScale;
            go.name = $"PanelRoad_{cell.x}_{cell.y}";
            DisableGameplay(go);
            _panelRoadTiles[cell] = go;
        }
    }

    private void ClearPanelRoadTiles()
    {
        foreach (var kvp in _panelRoadTiles)
        {
            if (kvp.Value != null) Destroy(kvp.Value);
        }
        _panelRoadTiles.Clear();
        _panelRoadRemove.Clear();
    }

    private void EnsurePanelRoadRoot()
    {
        if (panelRoadRoot != null) return;
        Transform parent = gridPlaneRoot != null ? gridPlaneRoot : root;
        Transform resolved = ResolveParent(parent);
        if (resolved == null) return;

        var go = new GameObject("PanelRoadTiles");
        panelRoadRoot = go.transform;
        panelRoadRoot.SetParent(resolved, false);
        ApplyPanelLayer(go);
    }

    private GameObject ResolvePanelRoadPrefab()
    {
        if (panelRoadTilePrefab != null) return panelRoadTilePrefab;
        if (!useBuildCellPrefabFallback) return null;
        return GameRoot.Instance != null ? GameRoot.Instance.BuildCellSizePrefab : null;
    }

    private void CachePanelRoadTileMetrics(GameObject prefab)
    {
        if (_panelRoadTileMetricsCached) return;
        _panelRoadTileMetricsCached = true;
        _panelRoadTileBottomOffset = 0f;
        _panelRoadTileHasBounds = false;
        _panelRoadTileCenter = Vector3.zero;
        _panelRoadTileBounds = default;
        _panelRoadTileBaseScale = prefab != null ? prefab.transform.localScale : Vector3.one;

        if (prefab == null) return;

        _panelRoadTileBounds = GetPrefabBounds(prefab);
        _panelRoadTileHasBounds = _panelRoadTileBounds.size.x > 0.0001f && _panelRoadTileBounds.size.z > 0.0001f;
        if (_panelRoadTileHasBounds && centerPanelRoadToCell)
            _panelRoadTileCenter = _panelRoadTileBounds.center;

        if (usePanelRoadBottomOffset && _panelRoadTileBounds.size.y > 0.0001f)
            _panelRoadTileBottomOffset = -_panelRoadTileBounds.min.y * Mathf.Max(0.01f, panelRoadTileScaleMultiplier);
    }

    private void GetPanelRoadTileTransform(out Vector3 scale, out Vector3 offset)
    {
        scale = _panelRoadTileBaseScale;
        offset = Vector3.zero;

        float mul = Mathf.Max(0.01f, panelRoadTileScaleMultiplier);
        if (normalizePanelRoadToCell && _panelRoadTileHasBounds && grid != null)
        {
            float cellX = grid.cellSize.x;
            float cellZ = grid.cellSize.z;
            GridSystem gridSystem = dataService != null ? dataService.GridSystem : null;
            if (isWorldVisualizer && gridSystem != null)
            {
                cellX = gridSystem.CellSizeX;
                cellZ = gridSystem.CellSizeZ;
            }
            float sx = cellX / Mathf.Max(0.0001f, _panelRoadTileBounds.size.x);
            float sz = cellZ / Mathf.Max(0.0001f, _panelRoadTileBounds.size.z);
            scale = new Vector3(_panelRoadTileBaseScale.x * sx, _panelRoadTileBaseScale.y, _panelRoadTileBaseScale.z * sz) * mul;
            if (centerPanelRoadToCell)
                offset = new Vector3(-_panelRoadTileCenter.x * sx, 0f, -_panelRoadTileCenter.z * sz);
        }
        else
        {
            scale = _panelRoadTileBaseScale * mul;
            if (centerPanelRoadToCell && _panelRoadTileHasBounds)
                offset = new Vector3(-_panelRoadTileCenter.x * _panelRoadTileBaseScale.x, 0f, -_panelRoadTileCenter.z * _panelRoadTileBaseScale.z);
        }

        if (matchPanelRoadToGridPlaneScale)
        {
            scale = new Vector3(scale.x * gridPlaneScale.x, scale.y, scale.z * gridPlaneScale.z);
            offset = new Vector3(offset.x * gridPlaneScale.x, offset.y, offset.z * gridPlaneScale.z);
        }
    }
}
