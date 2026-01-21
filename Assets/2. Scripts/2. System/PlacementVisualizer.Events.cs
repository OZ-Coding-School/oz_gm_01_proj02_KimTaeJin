using UnityEngine;

public sealed partial class PlacementVisualizer : MonoBehaviour
{
    private void HandleGridReset()
    {
        if (isWorldVisualizer) return;

        DestroyAllPlaced();
        ClearPreview();
        ClearGridPlanes();
        ClearCenterObject();
        ConfigurePanelRender();

        ApplyPanelGridCompensation();
        AlignPanelCameraToGrid();
        RebuildGridPlanes();
        RebuildGridLines();
        EnsureCenterObject();

        if (dataService == null) return;
        foreach (var kvp in dataService.Data)
            RebuildCell(kvp.Key);
        MarkGridPlaneOverlayDirty();
        if (debugLogs)
            Debug.Log($"[PlacementVisualizer] GridReset name={name} placed={_placed.Count}");
    }

    private void HandleDataChanged(Vector3Int cell)
    {
        RebuildCell(cell);
        MarkGridPlaneOverlayDirty();
        if (debugLogs)
            Debug.Log($"[PlacementVisualizer] DataChanged cell={cell} name={name}");
    }

    private void HandlePlacementCanceled()
    {
        ClearPreview();
        RestoreHiddenPlaced();
    }

    private void HandleCellHoverChanged(Vector3Int cell)
    {
        if (dataService == null || controller == null) return;

        RestoreHiddenPlaced();

        GridDataService.PlacementResult result = dataService.EvaluatePlacement(controller.Selected, cell);
        if (result.previewDef == null)
        {
            ClearPreview();
            ResetGridPlaneColors();
            return;
        }

        EnsurePreview(result.previewDef);
        SetPreviewTint(result.canPlace);
        SetPreviewPosition(cell, result.previewDef);
        UpdateGridPlaneColors(cell, result);

        if (result.hidePlaced)
            HidePlacedAt(cell);
        if (debugLogs)
            Debug.Log($"[PlacementVisualizer] Hover cell={cell} can={result.canPlace} name={name}");
    }
}
