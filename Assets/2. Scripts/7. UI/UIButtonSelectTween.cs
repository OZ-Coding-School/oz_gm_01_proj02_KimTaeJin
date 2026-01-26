using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class UIButtonSelectTween : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private RectTransform target;
    [SerializeField] private float normalScaleMultiplier = 1f;
    [SerializeField] private float highlightedScaleMultiplier = 1.15f;
    [SerializeField] private float tweenDuration = 0.15f;
    [SerializeField] private Ease tweenEase = Ease.OutBack;
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private bool useHover = true;
    [SerializeField] private bool useSelection = true;
    [SerializeField] private bool applyOnEnable = true;

    private Vector3 _baseScale = Vector3.one;
    private Tween _tween;
    private bool _hovered;
    private bool _selected;
    private bool _baseCached;

    private void Awake()
    {
        CacheBaseScale();
        if (applyOnEnable) ApplyState(true);
    }

    private void OnEnable()
    {
        CacheBaseScale();
        if (applyOnEnable) ApplyState(true);
    }

    private void OnDisable()
    {
        KillTween();
        _hovered = false;
        _selected = false;
        if (target != null)
            target.localScale = _baseScale * normalScaleMultiplier;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!useHover) return;
        _hovered = true;
        ApplyState(false);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!useHover) return;
        _hovered = false;
        ApplyState(false);
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (!useSelection) return;
        _selected = true;
        ApplyState(false);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        if (!useSelection) return;
        _selected = false;
        ApplyState(false);
    }

    public void ResetState(bool immediate)
    {
        _hovered = false;
        _selected = false;
        ApplyState(immediate);
    }

    private void CacheBaseScale()
    {
        if (_baseCached && target != null) return;
        if (target == null) target = GetComponent<RectTransform>();
        if (target == null) return;
        _baseScale = target.localScale;
        _baseCached = true;
    }

    private void ApplyState(bool immediate)
    {
        if (target == null) return;
        bool active = (useHover && _hovered) || (useSelection && _selected);
        float scaleMul = active ? highlightedScaleMultiplier : normalScaleMultiplier;
        if (immediate)
            target.localScale = _baseScale * scaleMul;
        else
            AnimateTo(scaleMul);
    }

    private void AnimateTo(float scaleMul)
    {
        if (target == null) return;
        KillTween();
        Vector3 next = _baseScale * scaleMul;
        _tween = target.DOScale(next, tweenDuration).SetEase(tweenEase);
        if (useUnscaledTime) _tween.SetUpdate(true);
    }

    private void KillTween()
    {
        if (_tween == null) return;
        _tween.Kill();
        _tween = null;
    }
}
