using UnityEngine;

public sealed class BaseFootprintReserver : MonoBehaviour
{
    [SerializeField] private Collider boundsCollider;
    [SerializeField] private Renderer boundsRenderer;
    [SerializeField] private float boundsPadding = 0.05f;

    private RunScope _scope;
    private bool _hasRect;
    private Vector2Int _minCell;
    private Vector2Int _maxCell;

    public bool TryGetCellRect(out Vector2Int minCell, out Vector2Int maxCell)
    {
        minCell = _minCell;
        maxCell = _maxCell;
        return _hasRect;
    }

    public void Construct(RunScope scope)
    {
        _scope = scope;
        TryReserve();
    }

    private void TryReserve()
    {
        if (_scope == null || _scope.Grid == null) return;
        var anchor = _scope.Grid.Anchor;
        if (anchor == null) return;

        if (boundsCollider == null) boundsCollider = anchor.GetComponentInChildren<Collider>();
        if (boundsRenderer == null) boundsRenderer = anchor.GetComponentInChildren<Renderer>();

        Bounds b;
        if (boundsCollider != null) b = boundsCollider.bounds;
        else if (boundsRenderer != null) b = boundsRenderer.bounds;
        else b = new Bounds(anchor.position, Vector3.one * 0.01f);

        if (boundsPadding > 0f) b.Expand(boundsPadding * 2f);

        const float eps = 0.0001f;
        Vector2Int min = _scope.Grid.WorldToCell(new Vector3(b.min.x + eps, 0f, b.min.z + eps));
        Vector2Int max = _scope.Grid.WorldToCell(new Vector3(b.max.x - eps, 0f, b.max.z - eps));

        min.x = Mathf.Clamp(min.x, 0, _scope.Grid.Width - 1);
        min.y = Mathf.Clamp(min.y, 0, _scope.Grid.Height - 1);
        max.x = Mathf.Clamp(max.x, 0, _scope.Grid.Width - 1);
        max.y = Mathf.Clamp(max.y, 0, _scope.Grid.Height - 1);

        _minCell = min;
        _maxCell = max;
        _hasRect = true;

        for (int y = min.y; y <= max.y; y++)
            for (int x = min.x; x <= max.x; x++)
                _scope.Grid.TryOccupy(new Vector2Int(x, y));
    }
}
