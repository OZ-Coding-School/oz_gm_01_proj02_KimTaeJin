using System.Collections.Generic;
using UnityEngine;

public sealed partial class PanelPreview3D : MonoBehaviour
{
    public void SyncFromGridView(PanelGridView grid)
    {
        if (grid == null) return;
        gridWidth = grid.Width;
        gridHeight = grid.Height;

        float cw = Mathf.Max(0.01f, grid.CellWidth);
        float ch = Mathf.Max(0.01f, grid.CellHeight);
        if (useGridViewCellAspect && useGridRootRectForAspect && gridRootRect != null)
        {
            float rectW = gridRootRect.rect.width;
            float rectH = gridRootRect.rect.height;
            if (rectW > 0f && rectH > 0f && gridWidth > 0 && gridHeight > 0)
            {
                cw = Mathf.Max(0.01f, rectW / gridWidth);
                ch = Mathf.Max(0.01f, rectH / gridHeight);
            }
        }
        float baseScale = Mathf.Max(0.01f, gridWorldScale);
        if (useGridCellSize && gridSystem != null)
            baseScale = Mathf.Max(0.01f, gridSystem.CellSizeX);
        cellWorldWidth = baseScale;
        if (useGridViewCellAspect)
        {
            _baseCellAspect = Mathf.Max(0.01f, ch / cw);
            cellWorldHeight = baseScale * _baseCellAspect * Mathf.Max(0.01f, cellHeightScale);
        }
        else
        {
            _baseCellAspect = 1f;
            cellWorldHeight = baseScale * Mathf.Max(0.01f, cellHeightScale);
        }
        ApplyCellAspectCompensation();

        FitRawImageToGrid();
        if (useWorldGridLines)
            RebuildGridLines();
        if (autoBuildTiles)
            RebuildTiles();
        ApplyCameraSettings();
    }

    public bool TryScreenToCell(Vector2 screen, Canvas canvas, Camera uiCamera, out Vector2Int cell)
    {
        cell = default;
        if (previewCamera == null || targetImage == null || canvas == null) return false;

        RectTransform rt = targetImage.rectTransform;
        if (rt == null) return false;

        Camera cam = (canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : uiCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screen, cam, out Vector2 local);

        Rect rect = rt.rect;
        if (rect.width <= 0f || rect.height <= 0f) return false;

        float u = (local.x - rect.xMin) / rect.width;
        float v = (local.y - rect.yMin) / rect.height;
        if (u < 0f || u > 1f || v < 0f || v > 1f) return false;

        Ray ray = previewCamera.ViewportPointToRay(new Vector3(u, v, 0f));
        Transform root = previewRoot != null ? previewRoot : transform;
        Vector3 planePoint = root.TransformPoint(new Vector3(0f, sceneOffset.y, 0f));
        Vector3 planeNormal = root.TransformDirection(Vector3.up);
        Plane plane = new Plane(planeNormal, planePoint);
        if (!plane.Raycast(ray, out float enter)) return false;

        Vector3 hit = ray.GetPoint(enter);
        Vector3 localHit = root.InverseTransformPoint(hit) - sceneOffset;
        return TryLocalToCell(localHit, out cell);
    }

    private void ApplyCellAspectCompensation()
    {
        if (!compensateTiltForSquareCells || !useOrthographic || lockCameraTopDown) return;
        if (cellWorldWidth <= 0.0001f) return;

        float pitch = Mathf.Abs(cameraEuler.x);
        if (previewCamera != null && !autoSetupCamera)
            pitch = Mathf.Abs(previewCamera.transform.localEulerAngles.x);

        float cos = Mathf.Cos(pitch * Mathf.Deg2Rad);
        if (cos < 0.0001f) return;

        float newHeight = cellWorldWidth * (_baseCellAspect / cos) * Mathf.Max(0.01f, cellHeightScale);
        if (Mathf.Approximately(newHeight, cellWorldHeight)) return;

        cellWorldHeight = newHeight;
        if (useWorldGridLines)
            RebuildGridLines();
        if (autoBuildTiles)
            RebuildTiles();
        RefreshInstances();
    }

    private void RefreshInstances()
    {
        if (_centerInstance != null)
        {
            FitToFootprint(_centerInstance, centerFootprint);
            PlaceAtGridCenter(_centerInstance.transform);
            ApplyCenterPreviewOffset(_centerInstance.transform);
        }

        if (_placementInstance != null)
        {
            FitToFootprint(_placementInstance, _placementFootprint);
            if (_hasPlacementCell)
                SetPlacementCell(_placementCell, false);
        }

        if (_placedCells.Count == 0) return;
        foreach (var kvp in _placedCells)
        {
            var t = kvp.Key;
            if (t == null) continue;
            Vector2Int footprint = Vector2Int.one;
            if (_placedFootprints.TryGetValue(t, out Vector2Int fp))
                footprint = fp;
            Vector2Int pivot = Vector2Int.zero;
            if (_placedPivots.TryGetValue(t, out Vector2Int p))
                pivot = p;
            FitToFootprint(t.gameObject, footprint);
            PlaceAtCell(t, kvp.Value, footprint, pivot, GetPlacedYOffset(t));
        }
    }

