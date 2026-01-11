using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed class BuildMenuPanel : MonoBehaviour
{
    [Header("Panel Root")]
    [SerializeField] private GameObject root;

    [Header("Fade")]
    [SerializeField] private CanvasGroup dimGroup;
    [SerializeField] private CanvasGroup panelGroup;
    [SerializeField] private float fadeDuration = 0.18f;

    [Header("Grid View")]
    [SerializeField] private PanelGridView gridView;
    [SerializeField] private PanelPreview3D preview3D;

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

    [Header("Placement Preview")]
    [SerializeField] private RectTransform placementPreviewRect;
    [SerializeField] private Image placementPreviewImage;
    [SerializeField] private Canvas placementCanvas;
    [SerializeField] private Color canPlaceColor = new Color(0.2f, 1f, 0.2f, 0.8f);
    [SerializeField] private Color cannotPlaceColor = new Color(1f, 0.2f, 0.2f, 0.8f);
    [SerializeField] private KeyCode confirmKey = KeyCode.Return;
    [SerializeField] private KeyCode cancelKey = KeyCode.Escape;
    [SerializeField] private float exitDelayAfterPlace = 1f;

    private RunScope _scope;
    private readonly List<TowerDefinitionSO> _draft = new();
    private bool _economyBound;

    private bool _placing;
    private TowerDefinitionSO _placingDef;
    private Vector2Int _placingCell;
    private bool _hasPlacementCell;
    private bool _useMouse;
    private Vector2 _lastMousePos;
    private int _selectedIndex = -1;
    private bool _pendingClose;
    private Coroutine _closeRoutine;
    private Tween _panelFade;
    private Tween _dimFade;

    private void Awake()
    {
        if (root == null) root = gameObject;
        if (gridView == null) gridView = GetComponentInChildren<PanelGridView>(true);
        if (preview3D == null) preview3D = GetComponentInChildren<PanelPreview3D>(true);

        if (rerollButton != null) rerollButton.onClick.AddListener(OnRerollClicked);
        if (closeButton != null) closeButton.onClick.AddListener(ExitBuildMode);
        if (rerollCostText != null) rerollCostText.text = rerollCost.ToString();

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

        if (placementCanvas == null)
            placementCanvas = GetComponentInParent<Canvas>();

        if (placementPreviewRect != null)
            placementPreviewRect.gameObject.SetActive(false);

        root.SetActive(false);
    }

    private void Update()
    {
        if (_pendingClose) return;
        if (!_placing)
        {
            HandleOptionNavigation();
            return;
        }

        if (_scope == null || _scope.TowerBuild == null || gridView == null) return;

        if (Input.GetKeyDown(cancelKey) || Input.GetMouseButtonDown(1))
        {
            CancelPlacement();
            return;
        }

        Vector2Int cell = _placingCell;
        bool moved = false;
        bool usedKeyboard = false;

        if (Input.GetKeyDown(KeyCode.W)) { cell += new Vector2Int(0, 1); moved = true; }
        if (Input.GetKeyDown(KeyCode.S)) { cell += new Vector2Int(0, -1); moved = true; }
        if (Input.GetKeyDown(KeyCode.A)) { cell += new Vector2Int(-1, 0); moved = true; }
        if (Input.GetKeyDown(KeyCode.D)) { cell += new Vector2Int(1, 0); moved = true; }
        if (moved) usedKeyboard = true;

        Vector2 mousePos = Input.mousePosition;
        if ((mousePos - _lastMousePos).sqrMagnitude > 1f)
        {
            _useMouse = true;
            _lastMousePos = mousePos;
        }
        if (usedKeyboard) _useMouse = false;

        if (_useMouse && TryGetMouseCell(out Vector2Int mouseCell))
        {
            cell = mouseCell;
            moved = true;
        }

        if (moved || !_hasPlacementCell)
        {
            _hasPlacementCell = true;
            cell.x = Mathf.Clamp(cell.x, 0, gridView.Width - 1);
            cell.y = Mathf.Clamp(cell.y, 0, gridView.Height - 1);
            _placingCell = cell;
        }

        UpdatePlacementPreview(cell);

        if (Input.GetKeyDown(confirmKey))
        {
            Vector2Int offset = GetOffsetFromCenter(cell);
            if (_scope.TowerBuild.CanPlaceOffset(_placingDef, offset))
            {
                bool placed = _scope.TowerBuild.TryPlaceTowerOffset(_placingDef, offset, Quaternion.identity);
                if (placed)
                {
                    _pendingClose = true;
                    if (preview3D != null)
                        preview3D.SetPlacementTint(canPlaceColor);
                    StartCloseDelay();
                }
            }
            else
            {
                if (_scope.TowerBuild.CanPlaceOffsetDetailed(_placingDef, offset, out string reason))
                    reason = "Unknown";
                Vector2Int center = _scope.Grid != null ? _scope.Grid.CenterCell : Vector2Int.zero;
                Debug.LogWarning($"[BuildMenuPanel] Cannot place. offset={offset} center={center} reason={reason}");
            }
        }
    }

    private void TryBindScope()
    {
        if (_scope != null) return;
        _scope = RunScopeLocator.Current;
    }

    public void Open()
    {
        TryBindScope();
        if (_scope == null) return;

        _pendingClose = false;
        StopCloseDelay();
        ShowPanel(true);

        _scope.Events.PushBuildMode(this);

        if (preview3D != null)
            preview3D.SyncFromGridView(gridView);

        BindEconomy();
        Draft();
        ClearSelection();
        EnsureDefaultSelection();

        if (preview3D != null)
            preview3D.ShowCenter();
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
            if (panelGroup != null)
            {
                panelGroup.alpha = 0f;
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

            if (panelGroup != null)
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
                });
            }
            else
            {
                root.SetActive(false);
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
            int next = FindNextSelectable(_selectedIndex, dir);
            if (next >= 0) SetSelectedIndex(next);
        }

        if (Input.GetKeyDown(confirmKey) && _selectedIndex >= 0)
        {
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

        _placing = true;
        _placingDef = clicked.Tower;
        _hasPlacementCell = false;

        SetButtonsForPlacement(clicked);

        if (preview3D != null)
        {
            preview3D.SetPlacementPrefab(_placingDef.prefab != null ? _placingDef.prefab.gameObject : null);
            preview3D.SetPlacementFootprint(_placingDef.footprint);
            preview3D.SetPlacementActive(true);
        }
        else
        {
            if (placementPreviewRect != null)
                placementPreviewRect.gameObject.SetActive(true);
            if (placementPreviewImage != null)
            {
                placementPreviewImage.sprite = (_placingDef.preview != null) ? _placingDef.preview : _placingDef.icon;
                placementPreviewImage.color = canPlaceColor;
            }
        }

        UpdatePlacementPreview(GetDefaultPlacementCell());
    }

    private Vector2Int GetDefaultPlacementCell()
    {
        if (gridView == null)
            return Vector2Int.zero;

        return gridView.CenterCell;
    }

    private Vector2Int GetOffsetFromCenter(Vector2Int cell)
    {
        if (gridView == null) return Vector2Int.zero;
        return cell - gridView.CenterCell;
    }

    private void UpdatePlacementPreview(Vector2Int cell)
    {
        if (!_hasPlacementCell)
        {
            _placingCell = cell;
            _hasPlacementCell = true;
        }

        if (gridView == null) return;

        Vector2Int offset = GetOffsetFromCenter(cell);
        bool can = _scope.TowerBuild != null && _scope.TowerBuild.CanPlaceOffset(_placingDef, offset);

        if (preview3D != null)
        {
            preview3D.SetPlacementCell(cell, !_useMouse);
            preview3D.SetPlacementTint(can ? canPlaceColor : cannotPlaceColor);
        }
        else
        {
            if (placementPreviewRect == null) return;
            placementPreviewRect.anchoredPosition = gridView.CellToLocalCenter(cell);
            if (placementPreviewImage != null)
                placementPreviewImage.color = can ? canPlaceColor : cannotPlaceColor;
        }
    }

    private bool TryGetMouseCell(out Vector2Int cell)
    {
        cell = default;
        if (gridView == null || placementCanvas == null) return false;
        Camera cam = placementCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : (placementCanvas.worldCamera != null ? placementCanvas.worldCamera : Camera.main);

        return gridView.TryScreenToCell(Input.mousePosition, placementCanvas, cam, out cell);
    }

    private void CancelPlacement()
    {
        _placing = false;
        _placingDef = null;
        _hasPlacementCell = false;
        _pendingClose = false;
        StopCloseDelay();

        if (preview3D != null)
            preview3D.SetPlacementActive(false);
        else if (placementPreviewRect != null)
            placementPreviewRect.gameObject.SetActive(false);

        RestoreButtonsAfterPlacement();
    }

    private void StartCloseDelay()
    {
        StopCloseDelay();
        _closeRoutine = StartCoroutine(CloseAfterDelay());
    }

    private void StopCloseDelay()
    {
        if (_closeRoutine != null)
            StopCoroutine(_closeRoutine);
        _closeRoutine = null;
    }

    private System.Collections.IEnumerator CloseAfterDelay()
    {
        float delay = Mathf.Max(0f, exitDelayAfterPlace);
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

        var pool = new List<TowerDefinitionSO>(catalog.Length);
        for (int i = 0; i < catalog.Length; i++)
            if (catalog[i] != null) pool.Add(catalog[i]);

        if (pool.Count == 0) return;

        bool dup = allowDuplicates || pool.Count < n;

        for (int i = 0; i < n; i++)
        {
            int idx = Random.Range(0, pool.Count);
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
}
