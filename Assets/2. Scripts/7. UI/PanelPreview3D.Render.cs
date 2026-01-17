using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public sealed partial class PanelPreview3D : MonoBehaviour
{
    private void SetupRenderTarget()
    {
        if (previewCamera == null || targetImage == null) return;

        if (renderTexture == null)
            renderTexture = new RenderTexture(1024, 1024, 16, RenderTextureFormat.ARGB32);

        previewCamera.targetTexture = renderTexture;
        targetImage.texture = renderTexture;
        if (renderTexture != null)
            previewCamera.aspect = (float)renderTexture.width / Mathf.Max(1f, renderTexture.height);
    }

    private void FitRawImageToGrid()
    {
        if (!autoFitRawImageToGrid || targetImage == null || gridRootRect == null) return;
        RectTransform rt = targetImage.rectTransform;
        if (rt == null) return;

        if (rt.parent != gridRootRect)
            rt.SetParent(gridRootRect, false);

        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.SetSiblingIndex(0);
    }

    private void UpdateRenderTextureFromTarget()
    {
        if (previewCamera == null || targetImage == null) return;
        var rt = targetImage.rectTransform;
        Vector2 size = rt.rect.size;
        if (size.x < 1f || size.y < 1f) return;

        int w = Mathf.Clamp(Mathf.RoundToInt(size.x * Mathf.Max(0.1f, renderTextureScale)), renderTextureMinSize, renderTextureMaxSize);
        int h = Mathf.Clamp(Mathf.RoundToInt(size.y * Mathf.Max(0.1f, renderTextureScale)), renderTextureMinSize, renderTextureMaxSize);
        if (_rtSize.x == w && _rtSize.y == h && renderTexture != null) return;

        _rtSize = new Vector2Int(w, h);
        if (renderTexture != null)
            renderTexture.Release();

        renderTexture = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32);
        previewCamera.targetTexture = renderTexture;
        targetImage.texture = renderTexture;
        previewCamera.aspect = (float)w / Mathf.Max(1f, h);
        ApplyCameraSettings();
    }

    private void ApplyCameraSettings()
    {
        EnsurePreviewRoot();
        if (previewCamera == null) return;
        if (previewCamera.GetComponent<PlayAreaFogIgnore>() == null)
            previewCamera.gameObject.AddComponent<PlayAreaFogIgnore>();
        if (isolatePreviewWorld)
        {
            previewCamera.nearClipPlane = Mathf.Max(0.01f, previewNearClip);
            previewCamera.farClipPlane = Mathf.Max(previewCamera.nearClipPlane + 1f, previewFarClip);
        }
        previewCamera.orthographic = useOrthographic;
        if (useOrthographic)
        {
            if (autoFitToGrid)
            {
                float halfH = (gridHeight * cellWorldHeight) * 0.5f;
                float halfW = (gridWidth * cellWorldWidth) * 0.5f;
                float aspect = Mathf.Max(0.01f, previewCamera.aspect);
                float fit = Mathf.Max(halfH, halfW / aspect) * Mathf.Max(1f, fitPadding);
                previewCamera.orthographicSize = fit;
            }
            else
            {
                previewCamera.orthographicSize = (gridHeight * cellWorldHeight) * 0.5f;
            }
        }
        else
        {
            previewCamera.fieldOfView = Mathf.Clamp(perspectiveFov, 10f, 80f);
        }
        previewCamera.cullingMask = GetPreviewLayerMask();
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        bool useTransparent = transparentBackground && !ShouldForceOpaqueBackground();
        if (useTransparent)
        {
            previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        }
        else
        {
            Color bg = previewBackgroundColor;
            bg.a = 1f;
            previewCamera.backgroundColor = bg;
        }
        ApplyTargetImageTint(useTransparent);
        ApplyPreviewLayerExclusion();

        bool forceRig = isolatePreviewWorld;
        if ((autoSetupCamera || forceRig) && previewRoot != null)
        {
            previewCamera.transform.SetParent(previewRoot, false);
            if (autoSetupCamera && lockCameraTopDown)
            {
                previewCamera.transform.localPosition = new Vector3(0f, Mathf.Max(0.1f, topDownHeight), 0f);
                previewCamera.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            else if (autoSetupCamera)
            {
                previewCamera.transform.localPosition = cameraOffset;
                if (cameraEuler != Vector3.zero)
                    previewCamera.transform.localRotation = Quaternion.Euler(cameraEuler);
                else
                    previewCamera.transform.LookAt(previewRoot.position + cameraLookAtOffset);
            }
        }

        if ((autoSetupCamera || forceRig) && previewLight != null && previewRoot != null)
        {
            previewLight.transform.SetParent(previewRoot, false);
            if (autoSetupCamera && lockCameraTopDown)
            {
                previewLight.transform.localPosition = new Vector3(0f, Mathf.Max(0.1f, topDownHeight), 0f);
                previewLight.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            else if (autoSetupCamera)
            {
                previewLight.transform.localPosition = cameraOffset;
                if (cameraEuler != Vector3.zero)
                    previewLight.transform.localRotation = Quaternion.Euler(cameraEuler);
                else
                    previewLight.transform.LookAt(previewRoot.position + cameraLookAtOffset);
            }
        }

        ApplyPerspectiveFitToGrid();
        ApplyCellAspectCompensation();
    }

    private void ApplyPerspectiveFitToGrid()
    {
        if (previewCamera == null || previewRoot == null) return;
        if (!autoSetupCamera || useOrthographic || !autoFitToGrid || lockCameraTopDown) return;

        if (!TryGetPreviewBounds(out Bounds bounds)) return;

        float padding = Mathf.Max(1f, fitPadding);
        bounds.extents *= padding;

        float fov = Mathf.Deg2Rad * Mathf.Clamp(previewCamera.fieldOfView, 10f, 80f);
        float tanHalfV = Mathf.Tan(fov * 0.5f);
        float aspect = Mathf.Max(0.01f, previewCamera.aspect);
        float tanHalfH = tanHalfV * aspect;

        Vector3[] corners = GetBoundsCorners(bounds);
        float maxDelta = 0f;
        float near = Mathf.Max(0.01f, previewCamera.nearClipPlane);

        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 p = previewCamera.transform.InverseTransformPoint(corners[i]);
            float reqZ = Mathf.Max(Mathf.Abs(p.x) / tanHalfH, Mathf.Abs(p.y) / tanHalfV);
            maxDelta = Mathf.Max(maxDelta, reqZ - p.z);
            maxDelta = Mathf.Max(maxDelta, near - p.z);
        }

        if (maxDelta > 0.001f)
        {
            Vector3 back = previewCamera.transform.forward * maxDelta;
            previewCamera.transform.position -= back;
            if (previewLight != null)
                previewLight.transform.position -= back;
        }
    }

    private float GetMaxPreviewHeight()
    {
        if (previewRoot == null) return 0f;
        float rootY = previewRoot.position.y;
        float max = 0f;

        if (_placementInstance != null)
            AccumulateHeight(_placementInstance, rootY, ref max);

        if (_centerInstance != null)
            AccumulateHeight(_centerInstance, rootY, ref max);

        for (int i = 0; i < _placedInstances.Count; i++)
        {
            var go = _placedInstances[i];
            if (go == null) continue;
            AccumulateHeight(go, rootY, ref max);
        }

        return Mathf.Max(0f, max);
    }

    private static void AccumulateHeight(GameObject go, float rootY, ref float maxHeight)
    {
        if (go == null) return;
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0) return;
        Bounds b = GetBounds(renderers);
        float h = b.max.y - rootY;
        if (h > maxHeight)
            maxHeight = h;
    }

    private void EnsurePreviewRoot()
    {
        bool needDetached = useDetachedPreviewRoot || isolatePreviewWorld;
        if (previewRoot != null && previewRoot is RectTransform)
            needDetached = true;

        if (needDetached)
        {
            if (_runtimeRoot == null)
            {
                var go = new GameObject("[PanelPreview3D_Root]");
                _runtimeRoot = go.transform;
            }
            if (isolatePreviewWorld)
                _runtimeRoot.position = GetClampedPreviewWorldOffset();
            previewRoot = _runtimeRoot;
            if (debugPreviewPlacements && !_loggedPreviewRoot)
            {
                _loggedPreviewRoot = true;
                Debug.Log($"[PanelPreview3D] PreviewRoot pos={previewRoot.position:F2} offset={previewWorldOffset:F2} isolate={isolatePreviewWorld}");
            }
            return;
        }

        if (previewRoot == null)
        {
            var go = new GameObject("PreviewRoot");
            go.transform.SetParent(transform, false);
            previewRoot = go.transform;
        }
    }

    private void RefreshPreviewLayer()
    {
        _previewLayerCached = false;
        EnsurePreviewLayerCache();

        if (previewRoot != null)
            SetLayerRecursive(previewRoot.gameObject, _cachedPreviewLayerIndex);

        if (previewCamera != null)
            previewCamera.cullingMask = _cachedPreviewLayerMask;

        if (previewLight != null)
            previewLight.cullingMask = _cachedPreviewLayerMask;
    }

    private Vector3 GetClampedPreviewWorldOffset()
    {
        const float maxAbs = 1000f;
        return new Vector3(
            Mathf.Clamp(previewWorldOffset.x, -maxAbs, maxAbs),
            Mathf.Clamp(previewWorldOffset.y, -maxAbs, maxAbs),
            Mathf.Clamp(previewWorldOffset.z, -maxAbs, maxAbs));
    }

    private void ApplyPreviewLayerExclusion()
    {
        if (!excludePreviewLayerFromMainCamera) return;
        LayerMask mask = GetPreviewLayerMask();
        if (mask.value == 0) return;

        Camera cam = Camera.main;
        if (cam == null || cam == previewCamera) return;
        cam.cullingMask &= ~mask.value;
    }

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        if (!excludePreviewLayerFromMainCamera) return;
        if (cam == null || cam == previewCamera) return;
        LayerMask mask = GetPreviewLayerMask();
        if (mask.value == 0) return;
        cam.cullingMask &= ~mask.value;
    }

    private void OnDestroy()
    {
        if (_runtimeRoot != null)
            Destroy(_runtimeRoot.gameObject);
    }

    private void PreparePreviewObject(GameObject go)
    {
        if (go == null) return;
        SetLayerRecursive(go, GetPreviewLayerIndex());
        if (stripPreviewComponents)
            StripPreviewComponents(go);
        EnsurePreviewRenderers(go);
    }

    private void PreparePlacementPreviewObject(GameObject go)
    {
        if (go == null) return;
        SetLayerRecursive(go, GetPreviewLayerIndex());
        ApplyPlacementPreviewFilters(go);
        EnsurePreviewRenderers(go);
    }

    private static void DisablePlacementPreviewComponentsCore(GameObject root)
    {
        if (root == null) return;

        var towers = root.GetComponentsInChildren<TowerEntity>(true);
        for (int i = 0; i < towers.Length; i++)
        {
            var tower = towers[i];
            if (tower != null) tower.enabled = false;
        }

        var colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            var col = colliders[i];
            if (col != null) col.enabled = false;
        }

        var rbs = root.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rbs.Length; i++)
        {
            var rb = rbs[i];
            if (rb == null) continue;
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }

        var audios = root.GetComponentsInChildren<AudioSource>(true);
        for (int i = 0; i < audios.Length; i++)
        {
            var audio = audios[i];
            if (audio != null) audio.enabled = false;
        }

        var agents = root.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true);
        for (int i = 0; i < agents.Length; i++)
        {
            var agent = agents[i];
            if (agent != null) agent.enabled = false;
        }
    }

    private void ApplyPlacementPreviewFilters(GameObject root)
    {
        if (root == null) return;
        DisablePlacementPreviewComponentsCore(root);

        if (!applyPreviewScriptFilter) return;

        bool hasWhitelist = previewScriptWhitelist != null && previewScriptWhitelist.Length > 0;
        bool hasBlacklist = previewScriptBlacklist != null && previewScriptBlacklist.Length > 0;

        var monos = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < monos.Length; i++)
        {
            var mb = monos[i];
            if (mb == null) continue;
            if (mb is TowerEntity) continue;
            if (!IsPreviewScriptAllowed(mb, hasWhitelist, hasBlacklist))
                mb.enabled = false;
        }
    }

    private bool IsPreviewScriptAllowed(MonoBehaviour mb, bool hasWhitelist, bool hasBlacklist)
    {
        var type = mb.GetType();
        string name = type.Name;
        string full = type.FullName ?? string.Empty;

        bool inWhitelist = hasWhitelist && IsScriptInList(previewScriptWhitelist, name, full);
        bool inBlacklist = hasBlacklist && IsScriptInList(previewScriptBlacklist, name, full);

        if (hasWhitelist)
            return inWhitelist && !inBlacklist;
        if (hasBlacklist)
            return !inBlacklist;
        return false;
    }

    private static bool IsScriptInList(string[] list, string name, string full)
    {
        if (list == null || list.Length == 0) return false;
        for (int i = 0; i < list.Length; i++)
        {
            string token = list[i];
            if (string.IsNullOrWhiteSpace(token)) continue;
            if (string.Equals(token, name, System.StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(token, full, System.StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private void LogPreviewRendererState(string tag, GameObject root, Renderer[] renderers, ref bool loggedFlag)
    {
        if (loggedFlag) return;
        loggedFlag = true;

        if (!Debug.isDebugBuild && !Application.isEditor) return;

        int total = renderers != null ? renderers.Length : 0;
        int enabled = 0;
        int activeInHierarchy = 0;
        int activeAndEnabled = 0;

        if (renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                if (r.enabled) enabled++;
                if (r.gameObject.activeInHierarchy) activeInHierarchy++;
                if (r.enabled && r.gameObject.activeInHierarchy) activeAndEnabled++;
            }
        }

        int meshFilters = root != null ? root.GetComponentsInChildren<MeshFilter>(true).Length : 0;

        var sb = new StringBuilder();
        sb.Append("[PanelPreview3D] PreviewRendererState ");
        sb.Append("tag=").Append(tag).Append(" ");
        sb.Append("root=").Append(root != null ? root.name : "null").Append(" ");
        sb.Append("active=").Append(root != null && root.activeInHierarchy).Append(" ");
        sb.Append("layer=").Append(root != null ? root.layer.ToString() : "null").Append(" ");
        sb.Append("renderers=").Append(total).Append(" ");
        sb.Append("enabled=").Append(enabled).Append(" ");
        sb.Append("activeInHierarchy=").Append(activeInHierarchy).Append(" ");
        sb.Append("activeAndEnabled=").Append(activeAndEnabled).Append(" ");
        sb.Append("meshFilters=").Append(meshFilters).Append(" ");
        sb.Append("previewMask=").Append(GetPreviewLayerMask().value).Append(" ");
        if (previewCamera != null)
            sb.Append("camMask=").Append(previewCamera.cullingMask);

        Debug.Log(sb.ToString());
    }

    private void EnsurePreviewRenderers(GameObject root)
    {
        if (root == null) return;

        var lodGroups = root.GetComponentsInChildren<LODGroup>(true);
        for (int i = 0; i < lodGroups.Length; i++)
        {
            var lod = lodGroups[i];
            if (lod == null) continue;
            lod.ForceLOD(0);
        }

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasVisible = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r != null && r.enabled && r.gameObject.activeInHierarchy)
            {
                hasVisible = true;
                break;
            }
        }
        if (hasVisible) return;

        // Some prefabs keep renderers inactive until runtime scripts run.
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            if (!r.gameObject.activeSelf)
                r.gameObject.SetActive(true);
            if (!r.enabled)
                r.enabled = true;
        }
    }

    private static void StripPreviewComponents(GameObject root)
    {
        if (root == null) return;

        var monos = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < monos.Length; i++)
        {
            var mb = monos[i];
            if (mb == null) continue;
            mb.enabled = false;
        }

        var colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            var col = colliders[i];
            if (col == null) continue;
            col.enabled = false;
        }

        var rbs = root.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rbs.Length; i++)
        {
            var rb = rbs[i];
            if (rb == null) continue;
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }

        var audios = root.GetComponentsInChildren<AudioSource>(true);
        for (int i = 0; i < audios.Length; i++)
        {
            var audio = audios[i];
            if (audio != null) audio.enabled = false;
        }

        var agents = root.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true);
        for (int i = 0; i < agents.Length; i++)
        {
            var agent = agents[i];
            if (agent == null) continue;
            agent.enabled = false;
        }
    }

    private int GetPreviewLayerIndex()
    {
        EnsurePreviewLayerCache();
        return _cachedPreviewLayerIndex;
    }

    private LayerMask GetPreviewLayerMask()
    {
        EnsurePreviewLayerCache();
        return _cachedPreviewLayerMask;
    }

    private void EnsurePreviewLayerCache()
    {
        if (_previewLayerCached) return;
        _cachedPreviewLayerMask = GetEffectivePreviewLayerMask();
        _cachedPreviewLayerIndex = GetFirstLayerIndex(_cachedPreviewLayerMask.value);
        _previewLayerCached = true;
    }

    private LayerMask GetEffectivePreviewLayerMask()
    {
        if (!autoIsolatePreviewLayer) return previewLayer;

        int worldTowerMask = GetWorldTowerLayerMask();
        int stashLayer = LayerMask.NameToLayer("Stash");
        if (stashLayer >= 0 && stashLayer < 32 && (worldTowerMask & (1 << stashLayer)) == 0)
            return 1 << stashLayer;

        int mask = previewLayer.value;

        if (mask != 0)
        {
            int isolatedIndex = GetIsolatedPreviewLayerIndex(mask);
            if (isolatedIndex >= 1 && (worldTowerMask & (1 << isolatedIndex)) == 0)
                return 1 << isolatedIndex;
        }

        int fallback = Mathf.Clamp(fallbackPreviewLayerIndex, 1, 31);
        if ((worldTowerMask & (1 << fallback)) == 0)
            return 1 << fallback;

        for (int i = 31; i >= 1; i--)
        {
            if ((worldTowerMask & (1 << i)) == 0)
                return 1 << i;
        }

        return 1 << fallback;
    }

    private bool ShouldForceOpaqueBackground()
    {
        int stashLayer = LayerMask.NameToLayer("Stash");
        if (stashLayer < 0 || stashLayer > 31) return false;
        int mask = GetPreviewLayerMask().value;
        return (mask & (1 << stashLayer)) != 0;
    }

    private bool TryGetPreviewBounds(out Bounds bounds)
    {
        bounds = new Bounds();
        if (previewRoot == null) return false;

        var renderers = previewRoot.GetComponentsInChildren<Renderer>(true);
        if (renderers != null && renderers.Length > 0)
        {
            bounds = GetBounds(renderers);
            return bounds.size.sqrMagnitude > 0.0001f;
        }

        float totalW = gridWidth * cellWorldWidth;
        float totalH = gridHeight * cellWorldHeight;
        Vector3 size = new Vector3(Mathf.Max(0.1f, totalW), 0.1f, Mathf.Max(0.1f, totalH));
        bounds = new Bounds(previewRoot.position + sceneOffset, size);
        return true;
    }

    private static Vector3[] GetBoundsCorners(Bounds b)
    {
        Vector3 c = b.center;
        Vector3 e = b.extents;
        return new[]
        {
            c + new Vector3(-e.x, -e.y, -e.z),
            c + new Vector3(-e.x, -e.y,  e.z),
            c + new Vector3(-e.x,  e.y, -e.z),
            c + new Vector3(-e.x,  e.y,  e.z),
            c + new Vector3( e.x, -e.y, -e.z),
            c + new Vector3( e.x, -e.y,  e.z),
            c + new Vector3( e.x,  e.y, -e.z),
            c + new Vector3( e.x,  e.y,  e.z),
        };
    }

    private void ApplyTargetImageTint(bool useTransparent)
    {
        if (targetImage == null || useTransparent) return;
        Color c = targetImage.color;
        if (c.a >= 0.999f) return;
        c.a = 1f;
        targetImage.color = c;
    }

    private static int GetIsolatedPreviewLayerIndex(int mask)
    {
        for (int i = 31; i >= 1; i--)
        {
            if ((mask & (1 << i)) != 0)
                return i;
        }
        return -1;
    }

    private static int GetFirstLayerIndex(int mask)
    {
        if (mask == 0) return 0;
        for (int i = 0; i < 32; i++)
            if ((mask & (1 << i)) != 0) return i;
        return 0;
    }

    private int GetWorldTowerLayerMask()
    {
        int mask = 0;
        var towers = FindObjectsOfType<TowerEntity>(true);
        for (int i = 0; i < towers.Length; i++)
        {
            var tower = towers[i];
            if (tower == null) continue;
            if (previewRoot != null && tower.transform.IsChildOf(previewRoot)) continue;
            mask |= 1 << tower.gameObject.layer;
        }
        return mask;
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        if (go == null) return;
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
    }

    private static Transform FindChildByName(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name)) return null;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t != null && string.Equals(t.name, name, System.StringComparison.OrdinalIgnoreCase))
                return t;
        }
        return null;
    }
}