    private bool TryLocalToCell(Vector3 local, out Vector2Int cell)
    {
        cell = default;
        float totalW = gridWidth * cellWorldWidth;
        float totalH = gridHeight * cellWorldHeight;

        float lx = local.x + totalW * 0.5f;
        float lz = local.z + totalH * 0.5f;

        if (lx < 0f || lx >= totalW || lz < 0f || lz >= totalH)
            return false;

        int x = Mathf.FloorToInt(lx / cellWorldWidth);
        int y = Mathf.FloorToInt(lz / cellWorldHeight);
        cell = new Vector2Int(x, y);
        return true;
    }

    public void SetTileStates(bool[,] buildable, bool[,] occupied)
    {
        if (_tileRenderers == null) return;

        int w = _tileRenderers.GetLength(0);
        int h = _tileRenderers.GetLength(1);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var r = _tileRenderers[x, y];
                if (r == null) continue;

                bool isOccupied = occupied != null && occupied[x, y];
                bool isBuildable = buildable != null && buildable[x, y];

                Color c = isOccupied ? tileOccupiedColor : (isBuildable ? tileNormalColor : tileBlockedColor);
                ApplyColor(r, _tileMpb, c);
            }
        }
    }

    public void SetRoadCells(IReadOnlyCollection<Vector2Int> cells)
    {
        ClearRoadTiles();
        if (cells == null || cells.Count == 0) return;
        if (roadTilePrefab == null || previewRoot == null) return;

        EnsureRoadRoot();
        if (roadRoot == null) return;

        Vector3 baseScale = roadTilePrefab.transform.localScale;
        Bounds tileBounds = default;
        Vector3 tileCenter = Vector3.zero;
        bool hasTileBounds = false;

        float scale = roadMatchGridTileSettings ? tileGridScale : roadTileScale;
        Vector2 gridOffset = roadMatchGridTileSettings ? tileGridOffset : roadTileGridOffset;
        float yOffset = roadMatchGridTileSettings ? tileYOffset : roadTileYOffset;

        float tileScale = Mathf.Max(0.01f, scale);
        float tileCellWidth = cellWorldWidth * tileScale;
        float tileCellHeight = cellWorldHeight * tileScale;

        if (normalizeTileToCell)
        {
            tileBounds = GetPrefabBounds(roadTilePrefab);
            hasTileBounds = tileBounds.size.x > 0.0001f && tileBounds.size.z > 0.0001f;
            if (centerTileToCell && hasTileBounds)
                tileCenter = tileBounds.center;
        }
        float bottomOffset = (roadUseBottomOffset && hasTileBounds) ? -tileBounds.min.y : 0f;

        foreach (var cell in cells)
        {
            var go = Instantiate(roadTilePrefab, roadRoot);
            go.name = $"[RoadTile]{cell.x}_{cell.y}";
            PreparePreviewObject(go);

            Vector3 pos = CellToWorld(cell, tileCellWidth, tileCellHeight) + sceneOffset;
            Vector3 offset = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            if (normalizeTileToCell && hasTileBounds)
            {
                float sx = tileCellWidth / Mathf.Max(0.0001f, tileBounds.size.x);
                float sz = tileCellHeight / Mathf.Max(0.0001f, tileBounds.size.z);
                go.transform.localScale = new Vector3(baseScale.x * sx, baseScale.y, baseScale.z * sz);
                if (centerTileToCell)
                    offset = new Vector3(-tileCenter.x * sx, 0f, -tileCenter.z * sz);
            }
            else
            {
                go.transform.localScale = new Vector3(baseScale.x * tileCellWidth, baseScale.y, baseScale.z * tileCellHeight);
                if (centerTileToCell && hasTileBounds)
                    offset = new Vector3(-tileCenter.x * baseScale.x, 0f, -tileCenter.z * baseScale.z);
            }

            go.transform.localPosition = pos + new Vector3(gridOffset.x, yOffset + bottomOffset, gridOffset.y) + offset;
            _roadInstances.Add(go);
        }
    }

    public void ClearRoadTiles()
    {
        if (_roadInstances.Count == 0) return;

        for (int i = 0; i < _roadInstances.Count; i++)
        {
            var go = _roadInstances[i];
            if (go == null) continue;
            go.SetActive(false);
            Destroy(go);
        }
        _roadInstances.Clear();
    }

    private void RebuildGridLines()
    {
        if (!useWorldGridLines) return;
        if (previewRoot == null) return;

        EnsureLineRoot();
        if (_lineRoot == null) return;

        for (int i = _lineRoot.childCount - 1; i >= 0; i--)
            Destroy(_lineRoot.GetChild(i).gameObject);

        float totalW = gridWidth * cellWorldWidth;
        float totalH = gridHeight * cellWorldHeight;
        float halfW = totalW * 0.5f;
        float halfH = totalH * 0.5f;
        float y = lineYOffset;

        for (int x = 0; x <= gridWidth; x++)
        {
            float px = -halfW + x * cellWorldWidth;
            Vector3 a = new Vector3(px, y, -halfH);
            Vector3 b = new Vector3(px, y, halfH);
            CreateLine(a, b);
        }

        for (int yIdx = 0; yIdx <= gridHeight; yIdx++)
        {
            float pz = -halfH + yIdx * cellWorldHeight;
            Vector3 a = new Vector3(-halfW, y, pz);
            Vector3 b = new Vector3(halfW, y, pz);
            CreateLine(a, b);
        }
    }

    private void RebuildTiles()
    {
        if (!autoBuildTiles || tilePrefab == null || previewRoot == null) return;
        EnsureTileRoot();
        ClearTiles();

        _tileRenderers = new Renderer[gridWidth, gridHeight];
        Vector3 baseScale = tilePrefab.transform.localScale;
        Bounds tileBounds = default;
        Vector3 tileCenter = Vector3.zero;
        bool hasTileBounds = false;
        float tileScale = Mathf.Max(0.01f, tileGridScale);
        float tileCellWidth = cellWorldWidth * tileScale;
        float tileCellHeight = cellWorldHeight * tileScale;
        if (normalizeTileToCell)
        {
            tileBounds = GetPrefabBounds(tilePrefab);
            hasTileBounds = tileBounds.size.x > 0.0001f && tileBounds.size.z > 0.0001f;
            if (centerTileToCell && hasTileBounds)
                tileCenter = tileBounds.center;
        }

        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                var go = Instantiate(tilePrefab, tileRoot);
                go.name = $"[Tile]{x}_{y}";
                PreparePreviewObject(go);

                Vector3 pos = CellToWorld(new Vector2Int(x, y), tileCellWidth, tileCellHeight) + sceneOffset;
                Vector3 offset = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                if (normalizeTileToCell && hasTileBounds)
                {
                    float sx = tileCellWidth / Mathf.Max(0.0001f, tileBounds.size.x);
                    float sz = tileCellHeight / Mathf.Max(0.0001f, tileBounds.size.z);
                    go.transform.localScale = new Vector3(baseScale.x * sx, baseScale.y, baseScale.z * sz);
                    if (centerTileToCell)
                        offset = new Vector3(-tileCenter.x * sx, 0f, -tileCenter.z * sz);
                }
                else
                {
                    go.transform.localScale = new Vector3(baseScale.x * tileCellWidth, baseScale.y, baseScale.z * tileCellHeight);
                    if (centerTileToCell && hasTileBounds)
                        offset = new Vector3(-tileCenter.x * baseScale.x, 0f, -tileCenter.z * baseScale.z);
                }
                go.transform.localPosition = pos + new Vector3(tileGridOffset.x, tileYOffset, tileGridOffset.y) + offset;

                var renderer = go.GetComponentInChildren<Renderer>();
                _tileRenderers[x, y] = renderer;
                if (renderer != null)
                    ApplyColor(renderer, _tileMpb, tileNormalColor);
            }
        }
    }

    private void ClearTiles()
    {
        if (tileRoot == null) return;
        for (int i = tileRoot.childCount - 1; i >= 0; i--)
            Destroy(tileRoot.GetChild(i).gameObject);
        _tileRenderers = null;
    }

    private void EnsureTileRoot()
    {
        if (tileRoot != null) return;
        var go = new GameObject("[PreviewTiles]");
        tileRoot = go.transform;
        tileRoot.SetParent(previewRoot, false);
        PreparePreviewObject(tileRoot.gameObject);
    }

    private void EnsureRoadRoot()
    {
        if (roadRoot != null) return;
        var go = new GameObject("[PreviewRoadTiles]");
        roadRoot = go.transform;
        roadRoot.SetParent(previewRoot, false);
        PreparePreviewObject(roadRoot.gameObject);
    }

    private void EnsureLineRoot()
    {
        if (_lineRoot != null) return;
        var go = new GameObject("[PreviewGridLines]");
        _lineRoot = go.transform;
        _lineRoot.SetParent(previewRoot, false);
        PreparePreviewObject(_lineRoot.gameObject);
    }

    private void CreateLine(Vector3 a, Vector3 b)
    {
        var go = new GameObject("L");
        go.transform.SetParent(_lineRoot, false);
        go.layer = GetPreviewLayerIndex();

        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.useWorldSpace = false;
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.numCapVertices = 0;
        lr.numCornerVertices = 0;

        if (lineMaterial == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            lineMaterial = new Material(shader);
        }

        lr.material = lineMaterial;
        lr.startColor = lineColor;
        lr.endColor = lineColor;
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);
    }
}
