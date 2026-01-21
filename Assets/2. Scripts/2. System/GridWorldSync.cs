using UnityEngine;

[DefaultExecutionOrder(100)]
public sealed class GridWorldSync : MonoBehaviour
{
    [SerializeField] private GridSystem gridSystem;
    [SerializeField] private Grid unityGrid;
    [SerializeField] private bool syncCellSize = true;
    [SerializeField] private bool syncOrigin = true;
    [SerializeField] private bool syncEveryLateUpdate = true;

    public void Configure(GridSystem system, Grid grid)
    {
        gridSystem = system;
        unityGrid = grid;
        SyncNow();
    }

    private void Awake()
    {
        if (unityGrid == null) unityGrid = GetComponent<Grid>();
    }

    private void OnEnable()
    {
        SyncNow();
    }

    private void LateUpdate()
    {
        if (!syncEveryLateUpdate) return;
        SyncNow();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;
        if (unityGrid == null) unityGrid = GetComponent<Grid>();
        SyncNow();
    }
#endif

    private void SyncNow()
    {
        if (gridSystem == null)
        {
            RunScope scope = RunScopeLocator.Current;
            if (scope != null) gridSystem = scope.Grid;
        }
        if (unityGrid == null) unityGrid = GetComponent<Grid>();
        if (gridSystem == null || unityGrid == null) return;

        if (syncCellSize)
        {
            Vector3 size = unityGrid.cellSize;
            size.x = gridSystem.CellSizeX;
            size.z = gridSystem.CellSizeZ;
            unityGrid.cellSize = size;
        }

        if (syncOrigin)
        {
            Vector3 pos = transform.position;
            Vector3 origin = gridSystem.Origin;
            pos.x = origin.x;
            pos.y = origin.y;
            pos.z = origin.z;
            transform.position = pos;
        }
    }
}
