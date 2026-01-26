using UnityEngine;
using DG.Tweening;

public sealed class EnemyEntity : MonoBehaviour
{
    [Header("Death Tween")]
    [SerializeField] private bool useDeathTween = true;
    [SerializeField] private bool deathUseUnscaledTime = true;
    [SerializeField] private float deathDuration = 0.1f;
    [SerializeField] private float deathRise = 0.6f;
    [SerializeField] private float deathPopScale = 1.25f;
    [SerializeField, Range(0.05f, 0.95f)] private float deathPopUpRatio = 0.9f;
    [SerializeField] private Ease deathPopUpEase = Ease.OutBack;
    [SerializeField] private Ease deathPopDownEase = Ease.InBack;
    [SerializeField] private Ease deathEase = Ease.InBack;
    [SerializeField] private Transform deathVisual;

    [Header("Death Sprite Fx")]
    [SerializeField] private EnemyDeathSpriteFx deathSpriteFxPrefab;
    [SerializeField] private bool deathSpriteUsePool = true;

    private RunScope _scope;
    private bool _constructed;
    private bool _dying;
    private bool _dropRewardsOnDeath = true;
    private Tween _deathTween;
    private Vector3 _deathStartPos;
    private Vector3 _deathStartScale;
    private Collider[] _colliders;
    private bool[] _colliderStates;
    private Rigidbody _rb;
    private bool _rbDefaultKinematic;
    private bool _rbDefaultUseGravity;
    private Transform _cachedDeathVisual;
    private Vector3 _cachedDeathScale;

    private void Awake()
    {
        CacheDefaults();
    }

    private void OnEnable()
    {
        ResetForSpawn();
    }

    public void Construct(RunScope scope, int maxHp = 20, float moveSpeed = 2.5f)
    { 
        _scope = scope;
        _constructed = true;

        var hp = GetComponent<HealthComponent>();
        if (hp == null) hp = gameObject.AddComponent<HealthComponent>();
        hp.Initialize(Mathf.Max(1, maxHp), OnDead);

        var brain = GetComponent<EnemyBrain>();
        if (brain == null) brain = gameObject.AddComponent<EnemyBrain>();
        brain.Construct(_scope, speed: Mathf.Max(0.1f, moveSpeed));

        if (GetComponent<EnemyContactDamage>() == null)
            gameObject.AddComponent<EnemyContactDamage>();
    }

    private void OnDead()
    {
        if (_dying) return;
        _dying = true;

        SpawnDeathSpriteFx();

        bool dropRewards = _dropRewardsOnDeath;
        if (!useDeathTween || deathDuration <= 0f)
        {
            Despawn(dropRewards);
            return;
        }

        PlayDeathTween(dropRewards);
    }

    private void Despawn(bool dropRewards)
    {
        if (_scope != null && _scope.Entities != null)
            _scope.Entities.UnregisterEnemy(this);

        if (dropRewards && GameRoot.Instance != null && GameRoot.Instance.EnemyExpDropPrefab != null)
        {
            var dropPrefab = GameRoot.Instance.EnemyExpDropPrefab;
            var pool = GameRoot.Instance.Pool;
            if (pool != null)
                pool.Spawn(dropPrefab, transform.position, Quaternion.identity);
            else
                Instantiate(dropPrefab, transform.position, Quaternion.identity);
        }

        if (_scope != null && _scope.App != null && _scope.App.Pool != null
            && GameRoot.Instance != null && GameRoot.Instance.EnemyPrefab != null)
            _scope.App.Pool.Despawn(gameObject, GameRoot.Instance.EnemyPrefab.gameObject);
        else
            Destroy(gameObject);
    }

    private void SpawnDeathSpriteFx()
    {
        if (deathSpriteFxPrefab == null) return;

        PoolService pool = null;
        if (deathSpriteUsePool)
            pool = _scope?.App?.Pool ?? GameRoot.Instance?.Pool;

        EnemyDeathSpriteFx fx;
        if (pool != null)
            fx = pool.Spawn(deathSpriteFxPrefab, transform.position, Quaternion.identity);
        else
            fx = Instantiate(deathSpriteFxPrefab, transform.position, Quaternion.identity);

        if (fx != null)
            fx.Play(transform.position, pool);
    }

