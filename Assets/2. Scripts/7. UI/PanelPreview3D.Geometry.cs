using UnityEngine;

public sealed partial class PanelPreview3D : MonoBehaviour
{
    private Vector2Int GetCenterCell()
        => CenterCell;

    private void PlaceAtCell(Transform t, Vector2Int cell, Vector2Int footprint)
    {
        PlaceAtCell(t, cell, footprint, Vector2Int.zero, 0f);
    }

    private void PlaceAtCell(Transform t, Vector2Int cell, Vector2Int footprint, Vector2Int pivot)
    {
        PlaceAtCell(t, cell, footprint, pivot, 0f);
    }

    private void PlaceAtCell(Transform t, Vector2Int cell, Vector2Int footprint, Vector2Int pivot, float yOffset)
    {
        Vector3 pos = GetCellLocalPosition(cell, footprint, pivot, t);
        if (!Mathf.Approximately(yOffset, 0f))
            pos.y += yOffset;
        t.localPosition = pos;
    }

    private void PlaceAtGridCenter(Transform t)
    {
        Vector2Int anchor = GetCenteredFootprintAnchor(centerFootprint, _centerPivot);
        PlaceAtCell(t, anchor, centerFootprint, _centerPivot, 0f);
    }

    private void ApplyCenterPreviewOffset(Transform t)
    {
        if (t == null) return;
        float yOffset = centerPreviewYOffset;
        if (matchCenterToRoadHeight)
            yOffset += GetRoadTileLift(t);
        Vector3 offset = centerPreviewOffset;
        if (!Mathf.Approximately(yOffset, 0f))
            offset += Vector3.up * yOffset;
        if (offset == Vector3.zero) return;
        t.localPosition += offset;
    }

    private float GetPlacedYOffset(Transform target)
    {
        float yOffset = placedPreviewYOffset;
        if (matchPlacedToRoadHeight)
            yOffset += GetRoadTileLift(target);
        return yOffset;
    }

    private float GetRoadTileLift(Transform target)
    {
        GameObject prefab = roadTilePrefab != null ? roadTilePrefab : tilePrefab;
        if (prefab == null) return 0f;

        float yOffset = roadMatchGridTileSettings ? tileYOffset : roadTileYOffset;

        Bounds b = GetPrefabBounds(prefab);
        if (b.size.y <= 0.0001f) return yOffset;

        bool hasFootprint = target != null && TryGetFootprintNode(target, out _);
        if (hasFootprint)
        {
            if (!roadUseBottomOffset)
                return yOffset + b.min.y;
            return yOffset;
        }

        if (!roadUseBottomOffset)
            return yOffset + b.max.y;

        return yOffset + b.size.y;
    }

    private void CacheCenterGridAnchor()
    {
        _centerGridAnchor = null;
        if (_centerInstance == null || string.IsNullOrEmpty(centerGridAnchorName)) return;
        _centerGridAnchor = FindChildByName(_centerInstance.transform, centerGridAnchorName);
    }

    private Transform GetPlacedParent()
    {
        if (useCenterGridAnchorForPlaced && _centerGridAnchor != null)
            return _centerGridAnchor;
        return previewRoot;
    }

    private void ReparentPlacedInstance(GameObject go)
    {
        if (go == null) return;
        Transform parent = GetPlacedParent();
        if (parent == null || go.transform.parent == parent) return;
        go.transform.SetParent(parent, true);
    }

    private void DetachPreviewsFromCenter()
    {
        if (_centerGridAnchor == null || previewRoot == null) return;
        if (_placementInstance != null && _placementInstance.transform.IsChildOf(_centerGridAnchor))
            _placementInstance.transform.SetParent(previewRoot, true);
        if (_hiddenPlaced != null && _hiddenPlaced.IsChildOf(_centerGridAnchor))
            _hiddenPlaced.SetParent(previewRoot, true);
        for (int i = 0; i < _placedInstances.Count; i++)
        {
            var go = _placedInstances[i];
            if (go == null) continue;
            if (go.transform.IsChildOf(_centerGridAnchor))
                go.transform.SetParent(previewRoot, true);
        }
    }

    private void ReparentPlacedInstances()
    {
        Transform parent = GetPlacedParent();
        if (parent == null) return;
        if (_placementInstance != null)
            _placementInstance.transform.SetParent(parent, true);
        if (_hiddenPlaced != null)
            _hiddenPlaced.SetParent(parent, true);
        for (int i = 0; i < _placedInstances.Count; i++)
        {
            var go = _placedInstances[i];
            if (go == null) continue;
            go.transform.SetParent(parent, true);
        }
    }

