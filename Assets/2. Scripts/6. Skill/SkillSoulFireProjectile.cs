using System.Collections.Generic;
using UnityEngine;

public sealed class SkillSoulFireProjectile : MonoBehaviour
{
    [Header("피격")]
    [SerializeField] private float radius = 1.2f;
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField] private float tickInterval = 0.2f;
    [SerializeField] private float tickDamageMultiplier = 1f;

    [Header("VFX (선택)")]
    [SerializeField] private GameObject muzzlePrefab;
    [SerializeField] private float muzzleLifeTime = 1.0f;
    [SerializeField] private bool muzzleFollowProjectile = true;
    [SerializeField] private GameObject hitPrefab;
    [SerializeField] private float hitLifeTime = 0.8f;
    [SerializeField] private bool spawnHitPerEnemy = true;

    [Header("끌어당김")]
    [SerializeField] private float pullForce = 0.6f;
    [SerializeField] private bool pullUseFalloff = true;

    private RunScope _scope;
    private GameObject _prefabKey;
    private Vector3 _dir;
    private float _speed;
    private float _life;
    private int _damage;

    private float _tickTimer;
    private bool _alive;
    private static readonly Collider[] _overlaps = new Collider[32];
    private readonly HashSet<EnemyEntity> _hit = new();
    private GameObject _muzzleInstance;

    public void Launch(RunScope scope, GameObject prefabKey, Vector3 dir, int damage, float speed, float life)
    {
        _scope = scope;
        _prefabKey = prefabKey;
        _dir = dir.sqrMagnitude < 0.0001f ? transform.forward : dir.normalized;
        _damage = Mathf.Max(0, damage);
        _speed = Mathf.Max(0f, speed);
        _life = Mathf.Max(0.05f, life);
        _tickTimer = 0f;
        _alive = true;
        SpawnMuzzle();
    }

    private void Update()
    {
        if (!_alive) return;
        if (_scope == null || _scope.App == null || _scope.App.Pool == null)
        {
            Destroy(gameObject);
            return;
        }

        float dt = Time.deltaTime;
        transform.position += _dir * (_speed * dt);

        _tickTimer -= dt;
        if (_tickTimer <= 0f)
        {
            _tickTimer = Mathf.Max(0.01f, tickInterval);
            ApplyDot();
        }

        _life -= dt;
        if (_life <= 0f)
            Despawn();
    }

    private void ApplyDot()
    {
        if (_scope == null) return;
        if (radius <= 0f) return;

        int count = Physics.OverlapSphereNonAlloc(transform.position, radius, _overlaps, hitMask, QueryTriggerInteraction.Ignore);
        if (count <= 0) return;

        _hit.Clear();
        for (int i = 0; i < count; i++)
        {
            var e = _overlaps[i].GetComponentInParent<EnemyEntity>();
            if (e != null) _hit.Add(e);
        }

        int dmg = Mathf.Max(0, Mathf.RoundToInt(_damage * Mathf.Max(0f, tickDamageMultiplier)));
        bool spawned = false;
        foreach (var e in _hit)
        {
            if (dmg > 0)
            {
                _scope.Combat.DealDamage(e, dmg);
                if (hitPrefab != null)
                {
                    if (spawnHitPerEnemy)
                        SpawnOneShotVfx(hitPrefab, e.transform.position, Quaternion.identity, hitLifeTime);
                    else if (!spawned)
                    {
                        SpawnOneShotVfx(hitPrefab, transform.position, Quaternion.identity, hitLifeTime);
                        spawned = true;
                    }
                }
            }
            ApplyPull(e);
        }
    }

    private void ApplyPull(EnemyEntity enemy)
    {
        if (pullForce <= 0f) return;
        if (enemy == null) return;

        var rb = enemy.GetComponent<Rigidbody>();
        if (rb == null || rb.isKinematic) return;

        Vector3 delta = transform.position - enemy.transform.position;
        delta.y = 0f;
        float dist = delta.magnitude;
        if (dist <= 0.0001f) return;

        float strength = pullUseFalloff ? pullForce * (1f - Mathf.Clamp01(dist / Mathf.Max(0.01f, radius))) : pullForce;
        if (strength <= 0f) return;

        rb.AddForce(delta / dist * strength, ForceMode.Acceleration);
    }

    private void Despawn()
    {
        if (!_alive) return;
        _alive = false;

        ClearMuzzle();
        if (_scope != null && _scope.App != null && _scope.App.Pool != null && _prefabKey != null)
            _scope.App.Pool.Despawn(gameObject, _prefabKey);
        else
            Destroy(gameObject);
    }

    private void SpawnMuzzle()
    {
        ClearMuzzle();
        if (muzzlePrefab == null) return;
        if (muzzleFollowProjectile)
        {
            _muzzleInstance = Instantiate(muzzlePrefab, transform);
            _muzzleInstance.transform.localPosition = Vector3.zero;
            _muzzleInstance.transform.localRotation = Quaternion.identity;
            return;
        }
        SpawnOneShotVfx(muzzlePrefab, transform.position, Quaternion.LookRotation(_dir, Vector3.up), muzzleLifeTime);
    }

    private void ClearMuzzle()
    {
        if (_muzzleInstance == null) return;
        Destroy(_muzzleInstance);
        _muzzleInstance = null;
    }

    private static void SpawnOneShotVfx(GameObject prefab, Vector3 pos, Quaternion rot, float lifeOverride)
    {
        if (prefab == null) return;
        var go = Instantiate(prefab, pos, rot);
        float t = lifeOverride;
        if (t <= 0f)
        {
            var ps = go.GetComponentInChildren<ParticleSystem>();
            if (ps != null)
            {
                var main = ps.main;
                t = main.duration + main.startLifetime.constantMax;
            }
            else t = 1f;
        }
        Destroy(go, Mathf.Clamp(t, 0.2f, 5f));
    }
}
