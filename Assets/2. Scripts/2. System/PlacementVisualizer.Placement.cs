using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public sealed partial class PlacementVisualizer : MonoBehaviour
{
    private void RebuildCell(Vector3Int cell)
    {
        if (dataService == null) return;

        if (!_placed.TryGetValue(cell, out PlacedView existing))
        {
            if (dataService.TryGet(cell, out GridDataService.TowerData data) && dataService.TryGetDefinition(data.towerId, out TowerDefinitionSO def))
                SpawnPlaced(cell, def);
            return;
        }

        if (!dataService.TryGet(cell, out GridDataService.TowerData currentData))
        {
            DespawnPlaced(cell);
            return;
        }

        if (dataService.TryGetDefinition(currentData.towerId, out TowerDefinitionSO newDef))
            ReplacePlaced(cell, newDef, existing != null ? existing.def : null);
        else
            DespawnPlaced(cell);
    }

    private void SpawnPlaced(Vector3Int cell, TowerDefinitionSO def)
    {
        if (def == null || def.prefab == null || root == null) return;
        if (_placed.ContainsKey(cell)) return;

        if (grid == null) return;
        Vector3 pos = GetPlacementPosition(def, cell);
        var tower = Instantiate(def.prefab, pos, Quaternion.identity);
        AttachToParent(tower.gameObject, root);
        tower.name = $"{def.id}_Tower";
        ApplyPanelLayer(tower.gameObject);

        Vector2Int cell2 = new Vector2Int(cell.x, cell.z);
        tower.SetCell(cell2);
        Vector2Int anchor = dataService != null ? ToCell2D(dataService.GetAnchorCell()) : Vector2Int.zero;
        tower.SetOffsetFromCenter(cell2 - anchor);

        FootprintMaskUtility.GetFootprintData(def, out FootprintMaskSO mask, out Vector2Int size, out Vector2Int pivot);
        tower.SetFootprint(size);
        var footprint = new List<Vector2Int>();
        FootprintMaskUtility.GetFootprintCells(mask, size, pivot, cell2, footprint);
        tower.SetOccupiedCells(footprint);

        if (isWorldVisualizer)
        {
            var resolvedScope = scope != null ? scope : RunScopeLocator.Current;
            tower.Construct(resolvedScope, def);
            resolvedScope?.Entities?.RegisterTower(tower);
            OccupyCells(footprint);
        }
        else
        {
            DisableGameplay(tower.gameObject);
            ApplyPanelBasePlateScale(tower.gameObject);
            ApplyAimSnapshot(cell, tower);
        }

        _placed[cell] = new PlacedView
        {
            instance = tower.gameObject,
            renderers = tower.GetComponentsInChildren<Renderer>(true),
            tower = tower,
            def = def
        };
    }

    private void ReplacePlaced(Vector3Int cell, TowerDefinitionSO def, TowerDefinitionSO prevDef)
    {
        bool playTween = !isWorldVisualizer && playPanelUpgradeTween && IsDirectUpgrade(prevDef, def);
        DespawnPlaced(cell);
        SpawnPlaced(cell, def);
        if (playTween && _placed.TryGetValue(cell, out PlacedView view) && view != null)
            PlayPanelUpgradeTween(view.instance);
    }

    private void PlayPanelUpgradeTween(GameObject instance)
    {
        if (instance == null) return;
        float duration = Mathf.Max(0.01f, panelUpgradeTweenDuration);
        Transform target = instance.transform;
        Vector3 basePos = target.position;
        target.DOKill();
        float drop = Mathf.Max(0f, panelUpgradeDropHeight);
        target.position = basePos + Vector3.up * drop;
        target.DOMove(basePos, duration).SetEase(panelUpgradeTweenEase).SetUpdate(true);
    }

    private static bool IsDirectUpgrade(TowerDefinitionSO prevDef, TowerDefinitionSO nextDef)
    {
        if (prevDef == null || nextDef == null) return false;
        if (prevDef.upgradeNext == null) return false;
        return string.Equals(prevDef.upgradeNext.id, nextDef.id, StringComparison.Ordinal);
    }

    private void DespawnPlaced(Vector3Int cell)
    {
        if (!_placed.TryGetValue(cell, out PlacedView view)) return;
        _placed.Remove(cell);

        if (view != null && view.tower != null)
        {
            ClearPanelBasePlateScaleCache(view.tower.gameObject);
            if (isWorldVisualizer)
            {
                ReleaseOccupiedCells(view.def, cell);
                view.tower.SuppressGridRelease();
                var resolvedScope = scope != null ? scope : RunScopeLocator.Current;
                resolvedScope?.Entities?.UnregisterTower(view.tower);
            }
            view.tower.gameObject.SetActive(false);
            Destroy(view.tower.gameObject);
            return;
        }

        if (view != null && view.instance != null)
        {
            ClearPanelBasePlateScaleCache(view.instance);
            view.instance.SetActive(false);
            Destroy(view.instance);
        }
    }

    private void DestroyAllPlaced()
    {
        foreach (var kvp in _placed)
        {
            if (kvp.Value != null && kvp.Value.instance != null)
            {
                ClearPanelBasePlateScaleCache(kvp.Value.instance);
                if (isWorldVisualizer)
                {
                    ReleaseOccupiedCells(kvp.Value.def, kvp.Key);
                    if (kvp.Value.tower != null)
                    {
                        kvp.Value.tower.SuppressGridRelease();
                        var resolvedScope = scope != null ? scope : RunScopeLocator.Current;
                        resolvedScope?.Entities?.UnregisterTower(kvp.Value.tower);
                    }
                }
                kvp.Value.instance.SetActive(false);
                Destroy(kvp.Value.instance);
            }
        }
        _placed.Clear();
        _hasHidden = false;
    }

    private void EnsurePreview(TowerDefinitionSO def)
    {
        if (def == null || def.prefab == null) return;

        if (_previewInstance != null && _previewTowerId == def.id) return;

        ClearPreview();

        _previewInstance = Instantiate(def.prefab.gameObject);
        AttachToParent(_previewInstance, root);
        _previewInstance.name = "[PlacementPreview]";
        DisableGameplay(_previewInstance);
        ApplyPanelBasePlateScale(_previewInstance);
        _previewRenderers = _previewInstance.GetComponentsInChildren<Renderer>(true);
        _previewTowerId = def.id;
    }

    private void SetPreviewPosition(Vector3Int cell, TowerDefinitionSO def)
    {
        if (_previewInstance == null || def == null) return;
        Vector3 pos = GetPlacementPosition(def, cell);
        _previewInstance.transform.SetPositionAndRotation(pos, Quaternion.identity);
    }

    private void SetPreviewTint(bool canPlace)
    {
        if (_previewRenderers == null) return;
        Color c = canPlace ? canPlaceColor : cannotPlaceColor;

        int baseColor = Shader.PropertyToID("_BaseColor");
        int color = Shader.PropertyToID("_Color");

        for (int i = 0; i < _previewRenderers.Length; i++)
        {
            Renderer r = _previewRenderers[i];
            if (r == null) continue;
            r.GetPropertyBlock(_previewMpb);
            _previewMpb.SetColor(baseColor, c);
            _previewMpb.SetColor(color, c);
            r.SetPropertyBlock(_previewMpb);
        }
    }

    private void ClearPreview()
    {
        ClearPanelBasePlateScaleCache(_previewInstance);
        if (_previewInstance != null) Destroy(_previewInstance);
        _previewInstance = null;
        _previewRenderers = null;
        _previewTowerId = null;
        ResetGridPlaneColors();
    }

    private void HidePlacedAt(Vector3Int cell)
    {
        if (!_placed.TryGetValue(cell, out PlacedView view)) return;
        if (view == null || view.renderers == null) return;
        SetRenderersEnabled(view.renderers, false);
        _hiddenCell = cell;
        _hasHidden = true;
    }

    private void RestoreHiddenPlaced()
    {
        if (!_hasHidden) return;
        if (_placed.TryGetValue(_hiddenCell, out PlacedView view) && view != null && view.renderers != null)
            SetRenderersEnabled(view.renderers, true);
        _hasHidden = false;
    }

    private void SetRenderersEnabled(Renderer[] renderers, bool on)
    {
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].enabled = on;
    }

    public void SetAimSnapshots(Dictionary<Vector2Int, AimSnapshot> snapshots)
    {
        _aimSnapshots.Clear();
        if (snapshots != null)
        {
            foreach (var kvp in snapshots)
                _aimSnapshots[kvp.Key] = kvp.Value;
        }
        if (!isWorldVisualizer)
            ApplyAimSnapshotsToPlaced();
    }

    public void ClearAimSnapshots()
    {
        _aimSnapshots.Clear();
    }

    private void ApplyAimSnapshotsToPlaced()
    {
        if (_aimSnapshots.Count == 0) return;
        foreach (var kvp in _placed)
        {
            var view = kvp.Value;
            if (view == null || view.tower == null) continue;
            Vector2Int cell = new Vector2Int(kvp.Key.x, kvp.Key.z);
            if (_aimSnapshots.TryGetValue(cell, out AimSnapshot snap))
                view.tower.ApplyAimSnapshot(snap.yawWorldRot, snap.pitchLocalRot, snap.hasPitch);
        }
    }

    private void ApplyAimSnapshot(Vector3Int cell, TowerEntity tower)
    {
        if (isWorldVisualizer) return;
        if (tower == null) return;
        if (_aimSnapshots.Count == 0) return;
        Vector2Int cell2 = new Vector2Int(cell.x, cell.z);
        if (_aimSnapshots.TryGetValue(cell2, out AimSnapshot snap))
            tower.ApplyAimSnapshot(snap.yawWorldRot, snap.pitchLocalRot, snap.hasPitch);
    }
}