    private Vector3 CellToWorld(Vector2Int cell)
    {
        float totalW = gridWidth * cellWorldWidth;
        float totalH = gridHeight * cellWorldHeight;

        float x = -totalW * 0.5f + (cell.x + 0.5f) * cellWorldWidth;
        float z = -totalH * 0.5f + (cell.y + 0.5f) * cellWorldHeight;
        return new Vector3(x, 0f, z);
    }

    private Vector3 CellToWorld(Vector2Int cell, float cellWidth, float cellHeight)
    {
        float totalW = gridWidth * cellWidth;
        float totalH = gridHeight * cellHeight;

        float x = -totalW * 0.5f + (cell.x + 0.5f) * cellWidth;
        float z = -totalH * 0.5f + (cell.y + 0.5f) * cellHeight;
        return new Vector3(x, 0f, z);
    }

    private Vector3 GetCellWorldPosition(Vector2Int cell, Vector2Int footprint, Vector2Int pivot, Transform t)
    {
        Vector3 pos = CellToWorld(cell);
        Vector3 footprintOffset = GetFootprintOffset(footprint, pivot);

        var renderers = GetRenderersForBounds(t, out _);
        Bounds b = GetBounds(renderers);
        Vector3 localCenter = t.InverseTransformPoint(b.center);
        Vector3 localExtents = t.InverseTransformVector(b.extents);
        float bottomOffset = -(localCenter.y - localExtents.y);
        Vector3 centerOffset = new Vector3(-localCenter.x, 0f, -localCenter.z);

        return pos + footprintOffset + centerOffset + new Vector3(0f, bottomOffset, 0f) + sceneOffset;
    }

    private Vector3 GetCellLocalPosition(Vector2Int cell, Vector2Int footprint, Vector2Int pivot, Transform t)
    {
        Vector3 previewLocal = GetCellWorldPosition(cell, footprint, pivot, t);
        if (t == null) return previewLocal;

        Transform parent = t.parent;
        if (parent == null || previewRoot == null || parent == previewRoot)
            return previewLocal;

        Vector3 world = previewRoot.TransformPoint(previewLocal);
        return parent.InverseTransformPoint(world);
    }

    private Vector3 GetFootprintOffset(Vector2Int footprint, Vector2Int pivot)
    {
        if (footprint.x < 1) footprint.x = 1;
        if (footprint.y < 1) footprint.y = 1;
        pivot.x = Mathf.Clamp(pivot.x, 0, footprint.x - 1);
        pivot.y = Mathf.Clamp(pivot.y, 0, footprint.y - 1);
        float ox = ((footprint.x - 1) * 0.5f - pivot.x) * cellWorldWidth;
        float oz = ((footprint.y - 1) * 0.5f - pivot.y) * cellWorldHeight;
        return new Vector3(ox, 0f, oz);
    }

    private Vector2Int GetCenteredFootprintAnchor(Vector2Int footprint, Vector2Int pivot)
    {
        if (footprint.x < 1) footprint.x = 1;
        if (footprint.y < 1) footprint.y = 1;
        pivot.x = Mathf.Clamp(pivot.x, 0, footprint.x - 1);
        pivot.y = Mathf.Clamp(pivot.y, 0, footprint.y - 1);

        int maxX = Mathf.Max(0, gridWidth - footprint.x);
        int maxY = Mathf.Max(0, gridHeight - footprint.y);
        int x = Mathf.FloorToInt((gridWidth - footprint.x) * 0.5f);
        int y = Mathf.FloorToInt((gridHeight - footprint.y) * 0.5f);
        if (evenFootprintBiasPositive && (footprint.x % 2 == 0)) x += 1;
        if (evenFootprintBiasPositive && (footprint.y % 2 == 0)) y += 1;
        x = Mathf.Clamp(x, 0, maxX);
        y = Mathf.Clamp(y, 0, maxY);
        int ax = Mathf.Clamp(x + pivot.x, 0, gridWidth - 1);
        int ay = Mathf.Clamp(y + pivot.y, 0, gridHeight - 1);
        return new Vector2Int(ax, ay);
    }

