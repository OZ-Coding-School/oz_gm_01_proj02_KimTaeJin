using UnityEngine;
using UnityEngine.Serialization;

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

    [Header("등장 조건")]
    [Min(1)] public int unlockBaseLevel = 1;

    [Header("Prefab")]
    public TowerEntity prefab;
    [Tooltip("Grid footprint in cells (width, height).")]
    public Vector2Int footprint = new Vector2Int(1, 1);
    [Tooltip("Optional footprint mask for non-rect shapes.")]
    public FootprintMaskSO footprintMask;

    [Header("Cost")]
    public int cost = 5;

    [Header("Upgrade")]
    public TowerDefinitionSO upgradeNext;

    [Header("전투")]
    public float range = 4f;
    [Min(0f)]
    [Tooltip("초당 발사 횟수 (예: 2 = 초당 2발)")]
    [FormerlySerializedAs("fireInterval")]
    public float attackSpeed = 2f;
    public int damage = 1;
    public float knockback = 0f;

    [Header("사운드")]
    public AudioClip fireSfx;
    [Range(0f, 1f)] public float fireSfxVolume = 1f;

    [Header("패시브 보너스")]
    [Tooltip("0.2 = 경험치 획득량 20% 증가")]
    [Min(0f)] public float passiveExpGainBonus = 0f;
    [Tooltip("0.2 = 포탑 공격력 20% 증가")]
    [Min(0f)] public float passiveTowerDamageBonus = 0f;
    [Tooltip("0.2 = 포탑 공격속도 20% 증가")]
    [Min(0f)] public float passiveTowerAttackSpeedBonus = 0f;

    [Header("Projectile (optional)")]
    public Projectile projectilePrefab;
    public float projectileSpeed = 18f;
    public float projectileLifeTime = 2f;

    [Header("피격 효과")]
    public ProjectileHitEffectConfig hitEffect;
}
