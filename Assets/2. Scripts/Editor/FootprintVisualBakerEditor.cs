using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(FootprintVisualBaker))]
public sealed class FootprintVisualBakerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(6f);

        var baker = (FootprintVisualBaker)target;
        if (baker == null) return;

        bool readyAuto = baker.HasRequiredInputsForAutoSetup(out string autoReason);
        if (!readyAuto)
            EditorGUILayout.HelpBox($"Missing required: {autoReason}", MessageType.Warning);

        bool readyRebuild = baker.HasRequiredInputs(out _);

        using (new EditorGUI.DisabledScope(!readyAuto))
        {
            if (GUILayout.Button("Auto Setup + Rebuild"))
                RunAutoSetup(baker);
        }

        using (new EditorGUI.DisabledScope(!readyRebuild))
        {
            if (GUILayout.Button("Rebuild"))
            {
                if (TryRunRebuildOnPrefabAsset(baker, out string reason))
                {
                    if (!string.IsNullOrEmpty(reason))
                        EditorUtility.DisplayDialog("Rebuild", reason, "OK");
                    return;
                }

                Undo.RegisterFullObjectHierarchyUndo(baker.gameObject, "Rebuild Footprint Visuals");
                baker.Rebuild();
                EditorUtility.SetDirty(baker.gameObject);
            }

            if (GUILayout.Button("Bake To Prefab"))
            {
                if (TryRunRebuildOnPrefabAsset(baker, out string reason))
                {
                    if (!string.IsNullOrEmpty(reason))
                        EditorUtility.DisplayDialog("Bake To Prefab", reason, "OK");
                    return;
                }

                Undo.RegisterFullObjectHierarchyUndo(baker.gameObject, "Bake Footprint Visuals");
                baker.Rebuild();

                if (PrefabUtility.IsPartOfPrefabInstance(baker.gameObject))
                    PrefabUtility.ApplyPrefabInstance(baker.gameObject, InteractionMode.UserAction);

                EditorUtility.SetDirty(baker.gameObject);
            }
        }
    }

    private static void RunAutoSetup(FootprintVisualBaker baker)
    {
        if (baker == null) return;

        if (TryRunAutoSetupOnPrefabAsset(baker, out string reason))
        {
            if (!string.IsNullOrEmpty(reason))
                EditorUtility.DisplayDialog("Auto Setup + Rebuild", reason, "OK");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(baker.gameObject, "Auto Setup + Rebuild Footprint Visuals");
        baker.AutoSetupAndRebuild();
        EditorUtility.SetDirty(baker.gameObject);
    }

    private static bool TryRunAutoSetupOnPrefabAsset(FootprintVisualBaker baker, out string reason)
    {
        reason = "";
        if (baker == null) return false;

        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        bool inPrefabStage = stage != null && stage.IsPartOfPrefabContents(baker.gameObject);
        if (inPrefabStage) return false;
        if (!PrefabUtility.IsPartOfPrefabAsset(baker.gameObject)) return false;

        string path = AssetDatabase.GetAssetPath(baker.gameObject);
        if (string.IsNullOrEmpty(path)) return false;

        bool apply = EditorUtility.DisplayDialog(
            "Auto Setup + Rebuild",
            $"This will modify the prefab asset.\n\nPath: {path}",
            "Apply",
            "Cancel");
        if (!apply)
        {
            reason = "";
            return true;
        }

        bool ok = RunAutoSetupOnPrefabAsset(path, out reason);
        return true;
    }

    private static bool TryRunRebuildOnPrefabAsset(FootprintVisualBaker baker, out string reason)
    {
        reason = "";
        if (baker == null) return false;

        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        bool inPrefabStage = stage != null && stage.IsPartOfPrefabContents(baker.gameObject);
        if (inPrefabStage) return false;
        if (!IsPrefabAssetOrInstance(baker.gameObject)) return false;

        string path = ResolvePrefabAssetPath(baker.gameObject);
        if (string.IsNullOrEmpty(path)) return false;

        bool apply = EditorUtility.DisplayDialog(
            "Rebuild",
            $"This will modify the prefab asset.\n\nPath: {path}",
            "Apply",
            "Cancel");
        if (!apply)
        {
            reason = "";
            return true;
        }

        bool ok = RunRebuildOnPrefabAsset(path, out reason);
        return true;
    }

    private static bool RunAutoSetupOnPrefabAsset(string path, out string reason)
    {
        reason = "";
        if (string.IsNullOrEmpty(path))
        {
            reason = "Invalid prefab path.";
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        if (root == null)
        {
            reason = "Failed to load prefab contents.";
            return false;
        }

        bool ok = true;
        var sb = new System.Text.StringBuilder();
        try
        {
            var bakers = root.GetComponentsInChildren<FootprintVisualBaker>(true);
            if (bakers == null || bakers.Length == 0)
            {
                ok = false;
                sb.Append("FootprintVisualBaker not found.");
            }
            else
            {
                for (int i = 0; i < bakers.Length; i++)
                {
                    var b = bakers[i];
                    if (b == null) continue;
                    if (!b.TryAutoSetupAndRebuild(out string bakerReason))
                    {
                        ok = false;
                        if (sb.Length > 0) sb.Append(" | ");
                        string name = b.name;
                        if (string.IsNullOrEmpty(bakerReason))
                            sb.Append($"{name}: Missing required inputs");
                        else
                            sb.Append($"{name}: {bakerReason}");
                    }
                }
            }

            if (ok)
                PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        reason = sb.ToString();
        return ok;
    }

    private static bool RunRebuildOnPrefabAsset(string path, out string reason)
    {
        reason = "";
        if (string.IsNullOrEmpty(path))
        {
            reason = "Invalid prefab path.";
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        if (root == null)
        {
            reason = "Failed to load prefab contents.";
            return false;
        }

        bool ok = true;
        var sb = new System.Text.StringBuilder();
        try
        {
            var bakers = root.GetComponentsInChildren<FootprintVisualBaker>(true);
            if (bakers == null || bakers.Length == 0)
            {
                ok = false;
                sb.Append("FootprintVisualBaker not found.");
            }
            else
            {
                for (int i = 0; i < bakers.Length; i++)
                {
                    var b = bakers[i];
                    if (b == null) continue;
                    if (!b.TryRebuild(out string bakerReason))
                    {
                        ok = false;
                        if (sb.Length > 0) sb.Append(" | ");
                        string name = b.name;
                        if (string.IsNullOrEmpty(bakerReason))
                            sb.Append($"{name}: Missing required inputs");
                        else
                            sb.Append($"{name}: {bakerReason}");
                    }
                }
            }

            if (ok)
                PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        reason = sb.ToString();
        return ok;
    }

    private static bool IsPrefabAssetOrInstance(GameObject go)
    {
        if (go == null) return false;
        return PrefabUtility.IsPartOfPrefabAsset(go) || PrefabUtility.IsPartOfPrefabInstance(go);
    }

    private static string ResolvePrefabAssetPath(GameObject go)
    {
        if (go == null) return string.Empty;

        string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
        if (string.IsNullOrEmpty(path))
            path = AssetDatabase.GetAssetPath(go);

        return path;
    }
}
