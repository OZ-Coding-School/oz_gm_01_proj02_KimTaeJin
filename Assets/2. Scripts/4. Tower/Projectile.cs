using System.Collections.Generic;
using UnityEngine;

public sealed class Projectile : MonoBehaviour
{
    [Header("Hit")]
    [SerializeField] private float radius = 0.15f;
    [SerializeField] private LayerMask hitMask = ~0; 

    [Header("VFX Root (optional)")]
    [Tooltip("Dummy Bullet 같은 이펙트 루트를 지정. 비우면 자기 자신(transform).")]
    [SerializeField] private Transform vfxRoot;

    [Tooltip("비주얼 축이 앞(Z+)이 아니라면 보정 회전(예: (0,90,0) 등)")]
    [SerializeField] private Vector3 vfxEulerOffset;

    [Header("Muzzle/Hit VFX (optional)")]
    [SerializeField] private GameObject muzzlePrefab;
    [SerializeField] private GameObject hitPrefab;

    [Header("Despawn Tail")]
    [Tooltip("명시값. 0이면 트레일/파티클에서 자동으로 tailTime을 계산해 사용")]
    [SerializeField] private float tailTimeOverride = 0f;

    private RunScope _scope;
    private GameObject _prefabKey;


    private Vector3 _dir;
    private float _speed;
    private float _life;
    private int _damage;
    private float _knockback;

    private Vector3 _lastPos;
    private bool _alive;

    private bool _dying;
    private float _dyingTimer;
    private float _tailTime;

    private ParticleSystem[] _ps;
    private TrailRenderer[] _trs;
    private Renderer[] _renderers;
    private IProjectileHitEffect[] _hitEffects;
    private ProjectileHitEffectConfig _hitEffectConfig;
    private bool _overrideHitEffects;
    private static readonly Collider[] _aoeOverlaps = new Collider[32];
    private static readonly HashSet<EnemyEntity> _aoeTargets = new();
    private static readonly HashSet<Harvestable> _aoeHarvestTargets = new();

    private void Awake()
    {
        if (vfxRoot == null) vfxRoot = transform;

        _ps = vfxRoot.GetComponentsInChildren<ParticleSystem>(true);
        _trs = vfxRoot.GetComponentsInChildren<TrailRenderer>(true);
        _renderers = vfxRoot.GetComponentsInChildren<Renderer>(true);
        _hitEffects = GetComponents<IProjectileHitEffect>();
    }

    public void Launch(
        RunScope scope,
        GameObject prefabKey,
        Vector3 dir,
        int dmg,
        float speed,
        float life,
        float knockback,
        ProjectileHitEffectConfig hitEffect)
    {
        _scope = scope;
        _prefabKey = prefabKey;

        _dir = dir.sqrMagnitude < 0.0001f ? transform.forward : dir.normalized;
        _damage = dmg;
        _speed = speed;
        _life = life;
        _knockback = knockback;
        _hitEffectConfig = hitEffect;
        _overrideHitEffects = hitEffect.overridePrefabEffects;

        _alive = true;
        _dying = false;
        _dyingTimer = 0f;

        _lastPos = transform.position;

        // 비주얼 회전(원하면)
        if (vfxRoot != null)
        {
            var rot = Quaternion.LookRotation(_dir, Vector3.up) * Quaternion.Euler(vfxEulerOffset);
            vfxRoot.rotation = rot;
        }

        ResetVfxOnSpawn();
        if (muzzlePrefab != null)
        {
            SpawnOneShotVfx(muzzlePrefab, transform.position, Quaternion.LookRotation(_dir, Vector3.up));
        }

        _tailTime = (tailTimeOverride > 0f) ? tailTimeOverride : ComputeTailTimeFromVfx();
        if (_tailTime < 0.05f) _tailTime = 0.05f;
    }