    private void FitToFootprint(GameObject go, Vector2Int footprint)
    {
        if (!autoScaleToFootprint) return;
        if (go == null) return;
        if (footprint.x <= 0) footprint.x = 1;
        if (footprint.y <= 0) footprint.y = 1;

        Transform boundsRoot;
        var renderers = GetRenderersForBounds(go.transform, out boundsRoot);
        Bounds b = GetBaseBounds(boundsRoot, renderers);

        if (b.size.x <= 0.0001f || b.size.z <= 0.0001f) return;

        Transform scaleTarget = go.transform;
        bool scaleFootprint = false;
        if (scaleFootprintNode && TryGetFootprintNode(go.transform, out Transform node))
        {
            scaleTarget = node;
            scaleFootprint = true;
        }

        Vector3 baseScale;
        if (!_baseScales.TryGetValue(scaleTarget, out baseScale))
            baseScale = scaleTarget.localScale;

        float targetX = footprint.x * cellWorldWidth;
        float targetZ = footprint.y * cellWorldHeight;
        if (scaleFootprint)
        {
            scaleTarget.localScale = baseScale;
        }
        else
        {
            float scale = Mathf.Min(targetX / b.size.x, targetZ / b.size.z);
            float finalScale = scale * Mathf.Max(0.01f, previewScale);
            Vector3 scaled = baseScale * finalScale;
            if (scaleTarget == go.transform && !Mathf.Approximately(previewHeightScale, 1f))
                scaled.y *= previewHeightScale;
            scaleTarget.localScale = scaled;
        }

        if (scaleTarget != go.transform && !Mathf.Approximately(previewHeightScale, 1f))
        {
            Transform root = go.transform;
            if (_baseScales.TryGetValue(root, out Vector3 rootBase))
            {
                Vector3 rootScale = root.localScale;
                rootScale.y = rootBase.y * previewHeightScale;
                root.localScale = rootScale;
            }
        }
    }

    private static Bounds GetBounds(Renderer[] renderers)
    {
        bool has = false;
        Bounds b = new Bounds(Vector3.zero, Vector3.zero);
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            if (!has)
            {
                b = r.bounds;
                has = true;
            }
            else b.Encapsulate(r.bounds);
        }
        return b;
    }

    private static Bounds GetPrefabBounds(GameObject prefab)
    {
        if (prefab == null) return new Bounds(Vector3.zero, Vector3.zero);
        var temp = Instantiate(prefab);
        temp.hideFlags = HideFlags.HideAndDontSave;
        temp.transform.position = Vector3.zero;
        temp.transform.rotation = Quaternion.identity;
        temp.transform.localScale = prefab.transform.localScale;

        var renderers = temp.GetComponentsInChildren<Renderer>(true);
        Bounds b = GetBounds(renderers);

        if (Application.isPlaying)
            Destroy(temp);
        else
            DestroyImmediate(temp);

        return b;
    }

    private void CacheBaseMetrics(GameObject go)
    {
        if (go == null) return;
        Transform t = go.transform;
        if (!_baseScales.ContainsKey(t))
            _baseScales[t] = t.localScale;
        if (!_baseBounds.ContainsKey(t))
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            _baseBounds[t] = GetBounds(renderers);
        }

        if (TryGetFootprintNode(t, out Transform node))
        {
            if (!_baseScales.ContainsKey(node))
                _baseScales[node] = node.localScale;
            if (!_baseBounds.ContainsKey(node))
            {
                var renderers = node.GetComponentsInChildren<Renderer>(true);
                _baseBounds[node] = GetBounds(renderers);
            }
        }
    }

    private Bounds GetBaseBounds(Transform t, Renderer[] renderers)
    {
        if (t != null && _baseBounds.TryGetValue(t, out Bounds b))
            return b;

        b = GetBounds(renderers);
        if (t != null)
            _baseBounds[t] = b;
        return b;
    }

    private void RemoveCachedMetrics(Transform t)
    {
        if (t == null) return;
        _baseScales.Remove(t);
        _baseBounds.Remove(t);
    }

    private bool TryGetFootprintNode(Transform root, out Transform node)
    {
        node = null;
        if (!useFootprintNodeForBounds || root == null) return false;
        if (string.IsNullOrWhiteSpace(footprintNodeName)) return false;

        if (TryFindNodeByName(root, footprintNodeName, out node))
            return true;

        if (!string.Equals(footprintNodeName, "BasePlate", System.StringComparison.OrdinalIgnoreCase)
            && TryFindNodeByName(root, "BasePlate", out node))
            return true;

        return false;
    }

    private static bool TryFindNodeByName(Transform root, string name, out Transform node)
    {
        node = null;
        if (root == null || string.IsNullOrWhiteSpace(name)) return false;

        var list = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < list.Length; i++)
        {
            var t = list[i];
            if (t != null && t.name == name)
            {
                node = t;
                return true;
            }
        }

        return false;
    }

    private Renderer[] GetRenderersForBounds(Transform root, out Transform boundsRoot)
    {
        if (TryGetFootprintNode(root, out Transform node))
        {
            boundsRoot = node;
            return node.GetComponentsInChildren<Renderer>(true);
        }

        boundsRoot = root;
        return root != null ? root.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
    }
}
