using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class TowerToolsWindow : EditorWindow
{
    private enum Tab
    {
        BakerAutomation = 0,
        BatchRebuild = 1,
        Authoring = 2
    }

    [SerializeField] private int tabIndex;
    [SerializeField] private List<GameObject> prefabs = new List<GameObject>();
    [SerializeField] private bool autoAssign = true;
    [SerializeField] private bool overwriteAutoAssign = false;
    [SerializeField] private GameObject activePrefab;
    [SerializeField] private string towerSoFolder = "Assets/6. SO/Tower SO";
    [SerializeField] private string maskSoFolder = "Assets/6. SO/Tower SO";
    [SerializeField] private string newTowerSoName = "";
    [SerializeField] private string newMaskSoName = "";
    [SerializeField] private GameObject baseTilePrefab;
    [SerializeField] private FootprintMaskSO activeMask;
    [SerializeField] private TowerDefinitionSO activeTower;
    [SerializeField] private BuildOptionCatalogSO activeCatalog;
    [SerializeField] private GameRoot activeGameRoot;
    [SerializeField] private bool autoRegisterCatalog = false;
    [SerializeField] private bool autoRegisterGameRoot = false;
    [SerializeField] private bool applyBaseTile = true;
    [SerializeField] private bool applyMask = true;
    [SerializeField] private bool applyToAllBakers = true;
    [SerializeField] private bool syncFootprintFromMask = true;
    [SerializeField] private bool showTowerEditor = true;
    [SerializeField] private bool showMaskEditor = false;
    [SerializeField] private bool showRegisteredPrefabs = false;
    private Vector2 scroll;
    private readonly List<string> failures = new List<string>();
    private string reportTitle = "Failed Prefabs";
    private string authoringReport = "";
    private MessageType authoringReportType = MessageType.Info;
    private Editor towerEditor;
    private Editor maskEditor;
    private bool defaultsResolved;
    private const string DefaultBaseTileName = "TileReal";
    private static readonly string[] DefaultMaskNames = { "Normal Tower SO", "Normal Tower", "NormalTower" };

    [MenuItem("Tools/Tower Tools")]
    public static void Open()
    {
        GetWindow<TowerToolsWindow>("Tower Tools");
    }

    private void OnGUI()
    {
        tabIndex = GUILayout.Toolbar(tabIndex, new[] { "Baker Automation", "Batch Rebuild", "Authoring" });
        GUILayout.Space(6f);

        switch ((Tab)tabIndex)
        {
            case Tab.BakerAutomation:
                DrawBakerTab();
                break;
            case Tab.BatchRebuild:
                DrawBatchTab();
                break;
            case Tab.Authoring:
                DrawAuthoringTab();
                break;
        }
    }

    private void DrawBakerTab()
    {
        DrawPrefabList();
        GUILayout.Space(6f);

        DrawAutoAssignOptions();
        GUILayout.Space(6f);

        if (GUILayout.Button("Auto Assign (Names)"))
            RunAutoAssign();

        if (GUILayout.Button("Auto Setup + Rebuild"))
            RunBatch(true, autoAssign, overwriteAutoAssign);

        if (GUILayout.Button("Validate"))
            RunValidation();

        DrawReportList();
    }

    private void DrawBatchTab()
    {
        DrawPrefabList();
        GUILayout.Space(6f);

        DrawAutoAssignOptions();
        GUILayout.Space(6f);

        if (GUILayout.Button("Batch Rebuild"))
            RunBatch(false, false, false);

        if (GUILayout.Button("Validate"))
            RunValidation();

        DrawReportList();
    }

    private void DrawAuthoringTab()
    {
        TryResolveDefaultAssets(false);
        DrawPrefabSelector();
        if (showRegisteredPrefabs)
        {
            GUILayout.Space(6f);
            DrawPrefabList();
        }

        GameObject prefabAsset = ResolvePrefabAsset(activePrefab, out string prefabPath, out string resolveReason);
        if (prefabAsset == null)
        {
            EditorGUILayout.HelpBox(string.IsNullOrEmpty(resolveReason) ? "Select a prefab asset." : resolveReason, MessageType.Warning);
            return;
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Prefab", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Path", prefabPath);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Open Prefab Mode"))
                AssetDatabase.OpenAsset(prefabAsset);
            if (GUILayout.Button("Ping"))
                EditorGUIUtility.PingObject(prefabAsset);
            if (GUILayout.Button("Select"))
                Selection.activeObject = prefabAsset;
        }

        EditorGUILayout.Space(6f);
        DrawAutoAssignOptions();
        GUILayout.Space(4f);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Auto Assign (Names)"))
                RunSingleAutoAssign(prefabAsset);
            if (GUILayout.Button("Auto Setup + Rebuild"))
                RunSingleAutoSetup(prefabAsset);
            if (GUILayout.Button("Validate"))
                RunSingleValidation(prefabAsset);
        }

        EditorGUILayout.Space(6f);
        DrawBakerAuthoring(prefabAsset);
        EditorGUILayout.Space(6f);
        DrawTowerAuthoring(prefabAsset);
        EditorGUILayout.Space(6f);
        DrawMaskAuthoring();
        EditorGUILayout.Space(6f);
        DrawAutoRegisterSection();
        DrawAuthoringReport();
    }

    private void DrawPrefabSelector()
    {
        EditorGUILayout.LabelField("Target Prefab", EditorStyles.boldLabel);
        activePrefab = (GameObject)EditorGUILayout.ObjectField("Active Prefab", activePrefab, typeof(GameObject), false);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Use Selected"))
            {
                var go = Selection.activeObject as GameObject;
                if (go != null) activePrefab = go;
            }
            if (GUILayout.Button("Use First Registered") && prefabs.Count > 0)
            {
                activePrefab = prefabs[0];
            }
            if (GUILayout.Button("Use Prefab Name"))
            {
                if (activePrefab != null)
                {
                    newTowerSoName = activePrefab.name;
                    newMaskSoName = activePrefab.name;
                }
            }
        }

        showRegisteredPrefabs = EditorGUILayout.Foldout(showRegisteredPrefabs, "Registered Prefabs");
    }

    private GameObject ResolvePrefabAsset(GameObject target, out string path, out string reason)
    {
        reason = "";
        path = "";
        if (target == null)
        {
            reason = "Active prefab is null.";
            return null;
        }

        if (PrefabUtility.IsPartOfPrefabAsset(target))
        {
            path = AssetDatabase.GetAssetPath(target);
            if (string.IsNullOrEmpty(path))
            {
                reason = "Invalid prefab path.";
                return null;
            }
            return target;
        }

        string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(target);
        if (string.IsNullOrEmpty(assetPath))
        {
            reason = "Target is not a prefab asset.";
            return null;
        }

        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (asset == null)
        {
            reason = "Prefab asset not found.";
            return null;
        }

        path = assetPath;
        return asset;
    }

    private void RunSingleAutoAssign(GameObject prefabAsset)
    {
        if (prefabAsset == null) return;
        if (ProcessPrefabAutoAssign(prefabAsset, overwriteAutoAssign, true, out string reason))
            SetAuthoringReport(string.IsNullOrEmpty(reason) ? "Auto Assign OK" : reason, MessageType.Info);
        else
            SetAuthoringReport(string.IsNullOrEmpty(reason) ? "Auto Assign failed" : reason, MessageType.Warning);
    }

    private void RunSingleAutoSetup(GameObject prefabAsset)
    {
        if (prefabAsset == null) return;
        if (ProcessPrefabAsset(prefabAsset, true, autoAssign, overwriteAutoAssign, true, out string reason))
            SetAuthoringReport(string.IsNullOrEmpty(reason) ? "Auto Setup + Rebuild OK" : reason, MessageType.Info);
        else
            SetAuthoringReport(string.IsNullOrEmpty(reason) ? "Auto Setup + Rebuild failed" : reason, MessageType.Warning);
    }

    private void RunSingleValidation(GameObject prefabAsset)
    {
        if (prefabAsset == null) return;
        if (ProcessPrefabValidation(prefabAsset, out string report))
            SetAuthoringReport(string.IsNullOrEmpty(report) ? "Validation OK" : report, MessageType.Info);
        else
            SetAuthoringReport(string.IsNullOrEmpty(report) ? "Validation failed" : report, MessageType.Warning);
    }

    private void DrawBakerAuthoring(GameObject prefabAsset)
    {
        EditorGUILayout.LabelField("Baker Inputs", EditorStyles.boldLabel);
        baseTilePrefab = (GameObject)EditorGUILayout.ObjectField("BaseTilePrefab", baseTilePrefab, typeof(GameObject), false);
        activeMask = (FootprintMaskSO)EditorGUILayout.ObjectField("Footprint Mask", activeMask, typeof(FootprintMaskSO), false);

        if (GUILayout.Button("Apply Defaults"))
        {
            TryResolveDefaultAssets(true);
        }

        applyBaseTile = EditorGUILayout.ToggleLeft("Apply BaseTilePrefab", applyBaseTile);
        applyMask = EditorGUILayout.ToggleLeft("Apply Mask", applyMask);
        applyToAllBakers = EditorGUILayout.ToggleLeft("Apply To All Bakers", applyToAllBakers);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Load From Baker"))
            {
                if (LoadBakerSettings(prefabAsset, out string reason))
                    SetAuthoringReport("Loaded baker inputs.", MessageType.Info);
                else
                    SetAuthoringReport(string.IsNullOrEmpty(reason) ? "Load failed" : reason, MessageType.Warning);
            }
            if (GUILayout.Button("Apply To Baker(s)"))
            {
                if (ApplyBakerSettings(prefabAsset, out string reason))
                    SetAuthoringReport("Applied baker inputs.", MessageType.Info);
                else
                    SetAuthoringReport(string.IsNullOrEmpty(reason) ? "Apply failed" : reason, MessageType.Warning);
            }
        }
    }

    private void DrawTowerAuthoring(GameObject prefabAsset)
    {
        EditorGUILayout.LabelField("TowerDefinitionSO", EditorStyles.boldLabel);
        activeTower = (TowerDefinitionSO)EditorGUILayout.ObjectField("Active Tower SO", activeTower, typeof(TowerDefinitionSO), false);
        towerSoFolder = EditorGUILayout.TextField("Tower SO Folder", towerSoFolder);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Find By Prefab"))
                activeTower = FindTowerDefinitionByPrefab(prefabAsset);
            if (GUILayout.Button("Ping") && activeTower != null)
                EditorGUIUtility.PingObject(activeTower);
        }

        newTowerSoName = EditorGUILayout.TextField("New Tower SO Name", newTowerSoName);
        if (GUILayout.Button("Create Tower SO"))
        {
            TowerDefinitionSO created = CreateTowerDefinition(prefabAsset, newTowerSoName, out string reason);
            if (created != null)
            {
                activeTower = created;
                SetAuthoringReport("Created TowerDefinitionSO.", MessageType.Info);
                TryAutoRegister(created);
            }
            else
            {
                SetAuthoringReport(string.IsNullOrEmpty(reason) ? "Create failed" : reason, MessageType.Warning);
            }
        }

        syncFootprintFromMask = EditorGUILayout.ToggleLeft("Sync Footprint From Mask", syncFootprintFromMask);
        if (GUILayout.Button("Apply Prefab/Mask To Tower SO"))
        {
            if (ApplyTowerDefinition(prefabAsset, activeTower, out string reason))
            {
                SetAuthoringReport("Applied TowerDefinitionSO settings.", MessageType.Info);
                TryAutoRegister(activeTower);
            }
            else
            {
                SetAuthoringReport(string.IsNullOrEmpty(reason) ? "Apply failed" : reason, MessageType.Warning);
            }
        }

        showTowerEditor = EditorGUILayout.Foldout(showTowerEditor, "TowerDefinitionSO Editor");
        if (showTowerEditor && activeTower != null)
        {
            DrawInlineEditor(ref towerEditor, activeTower);
        }
    }

    private void DrawMaskAuthoring()
    {
        EditorGUILayout.LabelField("FootprintMaskSO", EditorStyles.boldLabel);
        activeMask = (FootprintMaskSO)EditorGUILayout.ObjectField("Active Mask SO", activeMask, typeof(FootprintMaskSO), false);
        maskSoFolder = EditorGUILayout.TextField("Mask SO Folder", maskSoFolder);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Ping") && activeMask != null)
                EditorGUIUtility.PingObject(activeMask);
        }

        newMaskSoName = EditorGUILayout.TextField("New Mask SO Name", newMaskSoName);
        if (GUILayout.Button("Create Mask SO"))
        {
            FootprintMaskSO created = CreateFootprintMask(newMaskSoName, out string reason);
            if (created != null)
            {
                activeMask = created;
                SetAuthoringReport("Created FootprintMaskSO.", MessageType.Info);
            }
            else
            {
                SetAuthoringReport(string.IsNullOrEmpty(reason) ? "Create failed" : reason, MessageType.Warning);
            }
        }

        showMaskEditor = EditorGUILayout.Foldout(showMaskEditor, "FootprintMaskSO Editor");
        if (showMaskEditor && activeMask != null)
        {
            DrawInlineEditor(ref maskEditor, activeMask);
        }
    }

    private void DrawAutoRegisterSection()
    {
        EditorGUILayout.LabelField("Auto Register", EditorStyles.boldLabel);
        autoRegisterCatalog = EditorGUILayout.ToggleLeft("BuildOptionCatalogSO", autoRegisterCatalog);
        activeCatalog = (BuildOptionCatalogSO)EditorGUILayout.ObjectField("Catalog", activeCatalog, typeof(BuildOptionCatalogSO), false);

        autoRegisterGameRoot = EditorGUILayout.ToggleLeft("GameRoot (Scene)", autoRegisterGameRoot);
        activeGameRoot = (GameRoot)EditorGUILayout.ObjectField("GameRoot", activeGameRoot, typeof(GameRoot), true);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Find GameRoot"))
                activeGameRoot = FindObjectOfType<GameRoot>();
            if (GUILayout.Button("Register Active Tower SO"))
            {
                if (activeTower == null)
                {
                    SetAuthoringReport("Active TowerDefinitionSO is null.", MessageType.Warning);
                }
                else
                {
                    TryAutoRegister(activeTower);
                }
            }
        }
    }

    private void DrawAuthoringReport()
    {
        if (string.IsNullOrEmpty(authoringReport)) return;
        EditorGUILayout.HelpBox(authoringReport, authoringReportType);
    }

    private void SetAuthoringReport(string message, MessageType type)
    {
        authoringReport = message;
        authoringReportType = type;
    }

    private void AppendAuthoringReport(string message, MessageType type)
    {
        if (string.IsNullOrEmpty(authoringReport))
        {
            SetAuthoringReport(message, type);
            return;
        }

        authoringReport += $" | {message}";
        if (type > authoringReportType)
            authoringReportType = type;
    }

    private void DrawInlineEditor(ref Editor cachedEditor, Object target)
    {
        if (target == null)
        {
            if (cachedEditor != null)
                DestroyImmediate(cachedEditor);
            cachedEditor = null;
            return;
        }

        if (cachedEditor == null || cachedEditor.target != target)
        {
            if (cachedEditor != null)
                DestroyImmediate(cachedEditor);
            cachedEditor = Editor.CreateEditor(target);
        }

        if (cachedEditor != null)
            cachedEditor.OnInspectorGUI();
    }

    private void TryResolveDefaultAssets(bool force)
    {
        if (!force && defaultsResolved) return;
        defaultsResolved = true;

        if (baseTilePrefab == null)
        {
            baseTilePrefab = FindPrefabByName(DefaultBaseTileName);
            if (baseTilePrefab == null)
                AppendAuthoringReport($"Default BaseTilePrefab not found: {DefaultBaseTileName}", MessageType.Warning);
        }

        if (activeMask == null)
        {
            activeMask = FindMaskByNames(DefaultMaskNames);
            if (activeMask == null)
                AppendAuthoringReport($"Default FootprintMaskSO not found: {string.Join(", ", DefaultMaskNames)}", MessageType.Warning);
        }
    }

    private static GameObject FindPrefabByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        string[] guids = AssetDatabase.FindAssets($"t:Prefab {name}");
        GameObject first = null;
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            string assetName = System.IO.Path.GetFileNameWithoutExtension(path);
            if (!string.Equals(assetName, name, System.StringComparison.OrdinalIgnoreCase))
                continue;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
                return prefab;
            if (first == null) first = prefab;
        }
        return first;
    }

    private static FootprintMaskSO FindMaskByNames(string[] names)
    {
        if (names == null || names.Length == 0) return null;
        for (int i = 0; i < names.Length; i++)
        {
            string name = names[i];
            if (string.IsNullOrEmpty(name)) continue;
            string[] guids = AssetDatabase.FindAssets($"t:FootprintMaskSO {name}");
            for (int j = 0; j < guids.Length; j++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[j]);
                string assetName = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!string.Equals(assetName, name, System.StringComparison.OrdinalIgnoreCase))
                    continue;
                var asset = AssetDatabase.LoadAssetAtPath<FootprintMaskSO>(path);
                if (asset != null)
                    return asset;
            }
        }
        return null;
    }

    private void EnsureBakerOnRoot(GameObject root)
    {
        if (root == null) return;
        var bakers = root.GetComponentsInChildren<FootprintVisualBaker>(true);
        if (bakers != null && bakers.Length > 0) return;

        var tower = root.GetComponentInChildren<TowerEntity>(true);
        GameObject target = tower != null ? tower.gameObject : root;
        target.AddComponent<FootprintVisualBaker>();
    }

    private bool LoadBakerSettings(GameObject prefabAsset, out string reason)
    {
        reason = "";
        if (prefabAsset == null)
        {
            reason = "Prefab is null";
            return false;
        }

        string path = AssetDatabase.GetAssetPath(prefabAsset);
        if (string.IsNullOrEmpty(path))
        {
            reason = "Invalid prefab path";
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        if (root == null)
        {
            reason = "Failed to load prefab contents";
            return false;
        }

        try
        {
            var bakers = root.GetComponentsInChildren<FootprintVisualBaker>(true);
            if (bakers == null || bakers.Length == 0)
            {
                reason = "FootprintVisualBaker not found";
                return false;
            }

            var baker = bakers[0];
            var so = new SerializedObject(baker);
            baseTilePrefab = (GameObject)so.FindProperty("baseTilePrefab").objectReferenceValue;
            activeMask = (FootprintMaskSO)so.FindProperty("mask").objectReferenceValue;
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private bool ApplyBakerSettings(GameObject prefabAsset, out string reason)
    {
        reason = "";
        if (prefabAsset == null)
        {
            reason = "Prefab is null";
            return false;
        }

        TryResolveDefaultAssets(false);

        if (applyBaseTile && baseTilePrefab == null)
        {
            reason = "BaseTilePrefab is null";
            return false;
        }
        if (applyMask && activeMask == null)
        {
            reason = "FootprintMaskSO is null";
            return false;
        }

        string path = AssetDatabase.GetAssetPath(prefabAsset);
        if (string.IsNullOrEmpty(path))
        {
            reason = "Invalid prefab path";
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        if (root == null)
        {
            reason = "Failed to load prefab contents";
            return false;
        }

        try
        {
            var bakers = root.GetComponentsInChildren<FootprintVisualBaker>(true);
            if (bakers == null || bakers.Length == 0)
            {
                EnsureBakerOnRoot(root);
                bakers = root.GetComponentsInChildren<FootprintVisualBaker>(true);
            }

            if (bakers == null || bakers.Length == 0)
            {
                reason = "FootprintVisualBaker not found";
                return false;
            }

            for (int i = 0; i < bakers.Length; i++)
            {
                if (!applyToAllBakers && i > 0) break;
                var baker = bakers[i];
                if (baker == null) continue;

                var so = new SerializedObject(baker);
                if (applyBaseTile)
                    so.FindProperty("baseTilePrefab").objectReferenceValue = baseTilePrefab;
                if (applyMask)
                    so.FindProperty("mask").objectReferenceValue = activeMask;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private bool ApplyTowerDefinition(GameObject prefabAsset, TowerDefinitionSO tower, out string reason)
    {
        reason = "";
        if (tower == null)
        {
            reason = "TowerDefinitionSO is null";
            return false;
        }

        TowerEntity towerEntity = GetTowerEntityFromPrefab(prefabAsset);
        if (towerEntity == null)
        {
            reason = "TowerEntity not found in prefab";
            return false;
        }

        FootprintMaskSO mask = activeMask ?? ResolveMaskFromPrefab(prefabAsset);

        var so = new SerializedObject(tower);
        so.FindProperty("prefab").objectReferenceValue = towerEntity;
        if (mask != null)
        {
            so.FindProperty("footprintMask").objectReferenceValue = mask;
            if (syncFootprintFromMask)
                so.FindProperty("footprint").vector2IntValue = mask.Size;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(tower);
        AssetDatabase.SaveAssets();
        return true;
    }

    private FootprintMaskSO ResolveMaskFromPrefab(GameObject prefabAsset)
    {
        if (prefabAsset == null) return null;
        string path = AssetDatabase.GetAssetPath(prefabAsset);
        if (string.IsNullOrEmpty(path)) return null;

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        if (root == null) return null;

        try
        {
            var bakers = root.GetComponentsInChildren<FootprintVisualBaker>(true);
            if (bakers == null || bakers.Length == 0) return null;
            var baker = bakers[0];
            if (baker == null) return null;
            var so = new SerializedObject(baker);
            return (FootprintMaskSO)so.FindProperty("mask").objectReferenceValue;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private TowerDefinitionSO FindTowerDefinitionByPrefab(GameObject prefabAsset)
    {
        if (prefabAsset == null) return null;
        TowerEntity towerEntity = GetTowerEntityFromPrefab(prefabAsset);
        if (towerEntity == null) return null;

        string[] search = string.IsNullOrEmpty(towerSoFolder) ? new[] { "Assets" } : new[] { towerSoFolder };
        string[] guids = AssetDatabase.FindAssets("t:TowerDefinitionSO", search);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var asset = AssetDatabase.LoadAssetAtPath<TowerDefinitionSO>(path);
            if (asset != null && asset.prefab == towerEntity)
                return asset;
        }

        return null;
    }

    private TowerEntity GetTowerEntityFromPrefab(GameObject prefabAsset)
    {
        if (prefabAsset == null) return null;
        return prefabAsset.GetComponentInChildren<TowerEntity>(true);
    }

    private TowerDefinitionSO CreateTowerDefinition(GameObject prefabAsset, string name, out string reason)
    {
        reason = "";
        towerSoFolder = NormalizeFolder(towerSoFolder);
        if (!EnsureAssetFolder(towerSoFolder, out reason)) return null;

        string baseName = string.IsNullOrEmpty(name) ? (prefabAsset != null ? prefabAsset.name : "Tower") : name;
        string assetPath = GetUniqueAssetPath(towerSoFolder, baseName);
        var asset = ScriptableObject.CreateInstance<TowerDefinitionSO>();

        if (prefabAsset != null)
        {
            TowerEntity towerEntity = GetTowerEntityFromPrefab(prefabAsset);
            if (towerEntity != null)
                asset.prefab = towerEntity;
        }

        FootprintMaskSO mask = activeMask ?? ResolveMaskFromPrefab(prefabAsset);
        if (mask != null)
        {
            asset.footprintMask = mask;
            asset.footprint = mask.Size;
        }

        asset.id = baseName;
        asset.displayName = baseName;

        AssetDatabase.CreateAsset(asset, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return asset;
    }

    private FootprintMaskSO CreateFootprintMask(string name, out string reason)
    {
        reason = "";
        maskSoFolder = NormalizeFolder(maskSoFolder);
        if (!EnsureAssetFolder(maskSoFolder, out reason)) return null;

        string baseName = string.IsNullOrEmpty(name) ? "FootprintMask" : name;
        string assetPath = GetUniqueAssetPath(maskSoFolder, baseName);
        var asset = ScriptableObject.CreateInstance<FootprintMaskSO>();
        AssetDatabase.CreateAsset(asset, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return asset;
    }

    private string GetUniqueAssetPath(string folder, string baseName)
    {
        string candidate = baseName;
        string path = $"{folder}/{candidate}.asset";
        while (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
        {
            candidate = $"{candidate}_Dummy";
            path = $"{folder}/{candidate}.asset";
        }
        return path;
    }

    private bool EnsureAssetFolder(string folder, out string reason)
    {
        reason = "";
        if (string.IsNullOrEmpty(folder))
        {
            reason = "Folder is empty";
            return false;
        }
        if (!folder.StartsWith("Assets"))
        {
            reason = "Folder must start with Assets";
            return false;
        }

        if (AssetDatabase.IsValidFolder(folder)) return true;

        string[] parts = folder.Split('/');
        if (parts.Length == 0) return false;

        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }

        return AssetDatabase.IsValidFolder(folder);
    }

    private static string NormalizeFolder(string folder)
    {
        return string.IsNullOrEmpty(folder) ? folder : folder.Replace('\\', '/');
    }

    private void TryAutoRegister(TowerDefinitionSO tower)
    {
        if (tower == null) return;
        bool didAny = false;
        if (autoRegisterCatalog)
        {
            if (activeCatalog == null)
            {
                AppendAuthoringReport("Catalog missing", MessageType.Warning);
            }
            else if (RegisterInCatalog(activeCatalog, tower))
            {
                didAny = true;
            }
        }

        if (autoRegisterGameRoot)
        {
            if (activeGameRoot == null)
                activeGameRoot = FindObjectOfType<GameRoot>();

            if (activeGameRoot == null)
            {
                AppendAuthoringReport("GameRoot missing", MessageType.Warning);
            }
            else if (RegisterInGameRoot(activeGameRoot, tower))
            {
                didAny = true;
            }
        }

        if (didAny)
            AppendAuthoringReport("Auto register complete", MessageType.Info);
    }

    private bool RegisterInCatalog(BuildOptionCatalogSO catalog, TowerDefinitionSO tower)
    {
        if (catalog == null || tower == null) return false;
        var so = new SerializedObject(catalog);
        var prop = so.FindProperty("options");
        if (prop == null || !prop.isArray) return false;

        for (int i = 0; i < prop.arraySize; i++)
        {
            var item = prop.GetArrayElementAtIndex(i);
            if (item.objectReferenceValue == tower)
                return false;
        }

        int idx = prop.arraySize;
        prop.InsertArrayElementAtIndex(idx);
        prop.GetArrayElementAtIndex(idx).objectReferenceValue = tower;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        return true;
    }

    private bool RegisterInGameRoot(GameRoot root, TowerDefinitionSO tower)
    {
        if (root == null || tower == null) return false;
        var so = new SerializedObject(root);
        var prop = so.FindProperty("towerCatalog");
        if (prop == null || !prop.isArray) return false;

        for (int i = 0; i < prop.arraySize; i++)
        {
            var item = prop.GetArrayElementAtIndex(i);
            if (item.objectReferenceValue == tower)
                return false;
        }

        int idx = prop.arraySize;
        prop.InsertArrayElementAtIndex(idx);
        prop.GetArrayElementAtIndex(idx).objectReferenceValue = tower;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return true;
    }

    private void DrawPrefabList()
    {
        EditorGUILayout.LabelField("Registered Prefabs", EditorStyles.boldLabel);

        Rect dropRect = GUILayoutUtility.GetRect(0f, 48f, GUILayout.ExpandWidth(true));
        GUI.Box(dropRect, "Drag tower prefabs or scene objects here");
        HandleDragAndDrop(dropRect);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Selected"))
                AddSelected();
            if (GUILayout.Button("Clear"))
                prefabs.Clear();
        }

        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(120f));
        for (int i = 0; i < prefabs.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            prefabs[i] = (GameObject)EditorGUILayout.ObjectField(prefabs[i], typeof(GameObject), false);
            if (GUILayout.Button("Remove", GUILayout.Width(70f)))
            {
                prefabs.RemoveAt(i);
                i--;
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    private void HandleDragAndDrop(Rect rect)
    {
        Event evt = Event.current;
        if (!rect.Contains(evt.mousePosition)) return;

        if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                foreach (Object obj in DragAndDrop.objectReferences)
                {
                    var go = obj as GameObject;
                    if (go != null) AddPrefab(go);
                }
            }

            evt.Use();
        }
    }

    private void AddSelected()
    {
        Object[] selection = Selection.objects;
        if (selection == null || selection.Length == 0) return;

        for (int i = 0; i < selection.Length; i++)
        {
            var go = selection[i] as GameObject;
            if (go != null) AddPrefab(go);
        }
    }

    private void AddPrefab(GameObject go)
    {
        if (go == null) return;
        if (!prefabs.Contains(go))
            prefabs.Add(go);
    }

    private void RunBatch(bool autoSetup, bool doAutoAssign, bool overwriteAssign)
    {
        failures.Clear();
        reportTitle = "Failed Prefabs";
        int total = prefabs.Count;
        int success = 0;

        for (int i = 0; i < prefabs.Count; i++)
        {
            GameObject target = prefabs[i];
            if (ProcessTarget(target, autoSetup, doAutoAssign, overwriteAssign, out string reason))
            {
                success++;
                continue;
            }

            string name = target != null ? target.name : "(Missing)";
            if (string.IsNullOrEmpty(reason)) reason = "Unknown failure";
            failures.Add($"{name}: {reason}");
        }

        if (total > 0)
            ShowNotification(new GUIContent($"Rebuild {success}/{total}"));

        AssetDatabase.SaveAssets();
        Repaint();
    }

    private bool ProcessTarget(GameObject target, bool autoSetup, bool doAutoAssign, bool overwriteAssign, out string reason)
    {
        reason = "";
        if (target == null)
        {
            reason = "Missing prefab";
            return false;
        }

        if (PrefabUtility.IsPartOfPrefabAsset(target))
            return ProcessPrefabAsset(target, autoSetup, doAutoAssign, overwriteAssign, false, out reason);

        if (autoSetup)
        {
            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(target);
            if (string.IsNullOrEmpty(path))
            {
                reason = "Scene object is not a prefab asset";
                return false;
            }
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
            {
                reason = "Prefab asset not found";
                return false;
            }
            return ProcessPrefabAsset(asset, autoSetup, doAutoAssign, overwriteAssign, false, out reason);
        }

        GameObject root = PrefabUtility.GetNearestPrefabInstanceRoot(target) ?? target;
        Undo.RegisterFullObjectHierarchyUndo(root, "Tower Baker Rebuild");
        bool ok = ProcessBakers(root, autoSetup, doAutoAssign, overwriteAssign, false, out reason);
        if (ok)
            EditorUtility.SetDirty(root);
        return ok;
    }

    private bool ProcessPrefabAsset(GameObject asset, bool autoSetup, bool doAutoAssign, bool overwriteAssign, bool ensureBaker, out string reason)
    {
        reason = "";
        string path = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrEmpty(path))
        {
            reason = "Invalid prefab path";
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        if (root == null)
        {
            reason = "Failed to load prefab contents";
            return false;
        }
        try
        {
            bool ok = ProcessBakers(root, autoSetup, doAutoAssign, overwriteAssign, ensureBaker, out reason);
            if (ok)
                PrefabUtility.SaveAsPrefabAsset(root, path);
            return ok;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private bool ProcessBakers(GameObject root, bool autoSetup, bool doAutoAssign, bool overwriteAssign, bool ensureBaker, out string reason)
    {
        reason = "";
        if (root == null)
        {
            reason = "Missing root";
            return false;
        }

        var bakers = root.GetComponentsInChildren<FootprintVisualBaker>(true);
        if (bakers == null || bakers.Length == 0)
        {
            if (ensureBaker)
            {
                EnsureBakerOnRoot(root);
                bakers = root.GetComponentsInChildren<FootprintVisualBaker>(true);
            }

            if (bakers == null || bakers.Length == 0)
            {
                reason = "FootprintVisualBaker not found";
                return false;
            }
        }

        bool ok = true;
        var sb = new StringBuilder();

        for (int i = 0; i < bakers.Length; i++)
        {
            var baker = bakers[i];
            if (baker == null) continue;

            if (doAutoAssign)
                baker.TryAutoAssignReferences(out _, overwriteAssign);

            string bakerReason;
            bool built = autoSetup
                ? baker.TryAutoSetupAndRebuild(out bakerReason)
                : baker.TryRebuild(out bakerReason);
            if (!built)
            {
                ok = false;
                if (sb.Length > 0) sb.Append(" | ");
                string name = baker.name;
                if (string.IsNullOrEmpty(bakerReason))
                    sb.Append($"{name}: Missing required inputs");
                else
                    sb.Append($"{name}: {bakerReason}");
            }
        }

        if (!ok)
            reason = sb.ToString();

        return ok;
    }

    private void RunAutoAssign()
    {
        failures.Clear();
        reportTitle = "Auto Assign Report";
        int total = prefabs.Count;
        int changed = 0;

        for (int i = 0; i < prefabs.Count; i++)
        {
            GameObject target = prefabs[i];
            if (ProcessAutoAssign(target, overwriteAutoAssign, out string reason))
            {
                if (!string.IsNullOrEmpty(reason) && !string.Equals(reason, "No changes"))
                    failures.Add($"{target.name}: {reason}");
                changed++;
                continue;
            }

            string name = target != null ? target.name : "(Missing)";
            if (string.IsNullOrEmpty(reason)) reason = "Auto assign failed";
            failures.Add($"{name}: {reason}");
        }

        if (total > 0)
            ShowNotification(new GUIContent($"Auto Assign {changed}/{total}"));

        AssetDatabase.SaveAssets();
        Repaint();
    }

    private bool ProcessAutoAssign(GameObject target, bool overwriteAssign, out string reason)
    {
        reason = "";
        if (target == null)
        {
            reason = "Missing prefab";
            return false;
        }

        if (PrefabUtility.IsPartOfPrefabAsset(target))
            return ProcessPrefabAutoAssign(target, overwriteAssign, false, out reason);

        string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(target);
        if (string.IsNullOrEmpty(path))
        {
            reason = "Scene object is not a prefab asset";
            return false;
        }

        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null)
        {
            reason = "Prefab asset not found";
            return false;
        }

        return ProcessPrefabAutoAssign(asset, overwriteAssign, false, out reason);
    }

    private bool ProcessPrefabAutoAssign(GameObject asset, bool overwriteAssign, bool ensureBaker, out string reason)
    {
        reason = "";
        string path = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrEmpty(path))
        {
            reason = "Invalid prefab path";
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        if (root == null)
        {
            reason = "Failed to load prefab contents";
            return false;
        }

        bool ok = true;
        var sb = new StringBuilder();
        try
        {
            var bakers = root.GetComponentsInChildren<FootprintVisualBaker>(true);
            if (bakers == null || bakers.Length == 0)
            {
                if (ensureBaker)
                {
                    EnsureBakerOnRoot(root);
                    bakers = root.GetComponentsInChildren<FootprintVisualBaker>(true);
                }
            }

            if (bakers == null || bakers.Length == 0)
            {
                ok = false;
                sb.Append("FootprintVisualBaker not found");
            }
            else
            {
                for (int i = 0; i < bakers.Length; i++)
                {
                    var baker = bakers[i];
                    if (baker == null) continue;
                    if (!baker.TryAutoAssignReferences(out string bakerReason, overwriteAssign))
                    {
                        ok = false;
                        if (sb.Length > 0) sb.Append(" | ");
                        sb.Append($"{baker.name}: {bakerReason}");
                    }
                    else if (!string.IsNullOrEmpty(bakerReason))
                    {
                        if (sb.Length > 0) sb.Append(" | ");
                        sb.Append($"{baker.name}: {bakerReason}");
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

    private void RunValidation()
    {
        failures.Clear();
        reportTitle = "Validation Report";
        int total = prefabs.Count;
        int issueCount = 0;

        for (int i = 0; i < prefabs.Count; i++)
        {
            GameObject target = prefabs[i];
            if (ProcessValidation(target, out string report))
            {
                if (!string.Equals(report, "OK"))
                {
                    failures.Add($"{target.name}: {report}");
                    issueCount++;
                }
                continue;
            }

            string name = target != null ? target.name : "(Missing)";
            if (string.IsNullOrEmpty(report)) report = "Validation failed";
            failures.Add($"{name}: {report}");
            issueCount++;
        }

        if (total > 0)
        {
            string msg = issueCount == 0 ? "Validation OK" : $"Validation Issues {issueCount}/{total}";
            ShowNotification(new GUIContent(msg));
        }

        Repaint();
    }

    private bool ProcessValidation(GameObject target, out string report)
    {
        report = "";
        if (target == null)
        {
            report = "Missing prefab";
            return false;
        }

        if (PrefabUtility.IsPartOfPrefabAsset(target))
            return ProcessPrefabValidation(target, out report);

        string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(target);
        if (string.IsNullOrEmpty(path))
        {
            report = "Scene object is not a prefab asset";
            return false;
        }

        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null)
        {
            report = "Prefab asset not found";
            return false;
        }

        return ProcessPrefabValidation(asset, out report);
    }

    private bool ProcessPrefabValidation(GameObject asset, out string report)
    {
        report = "";
        string path = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrEmpty(path))
        {
            report = "Invalid prefab path";
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        if (root == null)
        {
            report = "Failed to load prefab contents";
            return false;
        }

        bool ok = true;
        var sb = new StringBuilder();
        try
        {
            var bakers = root.GetComponentsInChildren<FootprintVisualBaker>(true);
            if (bakers == null || bakers.Length == 0)
            {
                ok = false;
                sb.Append("FootprintVisualBaker not found");
            }
            else
            {
                for (int i = 0; i < bakers.Length; i++)
                {
                    var baker = bakers[i];
                    if (baker == null) continue;
                    if (!baker.GetValidationReport(out string bakerReport))
                        ok = false;
                    if (!string.Equals(bakerReport, "OK"))
                    {
                        if (sb.Length > 0) sb.Append(" | ");
                        sb.Append($"{baker.name}: {bakerReport}");
                    }
                }
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        report = sb.Length > 0 ? sb.ToString() : "OK";
        return ok;
    }

    private void DrawAutoAssignOptions()
    {
        EditorGUILayout.LabelField("Auto Assign", EditorStyles.boldLabel);
        autoAssign = EditorGUILayout.ToggleLeft("Auto Assign (Names)", autoAssign);
        overwriteAutoAssign = EditorGUILayout.ToggleLeft("Overwrite Existing", overwriteAutoAssign);
    }

    private void DrawReportList()
    {
        if (failures.Count == 0) return;
        GUILayout.Space(6f);
        EditorGUILayout.LabelField(reportTitle, EditorStyles.boldLabel);
        for (int i = 0; i < failures.Count; i++)
            EditorGUILayout.LabelField(failures[i]);
    }

    private void OnDisable()
    {
        if (towerEditor != null)
            DestroyImmediate(towerEditor);
        if (maskEditor != null)
            DestroyImmediate(maskEditor);
    }
}
