using UnityEngine;

[DisallowMultipleComponent]
public sealed class TowerHitEffectGizmo : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private TowerDefinitionSO towerDefinition;
    [SerializeField] private TowerEntity towerEntity;
    [SerializeField] private Transform centerOverride;

    [Header("표시")]
    [SerializeField] private bool drawAlways;
    [SerializeField] private float yOffset = 0.05f;
    [SerializeField] private int segments = 48;
    [SerializeField] private Color aoeColor = new Color(0.2f, 0.85f, 1f, 0.9f);

    private void OnDrawGizmos()
    {
        if (!drawAlways) return;
        DrawGizmosInternal();
    }

    private void OnDrawGizmosSelected()
    {
        if (drawAlways) return;
        DrawGizmosInternal();
    }

    private void DrawGizmosInternal()
    {
        TowerDefinitionSO def = ResolveDefinition();
        if (def == null) return;

        if (!TryGetAoeRadius(def, out float radius)) return;

        Transform center = centerOverride != null ? centerOverride : transform;
        Vector3 pos = center.position;
        pos.y += yOffset;

        Gizmos.color = aoeColor;
        DrawDisc(pos, radius, segments);
    }

    private TowerDefinitionSO ResolveDefinition()
    {
        if (towerDefinition != null) return towerDefinition;
        if (towerEntity == null)
            towerEntity = GetComponentInParent<TowerEntity>();
        return towerEntity != null ? towerEntity.Definition : null;
    }

    private static bool TryGetAoeRadius(TowerDefinitionSO def, out float radius)
    {
        radius = 0f;
        if (def == null) return false;

        var effect = def.hitEffect;
        bool isAoe = effect.effectType == ProjectileHitEffectConfig.EffectType.AoeSlow
            || effect.effectType == ProjectileHitEffectConfig.EffectType.AoeDamage;
        if (!isAoe) return false;

        radius = effect.aoeRadius;
        return radius > 0.0001f;
    }

    private static void DrawDisc(Vector3 center, float radius, int segments)
    {
        int seg = Mathf.Clamp(segments, 8, 128);
        float step = Mathf.PI * 2f / seg;
        Vector3 prev = center + new Vector3(Mathf.Cos(0f) * radius, 0f, Mathf.Sin(0f) * radius);
        for (int i = 1; i <= seg; i++)
        {
            float a = step * i;
            Vector3 next = center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}
