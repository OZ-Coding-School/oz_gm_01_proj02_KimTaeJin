using UnityEngine;

public sealed class PlayerMeleeAutoAttack : MonoBehaviour
{
    [Header("Tuning")]
    [SerializeField] private float attackRange = 4.0f;   
    [SerializeField] private float hitRadius = 2.0f;        
    [SerializeField] private float attacksPerSecond = 1.2f; 
    [SerializeField] private int damage = 10;
    [SerializeField] private float knockbackForce = 6f;

    [Header("VFX")]
    [SerializeField] private Transform attackSocket;
    [SerializeField] private GameObject hitVfxPrefab;
    [SerializeField] private float vfxLifeTime = 1.0f;
    [SerializeField] private Vector3 vfxOffset = new(0f, 0.8f, 0f);

    private RunScope _scope;
    private float _cooldown;

    public void Construct(RunScope scope) => _scope = scope;

    private void Update()
    {
        if (_scope == null) return;

        _cooldown -= Time.deltaTime;
        if (_cooldown > 0f) return;
        if (!HasEnemyWithin(attackRange))
            return;
        DoMeleeHit();
        _cooldown = (attacksPerSecond <= 0f) ? 0.2f : (1f / attacksPerSecond);
    }

    private bool HasEnemyWithin(float range)
    {
        var enemies = _scope.Entities?.Enemies;
        if (enemies == null || enemies.Count == 0) return false;

        Vector3 p = transform.position;
        float r2 = range * range;

        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            var e = enemies[i];
            if (e == null) continue;

            Vector3 d = e.transform.position - p;
            d.y = 0f;

            if (d.sqrMagnitude <= r2) return true;
        }

        return false;
    }

    private void DoMeleeHit()
    {
        Vector3 p = transform.position;

        // VFX
        SpawnVfx(hitVfxPrefab);


        // 범위 피해 + 넉백
        var enemies = _scope.Entities?.Enemies;
        if (enemies == null) return;

        float r2 = hitRadius * hitRadius;

        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            var e = enemies[i];
            if (e == null) continue;

            Vector3 d = e.transform.position - p;
            d.y = 0f;

            if (d.sqrMagnitude > r2) continue;

            _scope.Combat.DealDamage(e, damage);
            _scope.Combat.Knockback(e, p, knockbackForce);
        }
    }
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, hitRadius);

        // 전방 방향 표시(선택)
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * attackRange);
    }
#endif
    private void SpawnVfx(GameObject prefab)
    {
        if (!prefab || !attackSocket) return;

        var vfx = Instantiate(prefab, attackSocket.position, attackSocket.rotation);
        Destroy(vfx, vfxLifeTime);
    }
}
