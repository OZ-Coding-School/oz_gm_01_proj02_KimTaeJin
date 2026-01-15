using System.Collections.Generic;
using UnityEngine;

public sealed class PlayerHarvestAutoAttack : MonoBehaviour
{
    [Header("Detect")]
    [SerializeField] private float harvestRange = 3.5f;
    [SerializeField] private float hitRadius = 1.6f;
    [SerializeField] private LayerMask harvestMask;

    [Header("Attack")]
    [SerializeField] private float attacksPerSecond = 1.2f;
    [SerializeField] private int harvestDamage = 1;
    [SerializeField] private float rotateToTargetSpeed = 18f;

    [Header("Anim")]
    [SerializeField] private Animator animator; 
    private static readonly int AttackHash = Animator.StringToHash("Attack");

    [Header("Harvest VFX (선택)")]
    [SerializeField] private Transform vfxSocket;
    [SerializeField] private GameObject harvestVfxPrefab;
    [SerializeField] private float vfxLifeTime = 1.0f;

    private RunScope _scope;
    private float _cooldown;
    private readonly List<Harvestable> _harvestHits = new();
    private readonly HashSet<Harvestable> _harvestHitSet = new();

    public void Construct(RunScope scope)
    {
        _scope = scope;
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        if (_scope == null) return;

        _cooldown -= Time.deltaTime;
        if (_cooldown > 0f) return;

        var target = CollectHarvestablesInRadius();
        if (target == null) return;
        FaceTarget(target.transform.position);
        if (animator != null) animator.SetTrigger(AttackHash);
        SpawnHarvestVfx();
        Vector3 p = transform.position;
        for (int i = 0; i < _harvestHits.Count; i++)
        {
            var h = _harvestHits[i];
            if (h == null) continue;
            h.TakeHit(harvestDamage, p);
        }

        _cooldown = (attacksPerSecond <= 0f) ? 0.2f : (1f / attacksPerSecond);
    }

    private Harvestable CollectHarvestablesInRadius()
    {
        _harvestHits.Clear();
        _harvestHitSet.Clear();

        Vector3 p = transform.position;
        var cols = Physics.OverlapSphere(p, harvestRange, harvestMask, QueryTriggerInteraction.Ignore);
        if (cols == null || cols.Length == 0) return null;

        Harvestable best = null;
        float bestD2 = float.MaxValue;
        float r2 = hitRadius * hitRadius;

        for (int i = 0; i < cols.Length; i++)
        {
            var h = cols[i].GetComponentInParent<Harvestable>();
            if (h == null) continue;

            Vector3 d = h.transform.position - p;
            d.y = 0f;
            float d2 = d.sqrMagnitude;
            if (d2 > r2) continue;

            if (_harvestHitSet.Add(h))
                _harvestHits.Add(h);

            if (d2 < bestD2)
            {
                bestD2 = d2;
                best = h;
            }
        }

        return best;
    }

    private void FaceTarget(Vector3 targetPos)
    {
        Vector3 dir = targetPos - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotateToTargetSpeed * Time.deltaTime);
    }

    private void SpawnHarvestVfx()
    {
        if (!harvestVfxPrefab || !vfxSocket) return;

        // 지금은 Instantiate, 나중에 PoolService로 바꾸면 됨
        var vfx = Instantiate(harvestVfxPrefab, vfxSocket.position, vfxSocket.rotation);
        Destroy(vfx, vfxLifeTime);
    }
}