    private void PlayDeathTween(bool dropRewards)
    {
        Transform visual = ResolveDeathVisual();
        _deathStartPos = transform.position;
        _deathStartScale = visual.localScale;

        DisableForDeath();

        _deathTween?.Kill();
        var seq = DOTween.Sequence();
        if (deathUseUnscaledTime) seq.SetUpdate(true);

        float total = Mathf.Max(0.01f, deathDuration);
        float upTime = Mathf.Clamp(total * deathPopUpRatio, 0.01f, total);
        float holdTime = Mathf.Max(0f, total - upTime);
        Vector3 popScale = _deathStartScale * Mathf.Max(0.01f, deathPopScale);

        seq.Append(visual.DOScale(popScale, upTime).SetEase(deathPopUpEase));
        if (holdTime > 0f)
            seq.AppendInterval(holdTime);
        seq.Insert(0f, transform.DOMoveY(_deathStartPos.y + deathRise, total).SetEase(deathEase));
        seq.OnComplete(() =>
        {
            transform.position = _deathStartPos;
            visual.localScale = _deathStartScale;
            Despawn(dropRewards);
        });
        _deathTween = seq;
    }

    private void DisableForDeath()
    {
        var brain = GetComponent<EnemyBrain>();
        if (brain != null) brain.enabled = false;

        if (_rb != null)
        {
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic = true;
            _rb.useGravity = false;
        }

        if (_colliders == null) return;
        for (int i = 0; i < _colliders.Length; i++)
            _colliders[i].enabled = false;
    }

    private void CacheDefaults()
    {
        _rb = GetComponent<Rigidbody>();
        if (_rb != null)
        {
            _rbDefaultKinematic = _rb.isKinematic;
            _rbDefaultUseGravity = _rb.useGravity;
        }

        _colliders = GetComponentsInChildren<Collider>(true);
        if (_colliders != null && _colliders.Length > 0)
        {
            _colliderStates = new bool[_colliders.Length];
            for (int i = 0; i < _colliders.Length; i++)
                _colliderStates[i] = _colliders[i].enabled;
        }

        _cachedDeathVisual = ResolveDeathVisual();
        _cachedDeathScale = _cachedDeathVisual.localScale;
    }

    private void ResetForSpawn()
    {
        _dying = false;
        _dropRewardsOnDeath = true;
        _deathTween?.Kill();
        _deathTween = null;

        var brain = GetComponent<EnemyBrain>();
        if (brain != null) brain.enabled = true;

        if (_rb != null)
        {
            _rb.isKinematic = _rbDefaultKinematic;
            _rb.useGravity = _rbDefaultUseGravity;
        }

        if (_colliders != null && _colliderStates != null)
        {
            int count = Mathf.Min(_colliders.Length, _colliderStates.Length);
            for (int i = 0; i < count; i++)
                _colliders[i].enabled = _colliderStates[i];
        }

        if (_cachedDeathVisual != null)
            _cachedDeathVisual.localScale = _cachedDeathScale;
    }

    private Transform ResolveDeathVisual()
    {
        return deathVisual != null ? deathVisual : transform;
    }

    public void ApplyDamageNoDrop(int amount)
    {
        if (amount <= 0) return;

        var hp = GetComponent<HealthComponent>();
        if (hp == null)
        {
            _dropRewardsOnDeath = false;
            Despawn(false);
            return;
        }

        if (hp.Current - amount <= 0)
            _dropRewardsOnDeath = false;

        hp.ApplyDamage(amount);
    }

    private void OnDestroy()
    {
        if (_constructed && _scope != null && _scope.Entities != null)
            _scope.Entities.UnregisterEnemy(this);
    }
}
