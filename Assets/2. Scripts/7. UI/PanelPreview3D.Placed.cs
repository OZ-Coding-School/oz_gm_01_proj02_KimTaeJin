using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using UnityEngine;

public sealed partial class PanelPreview3D : MonoBehaviour
{
    public void SyncPlacedTowers(IReadOnlyList<TowerEntity> towers)
    {
        ClearPlacementPreview();
        SetPlacementActive(false);
        if (towers == null || previewRoot == null)
        {
            ClearPlacedTowers();
            return;
        }

        RefreshPreviewLayer();
        if (debugPreviewPlacements)
            Debug.Log($"[PanelPreview3D] SyncPlacedTowers count={towers.Count} center={GetCenterCell()} mask={GetEffectivePreviewLayerMask().value}");

        _placedToRemove.Clear();

        for (int i = 0; i < towers.Count; i++)
        {
            var t = towers[i];
            if (t == null) continue;
            var def = t.Definition;
            if (def == null || def.prefab == null) continue;

            if (!_placedByTower.TryGetValue(t, out GameObject go) || go == null)
            {
                go = Instantiate(def.prefab.gameObject, previewRoot);
                go.name = $"[PlacedPreview]{def.id}";
                PreparePreviewObject(go);
                _placedByTower[t] = go;
                _placedInstances.Add(go);
            }

            var towerEntity = go.GetComponentInChildren<TowerEntity>();
            if (towerEntity != null) towerEntity.enabled = false;

            go.transform.localRotation = Quaternion.Euler(placementRotation);
            FootprintMaskUtility.GetFootprintData(def, out FootprintMaskSO mask, out Vector2Int size, out Vector2Int pivot);
            Vector2Int cell = GetCenterCell() + t.OffsetFromCenter;
            if (debugPreviewPlacements)
                Debug.Log($"[PanelPreview3D] {t.name} def={def.id} cell={t.Cell} offset={t.OffsetFromCenter} previewCell={cell}");
            CacheBaseMetrics(go);
            FitToFootprint(go, size);
            PlaceAtCell(go.transform, cell, size, pivot, GetPlacedYOffset(go.transform));
            ReparentPlacedInstance(go);
            _placedCells[go.transform] = cell;
            _placedFootprints[go.transform] = size;
            _placedPivots[go.transform] = pivot;

            SetRenderersEnabled(go.transform, true);
            go.SetActive(true);
        }

        foreach (var kvp in _placedByTower)
        {
            if (kvp.Key == null || kvp.Value == null)
            {
                _placedToRemove.Add(kvp.Key);
                continue;
            }

            bool stillExists = false;
            for (int i = 0; i < towers.Count; i++)
            {
                if (towers[i] == kvp.Key)
                {
                    stillExists = true;
                    break;
                }
            }
            if (!stillExists)
                _placedToRemove.Add(kvp.Key);
        }

        for (int i = 0; i < _placedToRemove.Count; i++)
        {
            TowerEntity t = _placedToRemove[i];
            if (!_placedByTower.TryGetValue(t, out GameObject go)) continue;
            if (go != null)
            {
                go.SetActive(false);
                RemoveCachedMetrics(go.transform);
                _placedCells.Remove(go.transform);
                _placedFootprints.Remove(go.transform);
                _placedPivots.Remove(go.transform);
                _placedInstances.Remove(go);
                Destroy(go);
            }
            _placedByTower.Remove(t);
        }

        if (debugPreviewPlacements)
            Debug.Log($"[PanelPreview3D] SyncPlacedTowers done placedCount={_placedCells.Count} validPlaced={CountValidPlacedCells()} list={BuildPlacedCellsSummary(6)}");
    }

    public void ClearPlacedTowers()
    {
        ClearHiddenPlaced(false);
        for (int i = 0; i < _placedInstances.Count; i++)
        {
            var go = _placedInstances[i];
            if (go == null) continue;
            // Hide immediately to avoid one-frame overlap before Destroy completes.
            go.SetActive(false);
            RemoveCachedMetrics(go.transform);
            Destroy(go);
        }
        _placedInstances.Clear();
        _placedCells.Clear();
        _placedFootprints.Clear();
        _placedPivots.Clear();
        _placedByTower.Clear();
        _placedToRemove.Clear();
    }