    private void Update()
    {
        if (_scope == null || _scope.App == null || _scope.App.Pool == null)
        {
            Destroy(gameObject);
            return;
        }

        if (_dying)
        {
            _dyingTimer += Time.deltaTime;
            if (_dyingTimer >= _tailTime)
                _scope.App.Pool.Despawn(gameObject, _prefabKey);
            return;
        }

        if (!_alive) return;

        float dt = Time.deltaTime;

        transform.position += _dir * (_speed * dt);

        // hit test
        Vector3 now = transform.position;
        Vector3 delta = now - _lastPos;
        float dist = delta.magnitude;

        if (dist > 0.00001f)
        {
            if (Physics.SphereCast(_lastPos, radius, delta / dist, out RaycastHit hit, dist, hitMask, QueryTriggerInteraction.Ignore))
            {
                var enemy = hit.collider.GetComponentInParent<EnemyEntity>();
                if (enemy != null)
                {
                    _scope.Combat.DealDamage(enemy, _damage);

                    if (_knockback > 0f)
                        _scope.Combat.Knockback(enemy, hit.point - _dir, _knockback);

                    ApplyHitEffects(enemy, hit.point, _damage);
                }

                var harvestable = hit.collider.GetComponentInParent<Harvestable>();
                if (harvestable != null)
                {
                    harvestable.TakeHit(_damage, hit.point);
                }

                // hit vfx
                if (hitPrefab != null)
                {
                    var rot = Quaternion.LookRotation(_dir, Vector3.up);
                    SpawnOneShotVfx(hitPrefab, hit.point, rot);
                }

                BeginDying();
                return;
            }
        }

        _lastPos = now;

        _life -= dt;
        if (_life <= 0f)
        {
            BeginDying(); 
        }
    }

    private void BeginDying()
    {
        if (_dying) return;

        _alive = false;
        _dying = true;
        _dyingTimer = 0f;

        if (_renderers != null)
        {
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].enabled = false;
        }

        if (_trs != null)
        {
            for (int i = 0; i < _trs.Length; i++)
                if (_trs[i] != null) _trs[i].emitting = false;
        }

        if (_ps != null)
        {
            for (int i = 0; i < _ps.Length; i++)
            {
                var p = _ps[i];
                if (p == null) continue;
                p.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }

    private void ResetVfxOnSpawn()
    {
        if (_renderers != null)
        {
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].enabled = true;
        }

        if (_trs != null)
        {
            for (int i = 0; i < _trs.Length; i++)
            {
                var t = _trs[i];
                if (t == null) continue;
                t.Clear();
                t.emitting = true;
            }
        }

        if (_ps != null)
        {
            for (int i = 0; i < _ps.Length; i++)
            {
                var p = _ps[i];
                if (p == null) continue;
                p.Clear(true);
                p.Play(true);
            }
        }
    }

    private float ComputeTailTimeFromVfx()
    {
        float tail = 0.15f;

        if (_trs != null)
        {
            for (int i = 0; i < _trs.Length; i++)
                if (_trs[i] != null)
                    tail = Mathf.Max(tail, _trs[i].time);
        }

        if (_ps != null)
        {
            for (int i = 0; i < _ps.Length; i++)
            {
                var p = _ps[i];
                if (p == null) continue;

                var main = p.main;
                float life = main.startLifetime.constantMax;
                tail = Mathf.Max(tail, life);
            }
        }

        return tail + 0.05f;
    }

    private void SpawnOneShotVfx(GameObject prefab, Vector3 pos, Quaternion rot)
    {
        var go = Instantiate(prefab, pos, rot);

        float t = 1.0f;
        var ps = go.GetComponentInChildren<ParticleSystem>();
        if (ps != null)
        {
            var main = ps.main;
            t = main.duration + main.startLifetime.constantMax;
        }

        Destroy(go, Mathf.Clamp(t, 0.2f, 5f));
    }

    private void ApplyHitEffects(EnemyEntity enemy, Vector3 hitPoint, int baseDamage)
    {
        if (!_overrideHitEffects && _hitEffects != null && _hitEffects.Length > 0)
        {
            for (int i = 0; i < _hitEffects.Length; i++)
            {
                var effect = _hitEffects[i];
                if (effect == null) continue;
                effect.OnProjectileHit(_scope, enemy, hitPoint, _dir, baseDamage);
            }
        }

        ApplyConfiguredHitEffect(enemy, hitPoint, baseDamage);
    }

