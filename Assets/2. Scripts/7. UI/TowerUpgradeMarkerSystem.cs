using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TowerUpgradeMarkerSystem : MonoBehaviour
{
    [Header("Marker")]
    [SerializeField] private GameObject markerPrefab;
    [SerializeField] private Transform markerRoot;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2.2f, 0f);
    [SerializeField] private bool faceCamera = true;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private string markerText = "Upgrade!";

    [Header("Panel Grid (Optional)")]
    [SerializeField] private bool usePanelGrid = false;
    [SerializeField] private PanelGridView panelGrid;
    [SerializeField] private RectTransform panelMarkerRoot;
    [SerializeField] private Vector2 panelOffset = new Vector2(0f, 24f);
    [SerializeField] private float panelBobHeight = 12f;

    [Header("Visibility")]
    [SerializeField] private bool showOnlyWhenBuildMenuOpen = true;
    [SerializeField] private bool showOnlyWhenPlacing = true;
    [SerializeField] private bool filterBySelectedChain = true;
    [SerializeField] private BuildMenuPanel buildMenu;
    [SerializeField] private TowerPlacementController placementController;

    private RunScope _scope;
    private GridDataService _data;
    private readonly Dictionary<TowerEntity, MarkerState> _markers = new();
    private readonly List<TowerEntity> _remove = new();
    private string _lastSelectedId;

    [Header("Bobbing")]
    [SerializeField] private bool enableBobbing = true;
    [SerializeField] private float bobHeight = 0.25f;
    [SerializeField] private float bobDuration = 0.6f;
    [SerializeField] private Ease bobEase = Ease.InOutSine;
    [SerializeField] private bool randomizePhase = true;

    private sealed class MarkerState
    {
        public Transform tr;
        public Tween bobTween;
        public float bobOffset;
    }

    private void OnEnable()
    {
        Bind();
        RefreshMarkers();
        if (_data != null)
        {
            _data.OnDataChanged += OnDataChanged;
            _data.OnGridReset += OnGridReset;
        }
    }

    private void OnDisable()
    {
        if (_data != null)
        {
            _data.OnDataChanged -= OnDataChanged;
            _data.OnGridReset -= OnGridReset;
        }
        ClearMarkers();
    }

    private void LateUpdate()
    {
        RefreshOnSelectionChanged();
        if (_markers.Count == 0) return;
        if (!ShouldShowMarkers())
        {
            SetMarkersActive(false);
            return;
        }

        SetMarkersActive(true);
        if (usePanelGrid && ResolvePanelGrid())
        {
            UpdatePanelMarkers();
            return;
        }

        Camera cam = faceCamera ? (targetCamera != null ? targetCamera : Camera.main) : null;

        foreach (var kvp in _markers)
        {
            var tower = kvp.Key;
            var state = kvp.Value;
            var marker = state != null ? state.tr : null;
            if (tower == null || marker == null)
            {
                _remove.Add(tower);
                continue;
            }

            float bob = state != null ? state.bobOffset : 0f;
            marker.position = tower.transform.position + worldOffset + Vector3.up * bob;
            if (cam != null)
            {
                Vector3 to = marker.position - cam.transform.position;
                if (to.sqrMagnitude > 0.0001f)
                    marker.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
            }
        }

        if (_remove.Count > 0)
        {
            for (int i = 0; i < _remove.Count; i++)
                RemoveMarker(_remove[i]);
            _remove.Clear();
        }
    }

    private void Bind()
    {
        if (_scope == null) _scope = RunScopeLocator.Current;
        if (_scope != null) _data = _scope.GridData;
    }

    private void OnDataChanged(Vector3Int cell)
    {
        RefreshMarkers();
    }

    private void OnGridReset()
    {
        RefreshMarkers();
    }

    private void RefreshMarkers()
    {
        Bind();
        if (_scope == null || _scope.Entities == null) return;

        var towers = _scope.Entities.Towers;
        if (towers == null) return;

        for (int i = 0; i < towers.Count; i++)
        {
            var tower = towers[i];
            if (tower == null || tower.Definition == null) continue;
            if (tower.Definition.upgradeNext == null) continue;
            if (!ShouldIncludeTower(tower)) continue;
            EnsureMarker(tower);
        }

        _remove.Clear();
        foreach (var kvp in _markers)
        {
            var tower = kvp.Key;
            if (tower == null || tower.Definition == null || tower.Definition.upgradeNext == null || !ShouldIncludeTower(tower))
                _remove.Add(tower);
        }
        for (int i = 0; i < _remove.Count; i++)
            RemoveMarker(_remove[i]);
        _remove.Clear();
    }

    private void EnsureMarker(TowerEntity tower)
    {
        if (markerPrefab == null || tower == null) return;
        if (_markers.ContainsKey(tower)) return;

        Transform parent = ResolveMarkerParent();
        var go = Instantiate(markerPrefab, parent);
        go.transform.position = tower.transform.position + worldOffset;
        var tmp = go.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null) tmp.text = markerText;
        var state = new MarkerState { tr = go.transform };
        if (enableBobbing && bobHeight > 0f && bobDuration > 0f)
        {
            state.bobTween = DOTween.To(() => state.bobOffset, v => state.bobOffset = v, bobHeight, bobDuration)
                .SetEase(bobEase)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);

            if (randomizePhase)
                state.bobTween.Goto(UnityEngine.Random.value * bobDuration, true);
        }
        _markers[tower] = state;
    }

    private void RemoveMarker(TowerEntity tower)
    {
        if (tower == null) return;
        if (!_markers.TryGetValue(tower, out MarkerState state)) return;
        _markers.Remove(tower);
        if (state != null && state.bobTween != null) state.bobTween.Kill();
        if (state != null && state.tr != null) Destroy(state.tr.gameObject);
    }

    private void ClearMarkers()
    {
        foreach (var kvp in _markers)
        {
            var state = kvp.Value;
            if (state != null && state.bobTween != null) state.bobTween.Kill();
            if (state != null && state.tr != null)
                Destroy(state.tr.gameObject);
        }
        _markers.Clear();
    }

    private Transform ResolveMarkerParent()
    {
        if (usePanelGrid)
        {
            if (panelMarkerRoot != null) return panelMarkerRoot;
            if (markerRoot != null) return markerRoot;
            if (panelGrid != null) return panelGrid.transform;
        }
        return markerRoot != null ? markerRoot : transform;
    }

    private bool ResolvePanelGrid()
    {
        if (!usePanelGrid) return false;
        if (panelGrid == null)
        {
            if (placementController == null)
                placementController = FindObjectOfType<TowerPlacementController>(true);
            if (placementController != null)
                panelGrid = placementController.PanelGrid;
        }
        if (panelGrid != null) return true;
        panelGrid = FindObjectOfType<PanelGridView>(true);
        return panelGrid != null;
    }

    private void UpdatePanelMarkers()
    {
        if (panelGrid == null) return;

        foreach (var kvp in _markers)
        {
            var tower = kvp.Key;
            var state = kvp.Value;
            var marker = state != null ? state.tr : null;
            if (tower == null || marker == null)
            {
                _remove.Add(tower);
                continue;
            }

            Vector2 local = panelGrid.CellToLocalCenter(tower.Cell);
            float bob = GetPanelBobOffset(state);
            Vector2 pos = local + panelOffset + Vector2.up * bob;

            if (marker is RectTransform rt)
            {
                rt.anchoredPosition = pos;
                rt.localRotation = Quaternion.identity;
            }
            else
            {
                marker.localPosition = new Vector3(pos.x, pos.y, 0f);
                marker.localRotation = Quaternion.identity;
            }
        }

        if (_remove.Count > 0)
        {
            for (int i = 0; i < _remove.Count; i++)
                RemoveMarker(_remove[i]);
            _remove.Clear();
        }
    }

    private float GetPanelBobOffset(MarkerState state)
    {
        if (state == null) return 0f;
        if (!enableBobbing) return 0f;
        if (bobHeight <= 0f) return 0f;
        return (state.bobOffset / bobHeight) * panelBobHeight;
    }

    private void RefreshOnSelectionChanged()
    {
        if (!filterBySelectedChain) return;
        TowerDefinitionSO selected = GetSelectedDefinition();
        string id = selected != null ? selected.id : string.Empty;
        if (string.Equals(_lastSelectedId, id, StringComparison.Ordinal)) return;
        _lastSelectedId = id;
        RefreshMarkers();
    }

    private TowerDefinitionSO GetSelectedDefinition()
    {
        if (placementController == null)
            placementController = FindObjectOfType<TowerPlacementController>(true);
        return placementController != null ? placementController.Selected : null;
    }

    private bool ShouldIncludeTower(TowerEntity tower)
    {
        if (!filterBySelectedChain) return true;
        if (tower == null || tower.Definition == null) return false;
        TowerDefinitionSO selected = GetSelectedDefinition();
        if (selected == null) return false;
        return IsSameUpgradeChainById(selected, tower.Definition);
    }

    private static bool IsSameUpgradeChainById(TowerDefinitionSO a, TowerDefinitionSO b)
    {
        if (a == null || b == null) return false;
        if (IsUpgradeChainMatchById(a, b)) return true;
        return IsUpgradeChainMatchById(b, a);
    }

    private static bool IsUpgradeChainMatchById(TowerDefinitionSO root, TowerDefinitionSO target)
    {
        if (root == null || target == null) return false;
        for (TowerDefinitionSO cur = root; cur != null; cur = cur.upgradeNext)
        {
            if (string.Equals(cur.id, target.id, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private bool ShouldShowMarkers()
    {
        if (showOnlyWhenBuildMenuOpen)
        {
            if (buildMenu == null)
                buildMenu = FindObjectOfType<BuildMenuPanel>(true);
            if (buildMenu != null && !buildMenu.IsOpen) return false;
        }

        if (showOnlyWhenPlacing)
        {
            if (placementController == null)
                placementController = FindObjectOfType<TowerPlacementController>(true);
            if (placementController != null && !placementController.IsPlacing) return false;
        }

        return true;
    }

    private void SetMarkersActive(bool on)
    {
        foreach (var kvp in _markers)
        {
            var state = kvp.Value;
            if (state == null || state.tr == null) continue;
            if (state.tr.gameObject.activeSelf == on) continue;
            state.tr.gameObject.SetActive(on);
        }
    }
}
