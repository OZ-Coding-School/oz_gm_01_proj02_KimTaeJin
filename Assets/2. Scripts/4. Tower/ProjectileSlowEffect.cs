using UnityEngine;

public sealed class ProjectileSlowEffect : MonoBehaviour, IProjectileHitEffect
{
    [SerializeField, Range(0.05f, 1f)] private float slowMultiplier = 0.6f;
    [SerializeField] private float duration = 1.2f;

    public void OnProjectileHit(RunScope scope, EnemyEntity enemy, Vector3 hitPoint, Vector3 dir, int baseDamage)
    {
        if (enemy == null) return;
        if (duration <= 0f) return;

        var slow = enemy.GetComponent<EnemySlowEffect>();
        if (slow == null) slow = enemy.gameObject.AddComponent<EnemySlowEffect>();
        slow.ApplySlow(slowMultiplier, duration);
    }
}
