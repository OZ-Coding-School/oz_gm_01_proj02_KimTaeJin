using System;
using UnityEngine;

public sealed partial class PlacementVisualizer : MonoBehaviour
{
    private void ConfigurePanelRender()
    {
        if (isWorldVisualizer) return;
        ApplyPanelCameraPitch();
        if (!autoConfigurePanelCamera) return;

        int layer = GetPanelLayer();
        if (panelCamera != null)
        {
            if (panelTexture != null)
                panelCamera.targetTexture = panelTexture;
            if (layer >= 0)
                panelCamera.cullingMask = 1 << layer;
            if (ignorePlayAreaFogOnPanelCamera && panelCamera.GetComponent<PlayAreaFogIgnore>() == null)
                panelCamera.gameObject.AddComponent<PlayAreaFogIgnore>();
        }

        if (panelTargetImage != null && panelTexture != null)
            panelTargetImage.texture = panelTexture;
    }

    private void ApplyPanelGridCompensation()
    {
        if (isWorldVisualizer) return;
        if (!compensatePanelPitch) return;
        ApplyPanelCameraPitch();
        if (grid == null || panelCamera == null) return;

        float baseCellX = grid.cellSize.x;
        float baseCellZ = grid.cellSize.z;
        int w = 1;
        int h = 1;
        if (dataService != null && dataService.GridSystem != null)
        {
            baseCellX = dataService.GridSystem.CellSizeX;
            baseCellZ = dataService.GridSystem.CellSizeZ;
            w = dataService.GridSystem.Width;
            h = dataService.GridSystem.Height;
        }

        if (!_panelBaseCaptured || !Mathf.Approximately(_panelBaseCellZ, baseCellZ))
        {
            _panelBaseCellZ = baseCellZ;
            _panelBaseCenterOffsetZ = centerOffset.z;
            _panelBaseCaptured = true;
        }

        float panelAspect = panelCamera.aspect;
        bool hasRectAspect = false;
        float rectW = 0f;
        float rectH = 0f;
        if (panelTargetImage != null)
        {
            Rect rect = panelTargetImage.rectTransform.rect;
            Vector3 scale = panelTargetImage.rectTransform.lossyScale;
            rectW = rect.width * Mathf.Abs(scale.x);
            rectH = rect.height * Mathf.Abs(scale.y);
            if (rectW > 0.01f && rectH > 0.01f)
            {
                panelAspect = rectW / rectH;
                hasRectAspect = true;
            }
        }
        if (!hasRectAspect && panelTexture != null && panelTexture.width > 0 && panelTexture.height > 0)
            panelAspect = panelTexture.width / (float)panelTexture.height;

        float aspectSafe = Mathf.Max(0.01f, panelAspect);
        float widthSafe = Mathf.Max(1, w);
        float heightSafe = Mathf.Max(1, h);
        float screenCellRatio = (1f / aspectSafe) * (widthSafe / heightSafe);
        PanelGridView panelGridView = controller != null ? controller.PanelGrid : null;
        if (panelGridView != null)
        {
            float cellW = panelGridView.CellWidth;
            float cellH = panelGridView.CellHeight;
            if (cellW > 0.01f && cellH > 0.01f)
                screenCellRatio = cellH / cellW;
        }

        Vector3 zInCam = panelCamera.transform.InverseTransformVector(Vector3.forward);
        Vector3 xInCam = panelCamera.transform.InverseTransformVector(Vector3.right);

        float zScale = Mathf.Abs(zInCam.y);
        float xScale = Mathf.Abs(xInCam.x);
        zScale = Mathf.Clamp(zScale, 0.01f, 1f);
        xScale = Mathf.Clamp(xScale, 0.01f, 1f);

        float newCellX = baseCellX;
        float newCellZ = baseCellX * screenCellRatio * (xScale / zScale);
        float factor = baseCellZ > 0.0001f ? newCellZ / baseCellZ : 1f;

        Vector3 size = grid.cellSize;
        size.x = newCellX;
        size.z = newCellZ;
        grid.cellSize = size;

        centerOffset.z = _panelBaseCenterOffsetZ * factor;

        if (panelCamera.orthographic)
        {
            float projectedHeight = heightSafe * newCellZ * zScale;
            float projectedWidth = widthSafe * newCellX * xScale;
            float targetOrtho = 0.5f * Mathf.Max(projectedHeight, projectedWidth / aspectSafe);
            if (!Mathf.Approximately(panelCamera.orthographicSize, targetOrtho))
                panelCamera.orthographicSize = targetOrtho;
        }

        bool changed = !_panelCompensatedSizeValid
                       || !Mathf.Approximately(_panelCompensatedCellX, newCellX)
                       || !Mathf.Approximately(_panelCompensatedCellZ, newCellZ);

        if (changed)
        {
            _panelCompensatedCellX = newCellX;
            _panelCompensatedCellZ = newCellZ;
            _panelCompensatedSizeValid = true;
            RebuildGridPlanes();
            RebuildGridLines();
            MarkGridPlaneOverlayDirty();
            RefreshPanelBasePlateScales();
        }
    }

