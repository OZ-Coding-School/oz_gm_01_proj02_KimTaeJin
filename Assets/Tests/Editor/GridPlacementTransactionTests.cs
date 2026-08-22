using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class GridSystemTransactionTests
{
    private GameObject _gridObject;
    private GameObject _anchorObject;
    private GridSystem _grid;

    [SetUp]
    public void SetUp()
    {
        _gridObject = new GameObject("GridSystemTransactionTests_Grid");
        _anchorObject = new GameObject("GridSystemTransactionTests_Anchor");
        _grid = _gridObject.AddComponent<GridSystem>();
        _grid.Configure(1f, 1f, _anchorObject.transform, 5, 5, Vector3.zero, true);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_gridObject);
        Object.DestroyImmediate(_anchorObject);
    }

    [Test]
    public void TryOccupy_RejectsOutOfBoundsCell()
    {
        Assert.That(_grid.TryOccupy(new Vector2Int(-1, 0)), Is.False);
        Assert.That(_grid.TryOccupy(new Vector2Int(5, 0)), Is.False);
        Assert.That(_grid.OccupiedCount, Is.Zero);
    }

    [Test]
    public void TryOccupyAll_CommitsEveryCellTogether()
    {
        var cells = new[] { new Vector2Int(1, 1), new Vector2Int(1, 2), new Vector2Int(2, 2) };

        Assert.That(_grid.TryOccupyAll(cells), Is.True);
        Assert.That(_grid.OccupiedCount, Is.EqualTo(3));
        foreach (Vector2Int cell in cells)
            Assert.That(_grid.IsOccupied(cell), Is.True);
    }

    [Test]
    public void TryOccupyAll_WhenOneCellConflicts_LeavesNoPartialReservation()
    {
        var blocker = new Vector2Int(2, 2);
        _grid.TryOccupy(blocker);
        var cells = new[] { new Vector2Int(1, 2), blocker, new Vector2Int(3, 2) };

        Assert.That(_grid.TryOccupyAll(cells), Is.False);
        Assert.That(_grid.OccupiedCount, Is.EqualTo(1));
        Assert.That(_grid.IsOccupied(new Vector2Int(1, 2)), Is.False);
        Assert.That(_grid.IsOccupied(new Vector2Int(3, 2)), Is.False);
    }

    [Test]
    public void TryOccupyAll_RejectsDuplicateAndOutOfBoundsCellsWithoutMutation()
    {
        Assert.That(_grid.TryOccupyAll(new[] { Vector2Int.one, Vector2Int.one }), Is.False);
        Assert.That(_grid.TryOccupyAll(new[] { Vector2Int.one, new Vector2Int(5, 1) }), Is.False);
        Assert.That(_grid.OccupiedCount, Is.Zero);
    }

    [Test]
    public void TryReplaceAll_AllowsOverlapAndReleasesOldCells()
    {
        var current = new[] { new Vector2Int(2, 2), new Vector2Int(2, 3) };
        var next = new[] { new Vector2Int(2, 3), new Vector2Int(2, 4), new Vector2Int(3, 4) };
        Assert.That(_grid.TryOccupyAll(current), Is.True);

        Assert.That(_grid.TryReplaceAll(current, next), Is.True);
        Assert.That(_grid.IsOccupied(new Vector2Int(2, 2)), Is.False);
        Assert.That(_grid.OccupiedCount, Is.EqualTo(3));
        foreach (Vector2Int cell in next)
            Assert.That(_grid.IsOccupied(cell), Is.True);
    }

    [Test]
    public void TryReplaceAll_WhenExpansionConflicts_PreservesOriginalReservation()
    {
        var current = new[] { new Vector2Int(2, 2), new Vector2Int(2, 3) };
        var blocker = new Vector2Int(2, 4);
        _grid.TryOccupyAll(current);
        _grid.TryOccupy(blocker);

        Assert.That(_grid.TryReplaceAll(current, new[] { new Vector2Int(2, 3), blocker }), Is.False);
        Assert.That(_grid.IsOccupied(current[0]), Is.True);
        Assert.That(_grid.IsOccupied(current[1]), Is.True);
        Assert.That(_grid.IsOccupied(blocker), Is.True);
        Assert.That(_grid.OccupiedCount, Is.EqualTo(3));
    }
}

public sealed class GridDataServiceTransactionTests
{
    private readonly List<Object> _createdObjects = new();
    private GameObject _root;
    private GameObject _anchor;
    private GridSystem _grid;
    private GridDataService _data;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("GridDataServiceTransactionTests_Root");
        _anchor = new GameObject("GridDataServiceTransactionTests_Anchor");
        _grid = _root.AddComponent<GridSystem>();
        _grid.Configure(1f, 1f, _anchor.transform, 5, 5, Vector3.zero, true);
        _data = _root.AddComponent<GridDataService>();
        SetPrivateField(_data, "gridSystem", _grid);
        SetPrivateField(_data, "autoSyncWorldGrid", false);