    private void ApplyConfiguredHitEffect(EnemyEntity enemy, Vector3 hitPoint, int baseDamage)
    {
        switch (_hitEffectConfig.effectType)
        {
            case ProjectileHitEffectConfig.EffectType.Slow:
                ApplySlow(enemy, _hitEffectConfig.slowMultiplier, _hitEffectConfig.slowDuration);
                return;
            case ProjectileHitEffectConfig.EffectType.AoeSlow:
                ApplyAoeSlow(hitPoint, _hitEffectConfig.aoeRadius, _hitEffectConfig.slowMultiplier, _hitEffectConfig.slowDuration);
                if (_hitEffectConfig.aoeDamageOverride > 0 || _hitEffectConfig.aoeDamageMultiplier > 0f)
                    ApplyAoeDamage(enemy, hitPoint, baseDamage);
                return;
            case ProjectileHitEffectConfig.EffectType.AoeDamage:
                ApplyAoeDamage(enemy, hitPoint, baseDamage);
                return;
            default:
                return;
        }
    }

    private static void ApplySlow(EnemyEntity enemy, float multiplier, float duration)
    {
        if (enemy == null) return;
        if (duration <= 0f) return;
        if (multiplier <= 0f) return;

        var slow = enemy.GetComponent<EnemySlowEffect>();
        if (slow == null) slow = enemy.gameObject.AddComponent<EnemySlowEffect>();
        slow.ApplySlow(multiplier, duration);
    }

    private void ApplyAoeSlow(Vector3 hitPoint, float radius, float multiplier, float duration)
    {
        if (radius <= 0f) return;
        if (duration <= 0f) return;
        if (multiplier <= 0f) return;

        int count = Physics.OverlapSphereNonAlloc(hitPoint, radius, _aoeOverlaps, hitMask, QueryTriggerInteraction.Ignore);
        if (count <= 0) return;

        _aoeTargets.Clear();
        for (int i = 0; i < count; i++)
        {
            var e = _aoeOverlaps[i].GetComponentInParent<EnemyEntity>();
            if (e != null) _aoeTargets.Add(e);
        }

        foreach (var e in _aoeTargets)
            ApplySlow(e, multiplier, duration);
    }

    private void ApplyAoeDamage(EnemyEntity directTarget, Vector3 hitPoint, int baseDamage)
    {
        float radius = _hitEffectConfig.aoeRadius;
        if (radius <= 0f) return;

        int dmg = _hitEffectConfig.aoeDamageOverride > 0
            ? _hitEffectConfig.aoeDamageOverride
            : Mathf.Max(0, Mathf.RoundToInt(baseDamage * Mathf.Max(0f, _hitEffectConfig.aoeDamageMultiplier)));
        if (dmg <= 0) return;

        int count = Physics.OverlapSphereNonAlloc(hitPoint, radius, _aoeOverlaps, hitMask, QueryTriggerInteraction.Ignore);
        if (count <= 0) return;

        _aoeTargets.Clear();
        _aoeHarvestTargets.Clear();
        for (int i = 0; i < count; i++)
        {
            var e = _aoeOverlaps[i].GetComponentInParent<EnemyEntity>();
            if (e == null) continue;
            if (!_hitEffectConfig.aoeIncludeDirectTarget && e == directTarget) continue;
            _aoeTargets.Add(e);
        }

        foreach (var e in _aoeTargets)
            _scope.Combat.DealDamage(e, dmg);

        for (int i = 0; i < count; i++)
        {
            var h = _aoeOverlaps[i].GetComponentInParent<Harvestable>();
            if (h != null) _aoeHarvestTargets.Add(h);
        }

        foreach (var h in _aoeHarvestTargets)
            h.TakeHit(dmg, hitPoint);
    }
}