    private void AlignPanelCameraToGrid()
    {
        if (isWorldVisualizer) return;
        if (!autoAlignPanelCamera) return;
        ApplyPanelCameraPitch();
        if (panelCamera == null || grid == null) return;

        int w = dataService != null && dataService.GridSystem != null ? dataService.GridSystem.Width : 1;
        int h = dataService != null && dataService.GridSystem != null ? dataService.GridSystem.Height : 1;

        Vector3 origin = grid.transform.position;
        Vector3 size = grid.cellSize;
        Vector3 center = origin + new Vector3(size.x * w * 0.5f, 0f, size.z * h * 0.5f);

        Vector3 forward = panelCamera.transform.forward;
        if (Mathf.Abs(forward.y) < 0.0001f) return;

        float y = panelCamera.transform.position.y;
        float t = (center.y - y) / forward.y;
        Vector3 pos = center - forward * t;
        pos.y = y;
        panelCamera.transform.position = pos;
    }

    private void ApplyPanelCameraPitch()
    {
        if (!autoApplyPanelCameraPitch) return;
        if (panelCamera == null) return;

        Vector3 euler = panelCamera.transform.eulerAngles;
        if (Mathf.Approximately(euler.x, panelCameraPitch)) return;
        euler.x = panelCameraPitch;
        panelCamera.transform.eulerAngles = euler;
    }

    private void CapturePanelBase()
    {
        if (_panelBaseCaptured) return;
        if (grid == null) return;
        _panelBaseCellZ = grid.cellSize.z;
        _panelBaseCenterOffsetZ = centerOffset.z;
        _panelBaseCaptured = true;
    }

    private void ResolvePanelLayer()
    {
        string resolvedName = panelLayerName != null ? panelLayerName.Trim() : null;
        if (string.Equals(_panelLayerNameCache, resolvedName, StringComparison.Ordinal) && _panelLayer >= 0)
            return;

        _panelLayerNameCache = resolvedName;
        _panelLayer = string.IsNullOrEmpty(resolvedName) ? -1 : LayerMask.NameToLayer(resolvedName);

        if (_panelLayer < 0 && !_invalidLayerWarned && !string.IsNullOrEmpty(resolvedName))
        {
            _invalidLayerWarned = true;
            Debug.LogWarning($"[PlacementVisualizer] PanelPreview 레이어를 찾을 수 없음: {resolvedName}", this);
        }
    }

    private int GetPanelLayer()
    {
        if (_panelLayer < 0 || !string.Equals(_panelLayerNameCache, panelLayerName, StringComparison.Ordinal))
            ResolvePanelLayer();
        return _panelLayer;
    }

    private void ApplyPanelLayer(GameObject go)
    {
        if (isWorldVisualizer) return;
        if (go == null) return;
        int layer = GetPanelLayer();
        if (layer < 0 && root != null)
            layer = root.gameObject.layer;
        if (layer < 0) return;
        SetLayerRecursively(go.transform, layer);
    }

    private void SetLayerRecursively(Transform rootTransform, int layer)
    {
        if (rootTransform == null) return;
        rootTransform.gameObject.layer = layer;
        for (int i = 0; i < rootTransform.childCount; i++)
            SetLayerRecursively(rootTransform.GetChild(i), layer);
    }

    private void RefreshPanelBasePlateScales()
    {
        if (isWorldVisualizer) return;

        foreach (var kvp in _placed)
        {
            if (kvp.Value != null && kvp.Value.instance != null)
                ApplyPanelBasePlateScale(kvp.Value.instance);
        }

        if (_previewInstance != null)
            ApplyPanelBasePlateScale(_previewInstance);
        if (_centerInstance != null)
            ApplyPanelBasePlateScale(_centerInstance);
    }

    private void ApplyPanelBasePlateScale(GameObject instance)
    {
        if (isWorldVisualizer) return;
        if (instance == null || grid == null) return;

        Transform basePlate = FindChildByName(instance.transform, "BasePlate");
        if (basePlate == null) return;

        float baseCellX = grid.cellSize.x;
        float baseCellZ = grid.cellSize.z;
        GridSystem gridSystem = dataService != null ? dataService.GridSystem : null;
        if (gridSystem != null)
        {
            baseCellX = gridSystem.CellSizeX;
            baseCellZ = gridSystem.CellSizeZ;
        }

        float scaleX = baseCellX > 0.0001f ? grid.cellSize.x / baseCellX : 1f;
        float scaleZ = baseCellZ > 0.0001f ? grid.cellSize.z / baseCellZ : 1f;

        if (!_panelBasePlateScaleCache.TryGetValue(basePlate, out Vector3 baseScale))
        {
            baseScale = basePlate.localScale;
            _panelBasePlateScaleCache[basePlate] = baseScale;
        }

        basePlate.localScale = new Vector3(baseScale.x * scaleX, baseScale.y, baseScale.z * scaleZ);
    }

    private void ClearPanelBasePlateScaleCache(GameObject instance)
    {
        if (instance == null) return;
        Transform basePlate = FindChildByName(instance.transform, "BasePlate");
        if (basePlate == null) return;
        _panelBasePlateScaleCache.Remove(basePlate);
    }
}
