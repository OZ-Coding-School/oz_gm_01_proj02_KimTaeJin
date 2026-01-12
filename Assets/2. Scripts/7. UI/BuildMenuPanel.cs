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
    private bool[,] _buildableMask;
    private bool[,] _occupiedMask;

    private void Awake()
    {
        if (root == null) root = gameObject;
        if (gridView == null) gridView = GetComponentInChildren<PanelGridView>(true);
        if (preview3D == null) preview3D = GetComponentInChildren<PanelPreview3D>(true);
        if (gridView != null)
            gridView.GridChanged += OnGridChanged;

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

        root.SetActive(false);
    }

    private void OnDestroy()
    {
        if (gridView != null)
            gridView.GridChanged -= OnGridChanged;
    }

    private void Update()
    {
        if (_pendingClose) return;
        if (!_placing)
        {
            HandleOptionNavigation();
            return;
        }

        if (_scope == null || _scope.TowerBuild == null) return;

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
            GetPlacementClamp(out Vector2Int minCell, out Vector2Int maxCell);
            cell.x = Mathf.Clamp(cell.x, minCell.x, maxCell.x);
            cell.y = Mathf.Clamp(cell.y, minCell.y, maxCell.y);
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
                    {
                        preview3D.SetPlacementTint(canPlaceColor);
                        preview3D.SetPlacementActive(false);
                        preview3D.SyncPlacedTowers(_scope.Entities != null ? _scope.Entities.Towers : null);
                        RefreshGridTiles();
                    }
                    StartCloseDelay();
                }
            }
            else
            {
                if (_scope.TowerBuild.CanPlaceOffsetDetailed(_placingDef, offset, out string reason))
                    reason = "Unknown";
                Vector2Int center = GetCenterCell();
                Debug.LogWarning($"[BuildMenuPanel] Cannot place. offset={offset} center={center} reason={reason}");
            }
        }
    }

    private void TryBindScope()
    {
        if (_scope != null) return;
        _scope = RunScopeLocator.Current;
    }

    private void OnGridChanged(PanelGridView grid)
    {
        if (preview3D == null || grid == null) return;
        preview3D.SyncFromGridView(grid);
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
        {
            if (gridView != null)
                gridView.Refresh();
            preview3D.SetGridSystem(_scope.Grid);
            preview3D.SyncFromGridView(gridView);
            var baseReserver = _scope.BaseFootprintReserver != null
                ? _scope.BaseFootprintReserver
                : _scope.GetComponent<BaseFootprintReserver>();
            if (baseReserver != null && baseReserver.UseFixedFootprint)
            {
                if (baseReserver.UseFootprintMask && baseReserver.FixedFootprintMask != null)
                    preview3D.SetCenterFootprint(baseReserver.FixedFootprintMask, baseReserver.EvenFootprintBiasPositive);
                else
                    preview3D.SetCenterFootprint(baseReserver.FixedFootprintSize, baseReserver.EvenFootprintBiasPositive);
            }
            preview3D.SyncPlacedTowers(_scope.Entities != null ? _scope.Entities.Towers : null);
            RefreshGridTiles();
        }

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
        if (preview3D != null)
            preview3D.ClearPlacedTowers();
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

        if (preview3D == null) return;
        FootprintMaskUtility.GetFootprintData(_placingDef, out FootprintMaskSO mask, out Vector2Int size, out Vector2Int pivot);
        preview3D.SetPlacementFootprint(mask, size, pivot);
        preview3D.SetPlacementPrefab(_placingDef.prefab != null ? _placingDef.prefab.gameObject : null);
        preview3D.SetPlacementActive(true);

        UpdatePlacementPreview(GetDefaultPlacementCell());
    }

    private Vector2Int GetDefaultPlacementCell()
    {
        Vector2Int cell = GetCenterCell();
        GetPlacementClamp(out Vector2Int minCell, out Vector2Int maxCell);
        cell.x = Mathf.Clamp(cell.x, minCell.x, maxCell.x);
        cell.y = Mathf.Clamp(cell.y, minCell.y, maxCell.y);
        return cell;
    }

    private Vector2Int GetOffsetFromCenter(Vector2Int cell)
    {
        return cell - GetCenterCell();
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

        if (preview3D == null) return;
        preview3D.SetPlacementCell(cell, !_useMouse);
        preview3D.SetPlacementTint(can ? canPlaceColor : cannotPlaceColor);
    }

    private bool TryGetMouseCell(out Vector2Int cell)
    {
        cell = default;
        if (placementCanvas == null) return false;
        Camera cam = placementCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : (placementCanvas.worldCamera != null ? placementCanvas.worldCamera : Camera.main);

        if (preview3D != null && preview3D.TryScreenToCell(Input.mousePosition, placementCanvas, cam, out cell))
            return true;

        if (gridView == null) return false;
        return gridView.TryScreenToCell(Input.mousePosition, placementCanvas, cam, out cell);
    }

    private int GetGridWidth()
    {
        if (_scope != null && _scope.Grid != null) return _scope.Grid.Width;
        if (gridView != null) return gridView.Width;
        if (preview3D != null) return preview3D.GridWidth;
        return 1;
    }

    private int GetGridHeight()
    {
        if (_scope != null && _scope.Grid != null) return _scope.Grid.Height;
        if (gridView != null) return gridView.Height;
        if (preview3D != null) return preview3D.GridHeight;
        return 1;
    }

    private Vector2Int GetCenterCell()
    {
        TryBindScope();
        if (_scope != null)
        {
            if (_scope.TowerBuild != null) return _scope.TowerBuild.GetAnchorCell();
            if (_scope.Grid != null) return _scope.Grid.CenterCell;
        }
        if (gridView != null) return gridView.CenterCell;
        if (preview3D != null) return preview3D.CenterCell;
        return Vector2Int.zero;
    }

    private void GetPlacementClamp(out Vector2Int minCell, out Vector2Int maxCell)
    {
        int w = GetGridWidth();
        int h = GetGridHeight();
        FootprintMaskUtility.GetFootprintData(_placingDef, out _, out Vector2Int size, out Vector2Int pivot);
        size.x = Mathf.Max(1, size.x);
        size.y = Mathf.Max(1, size.y);
        pivot.x = Mathf.Clamp(pivot.x, 0, size.x - 1);
        pivot.y = Mathf.Clamp(pivot.y, 0, size.y - 1);

        minCell = new Vector2Int(pivot.x, pivot.y);
        maxCell = new Vector2Int(
            Mathf.Max(minCell.x, w - size.x + pivot.x),
            Mathf.Max(minCell.y, h - size.y + pivot.y));
    }

    private void RefreshGridTiles()
    {
        if (preview3D == null || _scope == null || _scope.Grid == null) return;
        int w = _scope.Grid.Width;
        int h = _scope.Grid.Height;

        if (!EnsureGridMasks(w, h)) return;

        BuildGridRules.ComputeBuildable(_scope.Grid, _buildableMask, _occupiedMask);
        preview3D.SetTileStates(_buildableMask, _occupiedMask);
    }

    private bool EnsureGridMasks(int w, int h)
    {
        if (_buildableMask == null || _buildableMask.GetLength(0) != w || _buildableMask.GetLength(1) != h)
            _buildableMask = new bool[w, h];
        if (_occupiedMask == null || _occupiedMask.GetLength(0) != w || _occupiedMask.GetLength(1) != h)
            _occupiedMask = new bool[w, h];
        return true;
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
