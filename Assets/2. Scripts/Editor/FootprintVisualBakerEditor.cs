using UnityEditor;
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

        if (GUILayout.Button("Rebuild"))
        {
            Undo.RegisterFullObjectHierarchyUndo(baker.gameObject, "Rebuild Footprint Visuals");
            baker.Rebuild();
            EditorUtility.SetDirty(baker.gameObject);
        }

        if (GUILayout.Button("Bake To Prefab"))
        {
            Undo.RegisterFullObjectHierarchyUndo(baker.gameObject, "Bake Footprint Visuals");
            baker.Rebuild();

            if (PrefabUtility.IsPartOfPrefabInstance(baker.gameObject))
                PrefabUtility.ApplyPrefabInstance(baker.gameObject, InteractionMode.UserAction);

            EditorUtility.SetDirty(baker.gameObject);
        }
    }
}
