using System.Collections.Generic;
using UnityEngine;

public sealed class ProjectileAoeDamage : MonoBehaviour, IProjectileHitEffect
{
    [SerializeField] private float radius = 2f;
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField] private float damageMultiplier = 1f;
    [SerializeField] private int damageOverride = 0;
    [SerializeField] private bool includeDirectTarget = true;

    public void OnProjectileHit(RunScope scope, EnemyEntity enemy, Vector3 hitPoint, Vector3 dir, int baseDamage)
    {
        if (scope == null) return;
        if (radius <= 0f) return;

        int dmg = damageOverride > 0
            ? damageOverride
            : Mathf.Max(0, Mathf.RoundToInt(baseDamage * Mathf.Max(0f, damageMultiplier)));
        if (dmg <= 0) return;

        var cols = Physics.OverlapSphere(hitPoint, radius, hitMask, QueryTriggerInteraction.Ignore);
        if (cols == null || cols.Length == 0) return;

        var set = new HashSet<EnemyEntity>();
        for (int i = 0; i < cols.Length; i++)
        {
            var e = cols[i].GetComponentInParent<EnemyEntity>();
            if (e == null) continue;
            if (!includeDirectTarget && e == enemy) continue;
            set.Add(e);
        }

        foreach (var e in set)
            scope.Combat.DealDamage(e, dmg);
    }
}
