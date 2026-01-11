using UnityEngine;

[CreateAssetMenu(menuName = "Game/Tower Definition", fileName = "Tower_")]
public sealed class TowerDefinitionSO : ScriptableObject
{
    [Header("Identity")]
    public string id = "tower_basic";
    public string displayName = "Basic Tower";

    [Header("UI")]
    public Sprite icon;
    public Sprite preview;
    [TextArea(2, 4)] public string description;

    [Header("Prefab")]
    public TowerEntity prefab;
    [Tooltip("Grid footprint in cells (width, height).")]
    public Vector2Int footprint = new Vector2Int(1, 1);

    [Header("Cost")]
    public int cost = 5;

    [Header("Combat")]
    public float range = 4f;
    public float fireInterval = 0.5f;
    public int damage = 1;
    public float knockback = 0f;

    [Header("Projectile (optional)")]
    public Projectile projectilePrefab;
    public float projectileSpeed = 18f;
    public float projectileLifeTime = 2f;
}
