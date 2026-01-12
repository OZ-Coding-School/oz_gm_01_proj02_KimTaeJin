using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FootprintMaskSO))]
public sealed class FootprintMaskSOEditor : Editor
{
    private const int CellSize = 22;
    private const int PivotSize = 6;
    private static readonly Color GateColor = new Color(1f, 0.4f, 0.2f, 0.9f);
    private static readonly Color WallColor = new Color(0.9f, 0.9f, 0.2f, 0.9f);
    private static readonly Color DecoColor = new Color(0.2f, 0.9f, 0.9f, 0.9f);

    private enum PaintMode
    {
        Occupancy = 0,
        Anchor = 1
    }

    private PaintMode _mode = PaintMode.Occupancy;
    private FootprintAnchorType _anchorType = FootprintAnchorType.Gate;

    public override void OnInspectorGUI()
    {
        var mask = (FootprintMaskSO)target;
        if (mask == null) return;

        int width = mask.Width;
        int height = mask.Height;
        Vector2Int pivot = mask.Pivot;

        EditorGUI.BeginChangeCheck();
        int newWidth = EditorGUILayout.IntField("Width", width);
        int newHeight = EditorGUILayout.IntField("Height", height);
        Vector2Int newPivot = EditorGUILayout.Vector2IntField("Pivot", pivot);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(mask, "Resize Footprint Mask");
            mask.Resize(newWidth, newHeight);
            newPivot.x = Mathf.Clamp(newPivot.x, 0, mask.Width - 1);
            newPivot.y = Mathf.Clamp(newPivot.y, 0, mask.Height - 1);
            SetPivot(mask, newPivot);
            EditorUtility.SetDirty(mask);
        }

        GUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Fill")) { Record(mask, "Fill Mask"); mask.Fill(); }
        if (GUILayout.Button("Clear")) { Record(mask, "Clear Mask"); mask.Clear(); }
        if (GUILayout.Button("Invert")) { Record(mask, "Invert Mask"); mask.Invert(); }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(6);
        _mode = (PaintMode)GUILayout.Toolbar((int)_mode, new[] { "Occupancy", "Anchor" });
        if (_mode == PaintMode.Anchor)
        {
            _anchorType = (FootprintAnchorType)EditorGUILayout.EnumPopup("Anchor Type", _anchorType);
            if (GUILayout.Button("Clear Anchors"))
            {
                Record(mask, "Clear Anchors");
                mask.ClearAnchors();
                EditorUtility.SetDirty(mask);
            }
        }
        GUILayout.Space(4);
        DrawGrid(mask);
    }

    private void DrawGrid(FootprintMaskSO mask)
    {
        int w = mask.Width;
        int h = mask.Height;
        Vector2Int pivot = mask.Pivot;

        var center = GUILayoutUtility.GetRect(w * CellSize, h * CellSize, GUILayout.ExpandWidth(false));
        Rect gridRect = new Rect(center.x, center.y, w * CellSize, h * CellSize);

        Handles.BeginGUI();
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int drawY = (h - 1 - y);
                Rect r = new Rect(
                    gridRect.x + x * CellSize,
                    gridRect.y + drawY * CellSize,
                    CellSize,
                    CellSize);

                bool filled = mask.GetCell(x, y);
                Color c = filled ? new Color(0.2f, 0.8f, 1f, 0.65f) : new Color(0.1f, 0.1f, 0.1f, 0.2f);
                EditorGUI.DrawRect(r, c);
                Handles.color = new Color(1f, 1f, 1f, 0.15f);
                Handles.DrawLine(new Vector3(r.xMin, r.yMin), new Vector3(r.xMax, r.yMin));
                Handles.DrawLine(new Vector3(r.xMin, r.yMin), new Vector3(r.xMin, r.yMax));

                FootprintAnchorType anchor = mask.GetAnchor(x, y);
                if (anchor != FootprintAnchorType.None)
                {
                    Color ac = AnchorToColor(anchor);
                    Rect ar = new Rect(r.xMin + 3f, r.yMin + 3f, 6f, 6f);
                    EditorGUI.DrawRect(ar, ac);
                }

                if (x == pivot.x && y == pivot.y)
                {
                    Rect pr = new Rect(
                        r.center.x - PivotSize * 0.5f,
                        r.center.y - PivotSize * 0.5f,
                        PivotSize,
                        PivotSize);
                    EditorGUI.DrawRect(pr, new Color(1f, 0.3f, 0.3f, 0.95f));
                }
            }
        }
        Handles.EndGUI();

        GUILayout.Space(gridRect.height + 6f);

        Event e = Event.current;
        if (e.type == EventType.MouseDown && gridRect.Contains(e.mousePosition))
        {
            int x = Mathf.FloorToInt((e.mousePosition.x - gridRect.x) / CellSize);
            int y = h - 1 - Mathf.FloorToInt((e.mousePosition.y - gridRect.y) / CellSize);
            if (x >= 0 && y >= 0 && x < w && y < h)
            {
                if (e.button == 0)
                {
                    if (_mode == PaintMode.Occupancy)
                    {
                        Record(mask, "Toggle Footprint Cell");
                        bool next = !mask.GetCell(x, y);
                        mask.SetCell(x, y, next);
                    }
                    else
                    {
                        Record(mask, "Paint Anchor Cell");
                        if (e.shift)
                        {
                            mask.SetAnchor(x, y, FootprintAnchorType.None);
                        }
                        else
                        {
                            mask.SetAnchor(x, y, _anchorType);
                        }
                    }
                    EditorUtility.SetDirty(mask);
                    e.Use();
                }
                else if (e.button == 1)
                {
                    Record(mask, "Set Pivot");
                    SetPivot(mask, new Vector2Int(x, y));
                    EditorUtility.SetDirty(mask);
                    e.Use();
                }
            }
        }
    }

    private static void SetPivot(FootprintMaskSO mask, Vector2Int pivot)
    {
        var so = new SerializedObject(mask);
        var prop = so.FindProperty("pivot");
        if (prop != null)
        {
            prop.vector2IntValue = pivot;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void Record(Object target, string label)
    {
        Undo.RecordObject(target, label);
    }

    private static Color AnchorToColor(FootprintAnchorType type)
    {
        return type switch
        {
            FootprintAnchorType.Gate => GateColor,
            FootprintAnchorType.Wall => WallColor,
            FootprintAnchorType.Deco => DecoColor,
            _ => new Color(1f, 1f, 1f, 0.6f)
        };
    }
}
