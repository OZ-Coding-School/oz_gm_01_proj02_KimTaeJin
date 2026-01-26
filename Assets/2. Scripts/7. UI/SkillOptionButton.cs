using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

[RequireComponent(typeof(Button))]
public sealed class SkillOptionButton : MonoBehaviour, IPointerEnterHandler
{
    public PlayerSkillOptionSO Option => option;

    public System.Action<SkillOptionButton> Selected;
    public System.Action<SkillOptionButton, PlayerSkillOptionSO> Confirmed;

    [SerializeField] private PlayerSkillOptionSO option;

    [Header("A1 - Base")]
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text descText;

    [Header("A2 - Title (Selected Only)")]
    [SerializeField] private GameObject titleRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private CanvasGroup titleCanvasGroup;

    [Header("A3 - Icon Move")]
    [SerializeField] private RectTransform iconRect;
    [SerializeField] private RectTransform iconSlotUnselected;
    [SerializeField] private RectTransform iconSlotSelected;
    [SerializeField] private float iconLiftWhenSelected = 60f;
    [SerializeField] private float iconMoveDuration = 0.18f;
    [SerializeField] private Ease iconMoveEase = Ease.OutQuad;
    [SerializeField] private bool ignoreLayoutForIcon = true;

    [Header("Optional")]
    [SerializeField] private GameObject selectedRoot;

    private Button _btn;
    private bool _isSelected;
    private Vector2 _iconPosUnselected;
    private Vector2 _iconPosSelected;
    private Tween _iconTween;
    private Tween _titleTween;

    private void Awake()
    {
        _btn = GetComponent<Button>();
        _btn.onClick.AddListener(OnClick);

        if (iconRect == null && icon != null)
            iconRect = icon.rectTransform;
        if (titleRoot == null && titleText != null)
            titleRoot = titleText.gameObject;
        if (titleCanvasGroup == null && titleRoot != null)
            titleCanvasGroup = titleRoot.GetComponent<CanvasGroup>();

        if (iconRect != null && ignoreLayoutForIcon)
        {
            var layout = iconRect.GetComponent<LayoutElement>();
            if (layout == null) layout = iconRect.gameObject.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
        }

        CacheIconPositions();
        Refresh();
        SetSelected(false);
    }

    private void OnEnable()
    {
        CacheIconPositions();
        MoveIcon(_isSelected);
    }

    private void OnDisable()
    {
        _iconTween?.Kill();
        _titleTween?.Kill();
    }

    public void SetOption(PlayerSkillOptionSO newOption)
    {
        option = newOption;
        Refresh();
        SetSelected(false);
    }

    public void SetSelected(bool on)
    {
        _isSelected = on;
        UpdateTitleVisual(on);
        MoveIcon(on);

        if (selectedRoot != null)
            selectedRoot.SetActive(on);
    }

    public void SetInteractable(bool on)
    {
        if (_btn != null) _btn.interactable = on;
    }

    private void Refresh()
    {
        if (option == null) return;
        if (icon != null) icon.sprite = option.icon;
        if (descText != null) descText.text = option.description;
        if (titleText != null) titleText.text = option.displayName;
    }

    private void CacheIconPositions()
    {
        if (iconRect == null) return;

        if (iconSlotUnselected != null)
            _iconPosUnselected = iconSlotUnselected.anchoredPosition;
        else
            _iconPosUnselected = iconRect.anchoredPosition;

        if (iconSlotSelected != null)
            _iconPosSelected = iconSlotSelected.anchoredPosition;
        else
            _iconPosSelected = _iconPosUnselected + Vector2.up * iconLiftWhenSelected;
    }

    private void MoveIcon(bool selected)
    {
        if (iconRect == null) return;

        Vector2 target = selected ? _iconPosSelected : _iconPosUnselected;
        _iconTween?.Kill();
        _iconTween = iconRect.DOAnchorPos(target, iconMoveDuration)
            .SetEase(iconMoveEase)
            .SetUpdate(true);
    }

    private void UpdateTitleVisual(bool selected)
    {
        if (titleRoot == null) return;

        if (titleCanvasGroup == null)
        {
            titleRoot.SetActive(selected);
            return;
        }

        titleRoot.SetActive(true);
        _titleTween?.Kill();
        float targetAlpha = selected ? 1f : 0f;
        _titleTween = titleCanvasGroup.DOFade(targetAlpha, 0.12f)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                if (!selected) titleRoot.SetActive(false);
            });
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (option == null) return;
        Selected?.Invoke(this);
    }

    private void OnClick()
    {
        if (option == null) return;
        Confirmed?.Invoke(this, option);
    }
}
