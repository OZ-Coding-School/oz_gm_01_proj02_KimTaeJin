using UnityEngine;

[DisallowMultipleComponent]
public sealed class BoundaryDamage : MonoBehaviour
{
    [SerializeField] private PlayAreaBoundary boundary;
    [SerializeField] private int damagePerSecond = 50;
    [SerializeField] private float tickInterval = 0.2f;

    private HealthComponent _hp;
    private float _tick;
    private bool _warnedNoHp;
    private bool _boundaryResolved;

    private void Awake()
    {
        _hp = GetComponent<HealthComponent>();
        if (_hp == null)
            _hp = GetComponentInChildren<HealthComponent>();

        ResolveBoundary();
    }

    private void Update()
    {
        if (!ResolveBoundary()) return;
        if (damagePerSecond <= 0 || tickInterval <= 0f) return;

        if (_hp == null)
        {
            if (!_warnedNoHp)
            {
                _warnedNoHp = true;
                Debug.LogWarning("[BoundaryDamage] HealthComponent missing, damage ignored.");
            }
            return;
        }

        if (boundary.IsInsideXZ(transform.position))
        {
            _tick = 0f;
            return;
        }

        _tick += Time.deltaTime;
        if (_tick < tickInterval) return;

        int damage = Mathf.CeilToInt(damagePerSecond * _tick);
        _tick = 0f;
        _hp.ApplyDamage(damage);
    }

    private bool ResolveBoundary()
    {
        if (boundary != null) return true;
        if (_boundaryResolved) return false;
        _boundaryResolved = true;
        boundary = FindObjectOfType<PlayAreaBoundary>();
        return boundary != null;
    }
}
