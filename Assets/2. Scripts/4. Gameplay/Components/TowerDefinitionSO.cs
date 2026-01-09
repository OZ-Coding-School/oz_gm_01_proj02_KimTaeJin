using UnityEngine;

[CreateAssetMenu(menuName = "Game/Tower Definition", fileName = "Tower_")]
public sealed class TowerDefinitionSO : ScriptableObject
{
    [Header("Identity")]
    public string id = "tower_basic";
    public string displayName = "Basic Tower";

    [Header("Prefab (TowerEntity پ־ )")]
    public TowerEntity prefab;

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
