using System.Collections.Generic;
using UnityEngine;

public sealed class SkillPiercingProjectile : MonoBehaviour
{
    [Header("피격")]
    [SerializeField] private float radius = 0.25f;
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField] private int maxHits = -1;

    [Header("넉백")]
    [SerializeField] private float knockbackForce = 0f;

    [Header("VFX (선택)")]
    [SerializeField] private GameObject muzzlePrefab;
    [SerializeField] private float muzzleLifeTime = 1.0f;
    [SerializeField] private bool muzzleFollowProjectile = true;
    [SerializeField] private GameObject hitPrefab;
    [SerializeField] private float hitLifeTime = 0.8f;

    private RunScope _scope;
    private GameObject _prefabKey;
    private Vector3 _dir;
    private float _speed;
    private float _life;
    private int _damage;

    private Vector3 _lastPos;
    private bool _alive;
    private int _hitCount;
    private readonly HashSet<EnemyEntity> _hit = new();
    private static readonly RaycastHit[] _hits = new RaycastHit[16];
    private GameObject _muzzleInstance;

    public void Launch(RunScope scope, GameObject prefabKey, Vector3 dir, int damage, float speed, float life)
    {
        _scope = scope;
        _prefabKey = prefabKey;
        _dir = dir.sqrMagnitude < 0.0001f ? transform.forward : dir.normalized;
        _damage = Mathf.Max(0, damage);
        _speed = Mathf.Max(0f, speed);
        _life = Mathf.Max(0.05f, life);
        _alive = true;
        _hitCount = 0;
        _hit.Clear();
        _lastPos = transform.position;
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

        Vector3 now = transform.position;
        Vector3 delta = now - _lastPos;
        float dist = delta.magnitude;

        if (dist > 0.00001f)
        {
            int hitCount = Physics.SphereCastNonAlloc(_lastPos, radius, delta / dist, _hits, dist, hitMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                var col = _hits[i].collider;
                if (col == null) continue;
                var enemy = col.GetComponentInParent<EnemyEntity>();
                if (enemy == null || _hit.Contains(enemy)) continue;

                _hit.Add(enemy);
                _scope.Combat.DealDamage(enemy, _damage);
                if (knockbackForce > 0f)
                    _scope.Combat.Knockback(enemy, _hits[i].point, knockbackForce);
                _hitCount++;
                if (hitPrefab != null)
                    SpawnOneShotVfx(hitPrefab, _hits[i].point, Quaternion.LookRotation(_dir, Vector3.up), hitLifeTime);

                if (maxHits > 0 && _hitCount >= maxHits)
                {
                    Despawn();
                    return;
                }
            }
        }

        _lastPos = now;
        _life -= dt;
        if (_life <= 0f)
            Despawn();
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
