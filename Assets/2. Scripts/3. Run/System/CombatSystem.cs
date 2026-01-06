using UnityEngine;

public sealed class CombatSystem
{
    public void DealDamage(EnemyEntity target, int amount)
    {
        if (target == null) return;
        var hp = target.GetComponent<HealthComponent>();
        if (hp == null) return;
        hp.ApplyDamage(amount);
    }

    public void Knockback(EnemyEntity target, Vector3 from, float force)
    {
        if (target == null) return;

        var brain = target.GetComponent<EnemyBrain>();
        if (brain == null) return;

        Vector3 dir = (target.transform.position - from);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = target.transform.forward;
        dir.Normalize();

        brain.Knockback(dir, force, 0.12f);
    }


}