        Assert.That(_grid.TryOccupy(new Vector2Int(2, 2)), Is.True, "The center building seeds buildable rows and columns.");
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = _createdObjects.Count - 1; i >= 0; i--)
            if (_createdObjects[i] != null) Object.DestroyImmediate(_createdObjects[i]);
        _createdObjects.Clear();
        Object.DestroyImmediate(_root);
        Object.DestroyImmediate(_anchor);
    }

    [Test]
    public void TryApplyPlacement_ReservesMultiCellFootprintWithoutVisualizer()
    {
        TowerDefinitionSO tower = CreateDefinition("vertical", new Vector2Int(1, 2));
        ConfigureCatalog(tower);
        var anchor = new Vector3Int(2, 0, 3);

        Assert.That(_data.TryApplyPlacement(tower, anchor, out GridDataService.PlacementResult result), Is.True);
        Assert.That(result.canPlace, Is.True);
        Assert.That(_grid.IsOccupied(new Vector2Int(2, 3)), Is.True);
        Assert.That(_grid.IsOccupied(new Vector2Int(2, 4)), Is.True);
        Assert.That(_grid.OccupiedCount, Is.EqualTo(3));
    }

    [Test]
    public void TryRemove_ReleasesExactlyTheOwnedFootprint()
    {
        TowerDefinitionSO tower = CreateDefinition("vertical", new Vector2Int(1, 2));
        ConfigureCatalog(tower);
        var anchor = new Vector3Int(2, 0, 3);
        _data.TryApplyPlacement(tower, anchor, out _);

        Assert.That(_data.TryRemove(anchor), Is.True);
        Assert.That(_grid.IsOccupied(new Vector2Int(2, 3)), Is.False);
        Assert.That(_grid.IsOccupied(new Vector2Int(2, 4)), Is.False);
        Assert.That(_grid.IsOccupied(new Vector2Int(2, 2)), Is.True, "The center building reservation must remain.");
        Assert.That(_grid.OccupiedCount, Is.EqualTo(1));
    }

    [Test]
    public void Upgrade_GrowsFootprintAsOneTransaction()
    {
        TowerDefinitionSO level2 = CreateDefinition("tower_l2", new Vector2Int(1, 2));
        TowerDefinitionSO level1 = CreateDefinition("tower_l1", Vector2Int.one);
        level1.upgradeNext = level2;
        ConfigureCatalog(level1);
        var anchor = new Vector3Int(2, 0, 3);
        _data.TryApplyPlacement(level1, anchor, out _);

        Assert.That(_data.TryApplyPlacement(level1, anchor, out GridDataService.PlacementResult result), Is.True);
        Assert.That(result.isUpgrade, Is.True);
        Assert.That(_grid.IsOccupied(new Vector2Int(2, 3)), Is.True);
        Assert.That(_grid.IsOccupied(new Vector2Int(2, 4)), Is.True);
        Assert.That(_data.TryGet(anchor, out GridDataService.TowerData stored), Is.True);
        Assert.That(stored.towerId, Is.EqualTo("tower_l2"));
        Assert.That(stored.level, Is.EqualTo(2));
    }

    [Test]
    public void Upgrade_WhenExpansionConflicts_PreservesLevelAndFootprint()
    {
        TowerDefinitionSO level2 = CreateDefinition("tower_l2", new Vector2Int(1, 2));
        TowerDefinitionSO level1 = CreateDefinition("tower_l1", Vector2Int.one);
        TowerDefinitionSO blocker = CreateDefinition("blocker", Vector2Int.one);
        level1.upgradeNext = level2;
        ConfigureCatalog(level1, blocker);
        var upgradeAnchor = new Vector3Int(2, 0, 3);
        var blockerAnchor = new Vector3Int(2, 0, 4);
        _data.TryApplyPlacement(level1, upgradeAnchor, out _);
        _data.TryApplyPlacement(blocker, blockerAnchor, out _);

        Assert.That(_data.TryApplyPlacement(level1, upgradeAnchor, out GridDataService.PlacementResult result), Is.False);
        Assert.That(result.isUpgrade, Is.True);
        Assert.That(_data.TryGet(upgradeAnchor, out GridDataService.TowerData stored), Is.True);
        Assert.That(stored.towerId, Is.EqualTo("tower_l1"));
        Assert.That(stored.level, Is.EqualTo(1));
        Assert.That(_grid.IsOccupied(new Vector2Int(2, 3)), Is.True);
        Assert.That(_grid.IsOccupied(new Vector2Int(2, 4)), Is.True);
        Assert.That(_grid.OccupiedCount, Is.EqualTo(3));
    }

    [Test]
    public void ClearAll_ReleasesTowersButPreservesExternalCenterReservation()
    {
        TowerDefinitionSO tower = CreateDefinition("tower", Vector2Int.one);
        ConfigureCatalog(tower);
        _data.TryApplyPlacement(tower, new Vector3Int(2, 0, 3), out _);

        _data.ClearAll();

        Assert.That(_data.Data, Is.Empty);
        Assert.That(_grid.IsOccupied(new Vector2Int(2, 3)), Is.False);
        Assert.That(_grid.IsOccupied(new Vector2Int(2, 2)), Is.True);
        Assert.That(_grid.OccupiedCount, Is.EqualTo(1));
    }

    private TowerDefinitionSO CreateDefinition(string id, Vector2Int footprint)
    {
        var definition = ScriptableObject.CreateInstance<TowerDefinitionSO>();
        definition.id = id;
        definition.footprint = footprint;
        var prefab = new GameObject(id + "_Prefab");
        prefab.SetActive(false);
        definition.prefab = prefab.AddComponent<TowerEntity>();
        _createdObjects.Add(definition);
        _createdObjects.Add(prefab);
        return definition;
    }

    private void ConfigureCatalog(params TowerDefinitionSO[] definitions)
    {
        SetPrivateField(_data, "towerCatalog", definitions);
    }

    private static void SetPrivateField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing private field: {name}");
        field.SetValue(target, value);
    }
}

public sealed class MainSceneIntegrityTests
{
    [Test]
    public void MainScene_LoadsWithoutMissingComponents()
    {
        const string scenePath = "Assets/0. Scenes/Main.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        int missing = 0;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                Component[] components = child.GetComponents<Component>();
                for (int i = 0; i < components.Length; i++)
                    if (components[i] == null) missing++;
            }
        }

        Assert.That(scene.isLoaded, Is.True);
        Assert.That(missing, Is.Zero, "The portfolio build scene contains missing MonoBehaviour references.");
    }
}
