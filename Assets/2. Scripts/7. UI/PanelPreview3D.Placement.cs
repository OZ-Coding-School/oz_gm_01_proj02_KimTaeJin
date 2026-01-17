using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using UnityEngine;

public sealed partial class PanelPreview3D : MonoBehaviour
{
    public void SetPlacementPrefab(GameObject prefab)
    {
        if (!showPlacementPreview)
        {
            ClearPlacementPreview();
            return;
        }
        if (_placementPrefabSource != prefab)
        {
            _placementPrefabSource = prefab;
            DestroyUpgradePreview();
            _loggedPlacementPreviewState = false;
        }
        if (_placementInstance != null)
        {
            RemoveCachedMetrics(_placementInstance.transform);
            Destroy(_placementInstance);
        }
        _placementRenderers = null;
        _placementMoveTween?.Kill();
        _placementDropTween?.Kill();

        if (prefab == null)
        {
            ClearHiddenPlaced(true);
            return;
        }

        _placementInstance = Instantiate(prefab, previewRoot);
        _placementInstance.name = "[PlacementPreview]";
        PreparePlacementPreviewObject(_placementInstance);
        var placementEntity = _placementInstance.GetComponentInChildren<TowerEntity>();
        if (placementEntity != null) placementEntity.enabled = false;
        _placementInstance.transform.localRotation = Quaternion.Euler(placementRotation);
        ReparentPlacedInstance(_placementInstance);

        _placementRenderers = _placementInstance.GetComponentsInChildren<Renderer>(true);
        if (_placementRenderers != null)
        {
            for (int i = 0; i < _placementRenderers.Length; i++)
            {
                var r = _placementRenderers[i];
                if (r != null) r.enabled = true;
            }
        }
        LogPreviewRendererState("PlacementPreview", _placementInstance, _placementRenderers, ref _loggedPlacementPreviewState);
        CacheBaseMetrics(_placementInstance);
        FitToFootprint(_placementInstance, _placementFootprint);
        ApplyTintToRenderers(_placementRenderers, _placementTintColor);
        if (Debug.isDebugBuild)
        {
            int rendererCount = _placementRenderers != null ? _placementRenderers.Length : 0;
            int layer = _placementInstance != null ? _placementInstance.layer : -1;
            Debug.Log($"[PanelPreview3D] PlacementPrefab set prefab={prefab.name} renderers={rendererCount} layer={layer} previewMask={GetPreviewLayerMask().value}");
        }
        if (_hasPlacementCell)
        {
            Vector3 target = GetCellLocalPosition(_placementCell, _placementFootprint, _placementPivot, _placementInstance.transform);
            _placementInstance.transform.localPosition = target;
            UpdatePlacementOverlap(_placementCell);
        }
        _placementInstance.SetActive(false);
    }

    public void ClearPlacementPreview()
    {
        ClearHiddenPlaced(true);
        DestroyUpgradePreview();
        if (_placementInstance != null)
        {
            RemoveCachedMetrics(_placementInstance.transform);
            Destroy(_placementInstance);
        }
        _placementInstance = null;
        _placementRenderers = null;
        _placementPrefabSource = null;
        _placementMoveTween?.Kill();
        _placementDropTween?.Kill();
        _hasPlacementCell = false;
    }

    public void SetPlacementRotation(Vector3 euler)
    {
        placementRotation = euler;
        if (_placementInstance != null)
            _placementInstance.transform.localRotation = Quaternion.Euler(placementRotation);
        if (_upgradePreviewInstance != null)
            _upgradePreviewInstance.transform.localRotation = Quaternion.Euler(placementRotation);
    }

    public void SetPlacementFootprint(FootprintMaskSO mask, Vector2Int size, Vector2Int pivot)
    {
        _placementFootprint = size;
        _placementPivot = pivot;
        if (_placementInstance != null)
            FitToFootprint(_placementInstance, _placementFootprint);
    }

