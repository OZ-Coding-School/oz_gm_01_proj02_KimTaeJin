using UnityEngine;

public interface IProjectileHitEffect
{
    void OnProjectileHit(RunScope scope, EnemyEntity enemy, Vector3 hitPoint, Vector3 dir, int baseDamage);
}
