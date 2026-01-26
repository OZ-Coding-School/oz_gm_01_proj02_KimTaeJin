using UnityEngine;
using DG.Tweening;

[DisallowMultipleComponent]
public sealed class EnemyDeathSpriteFx : MonoBehaviour
{
    [Header("비주얼")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private bool faceCamera = true;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.1f, 0f);

    [Header("트윈")]
    [SerializeField] private float duration = 0.6f;
    [SerializeField] private float popScale = 1.4f;
    [SerializeField] private float endScale = 0.6f;
    [SerializeField] private float rise = 0.25f;
    [SerializeField] private float fadeDelay = 0.05f;
    [SerializeField] private Ease popEase = Ease.OutBack;
    [SerializeField] private Ease shrinkEase = Ease.InQuad;
    [SerializeField] private Ease riseEase = Ease.OutQuad;
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private bool autoPlayOnEnable = false;

    private Tween _tween;
    private Vector3 _baseScale;
    private Color _baseColor;
    private PoolService _pool;
    private bool _usePool;
    private bool _played;

    private void Awake()
    {
        ResolveRenderer();
        CacheBase();
    }

    private void OnEnable()
    {
        _played = false;
        if (autoPlayOnEnable)
            Play(transform.position, null);
    }

    private void OnDisable()
    {
        KillTween();
    }

    public void Play(Vector3 position, PoolService pool)
    {
        _pool = pool;
        _usePool = pool != null;
        StartEffect(position);
    }

    private void StartEffect(Vector3 position)
    {
        ResolveRenderer();
        if (spriteRenderer == null)
        {
            Finish();
            return;
        }

        _played = true;
        KillTween();

        Transform visual = spriteRenderer.transform;
        transform.position = position + worldOffset;
        if (faceCamera) AlignToCamera();

        if (_baseScale == Vector3.zero)
            CacheBase();

        visual.localScale = _baseScale * 0.1f;
        var c = _baseColor;
        c.a = 1f;
        spriteRenderer.color = c;

        float total = Mathf.Max(0.05f, duration);
        float popTime = Mathf.Clamp(total * 0.35f, 0.05f, total);
        float shrinkTime = Mathf.Max(0.01f, total - popTime);
        float fadeTime = Mathf.Max(0.01f, total - fadeDelay);

        var seq = DOTween.Sequence();
        if (useUnscaledTime) seq.SetUpdate(true);
        seq.Append(visual.DOScale(_baseScale * Mathf.Max(0.01f, popScale), popTime).SetEase(popEase));
        seq.Append(visual.DOScale(_baseScale * Mathf.Max(0.01f, endScale), shrinkTime).SetEase(shrinkEase));
        seq.Join(spriteRenderer.DOFade(0f, fadeTime).SetDelay(fadeDelay));
        seq.Join(transform.DOMoveY(transform.position.y + rise, total).SetEase(riseEase));
        seq.OnComplete(Finish);
        _tween = seq;
    }

    private void Finish()
    {
        if (!_played) return;
        if (_usePool && _pool != null)
        {
            _pool.Despawn(gameObject);
            return;
        }

        Destroy(gameObject);
    }

    private void ResolveRenderer()
    {
        if (spriteRenderer != null) return;
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void CacheBase()
    {
        if (spriteRenderer == null) return;
        _baseScale = spriteRenderer.transform.localScale;
        _baseColor = spriteRenderer.color;
    }

    private void AlignToCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;
        transform.forward = -cam.transform.forward;
    }

    private void KillTween()
    {
        _tween?.Kill();
        _tween = null;
    }
}
