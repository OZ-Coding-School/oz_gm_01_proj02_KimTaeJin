using System;
using UnityEngine;

public sealed class TowerPlacementController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GridDataService dataService;
    [SerializeField] private PanelGridView panelGrid;
    [SerializeField] private Canvas panelCanvas;
    [SerializeField] private Camera panelCamera;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Input")]
    [SerializeField] private KeyCode confirmKey = KeyCode.Return;
    [SerializeField] private KeyCode cancelKey = KeyCode.Escape;
    [SerializeField] private KeyCode upKey = KeyCode.W;
    [SerializeField] private KeyCode downKey = KeyCode.S;
    [SerializeField] private KeyCode leftKey = KeyCode.A;
    [SerializeField] private KeyCode rightKey = KeyCode.D;

    [Header("Debug")]
    [SerializeField] private bool debugCellMapping;

    public event Action<Vector3Int> OnCellHoverChanged;
    public event Action<bool> OnPlacementConfirmed;
    public event Action OnPlacementCanceled;

    public bool IsPlacing => _placing;
    public TowerDefinitionSO Selected => _selected;
    public Vector3Int CurrentCell => _currentCell;
    public PanelGridView PanelGrid => panelGrid;

    private bool _placing;
    private TowerDefinitionSO _selected;
    private Vector3Int _currentCell;
    private bool _hasCell;
    private bool _useMouse = true;
    private Vector2 _lastMousePos;
    private CellSource _lastSource = CellSource.None;

    private enum CellSource
    {
        None,
        Panel,
        World,
        Keyboard
    }

    private void Awake()
    {
        if (dataService == null) dataService = FindObjectOfType<GridDataService>(true);
        if (panelCanvas == null && panelGrid != null)
            panelCanvas = panelGrid.GetComponentInParent<Canvas>();
    }

    private void Update()
    {
        if (!_placing) return;

        Vector3Int cell = _currentCell;
        bool moved = false;
        bool usedKeyboard = false;

        if (Input.GetKeyDown(upKey)) { cell += new Vector3Int(0, 0, 1); moved = true; usedKeyboard = true; }
        if (Input.GetKeyDown(downKey)) { cell += new Vector3Int(0, 0, -1); moved = true; usedKeyboard = true; }
        if (Input.GetKeyDown(leftKey)) { cell += new Vector3Int(-1, 0, 0); moved = true; usedKeyboard = true; }
        if (Input.GetKeyDown(rightKey)) { cell += new Vector3Int(1, 0, 0); moved = true; usedKeyboard = true; }
        if (usedKeyboard) _useMouse = false;
        if (usedKeyboard) _lastSource = CellSource.Keyboard;

        Vector2 mousePos = Input.mousePosition;
        if ((mousePos - _lastMousePos).sqrMagnitude > 1f)
        {
            _useMouse = true;
            _lastMousePos = mousePos;
        }

        if (_useMouse && TryGetMouseCell(out Vector3Int mouseCell))
        {
            cell = mouseCell;
            moved = true;
        }

        if (moved || !_hasCell)
        {
            _hasCell = true;
            cell = ClampCell(cell);
            if (cell != _currentCell)
            {
                _currentCell = cell;
                OnCellHoverChanged?.Invoke(_currentCell);
                if (debugCellMapping)
                    LogCellMapping(_currentCell);
            }
        }

    }

    private void LateUpdate()
    {
        if (!_placing) return;

        if (Input.GetKeyDown(cancelKey) || Input.GetMouseButtonDown(1))
        {
            CancelPlacement();
            return;
        }

        if (Input.GetKeyDown(confirmKey))
            ConfirmPlacement();
    }

    public void BeginPlacement(TowerDefinitionSO def)
    {
        if (def == null) return;
        _selected = def;
        _placing = true;
        _hasCell = false;
        _useMouse = true;
        _currentCell = dataService != null ? dataService.GetAnchorCell() : Vector3Int.zero;
        _hasCell = true;
        OnCellHoverChanged?.Invoke(_currentCell);
    }

    public void CancelPlacement()
    {
        if (!_placing) return;
        _placing = false;
        _selected = null;
        _hasCell = false;
        OnPlacementCanceled?.Invoke();
    }

    private void ConfirmPlacement()
    {
        if (!_placing || _selected == null || dataService == null) return;
        bool ok = dataService.TryApplyPlacement(_selected, _currentCell, out _);
        OnPlacementConfirmed?.Invoke(ok);
    }

    private bool TryGetMouseCell(out Vector3Int cell)
    {
        cell = default;

        if (panelGrid != null && panelCanvas != null)
        {
            Camera cam = panelCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : (panelCamera != null ? panelCamera : Camera.main);

            if (panelGrid.TryScreenToCell(Input.mousePosition, panelCanvas, cam, out Vector2Int panelCell))
            {
                cell = new Vector3Int(panelCell.x, 0, panelCell.y);
                _lastSource = CellSource.Panel;
                return true;
            }
        }

        Grid worldGrid = dataService != null ? dataService.WorldGrid : null;
        if (worldGrid == null) return false;

        if (worldCamera == null) worldCamera = Camera.main;
        if (worldCamera == null) return false;

        Ray ray = worldCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 500f, groundMask, QueryTriggerInteraction.Ignore))
        {
            cell = worldGrid.WorldToCell(hit.point);
            _lastSource = CellSource.World;
            return true;
        }

        return false;
    }

    private Vector3Int ClampCell(Vector3Int cell)
    {
        int w = 1;
        int h = 1;

        if (dataService != null && dataService.GridSystem != null)
        {
            w = dataService.GridSystem.Width;
            h = dataService.GridSystem.Height;
        }
        else if (panelGrid != null)
        {
            w = panelGrid.Width;
            h = panelGrid.Height;
        }

        cell.x = Mathf.Clamp(cell.x, 0, Mathf.Max(0, w - 1));
        cell.z = Mathf.Clamp(cell.z, 0, Mathf.Max(0, h - 1));
        cell.y = 0;
        return cell;
    }

    private void LogCellMapping(Vector3Int cell)
    {
        Grid worldGrid = dataService != null ? dataService.WorldGrid : null;
        GridSystem gridSystem = dataService != null ? dataService.GridSystem : null;

        Vector3 gridPos = worldGrid != null ? worldGrid.transform.position : Vector3.zero;
        Vector3 origin = gridSystem != null ? gridSystem.Origin : Vector3.zero;
        Vector3 anchorPos = gridSystem != null && gridSystem.Anchor != null ? gridSystem.Anchor.position : Vector3.zero;
        Vector3 cellSize = worldGrid != null ? worldGrid.cellSize : Vector3.zero;

        Debug.Log($"[PlacementCtrl] src={_lastSource} cell={cell} gridPos={gridPos} origin={origin} anchor={anchorPos} cellSize={cellSize}", this);
    }
}
