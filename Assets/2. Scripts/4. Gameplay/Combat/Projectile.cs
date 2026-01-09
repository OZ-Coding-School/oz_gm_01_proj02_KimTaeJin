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

    private void Awake()
    {
        if (vfxRoot == null) vfxRoot = transform;

        _ps = vfxRoot.GetComponentsInChildren<ParticleSystem>(true);
        _trs = vfxRoot.GetComponentsInChildren<TrailRenderer>(true);
        _renderers = vfxRoot.GetComponentsInChildren<Renderer>(true);
    }

    public void Launch(
    RunScope scope,
    GameObject prefabKey,  
    Vector3 dir,
    int dmg,
    float speed,
    float life,
    float knockback)


    {
        _scope = scope;
        _prefabKey = prefabKey;

        _dir = dir.sqrMagnitude < 0.0001f ? transform.forward : dir.normalized;
        _damage = dmg;
        _speed = speed;
        _life = life;
        _knockback = knockback;

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
}
