using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class BoundaryDamage : MonoBehaviour
{
    [SerializeField] private PlayAreaProgressController playArea;
    [FormerlySerializedAs("radiusOverride")]
    [SerializeField] private float radiusPadding = 0f;
    [SerializeField] private int damagePerSecond = 50;
    [SerializeField] private float tickInterval = 0.2f;

    private HealthComponent _hp;
    private float _tick;
    private bool _warnedNoHp;
    private bool _playAreaResolved;

    private void Awake()
    {
        _hp = GetComponent<HealthComponent>();
        if (_hp == null)
            _hp = GetComponentInChildren<HealthComponent>();

        ResolvePlayArea();
    }

    private void Update()
    {
        if (!ResolvePlayArea()) return;
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

        if (playArea.IsInsideXZ(transform.position, radiusPadding))
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

    private bool ResolvePlayArea()
    {
        if (playArea != null) return true;
        if (_playAreaResolved) return false;
        _playAreaResolved = true;
        playArea = FindObjectOfType<PlayAreaProgressController>();
        return playArea != null;
    }
}
