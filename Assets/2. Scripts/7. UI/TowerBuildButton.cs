using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Button))]
public sealed class TowerBuildButton : MonoBehaviour, IPointerEnterHandler
{
    public TowerDefinitionSO Tower => tower;

    // BuildMenuPanel subscribes to these.
    public System.Action<TowerBuildButton> Selected;
    public System.Action<TowerBuildButton, TowerDefinitionSO> Confirmed;

    [SerializeField] private TowerDefinitionSO tower;

    [Header("A1 - Base")]
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text descText;
    [SerializeField] private TMP_Text costText;

    [Header("A2 - Title (Selected Only)")]
    [SerializeField] private GameObject titleRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private CanvasGroup titleCanvasGroup;

    [Header("A3 - Preview")]
    [SerializeField] private RectTransform previewRect;
    [SerializeField] private Image previewImage;
    [SerializeField] private float previewLiftWhenSelected = 60f;
    [SerializeField] private RectTransform previewSlotUnselected;
    [SerializeField] private RectTransform previewSlotSelected;
    [SerializeField] private float previewMoveDuration = 0.18f;
    [SerializeField] private Ease previewMoveEase = Ease.OutQuad;
    [SerializeField] private bool ignoreLayoutForPreview = true;

    [Header("Optional")]
    [SerializeField] private GameObject selectedFxRoot;

    private Button _btn;
    private bool _isSelected;
    private bool _placementMode;
    private Vector2 _previewPosUnselected;
    private Vector2 _previewPosSelected;
    private Tween _previewTween;
    private Tween _titleTween;

    private void Awake()
    {
        _btn = GetComponent<Button>();
        _btn.onClick.AddListener(OnClick);

        if (previewRect != null && ignoreLayoutForPreview)
        {
            var layout = previewRect.GetComponent<LayoutElement>();
            if (layout == null) layout = previewRect.gameObject.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
        }

        CachePreviewPositions();

        SetSelected(false);
        Refresh();
    }

    private void OnEnable()
    {
        CachePreviewPositions();
        if (!_placementMode)
            MovePreview(_isSelected);
    }

    private void OnDisable()
    {
        _previewTween?.Kill();
        _titleTween?.Kill();
    }

    public void SetTower(TowerDefinitionSO newTower)
    {
        tower = newTower;
        Refresh();
        SetSelected(false);
        SetPlacementMode(false);
    }

    public void SetSelected(bool on)
    {
        _isSelected = on;

        if (!_placementMode)
        {
            UpdateTitleVisual(on);
            MovePreview(on);
        }

        if (selectedFxRoot != null)
            selectedFxRoot.SetActive(on && !_placementMode);
    }

    public void SetPlacementMode(bool on)
    {
        _placementMode = on;

        if (previewRect != null)
            previewRect.gameObject.SetActive(!on);

        if (selectedFxRoot != null)
            selectedFxRoot.SetActive(!on && _isSelected);

        if (on)
        {
            if (titleRoot != null) titleRoot.SetActive(false);
        }
        else
        {
            UpdateTitleVisual(_isSelected);
            MovePreview(_isSelected);
        }
    }

    public void SetInteractable(bool on)
    {
        if (_btn != null) _btn.interactable = on;
    }

    private void Refresh()
    {
        if (tower == null) return;

        if (descText != null) descText.text = tower.description;
        if (costText != null) costText.text = tower.cost.ToString();

        if (titleText != null) titleText.text = tower.displayName;

        if (previewImage != null)
            previewImage.sprite = (tower.preview != null) ? tower.preview : tower.icon;
    }

    private void CachePreviewPositions()
    {
        if (previewRect == null) return;

        if (previewSlotUnselected != null)
            _previewPosUnselected = previewSlotUnselected.anchoredPosition;
        else
            _previewPosUnselected = previewRect.anchoredPosition;

        if (previewSlotSelected != null)
            _previewPosSelected = previewSlotSelected.anchoredPosition;
        else
            _previewPosSelected = _previewPosUnselected + Vector2.up * previewLiftWhenSelected;
    }

    private void MovePreview(bool selected)
    {
        if (previewRect == null) return;

        Vector2 target = selected ? _previewPosSelected : _previewPosUnselected;
        _previewTween?.Kill();
        _previewTween = previewRect.DOAnchorPos(target, previewMoveDuration)
            .SetEase(previewMoveEase)
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
        if (tower == null) return;
        Selected?.Invoke(this);
    }

    private void OnClick()
    {
        if (tower == null) return;
        Confirmed?.Invoke(this, tower);
    }
}
