using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

[DisallowMultipleComponent]
public sealed class WorldHpBar : MonoBehaviour
{
    private enum SourceType
    {
        PlayerHealth = 0,
        SharedBuilding = 1
    }

    [Header("소스")]
    [SerializeField] private SourceType sourceType = SourceType.PlayerHealth;
    [SerializeField] private HealthComponent health;
    [SerializeField] private SharedBuildingHealth sharedHealth;
    [SerializeField] private SharedBuildingHealthProxy sharedProxy;
    [SerializeField] private bool autoFind = true;

    [Header("UI")]
    [SerializeField] private Slider slider;
    [SerializeField] private Image fillImage;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private string hpFormat = "{0}/{1}";
    [SerializeField] private bool clampFill01 = true;

    [Header("트윈")]
    [SerializeField] private bool useHpTween = true;
    [SerializeField] private float hpTweenDuration = 0.2f;
    [SerializeField] private Ease hpTweenEase = Ease.OutQuad;
    [SerializeField] private bool hpTweenUnscaledTime = true;

    [Header("추적")]
    [SerializeField] private Transform followTarget;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.6f, 0f);
    [SerializeField] private bool snapOnBind = true;
    [SerializeField] private bool useSmoothFollow = true;
    [SerializeField] private float followSpeed = 12f;
    [SerializeField] private bool faceCamera = true;
    [SerializeField] private bool lockToCameraForward = true;
    [SerializeField] private Camera targetCamera;

    [Header("재탐색")]
    [SerializeField] private bool autoRebind = true;
    [SerializeField] private float rebindInterval = 0.2f;

    private RunScope _scope;
    private HealthComponent _boundHealth;
    private SharedBuildingHealth _boundShared;
    private float _rebindTimer;
    private Tween _hpTween;
    private float _displayHp;
    private int _displayMax;
    private bool _hpInitialized;

    private void Awake()
    {
        DisableSliderInput(slider);
    }

    private void OnEnable()
    {
        RunScopeLocator.Changed += OnScopeChanged;
        Bind();
    }

    private void OnDisable()
    {
        RunScopeLocator.Changed -= OnScopeChanged;
        Unbind();
        KillHpTween();
    }

    private void LateUpdate()
    {
        TryAutoRebind();
        FollowTarget();
    }

    private void OnScopeChanged(RunScope scope)
    {
        Bind();
    }

    private void Bind()
    {
        Unbind();
        ResolveSources();
        Subscribe();
        RefreshUI();
        if (snapOnBind) SnapToTarget();
    }

    private void Unbind()
    {
        if (_boundHealth != null)
            _boundHealth.HpChanged -= OnHealthChanged;
        if (_boundShared != null)
            _boundShared.HpChanged -= OnSharedHealthChanged;
        _boundHealth = null;
        _boundShared = null;
        _hpInitialized = false;
        _displayHp = 0f;
        _displayMax = 0;
        KillHpTween();
    }

    private void ResolveSources()
    {
        _scope = RunScopeLocator.Current;
        CleanInvalidRefs();

        if (sourceType == SourceType.PlayerHealth)
        {
            if (health == null && autoFind)
            {
                health = GetComponentInParent<HealthComponent>();
                if (health == null && _scope?.Entities?.Player != null)
                    health = _scope.Entities.Player.Health;
            }

            if (followTarget == null && health != null)
                followTarget = health.transform;
            return;
        }

        if (sharedProxy == null && autoFind)
            sharedProxy = GetComponentInParent<SharedBuildingHealthProxy>();
        if (sharedHealth == null)
            sharedHealth = sharedProxy != null ? sharedProxy.SharedHealth : null;
        if (sharedHealth == null && autoFind)
        {
            if (_scope != null)
                sharedHealth = _scope.GetComponent<SharedBuildingHealth>();
            if (sharedHealth == null)
                sharedHealth = FindObjectOfType<SharedBuildingHealth>();
        }

        if (followTarget == null)
        {
            if (sharedProxy != null)
                followTarget = sharedProxy.transform;
            else if (sharedHealth != null)
                followTarget = sharedHealth.transform;
        }
    }

    private void TryAutoRebind()
    {
        if (!autoFind || !autoRebind) return;

        _rebindTimer -= Time.deltaTime;
        if (_rebindTimer > 0f) return;
        _rebindTimer = Mathf.Max(0.05f, rebindInterval);

        if (!NeedsRebind()) return;
        Bind();
    }

    private bool NeedsRebind()
    {
        if (sourceType == SourceType.PlayerHealth)
        {
            if (health == null || _boundHealth == null) return true;
            if (followTarget == null) return true;
            return false;
        }

        if (sharedHealth == null && sharedProxy == null) return true;
        if (_boundShared == null && sharedHealth != null) return true;
        if (followTarget == null) return true;
        return false;
    }

    private void CleanInvalidRefs()
    {
        if (health != null && !IsSceneObject(health)) health = null;
        if (sharedHealth != null && !IsSceneObject(sharedHealth)) sharedHealth = null;
        if (sharedProxy != null && !IsSceneObject(sharedProxy)) sharedProxy = null;
        if (followTarget != null && !IsSceneObject(followTarget)) followTarget = null;
    }

    private void Subscribe()
    {
        if (sourceType == SourceType.PlayerHealth)
        {
            if (health != null)
            {
                _boundHealth = health;
                _boundHealth.HpChanged += OnHealthChanged;
            }
            return;
        }

        if (sharedHealth != null)
        {
            _boundShared = sharedHealth;
            _boundShared.HpChanged += OnSharedHealthChanged;
        }
    }

    private void RefreshUI()
    {
        if (sourceType == SourceType.PlayerHealth && health != null)
        {
            ApplyHp(health.Current, health.Max);
            return;
        }

        if (sourceType == SourceType.SharedBuilding && sharedHealth != null)
        {
            ApplyHp(sharedHealth.Current, sharedHealth.Max);
            return;
        }

        ApplyHp(0, 0);
    }

    private void OnHealthChanged(int current, int max)
    {
        ApplyHp(current, max);
    }

    private void OnSharedHealthChanged(int current, int max)
    {
        ApplyHp(current, max);
    }

    private void ApplyHp(int current, int max)
    {
        int safeMax = Mathf.Max(1, max);
        int safeCur = Mathf.Max(0, current);

        if (!useHpTween || hpTweenDuration <= 0f)
        {
            _hpInitialized = true;
            _displayMax = safeMax;
            _displayHp = safeCur;
            ApplyDisplay(_displayHp, _displayMax);
            return;
        }

        if (!_hpInitialized)
        {
            _hpInitialized = true;
            _displayMax = safeMax;
            _displayHp = safeCur;
            ApplyDisplay(_displayHp, _displayMax);
            return;
        }

        _displayMax = safeMax;
        KillHpTween();
        _hpTween = DOTween.To(() => _displayHp, v =>
        {
            _displayHp = v;
            ApplyDisplay(_displayHp, _displayMax);
        }, safeCur, hpTweenDuration).SetEase(hpTweenEase).SetUpdate(hpTweenUnscaledTime);
    }

    private void ApplyDisplay(float displayHp, int max)
    {
        float fill = max > 0 ? displayHp / max : 0f;
        if (clampFill01) fill = Mathf.Clamp01(fill);

        if (slider != null)
            slider.SetValueWithoutNotify(fill);
        if (fillImage != null && !IsSliderFillImage(slider, fillImage))
            fillImage.fillAmount = fill;
        if (hpText != null)
            hpText.text = string.Format(hpFormat, Mathf.RoundToInt(displayHp), max);
    }

    private void FollowTarget()
    {
        if (followTarget == null) return;

        Vector3 targetPos = followTarget.position + worldOffset;
        if (useSmoothFollow)
            transform.position = Vector3.MoveTowards(transform.position, targetPos, followSpeed * Time.deltaTime);
        else
            transform.position = targetPos;

        if (faceCamera)
            AlignToCamera();
    }

    private void SnapToTarget()
    {
        if (followTarget == null) return;
        transform.position = followTarget.position + worldOffset;
        if (faceCamera)
            AlignToCamera();
    }

    private void AlignToCamera()
    {
        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null) return;

        if (lockToCameraForward)
        {
            Vector3 forward = cam.transform.forward;
            if (forward.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            return;
        }

        Vector3 to = transform.position - cam.transform.position;
        if (to.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
    }

    private void KillHpTween()
    {
        if (_hpTween == null) return;
        _hpTween.Kill();
        _hpTween = null;
    }

    private static bool IsSliderFillImage(Slider slider, Image image)
    {
        if (slider == null || image == null) return false;
        if (slider.fillRect == null) return false;
        return slider.fillRect.GetComponent<Image>() == image;
    }

    private static bool IsSceneObject(Component component)
    {
        if (component == null) return false;
        return component.gameObject.scene.IsValid();
    }

    private static bool IsSceneObject(Transform target)
    {
        if (target == null) return false;
        return target.gameObject.scene.IsValid();
    }

    private void DisableSliderInput(Slider target)
    {
        if (target == null) return;
        target.interactable = false;
        var nav = target.navigation;
        nav.mode = Navigation.Mode.None;
        target.navigation = nav;
    }
}
