using UnityEngine;
using UnityEngine.SceneManagement;

public sealed partial class PlacementVisualizer : MonoBehaviour
{
    private void AttachToParent(GameObject instance, Transform parent)
    {
        if (instance == null) return;

        Transform resolved = ResolveParent(parent);
        if (resolved == null) return;

        Scene parentScene = resolved.gameObject.scene;
        if (parentScene.IsValid() && instance.scene != parentScene)
            SceneManager.MoveGameObjectToScene(instance, parentScene);
        instance.transform.SetParent(resolved, true);
    }

    private Transform ResolveParent(Transform parent)
    {
        if (parent != null && parent.gameObject.scene.IsValid())
            return parent;

        if (transform != null && transform.gameObject.scene.IsValid())
        {
            WarnInvalidParent(parent);
            return transform;
        }

        WarnInvalidParent(parent);
        return null;
    }

    private void WarnInvalidParent(Transform parent)
    {
        if (_invalidParentWarned) return;
        _invalidParentWarned = true;
        string parentName = parent != null ? parent.name : "null";
        Debug.LogWarning(
            $"[PlacementVisualizer] 부모가 비어있거나 연결되지 않음. Hierarchy에서 서브오브젝트로 Root/GridPlaneRoot를 명시 지정하세요. parent={parentName}",
            this);
    }

    private static Transform FindChildByName(Transform rootTransform, string targetName)
    {
        if (rootTransform == null || string.IsNullOrEmpty(targetName)) return null;
        Transform direct = rootTransform.Find(targetName);
        if (direct != null) return direct;

        var list = rootTransform.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < list.Length; i++)
        {
            Transform t = list[i];
            if (t == null || t == rootTransform) continue;
            if (t.name == targetName) return t;
        }

        return null;
    }

    private static Bounds GetPrefabBounds(GameObject prefab)
    {
        if (prefab == null) return new Bounds(Vector3.zero, Vector3.zero);
        var temp = Instantiate(prefab);
        temp.hideFlags = HideFlags.HideAndDontSave;
        temp.transform.position = Vector3.zero;
        temp.transform.rotation = Quaternion.identity;
        temp.transform.localScale = prefab.transform.localScale;

        var colliders = temp.GetComponentsInChildren<Collider>(true);
        Bounds b = GetBounds(colliders);
        if (b.size.sqrMagnitude < 0.0001f)
        {
            var renderers = temp.GetComponentsInChildren<Renderer>(true);
            b = GetBounds(renderers);
        }

        if (Application.isPlaying)
            Destroy(temp);
        else
            DestroyImmediate(temp);

        return b;
    }

    private static Bounds GetBounds(Renderer[] renderers)
    {
        bool has = false;
        Bounds b = new Bounds(Vector3.zero, Vector3.zero);
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            if (!has)
            {
                b = r.bounds;
                has = true;
            }
            else b.Encapsulate(r.bounds);
        }
        return b;
    }

    private static Bounds GetBounds(Collider[] colliders)
    {
        bool has = false;
        Bounds b = new Bounds(Vector3.zero, Vector3.zero);
        for (int i = 0; i < colliders.Length; i++)
        {
            var c = colliders[i];
            if (c == null) continue;
            if (!has)
            {
                b = c.bounds;
                has = true;
            }
            else b.Encapsulate(c.bounds);
        }
        return b;
    }

    private void DisableGameplay(GameObject go)
    {
        foreach (var t in go.GetComponentsInChildren<TowerEntity>(true))
            t.SuppressGridRelease();

        foreach (var c in go.GetComponentsInChildren<Collider>(true))
            c.enabled = false;

        foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            mb.enabled = false;

        if (!isWorldVisualizer)
        {
            ApplyPanelLayer(go);
            return;
        }

        int ignore = LayerMask.NameToLayer("Ignore Raycast");
        if (ignore >= 0)
            SetLayerRecursively(go.transform, ignore);
    }
}