    public bool TryCommitPlacementPreview(TowerEntity tower, Vector2Int cell, Vector2Int footprint, Vector2Int pivot)
    {
        if (tower == null || previewRoot == null) return false;

        Transform hiddenPlaced = _hiddenPlaced;
        bool hasSnap = false;
        Vector3 snapPos = default;
        Quaternion snapRot = default;
        if (hiddenPlaced != null)
        {
            snapPos = hiddenPlaced.position;
            snapRot = hiddenPlaced.rotation;
            hasSnap = true;
        }

        _placementMoveTween?.Kill();
        _placementMoveTween = null;

        bool useUpgradePreview = _usingUpgradePreview
            && _upgradePreviewInstance != null
            && _upgradePreviewRenderers != null
            && CountEnabledRenderers(_upgradePreviewRenderers) > 0;
        GameObject go = useUpgradePreview ? _upgradePreviewInstance : _placementInstance;
        if (go == null)
            go = _upgradePreviewInstance != null ? _upgradePreviewInstance : _placementInstance;

        bool usingUpgradeInstance = go == _upgradePreviewInstance && go != null;
        Renderer[] previewRenderers = usingUpgradeInstance ? _upgradePreviewRenderers : _placementRenderers;

        if (go == null)
        {
            go = CreatePlacedPreviewFromTower(tower);
            if (go == null) return false;
            previewRenderers = go.GetComponentsInChildren<Renderer>(true);
            usingUpgradeInstance = false;
        }
        else
        {
            GameObject expectedPrefab = tower.Definition != null && tower.Definition.prefab != null
                ? tower.Definition.prefab.gameObject
                : null;
            if (expectedPrefab != null)
            {
                GameObject sourcePrefab = null;
                if (go == _upgradePreviewInstance) sourcePrefab = _upgradePreviewPrefabSource;
                else if (go == _placementInstance) sourcePrefab = _placementPrefabSource;

                if (sourcePrefab != null && sourcePrefab != expectedPrefab)
                {
                    RemoveCachedMetrics(go.transform);
                    Destroy(go);
                    if (go == _upgradePreviewInstance)
                    {
                        _upgradePreviewInstance = null;
                        _upgradePreviewRenderers = null;
                        _upgradePreviewPrefabSource = null;
                    }
                    if (go == _placementInstance)
                    {
                        _placementInstance = null;
                        _placementRenderers = null;
                    }
                    _usingUpgradePreview = false;

                    go = CreatePlacedPreviewFromTower(tower);
                    if (go == null) return false;
                    previewRenderers = go.GetComponentsInChildren<Renderer>(true);
                    usingUpgradeInstance = false;
                }
            }
        }

        if (previewRenderers != null)
        {
            for (int i = 0; i < previewRenderers.Length; i++)
            {
                var r = previewRenderers[i];
                if (r == null) continue;
                r.SetPropertyBlock(null);
            }
        }
        if (usingUpgradeInstance)
        {
            _upgradePreviewInstance = null;
            _upgradePreviewRenderers = null;
            _upgradePreviewPrefabSource = null;
            _usingUpgradePreview = false;
            if (_placementInstance != null)
            {
                RemoveCachedMetrics(_placementInstance.transform);
                Destroy(_placementInstance);
            }
        }
        else
        {
            _usingUpgradePreview = false;
        }

        if (_placedByTower.TryGetValue(tower, out GameObject existing) && existing != null && existing != go)
            RemovePlacedInstance(existing);

        ClearHiddenPlaced(false);
        RemovePlacedAtCell(cell, go);

        _placementInstance = null;
        _placementRenderers = null;
        _hasPlacementCell = false;

        _placedByTower[tower] = go;
        if (!_placedInstances.Contains(go))
            _placedInstances.Add(go);

        string id = tower.Definition != null ? tower.Definition.id : tower.name;
        go.name = $"[PlacedPreview]{id}";
        go.transform.localRotation = Quaternion.Euler(placementRotation);
        CacheBaseMetrics(go);
        FitToFootprint(go, footprint);
        bool aligned = false;
        if (hasSnap)
            aligned = AlignPreviewToPlaced(go.transform, hiddenPlaced);
        if (!aligned)
            PlaceAtCell(go.transform, cell, footprint, pivot, GetPlacedYOffset(go.transform));
        ReparentPlacedInstance(go);
        if (hasSnap && !aligned)
            SnapPreviewIfFar(go.transform, snapPos, snapRot, cell, "CommitPreview");
        PlayPlacementDrop(go.transform);

        _placedCells[go.transform] = cell;
        _placedFootprints[go.transform] = footprint;
        _placedPivots[go.transform] = pivot;

        SetRenderersEnabled(go.transform, true);
        go.SetActive(true);
        return true;
    }

    public bool TryGetPlacedTowerAtCell(Vector2Int cell, out TowerEntity tower)
    {
        tower = null;
        if (_placedByTower.Count == 0 || _placedCells.Count == 0) return false;

        foreach (var kvp in _placedByTower)
        {
            var t = kvp.Key;
            var go = kvp.Value;
            if (t == null || go == null) continue;
            if (!_placedCells.TryGetValue(go.transform, out Vector2Int placedCell)) continue;
            if (placedCell != cell) continue;
            tower = t;
            return true;
        }

        return false;
    }

    private bool TryGetPlacedTransformAtCell(Vector2Int cell, out Transform placed)
    {
        placed = null;
        foreach (var kvp in _placedCells)
        {
            var t = kvp.Key;
            if (t == null) continue;
            if (kvp.Value != cell) continue;
            placed = t;
            return true;
        }
        return false;
    }

