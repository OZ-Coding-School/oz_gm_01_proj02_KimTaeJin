using UnityEngine;

[System.Serializable]
public struct ProjectileHitEffectConfig
{
    public enum EffectType
    {
        None = 0,
        Slow = 1,
        AoeSlow = 2,
        AoeDamage = 3
    }

    public EffectType effectType;
    public bool overridePrefabEffects;

    [Tooltip("0.3 = 30% 감속 (속도 70%)")]
    [Range(0.05f, 1f)] public float slowMultiplier;
    public float slowDuration;

    public float aoeRadius;
    [Tooltip("기본 피해 * 배율 (예: 0.5 = 절반 피해)")]
    public float aoeDamageMultiplier;
    public int aoeDamageOverride;
    public bool aoeIncludeDirectTarget;
}