    public void SetCenterFootprint(Vector2Int footprint, bool biasPositive)
    {
        centerFootprint = new Vector2Int(Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y));
        evenFootprintBiasPositive = biasPositive;
        if (_centerInstance != null)
        {
            _centerPivot = Vector2Int.zero;
            FitToFootprint(_centerInstance, centerFootprint);
            PlaceAtGridCenter(_centerInstance.transform);
            ApplyCenterPreviewOffset(_centerInstance.transform);
        }
    }

    public void SetCenterFootprint(FootprintMaskSO mask, bool biasPositive)
    {
        if (mask == null || !mask.IsValid)
        {
            SetCenterFootprint(Vector2Int.one, biasPositive);
            return;
        }

        centerFootprint = mask.Size;
        _centerPivot = mask.Pivot;
        evenFootprintBiasPositive = biasPositive;

        if (_centerInstance != null)
        {
            FitToFootprint(_centerInstance, centerFootprint);
            PlaceAtGridCenter(_centerInstance.transform);
            ApplyCenterPreviewOffset(_centerInstance.transform);
        }
    }

    public void SetPlacementActive(bool on)
    {
        if (!showPlacementPreview) on = false;
        if (!on)
        {
            ClearHiddenPlaced(true);
            HideUpgradePreview();
        }
        if (_placementInstance != null)
            _placementInstance.SetActive(on);
    }

    public void SetPlacementCell(Vector2Int cell, bool smooth = true)
    {
        if (!showPlacementPreview || _placementInstance == null) return;
        EnsurePlacementVisible();
        _placementCell = cell;
        _hasPlacementCell = true;
        if (debugPreviewPlacements)
            Debug.Log($"[PanelPreview3D] SetPlacementCell cell={cell} active={_placementInstance.activeSelf} renderers={CountEnabledRenderers(_placementRenderers)}");
        if (_placementCanPlace && TryGetPlacedTransformAtCell(cell, out Transform placed) && placed != null)
        {
            _placementMoveTween?.Kill();
            AlignPlacementToPlaced(placed, true);
            UpdatePlacementOverlap(cell);
            return;
        }
        Vector3 target = GetCellLocalPosition(cell, _placementFootprint, _placementPivot, _placementInstance.transform);
        _placementMoveTween?.Kill();
        if (smooth)
        {
            _placementMoveTween = _placementInstance.transform.DOLocalMove(target, placementMoveDuration)
                .SetEase(placementMoveEase)
                .SetUpdate(true);
        }
        else
        {
            _placementInstance.transform.localPosition = target;
        }

        UpdatePlacementOverlap(cell);
    }

    public void SetPlacementTint(Color color)
    {
        if (!showPlacementPreview) return;
        _placementTintColor = color;
        ApplyTintToRenderers(_placementRenderers, color);
        ApplyTintToRenderers(_upgradePreviewRenderers, color);
    }

    private void ApplyTintToRenderers(Renderer[] renderers, Color color)
    {
        if (renderers == null || renderers.Length == 0) return;
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            ApplyColor(r, _mpb, color);
        }
    }

    private void SetPlacementPreviewVisible(bool visible)
    {
        if (_placementInstance == null) return;
        if (visible && !_placementInstance.activeSelf)
            _placementInstance.SetActive(true);
        if (_placementRenderers == null) return;
        for (int i = 0; i < _placementRenderers.Length; i++)
        {
            var r = _placementRenderers[i];
            if (r != null) r.enabled = visible;
        }
    }

    private void DestroyUpgradePreview()
    {
        if (_upgradePreviewInstance != null)
        {
            RemoveCachedMetrics(_upgradePreviewInstance.transform);
            Destroy(_upgradePreviewInstance);
        }
        _upgradePreviewInstance = null;
        _upgradePreviewRenderers = null;
        _upgradePreviewPrefabSource = null;
        _usingUpgradePreview = false;
    }

    private void HideUpgradePreview()
    {
        if (_upgradePreviewInstance != null)
        {
            if (_upgradePreviewRenderers != null)
            {
                for (int i = 0; i < _upgradePreviewRenderers.Length; i++)
                {
                    var r = _upgradePreviewRenderers[i];
                    if (r != null) r.enabled = false;
                }
            }
            if (_upgradePreviewInstance.activeSelf)
                _upgradePreviewInstance.SetActive(false);
        }
        _usingUpgradePreview = false;
    }

    private bool ShowUpgradePreview(Transform placed, Vector2Int cell)
    {
        if (!showPlacementPreview || previewRoot == null)
            return false;

        GameObject previewPrefab = ResolveUpgradePreviewPrefab(placed);
        if (previewPrefab == null) return false;
        if (_upgradePreviewInstance == null || _upgradePreviewPrefabSource != previewPrefab)
        {
            DestroyUpgradePreview();
            _upgradePreviewInstance = Instantiate(previewPrefab, previewRoot);
            _upgradePreviewInstance.name = "[UpgradePreview]";
            PreparePlacementPreviewObject(_upgradePreviewInstance);
            var previewEntity = _upgradePreviewInstance.GetComponentInChildren<TowerEntity>();
            if (previewEntity != null) previewEntity.enabled = false;
            _upgradePreviewInstance.transform.localRotation = Quaternion.Euler(placementRotation);
            ReparentPlacedInstance(_upgradePreviewInstance);
            _upgradePreviewRenderers = _upgradePreviewInstance.GetComponentsInChildren<Renderer>(true);
            CacheBaseMetrics(_upgradePreviewInstance);
            _upgradePreviewPrefabSource = previewPrefab;
            _loggedUpgradePreviewState = false;
        }

        _usingUpgradePreview = true;
        if (_upgradePreviewInstance != null && !_upgradePreviewInstance.activeSelf)
            _upgradePreviewInstance.SetActive(true);
        if (_upgradePreviewRenderers != null)
        {
            for (int i = 0; i < _upgradePreviewRenderers.Length; i++)
            {
                var r = _upgradePreviewRenderers[i];
                if (r != null) r.enabled = true;
            }
        }
        LogPreviewRendererState("UpgradePreview", _upgradePreviewInstance, _upgradePreviewRenderers, ref _loggedUpgradePreviewState);

        EnsurePreviewRenderers(_upgradePreviewInstance);
        FitToFootprint(_upgradePreviewInstance, _placementFootprint);
        PlaceAtCell(_upgradePreviewInstance.transform, cell, _placementFootprint, _placementPivot, GetPlacedYOffset(_upgradePreviewInstance.transform));
        if (placed != null)
            _upgradePreviewInstance.transform.rotation = placed.rotation;
        if (placed != null)
            SnapPreviewIfFar(_upgradePreviewInstance.transform, placed.position, placed.rotation, cell, "UpgradePreview");

        ApplyTintToRenderers(_upgradePreviewRenderers, _placementTintColor);

        bool upgradeVisible = CountEnabledRenderers(_upgradePreviewRenderers) > 0
            && IsRenderableInPreviewCamera(_upgradePreviewRenderers);
        if (upgradeVisible)
        {
            if (_placementInstance != null)
                SetPlacementPreviewVisible(false);
            return true;
        }

        _usingUpgradePreview = false;
        HideUpgradePreview();
        EnsurePlacementVisible();
        return CountEnabledRenderers(_placementRenderers) > 0;
    }

    public void SetPlacementCanPlace(bool canPlace)
    {
        _placementCanPlace = canPlace;
        if (canPlace) return;
        HideUpgradePreview();
        EnsurePlacementVisible();
        if (_hasHiddenPlaced || _hiddenPlacedList.Count > 0)
        {
            if (debugPreviewPlacements)
                Debug.Log($"[PanelPreview3D] RestoreHiddenByInvalid placed={(_hiddenPlaced != null ? _hiddenPlaced.name : "null")}");
            RestoreHiddenPlaced();
        }
    }

    private void UpdatePlacementOverlap(Vector2Int cell)
    {
        if (!HasPlacementVisual())
        {
            if (debugPreviewPlacements)
                Debug.Log($"[PanelPreview3D] OverlapSkip no placement visual cell={cell} active={_placementInstance != null && _placementInstance.activeSelf}");
            ClearHiddenPlaced(true);
            HideUpgradePreview();
            return;
        }

        Transform placed = null;
        if (_hasHiddenPlaced || _hiddenPlacedList.Count > 0)
        {
            if (_hiddenPlaced == null)
            {
                _hasHiddenPlaced = false;
                if (_hiddenPlacedList.Count > 0)
                {
                    RestoreHiddenPlaced();
                    HideUpgradePreview();
                }
            }
            else if (_hiddenPlacedCell == cell && _placementCanPlace)
            {
                if (debugPreviewPlacements)
                    Debug.Log($"[PanelPreview3D] OverlapKeepHidden cell={cell} placed={_hiddenPlaced.name}");
                HideUpgradePreview();
                EnsurePlacementVisible();
                return;
            }
            else
            {
                if (debugPreviewPlacements)
                    Debug.Log($"[PanelPreview3D] OverlapRestoreHidden cell={cell} restore={_hiddenPlaced.name}");
                RestoreHiddenPlaced();
                HideUpgradePreview();
            }
        }

        if (TryGetPlacedTransformAtCell(cell, out placed) && placed != null)
        {
            if (_placementCanPlace)
            {
                if (debugPreviewPlacements)
                    Debug.Log($"[PanelPreview3D] OverlapHidePlaced cell={cell} placed={placed.name}");
                if (ShouldLogVerbose())
                    LogPlacementContext("OverlapHidePlaced", placed);
                HidePlacedAtCell(cell);
                HideUpgradePreview();
                EnsurePlacementVisible();
                AlignPlacementToPlaced(placed, true);
                return;
            }
        }
        HideUpgradePreview();
        EnsurePlacementVisible();
        if (debugPreviewPlacements && (placed == null || !_placementCanPlace))
            Debug.Log($"[PanelPreview3D] OverlapNoPlaced cell={cell} placedCount={_placedCells.Count} validPlaced={CountValidPlacedCells()} list={BuildPlacedCellsSummary(6)}");
    }

    private bool AlignPlacementToPlaced(Transform placed, bool forceSnap)
    {
        if (_placementInstance == null || placed == null) return false;
        if (!forceSnap && _placedFootprints.TryGetValue(placed, out Vector2Int placedFootprint))
        {
            if (placedFootprint != _placementFootprint)
            {
                if (debugPreviewPlacements)
                    Debug.Log($"[PanelPreview3D] AlignSkip footprint placed={placedFootprint} placement={_placementFootprint} placed={placed.name}");
                return false;
            }
        }
        if (!forceSnap && _placedPivots.TryGetValue(placed, out Vector2Int placedPivot))
        {
            if (placedPivot != _placementPivot)
            {
                if (debugPreviewPlacements)
                    Debug.Log($"[PanelPreview3D] AlignSkip pivot placed={placedPivot} placement={_placementPivot} placed={placed.name}");
                return false;
            }
        }
        if (!forceSnap && !ShouldSnapPlacementToPlaced(placed)) return false;

        _placementMoveTween?.Kill();
        return AlignPreviewToPlaced(_placementInstance.transform, placed);
    }

    private bool AlignPreviewToPlaced(Transform preview, Transform placed)
    {
        if (preview == null || placed == null) return false;

        preview.rotation = placed.rotation;
        if (TryGetFootprintNode(preview, out Transform previewFoot)
            && TryGetFootprintNode(placed, out Transform placedFoot)
            && previewFoot != null && placedFoot != null)
        {
            Vector3 delta = placedFoot.position - previewFoot.position;
            preview.position += delta;
            return true;
        }

        preview.position = placed.position;
        return true;
    }

    private bool HasPlacementVisual()
    {
        if (_usingUpgradePreview && _upgradePreviewInstance != null && _upgradePreviewRenderers != null)
        {
            if (_upgradePreviewInstance.activeInHierarchy && CountEnabledRenderers(_upgradePreviewRenderers) > 0)
                return true;
        }
        if (_placementInstance == null || _placementRenderers == null || _placementRenderers.Length == 0)
            return false;
        if (!_placementInstance.activeInHierarchy)
            return false;
        for (int i = 0; i < _placementRenderers.Length; i++)
        {
            var r = _placementRenderers[i];
            if (r != null && r.enabled) return true;
        }
        return false;
    }

    private void EnsurePlacementVisible()
    {
        if (!showPlacementPreview || _placementInstance == null) return;
        if (_usingUpgradePreview)
        {
            if (_upgradePreviewInstance != null
                && _upgradePreviewRenderers != null
                && _upgradePreviewInstance.activeInHierarchy
                && CountEnabledRenderers(_upgradePreviewRenderers) > 0)
                return;
            _usingUpgradePreview = false;
        }
        SetPlacementPreviewVisible(true);
        if (!_placementInstance.activeSelf)
        {
            _placementInstance.SetActive(true);
            if (debugPreviewPlacements)
                Debug.Log("[PanelPreview3D] PlacementActivated");
        }
        if (_placementRenderers == null) return;
        for (int i = 0; i < _placementRenderers.Length; i++)
        {
            var r = _placementRenderers[i];
            if (r != null && !r.enabled) r.enabled = true;
        }
    }

    private bool ShouldSnapPlacementToPlaced(Transform placed)
    {
        if (_placementInstance == null || placed == null || previewRoot == null) return false;
        if (!_hasPlacementCell) return false;

        Vector3 expectedLocal = GetCellWorldPosition(_placementCell, _placementFootprint, _placementPivot, _placementInstance.transform);
        Vector3 expectedWorld = previewRoot.TransformPoint(expectedLocal);
        float tolerance = Mathf.Max(cellWorldWidth, cellWorldHeight) * 0.35f;
        float sqr = (expectedWorld - placed.position).sqrMagnitude;
        if (debugPreviewPlacements)
            Debug.Log($"[PanelPreview3D] SnapCheck cell={_placementCell} delta={Mathf.Sqrt(sqr):0.###} tol={tolerance:0.###} placed={placed.name}");
        if (debugPreviewPlacements && sqr > tolerance * tolerance && ShouldLogVerbose())
            LogPlacementContext("SnapMismatch", placed, expectedWorld);
        return sqr <= tolerance * tolerance;
    }

    private static int CountEnabledRenderers(Renderer[] renderers)
    {
        if (renderers == null || renderers.Length == 0) return 0;
        int count = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r != null && r.enabled && r.gameObject.activeInHierarchy)
                count++;
        }
        return count;
    }

    private bool IsRenderableInPreviewCamera(Renderer[] renderers)
    {
        if (previewCamera == null || renderers == null || renderers.Length == 0)
            return true;

        int mask = previewCamera.cullingMask;
        bool has = false;
        Bounds b = default;

        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            if ((mask & (1 << r.gameObject.layer)) == 0) continue;
            if (!has)
            {
                b = r.bounds;
                has = true;
            }
            else b.Encapsulate(r.bounds);
        }

        if (!has) return false;
        var planes = GeometryUtility.CalculateFrustumPlanes(previewCamera);
        return GeometryUtility.TestPlanesAABB(planes, b);
    }

    private static bool ShouldLogVerbose()
    {
        return Time.frameCount % 20 == 0;
    }

    private void LogPlacementContext(string tag, Transform placed, Vector3? expectedWorld = null)
    {
        if (!debugPreviewPlacements) return;

        StringBuilder sb = new StringBuilder();
        sb.Append("[PanelPreview3D] ").Append(tag).Append(" ");
        sb.Append("placed=").Append(placed != null ? placed.name : "null").Append(" ");

        Transform placement = _placementInstance != null ? _placementInstance.transform : null;
        Vector3 expected = expectedWorld ?? (previewRoot != null && placement != null && _hasPlacementCell
            ? previewRoot.TransformPoint(GetCellWorldPosition(_placementCell, _placementFootprint, _placementPivot, placement))
            : Vector3.zero);

        sb.Append("expected=").Append(expected.ToString("F2")).Append(" ");
        sb.Append("placement=").Append(placement != null ? placement.position.ToString("F2") : "null").Append(" ");
        sb.Append("placementCell=").Append(_placementCell).Append(" ");
        sb.Append("footprint=").Append(_placementFootprint).Append(" ");
        sb.Append("pivot=").Append(_placementPivot).Append(" ");
        sb.Append("inRootPlacement=").Append(placement != null && previewRoot != null && placement.IsChildOf(previewRoot)).Append(" ");
        sb.Append("placedInRoot=").Append(placed != null && previewRoot != null && placed.IsChildOf(previewRoot)).Append(" ");
        sb.Append("previewRoot=").Append(previewRoot != null ? previewRoot.name : "null");

        Debug.Log(sb.ToString());
    }

    private static int CountEnabledRenderers(Transform root)
    {
        if (root == null) return 0;
        int count = 0;
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r != null && r.enabled && r.gameObject.activeInHierarchy)
                count++;
        }
        return count;
    }

    private static string GetHierarchyPath(Transform t)
    {
        if (t == null) return "null";
        var sb = new StringBuilder();
        sb.Append(t.name);
        Transform p = t.parent;
        while (p != null)
        {
            sb.Insert(0, "/");
            sb.Insert(0, p.name);
            p = p.parent;
        }
        return sb.ToString();
    }

    private static void SetRenderersEnabled(Transform root, bool enabled)
    {
        if (root == null) return;
        if (enabled && !root.gameObject.activeSelf)
            root.gameObject.SetActive(true);
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r != null) r.enabled = enabled;
        }
    }

    private void ClearHiddenPlaced(bool restore)
    {
        if (!_hasHiddenPlaced && _hiddenPlacedList.Count == 0) return;
        if (restore)
            RestoreHiddenPlaced();
        else
        {
            _hiddenPlaced = null;
            _hiddenPlacedCell = default;
            _hasHiddenPlaced = false;
            _hiddenPlacedList.Clear();
        }
    }

    private void RestoreHiddenPlaced()
    {
        for (int i = 0; i < _hiddenPlacedList.Count; i++)
        {
            var t = _hiddenPlacedList[i];
            if (t == null) continue;
            SetRenderersEnabled(t, true);
            if (debugPreviewPlacements)
                Debug.Log($"[PanelPreview3D] RestoreApplied placed={t.name} active={t.gameObject.activeSelf} renderers={CountEnabledRenderers(t)} path={GetHierarchyPath(t)}");
        }
        _hiddenPlacedList.Clear();
        _hiddenPlaced = null;
        _hiddenPlacedCell = default;
        _hasHiddenPlaced = false;
    }

    private void HidePlacedAtCell(Vector2Int cell)
    {
        _hiddenPlacedList.Clear();
        CollectPlacedTransformsAtCell(cell, _hiddenPlacedList);

        for (int i = _hiddenPlacedList.Count - 1; i >= 0; i--)
        {
            var t = _hiddenPlacedList[i];
            if (t == null)
            {
                _hiddenPlacedList.RemoveAt(i);
                continue;
            }
            if (_placedInstances.Count > 0 && !_placedInstances.Contains(t.gameObject))
            {
                _hiddenPlacedList.RemoveAt(i);
                continue;
            }
            if (_placementInstance != null)
            {
                Transform placementRoot = _placementInstance.transform;
                if (t == placementRoot || t.IsChildOf(placementRoot))
                {
                    _hiddenPlacedList.RemoveAt(i);
                    continue;
                }
            }
            if (_upgradePreviewInstance != null)
            {
                Transform upgradeRoot = _upgradePreviewInstance.transform;
                if (t == upgradeRoot || t.IsChildOf(upgradeRoot))
                    _hiddenPlacedList.RemoveAt(i);
            }
        }

        if (_hiddenPlacedList.Count == 0)
        {
            if (debugPreviewPlacements)
                Debug.Log($"[PanelPreview3D] HideSkip no placed at cell={cell}");
            return;
        }

        for (int i = 0; i < _hiddenPlacedList.Count; i++)
        {
            var t = _hiddenPlacedList[i];
            if (t == null) continue;
            SetRenderersEnabled(t, false);
            if (debugPreviewPlacements)
                Debug.Log($"[PanelPreview3D] HideApplied placed={t.name} active={t.gameObject.activeSelf} renderers={CountEnabledRenderers(t)} path={GetHierarchyPath(t)}");
        }

        _hiddenPlaced = _hiddenPlacedList[0];
        _hiddenPlacedCell = cell;
        _hasHiddenPlaced = true;
    }

    private void CollectPlacedTransformsAtCell(Vector2Int cell, List<Transform> results)
    {
        results.Clear();
        foreach (var kvp in _placedCells)
        {
            var t = kvp.Key;
            if (t == null) continue;
            if (kvp.Value != cell) continue;
            if (!results.Contains(t))
                results.Add(t);
        }

        if (_placedCells.Count > 0) return;
        if (_placedInstances.Count == 0) return;
        for (int i = 0; i < _placedInstances.Count; i++)
        {
            var go = _placedInstances[i];
            if (go == null) continue;
            Transform t = go.transform;
            if (!TryWorldToCell(t.position, out Vector2Int rootCell)) continue;
            if (rootCell != cell) continue;
            if (!results.Contains(t))
                results.Add(t);
        }
    }

    private Transform GetTowerPreviewRoot(Transform t)
    {
        if (t == null) return null;
        Transform parentStop = _centerGridAnchor != null ? _centerGridAnchor : previewRoot;
        Transform cur = t;
        while (cur.parent != null && cur.parent != parentStop && cur.parent != previewRoot)
            cur = cur.parent;
        return cur;
    }

    private bool TryWorldToCell(Vector3 world, out Vector2Int cell)
    {
        cell = default;
        if (previewRoot == null) return false;
        Vector3 local = previewRoot.InverseTransformPoint(world) - sceneOffset;
        return TryLocalToCell(local, out cell);
    }

    private GameObject ResolveUpgradePreviewPrefab(Transform placed)
    {
        GameObject prefab = _placementPrefabSource;
        if (placed == null) return prefab;

        if (TryGetTowerForPlacedTransform(placed, out TowerEntity tower))
        {
            TowerDefinitionSO upgrade = tower.Definition != null ? tower.Definition.upgradeNext : null;
            if (upgrade != null && upgrade.prefab != null)
                prefab = upgrade.prefab.gameObject;
        }

        return prefab;
    }

    private bool TryGetTowerForPlacedTransform(Transform placed, out TowerEntity tower)
    {
        tower = null;
        if (placed == null || _placedByTower.Count == 0) return false;

        foreach (var kvp in _placedByTower)
        {
            var t = kvp.Key;
            var go = kvp.Value;
            if (t == null || go == null) continue;
            if (go.transform == placed || placed.IsChildOf(go.transform))
            {
                tower = t;
                return true;
            }
        }

        return false;
    }

    private static void ApplyColor(Renderer r, MaterialPropertyBlock mpb, Color color)
    {
        if (r == null || mpb == null) return;
        int baseColor = Shader.PropertyToID("_BaseColor");
        int c = Shader.PropertyToID("_Color");
        r.GetPropertyBlock(mpb);
        mpb.SetColor(baseColor, color);
        mpb.SetColor(c, color);
        r.SetPropertyBlock(mpb);
    }

    private void SnapPreviewIfFar(Transform preview, Vector3 snapPos, Quaternion snapRot, Vector2Int cell, string tag)
    {
        if (preview == null) return;
        float maxDelta = Mathf.Max(cellWorldWidth, cellWorldHeight) * 1.5f;
        if (maxDelta <= 0f) return;
        float dist = Vector3.Distance(preview.position, snapPos);
        if (dist <= maxDelta) return;

        preview.position = snapPos;
        preview.rotation = snapRot;
        if (debugPreviewPlacements)
            Debug.Log($"[PanelPreview3D] {tag} snap cell={cell} dist={dist:0.###} max={maxDelta:0.###}");
    }
}