    private void RemovePlacedAtCell(Vector2Int cell, GameObject ignore)
    {
        _placedTransformsBuffer.Clear();
        CollectPlacedTransformsAtCell(cell, _placedTransformsBuffer);
        if (_placedTransformsBuffer.Count == 0) return;

        for (int i = 0; i < _placedTransformsBuffer.Count; i++)
        {
            var t = _placedTransformsBuffer[i];
            if (t == null) continue;
            GameObject go = t.gameObject;
            if (go == null) continue;
            if (ignore != null && (go == ignore || t.IsChildOf(ignore.transform))) continue;
            RemovePlacedInstance(go);
        }
        _placedTransformsBuffer.Clear();
    }

    private void RemovePlacedInstance(GameObject go)
    {
        if (go == null) return;
        go.SetActive(false);
        RemoveCachedMetrics(go.transform);
        _placedCells.Remove(go.transform);
        _placedFootprints.Remove(go.transform);
        _placedPivots.Remove(go.transform);
        _placedInstances.Remove(go);

        TowerEntity removeKey = null;
        foreach (var kvp in _placedByTower)
        {
            if (kvp.Value == go)
            {
                removeKey = kvp.Key;
                break;
            }
        }
        if (removeKey != null)
            _placedByTower.Remove(removeKey);

        Destroy(go);
    }

    private void PlayPlacementDrop(Transform t)
    {
        if (t == null) return;
        if (placementDropHeight <= 0f || placementDropDuration <= 0f) return;
        _placementDropTween?.Kill();

        if (t.parent != null)
        {
            Vector3 target = t.localPosition;
            t.localPosition = target + Vector3.up * placementDropHeight;
            _placementDropTween = t.DOLocalMove(target, placementDropDuration)
                .SetEase(placementDropEase)
                .SetUpdate(true);
            return;
        }

        Vector3 worldTarget = t.position;
        t.position = worldTarget + Vector3.up * placementDropHeight;
        _placementDropTween = t.DOMove(worldTarget, placementDropDuration)
            .SetEase(placementDropEase)
            .SetUpdate(true);
    }

    private int CountValidPlacedCells()
    {
        if (_placedCells == null || _placedCells.Count == 0) return 0;
        int count = 0;
        foreach (var kvp in _placedCells)
        {
            if (kvp.Key != null) count++;
        }
        return count;
    }

    private string BuildPlacedCellsSummary(int max)
    {
        if (_placedCells == null || _placedCells.Count == 0) return "none";
        var sb = new StringBuilder();
        int added = 0;
        foreach (var kvp in _placedCells)
        {
            var t = kvp.Key;
            if (t == null) continue;
            if (added > 0) sb.Append(", ");
            sb.Append(t.name).Append(":").Append(kvp.Value);
            added++;
            if (added >= max) break;
        }
        if (added == 0) return "none";
        if (_placedCells.Count > max) sb.Append(" ...");
        return sb.ToString();
    }

    private void SyncPlacedTowerRotations()
    {
        if (_placedByTower == null || _placedByTower.Count == 0) return;

        foreach (var kvp in _placedByTower)
        {
            TowerEntity worldTower = kvp.Key;
            GameObject previewGo = kvp.Value;
            if (worldTower == null || previewGo == null) continue;
            if (!previewGo.activeInHierarchy) continue;

            Transform worldRoot = worldTower.transform;
            Transform previewRoot = previewGo.transform;

            Transform worldYaw = FindChildByName(worldRoot, "YawPivot") ?? FindChildByName(worldRoot, "Yaw");
            Transform previewYaw = FindChildByName(previewRoot, "YawPivot") ?? FindChildByName(previewRoot, "Yaw");
            if (worldYaw != null && previewYaw != null)
                previewYaw.rotation = worldYaw.rotation;

            Transform worldPitch = FindChildByName(worldRoot, "PitchPivot") ?? FindChildByName(worldRoot, "Pitch");
            Transform previewPitch = FindChildByName(previewRoot, "PitchPivot") ?? FindChildByName(previewRoot, "Pitch");
            if (worldPitch != null && previewPitch != null)
                previewPitch.localRotation = worldPitch.localRotation;
        }
    }

    private GameObject CreatePlacedPreviewFromTower(TowerEntity tower)
    {
        if (tower == null || tower.Definition == null || tower.Definition.prefab == null || previewRoot == null) return null;

        GameObject go = Instantiate(tower.Definition.prefab.gameObject, previewRoot);
        string id = tower.Definition != null ? tower.Definition.id : tower.name;
        go.name = $"[PlacedPreview]{id}";
        PreparePreviewObject(go);

        var previewEntity = go.GetComponentInChildren<TowerEntity>();
        if (previewEntity != null) previewEntity.enabled = false;

        go.transform.localRotation = Quaternion.Euler(placementRotation);
        CacheBaseMetrics(go);
        return go;
    }
}
