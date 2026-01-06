public sealed class CombatSystem
{
    public void DealDamage(EnemyEntity target, int amount)
    {
        if (target == null) return;

        var hp = target.GetComponent<HealthComponent>();
        if (hp == null) return;

        hp.ApplyDamage(amount);
    }
}
