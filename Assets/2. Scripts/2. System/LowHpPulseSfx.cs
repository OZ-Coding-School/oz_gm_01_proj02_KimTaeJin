using UnityEngine;

[DisallowMultipleComponent]
public sealed class LowHpPulseSfx : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private SharedBuildingHealth sharedHealth;
    [SerializeField] private HealthComponent playerHealth;
    [SerializeField] private bool autoFind = true;

    [Header("임계치")]
    [SerializeField, Range(0.01f, 0.99f)] private float lowHpStart01 = 0.3f;
    [SerializeField, Range(0.01f, 0.99f)] private float lowHpEnd01 = 0.1f;

    [Header("간격")]
    [SerializeField] private float pulseIntervalAtStart = 1.0f;
    [SerializeField] private float pulseIntervalAtEnd = 0.5f;
    [SerializeField] private bool useLoopBgm = true;
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private bool stopWhenDead = true;

    private float _timer;
    private bool _loopActive;

    private void OnEnable()
    {
        RunScopeLocator.Changed += OnScopeChanged;
        ResolveRefs();
        _timer = 0f;
        _loopActive = false;
    }

    private void OnDisable()
    {
        RunScopeLocator.Changed -= OnScopeChanged;
        if (_loopActive)
        {
            GameAudio.Instance?.SetLowHpLoopActive(false);
            _loopActive = false;
        }
    }

    private void OnScopeChanged(RunScope scope)
    {
        ResolveRefs();
    }

    private void Update()
    {
        ResolveRefs();

        if (!TryGetLowestHpRatio(out float ratio))
        {
            _timer = 0f;
            if (_loopActive)
            {
                GameAudio.Instance?.SetLowHpLoopActive(false);
                _loopActive = false;
            }
            return;
        }

        if (stopWhenDead && IsDeadInternal())
        {
            _timer = 0f;
            if (_loopActive)
            {
                GameAudio.Instance?.SetLowHpLoopActive(false);
                _loopActive = false;
            }
            return;
        }

        float start = Mathf.Clamp01(lowHpStart01);
        float end = Mathf.Clamp01(lowHpEnd01);
        float high = Mathf.Max(start, end);
        float low = Mathf.Min(start, end);

        if (ratio > high)
        {
            _timer = 0f;
            if (_loopActive)
            {
                GameAudio.Instance?.SetLowHpLoopActive(false);
                _loopActive = false;
            }
            return;
        }

        if (useLoopBgm)
        {
            if (!_loopActive)
            {
                GameAudio.Ensure()?.SetLowHpLoopActive(true);
                _loopActive = true;
            }
            return;
        }
        float t = Mathf.InverseLerp(high, low, ratio);
        float interval = Mathf.Lerp(pulseIntervalAtStart, pulseIntervalAtEnd, t);
        if (interval <= 0f) interval = 0.01f;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        _timer -= dt;
        if (_timer <= 0f)
        {
            GameAudio.Instance?.PlayLowHpPulse();
            _timer = interval;
        }
    }

    private bool IsDeadInternal()
    {
        bool buildingDead = sharedHealth != null && sharedHealth.IsDead;
        bool playerDead = playerHealth != null && playerHealth.Current <= 0;
        return buildingDead || playerDead;
    }

    private bool TryGetLowestHpRatio(out float ratio)
    {
        ratio = 1f;
        bool has = false;

        if (sharedHealth != null && sharedHealth.Max > 0)
        {
            ratio = Mathf.Min(ratio, sharedHealth.Current / (float)sharedHealth.Max);
            has = true;
        }

        if (playerHealth != null && playerHealth.Max > 0)
        {
            ratio = Mathf.Min(ratio, playerHealth.Current / (float)playerHealth.Max);
            has = true;
        }

        return has;
    }

    private void ResolveRefs()
    {
        if (!autoFind) return;

        var scope = RunScopeLocator.Current;
        if (sharedHealth == null && scope != null)
            sharedHealth = scope.GetComponent<SharedBuildingHealth>();

        if (playerHealth == null && scope != null && scope.Entities?.Player != null)
            playerHealth = scope.Entities.Player.Health;
    }
}
