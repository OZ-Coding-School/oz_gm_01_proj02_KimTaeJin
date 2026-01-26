using UnityEngine;

[DisallowMultipleComponent]
public sealed class GameOverVignetteUI : MonoBehaviour
{
    [Header("비네팅 UI")]
    [SerializeField] private CanvasGroup vignetteGroup;
    [SerializeField] private float targetAlpha = 0.85f;
    [SerializeField] private float fadeInDuration = 0.4f;
    [SerializeField] private float fadeOutDuration = 0.2f;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("대상")]
    [SerializeField] private SharedBuildingHealth sharedHealth;
    [SerializeField] private HealthComponent playerHealth;
    [SerializeField] private bool autoFind = true;

    [Header("저체력")]
    [SerializeField] private bool useLowHpThreshold = true;
    [SerializeField, Range(0.01f, 0.99f)] private float lowHpStart01 = 0.3f;
    [SerializeField, Range(0.01f, 0.99f)] private float lowHpEnd01 = 0.1f;
    [SerializeField, Range(0f, 1f)] private float lowHpAlphaAtStart = 0.1f;
    [SerializeField, Range(0f, 1f)] private float lowHpAlphaAtEnd = 0.3f;
    [SerializeField] private bool showWhenDead = true;

    private float _alpha;

    private void OnEnable()
    {
        RunScopeLocator.Changed += OnScopeChanged;
        ResolveRefs();
        EnsureCanvasGroup();
        SetAlpha(0f);
    }

    private void OnDisable()
    {
        RunScopeLocator.Changed -= OnScopeChanged;
    }

    private void OnScopeChanged(RunScope scope)
    {
        ResolveRefs();
    }

    private void Update()
    {
        ResolveRefs();
        float target = ResolveTargetAlpha();
        FadeTo(target);
    }

    private void FadeTo(float target)
    {
        if (vignetteGroup == null) return;

        if (Mathf.Abs(_alpha - target) <= 0.0001f)
        {
            SetAlpha(target);
            return;
        }

        float duration = target > _alpha ? fadeInDuration : fadeOutDuration;
        if (duration <= 0f)
        {
            SetAlpha(target);
            return;
        }

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float speed = Mathf.Abs(target - _alpha) / Mathf.Max(0.01f, duration);
        SetAlpha(Mathf.MoveTowards(_alpha, target, speed * dt));
    }

    private float ResolveTargetAlpha()
    {
        float lowAlpha = useLowHpThreshold ? ResolveLowHpAlpha() : 0f;
        float deadAlpha = showWhenDead && IsDeadInternal() ? targetAlpha : 0f;
        return Mathf.Max(lowAlpha, deadAlpha);
    }

    private bool IsDeadInternal()
    {
        bool buildingDead = sharedHealth != null && sharedHealth.IsDead;
        bool playerDead = playerHealth != null && playerHealth.Current <= 0;
        return buildingDead || playerDead;
    }

    private float ResolveLowHpAlpha()
    {
        if (!TryGetLowestHpRatio(out float ratio)) return 0f;

        float start = Mathf.Clamp01(lowHpStart01);
        float end = Mathf.Clamp01(lowHpEnd01);
        float high = Mathf.Max(start, end);
        float low = Mathf.Min(start, end);

        if (ratio > high) return 0f;

        float t = Mathf.InverseLerp(high, low, ratio);
        float a0 = Mathf.Clamp01(lowHpAlphaAtStart);
        float a1 = Mathf.Clamp01(lowHpAlphaAtEnd);
        return Mathf.Lerp(a0, a1, t);
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

    private void EnsureCanvasGroup()
    {
        if (vignetteGroup != null) return;
        vignetteGroup = GetComponent<CanvasGroup>();
    }

    private void SetAlpha(float value)
    {
        _alpha = Mathf.Clamp01(value);
        if (vignetteGroup != null)
            vignetteGroup.alpha = _alpha;
    }
}
