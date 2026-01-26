using UnityEngine;
using TMPro;
using DG.Tweening;

public sealed class DamageNumberItem : MonoBehaviour
{
    [SerializeField] private TMP_Text text;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float moveDuration = 0.4f;
    [SerializeField] private Vector2 moveOffset = new Vector2(0f, -60f);
    [SerializeField] private Ease moveEase = Ease.OutQuad;
    [SerializeField] private Ease fadeEase = Ease.OutQuad;
    [SerializeField] private bool useUnscaledTime = true;

    private RectTransform _rect;
    private Tween _tween;
    private System.Action<DamageNumberItem> _onDone;
    private bool _returned;

    public void Play(int amount, Vector2 startPos, System.Action<DamageNumberItem> onDone)
    {
        _onDone = onDone;
        _returned = false;
        if (_rect == null) _rect = transform as RectTransform;
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        if (text == null) text = GetComponentInChildren<TMP_Text>(true);

        if (text != null)
            text.text = amount.ToString();

        canvasGroup.alpha = 1f;
        _rect.anchoredPosition = startPos;

        _tween?.Kill();
        var seq = DOTween.Sequence();
        if (useUnscaledTime) seq.SetUpdate(true);
        seq.Append(_rect.DOAnchorPos(startPos + moveOffset, moveDuration).SetEase(moveEase));
        seq.Join(canvasGroup.DOFade(0f, moveDuration).SetEase(fadeEase));
        seq.OnComplete(Finish);
        _tween = seq;
    }

    private void OnDisable()
    {
        _tween?.Kill();
        Finish();
    }

    private void Finish()
    {
        if (_returned) return;
        _returned = true;
        _onDone?.Invoke(this);
    }
}
