using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public sealed class BuildMenuPanel : MonoBehaviour
{
    [Header("Panel Root")]
    [SerializeField] private GameObject root;

    [Header("Fade")]
    [SerializeField] private CanvasGroup dimGroup;
    [SerializeField] private CanvasGroup panelGroup;
    [SerializeField] private float fadeDuration = 0.18f;
    [SerializeField] private bool forcePanelVisibleOnOpen = false;

    [Header("Grid View")]
    [SerializeField] private PanelGridView gridView;
    [SerializeField] private TowerPlacementController placementController;

    [Header("Panel Aim Snapshot")]
    [SerializeField] private bool captureTowerAimSnapshot = true;
    [SerializeField] private PlacementVisualizer panelVisualizer;

    [Header("Options (3)")]
    [SerializeField] private TowerBuildButton[] optionButtons;

    [Header("Catalog")]
    [SerializeField] private BuildOptionCatalogSO optionCatalog;

    [Header("Controls")]
    [SerializeField] private Button rerollButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Text rerollCostText;

    [Header("Tuning")]
    [SerializeField] private int optionCount = 3;
    [SerializeField] private bool allowDuplicates = false;
    [SerializeField] private int rerollCost = 2;

    [Header("Placement")]
    [SerializeField] private KeyCode confirmKey = KeyCode.Return;
    [SerializeField] private float exitDelayAfterPlace = 1f;

    private RunScope _scope;
    private GridDataService _gridData;
    private readonly List<TowerDefinitionSO> _draft = new();
    private readonly Dictionary<Vector2Int, PlacementVisualizer.AimSnapshot> _aimSnapshots = new();
    private bool _economyBound;

    private bool _placing;
    private int _selectedIndex = -1;
    private bool _pendingClose;
    private Coroutine _closeRoutine;
    private Tween _panelFade;
    private Tween _dimFade;

    public bool IsOpen => root != null && root.activeInHierarchy;
    public event Action<bool> OpenStateChanged;

    private void Awake()
    {
        if (root == null) root = gameObject;
        if (gridView == null) gridView = GetComponentInChildren<PanelGridView>(true);
        if (placementController == null) placementController = FindObjectOfType<TowerPlacementController>(true);

        if (rerollButton != null) rerollButton.onClick.AddListener(OnRerollClicked);
        if (closeButton != null) closeButton.onClick.AddListener(ExitBuildMode);
        if (rerollCostText != null) rerollCostText.text = rerollCost.ToString();
        DisableNavigation(rerollButton);
        DisableNavigation(closeButton);

        if (placementController != null)
        {
            placementController.OnPlacementConfirmed += HandlePlacementConfirmed;
            placementController.OnPlacementCanceled += HandlePlacementCanceled;
        }

        if (optionButtons != null)
        {
            for (int i = 0; i < optionButtons.Length; i++)
            {
                var btn = optionButtons[i];
                if (btn == null) continue;

                btn.Selected -= OnOptionSelected;
                btn.Selected += OnOptionSelected;

                btn.Confirmed -= OnOptionConfirmed;
                btn.Confirmed += OnOptionConfirmed;
            }
        }

        root.SetActive(false);
    }

    private void OnDestroy()
    {
        if (placementController != null)
        {
            placementController.OnPlacementConfirmed -= HandlePlacementConfirmed;
            placementController.OnPlacementCanceled -= HandlePlacementCanceled;
        }
    }

    private void Update()
    {
        if (_pendingClose) return;
        if (_placing) return;
        HandleOptionNavigation();
    }

    private void TryBindScope()
    {
        if (_scope != null) return;
        _scope = RunScopeLocator.Current;
        _gridData = _scope != null ? _scope.GridData : null;
    }

    private void CaptureAimSnapshots()
    {
        if (!captureTowerAimSnapshot) return;
        TryBindScope();
        if (_scope == null || _scope.Entities == null) return;
        ResolvePanelVisualizer();
        if (panelVisualizer == null) return;

        _aimSnapshots.Clear();

        var towers = _scope.Entities.Towers;
        for (int i = 0; i < towers.Count; i++)
        {
            var tower = towers[i];
            if (tower == null) continue;
            if (!tower.TryGetAimSnapshot(out Quaternion yaw, out Quaternion pitch, out bool hasPitch)) continue;
            _aimSnapshots[tower.Cell] = new PlacementVisualizer.AimSnapshot
            {
                yawWorldRot = yaw,
                pitchLocalRot = pitch,
                hasPitch = hasPitch
            };
        }

        panelVisualizer.SetAimSnapshots(_aimSnapshots);
    }

    private void ResolvePanelVisualizer()
    {
        if (panelVisualizer != null) return;
        var list = FindObjectsOfType<PlacementVisualizer>(true);
        for (int i = 0; i < list.Length; i++)
        {
            if (list[i] != null && !list[i].IsWorldVisualizer)
            {
                panelVisualizer = list[i];
                break;
            }
        }
    }

    public void Open()
    {
        TryBindScope();
        if (_scope == null) return;

        _pendingClose = false;
        StopCloseDelay();
        ShowPanel(true);
        ClearUnitySelection();

        _scope.Events.PushBuildMode(this);

        CaptureAimSnapshots();

        if (gridView != null)
            gridView.Refresh();
        _gridData?.RequestGridReset();

        BindEconomy();
        Draft();
        ClearSelection();
        EnsureDefaultSelection();
    }

    private void ClosePanelOnly()
    {
        CancelPlacement();
        _pendingClose = false;
        StopCloseDelay();
        ShowPanel(false);
        UnbindEconomy();
    }

    public void ExitBuildMode()
    {
        ClosePanelOnly();

        TryBindScope();
        if (_scope != null)
            _scope.Events.PopBuildMode(this);

        ClearSelection();
    }

    private void ShowPanel(bool show)
    {
        if (root == null) return;

        if (panelGroup == null && dimGroup == null)
        {
            root.SetActive(show);
            return;
        }

        if (show)
        {
            root.SetActive(true);
            OpenStateChanged?.Invoke(true);
            if (panelGroup != null)
            {
                panelGroup.alpha = forcePanelVisibleOnOpen ? 1f : 0f;
                panelGroup.interactable = true;
                panelGroup.blocksRaycasts = true;
            }
            if (dimGroup != null)
            {
                dimGroup.alpha = 0f;
                dimGroup.blocksRaycasts = true;
            }

            _panelFade?.Kill();
            _dimFade?.Kill();

            if (panelGroup != null && !forcePanelVisibleOnOpen)
                _panelFade = panelGroup.DOFade(1f, fadeDuration).SetUpdate(true);
            if (dimGroup != null)
                _dimFade = dimGroup.DOFade(1f, fadeDuration).SetUpdate(true);
        }
        else
        {
            if (panelGroup != null)
            {
                panelGroup.interactable = false;
                panelGroup.blocksRaycasts = false;
            }
            if (dimGroup != null)
                dimGroup.blocksRaycasts = false;

            _panelFade?.Kill();
            _dimFade?.Kill();

            Tween last = null;
            if (panelGroup != null)
                last = panelGroup.DOFade(0f, fadeDuration).SetUpdate(true);
            if (dimGroup != null)
                _dimFade = dimGroup.DOFade(0f, fadeDuration).SetUpdate(true);

            if (last != null)
            {
                last.OnComplete(() =>
                {
                    if (root != null) root.SetActive(false);
                    OpenStateChanged?.Invoke(false);
                });
            }
            else
            {
                root.SetActive(false);
                OpenStateChanged?.Invoke(false);
            }
        }
    }

    private void HandleOptionNavigation()
    {
        if (optionButtons == null || optionButtons.Length == 0) return;

        if (_selectedIndex < 0 || !IsSelectable(_selectedIndex))
            EnsureDefaultSelection();

        bool moved = false;
        int dir = 0;
        if (Input.GetKeyDown(KeyCode.A)) { dir = -1; moved = true; }
        if (Input.GetKeyDown(KeyCode.D)) { dir = 1; moved = true; }

        if (moved)
        {
            ClearUnitySelection();
            int next = FindNextSelectable(_selectedIndex, dir);
            if (next >= 0) SetSelectedIndex(next);
        }

        if (Input.GetKeyDown(confirmKey) && _selectedIndex >= 0)
        {
            ClearUnitySelection();
            var btn = optionButtons[_selectedIndex];
            if (btn != null && btn.Tower != null)
                BeginPlacement(btn);
        }
    }

    private void EnsureDefaultSelection()
    {
        int idx = FindNextSelectable(-1, 1);
        if (idx >= 0)
            SetSelectedIndex(idx);
    }

    private bool IsSelectable(int index)
    {
        if (index < 0 || index >= optionButtons.Length) return false;
        var btn = optionButtons[index];
        return btn != null && btn.gameObject.activeInHierarchy && btn.Tower != null;
    }

    private int FindNextSelectable(int start, int dir)
    {
        if (optionButtons == null || optionButtons.Length == 0) return -1;
        int len = optionButtons.Length;
        int idx = start;
        for (int i = 0; i < len; i++)
        {
            idx = (idx + dir + len) % len;
            if (IsSelectable(idx)) return idx;
        }
        return -1;
    }

    private void SetSelectedIndex(int index)
    {
        _selectedIndex = index;

        for (int i = 0; i < optionButtons.Length; i++)
        {
            var b = optionButtons[i];
            if (b == null) continue;
            b.SetPlacementMode(false);
            b.SetSelected(i == _selectedIndex);
            b.SetInteractable(true);
        }
    }

    private void OnOptionSelected(TowerBuildButton clicked)
    {
        if (_placing) return;
        if (clicked == null) return;

        for (int i = 0; i < optionButtons.Length; i++)
        {
            if (optionButtons[i] == clicked)
            {
                SetSelectedIndex(i);
                break;
            }
        }
    }

    private void OnOptionConfirmed(TowerBuildButton clicked, TowerDefinitionSO def)
    {
        if (_placing) return;
        BeginPlacement(clicked);
    }

    private void BeginPlacement(TowerBuildButton clicked)
    {
        TryBindScope();
        if (_scope == null) return;
        if (clicked == null || clicked.Tower == null) return;
        if (placementController == null) return;

        _placing = true;
        TowerDefinitionSO def = clicked.Tower;
        _pendingClose = false;
        StopCloseDelay();

        SetButtonsForPlacement(clicked);
        placementController.BeginPlacement(def);
    }

    private void HandlePlacementConfirmed(bool ok)
    {
        if (!ok) return;
        _pendingClose = true;
        placementController?.CancelPlacement();
        StartCloseDelay(exitDelayAfterPlace);
    }

    private void HandlePlacementCanceled()
    {
        EndPlacementUI(_pendingClose);
    }

    private void CancelPlacement()
    {
        if (!_placing) return;
        placementController?.CancelPlacement();
        EndPlacementUI(_pendingClose);
    }

    private void EndPlacementUI(bool keepPending)
    {
        _placing = false;
        if (!keepPending) _pendingClose = false;
        if (!keepPending) StopCloseDelay();
        RestoreButtonsAfterPlacement();
    }

    private void StartCloseDelay(float delay)
    {
        StopCloseDelay();
        _closeRoutine = StartCoroutine(CloseAfterDelay(delay));
    }

    private void StopCloseDelay()
    {
        if (_closeRoutine != null)
            StopCoroutine(_closeRoutine);
        _closeRoutine = null;
    }

    private System.Collections.IEnumerator CloseAfterDelay(float delay)
    {
        delay = Mathf.Max(0f, delay);
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);
        ExitBuildMode();
    }

    private void SetButtonsForPlacement(TowerBuildButton selected)
    {
        if (optionButtons == null) return;

        for (int i = 0; i < optionButtons.Length; i++)
        {
            var b = optionButtons[i];
            if (b == null) continue;

            bool isSelected = b == selected;
            b.SetPlacementMode(isSelected);
            b.SetSelected(isSelected);
            b.SetInteractable(false);
        }
    }

    private void RestoreButtonsAfterPlacement()
    {
        if (optionButtons == null) return;

        for (int i = 0; i < optionButtons.Length; i++)
        {
            var b = optionButtons[i];
            if (b == null) continue;
            b.SetPlacementMode(false);
            b.SetSelected(false);
            b.SetInteractable(true);
        }
        _selectedIndex = -1;
    }

    private void ClearSelection()
    {
        if (optionButtons != null)
        {
            for (int i = 0; i < optionButtons.Length; i++)
            {
                var b = optionButtons[i];
                if (b == null) continue;
                b.SetPlacementMode(false);
                b.SetSelected(false);
                b.SetInteractable(true);
            }
        }
        _selectedIndex = -1;
    }

    private void BindEconomy()
    {
        if (_economyBound) return;
        if (_scope == null || _scope.Economy == null)
        {
            if (rerollCostText != null) rerollCostText.text = rerollCost.ToString();
            if (rerollButton != null) rerollButton.interactable = true;
            return;
        }

        _scope.Economy.OnGoldChanged += OnGoldChanged;
        _economyBound = true;
        OnGoldChanged(_scope.Economy.Gold);
    }

    private void UnbindEconomy()
    {
        if (!_economyBound) return;
        if (_scope != null && _scope.Economy != null)
            _scope.Economy.OnGoldChanged -= OnGoldChanged;
        _economyBound = false;
    }

    private void OnGoldChanged(int gold)
    {
        if (rerollCostText != null)
            rerollCostText.text = rerollCost.ToString();

        if (rerollButton != null)
            rerollButton.interactable = gold >= Mathf.Max(0, rerollCost);
    }

    private void OnRerollClicked()
    {
        TryBindScope();
        if (_scope == null) return;
        if (_placing) return;

        int cost = Mathf.Max(0, rerollCost);
        if (cost > 0 && _scope.Economy != null)
        {
            if (!_scope.Economy.Spend(cost)) return;
        }

        Draft();
    }

    private void DisableNavigation(Button btn)
    {
        if (btn == null) return;
        var nav = btn.navigation;
        nav.mode = Navigation.Mode.None;
        btn.navigation = nav;
    }

    private void ClearUnitySelection()
    {
        if (EventSystem.current == null) return;
        if (EventSystem.current.currentSelectedGameObject == null) return;
        EventSystem.current.SetSelectedGameObject(null);
    }

    private void Draft()
    {
        TryBindScope();
        if (_scope == null) return;

        var catalog = optionCatalog != null ? optionCatalog.Options : null;
        if (catalog == null || catalog.Length == 0)
            catalog = GameRoot.Instance != null ? GameRoot.Instance.TowerCatalog : null;
        if (catalog == null || catalog.Length == 0) return;

        int n = Mathf.Clamp(optionCount, 1, optionButtons != null ? optionButtons.Length : 3);

        _draft.Clear();

        int baseLevel = GetBaseLevel();

        var pool = new List<TowerDefinitionSO>(catalog.Length);
        for (int i = 0; i < catalog.Length; i++)
        {
            var def = catalog[i];
            if (def == null) continue;
            if (!IsUnlocked(def, baseLevel)) continue;
            pool.Add(def);
        }

        if (pool.Count == 0)
        {
            HideAllOptions();
            return;
        }

        bool dup = allowDuplicates || pool.Count < n;

        for (int i = 0; i < n; i++)
        {
            int idx = UnityEngine.Random.Range(0, pool.Count);
            var picked = pool[idx];
            _draft.Add(picked);
            if (!dup) pool.RemoveAt(idx);
        }

        for (int i = 0; i < optionButtons.Length; i++)
        {
            var btn = optionButtons[i];
            if (btn == null) continue;

            if (i < _draft.Count)
            {
                btn.gameObject.SetActive(true);
                btn.SetTower(_draft[i]);
            }
            else btn.gameObject.SetActive(false);
        }

        ClearSelection();
        EnsureDefaultSelection();
    }

    private int GetBaseLevel()
    {
        if (_scope != null && _scope.Progression != null)
            return Mathf.Max(1, _scope.Progression.BaseLevel);
        return 1;
    }

    private static bool IsUnlocked(TowerDefinitionSO def, int baseLevel)
    {
        if (def == null) return false;
        int need = Mathf.Max(1, def.unlockBaseLevel);
        return baseLevel >= need;
    }

    private void HideAllOptions()
    {
        if (optionButtons == null) return;
        for (int i = 0; i < optionButtons.Length; i++)
        {
            var btn = optionButtons[i];
            if (btn == null) continue;
            btn.gameObject.SetActive(false);
        }
        ClearSelection();
    }
}
