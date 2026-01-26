using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemySlowTint : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private EnemySlowEffect slowEffect;
    [SerializeField] private bool autoFind = true;
    [SerializeField] private Renderer[] renderers;
    [SerializeField] private bool includeInactive = true;

    [Header("색상")]
    [SerializeField] private Color tintColor = new Color(0.35f, 0.6f, 1f, 1f);
    [SerializeField, Range(0f, 1f)] private float tintStrength = 0.6f;
    [SerializeField] private bool useEmission = false;
    [SerializeField] private float emissionIntensity = 2f;

    private MaterialPropertyBlock _mpb;
    private Cache[] _cache;
    private bool _active;

    private struct Cache
    {
        public Renderer renderer;
        public int basePid;
        public Color baseColor;
        public bool hasBase;
        public int emisPid;
        public Color emisColor;
        public bool hasEmis;
    }

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        CacheAll();
    }

    private void OnEnable()
    {
        ResolveRefs();
        ApplyState(false);
    }

    private void Update()
    {
        ResolveRefs();
        bool on = slowEffect != null && slowEffect.HasSlow;
        if (on == _active) return;
        ApplyState(on);
    }

    private void ResolveRefs()
    {
        if (autoFind && slowEffect == null)
            slowEffect = GetComponent<EnemySlowEffect>();
        if (renderers == null || renderers.Length == 0)
            CacheAll();
    }

    private void CacheAll()
    {
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>(includeInactive);

        _cache = new Cache[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            var mat = r != null ? r.sharedMaterial : null;

            var c = new Cache
            {
                renderer = r,
                basePid = BaseColorId,
                baseColor = Color.white,
                hasBase = false,
                emisPid = EmissionColorId,
                emisColor = Color.black,
                hasEmis = false
            };

            if (mat != null)
            {
                if (mat.HasProperty(BaseColorId))
                {
                    c.basePid = BaseColorId;
                    c.baseColor = mat.GetColor(BaseColorId);
                    c.hasBase = true;
                }
                else if (mat.HasProperty(ColorId))
                {
                    c.basePid = ColorId;
                    c.baseColor = mat.GetColor(ColorId);
                    c.hasBase = true;
                }

                if (useEmission && mat.HasProperty(EmissionColorId))
                {
                    c.emisColor = mat.GetColor(EmissionColorId);
                    c.hasEmis = true;
                }
            }

            _cache[i] = c;
        }
    }

    private void ApplyState(bool on)
    {
        _active = on;
        if (_cache == null || _cache.Length == 0) return;

        if (on)
            ApplyTint();
        else
            Restore();
    }

    private void ApplyTint()
    {
        for (int i = 0; i < _cache.Length; i++)
        {
            var c = _cache[i];
            if (c.renderer == null) continue;
            c.renderer.GetPropertyBlock(_mpb);

            if (c.hasBase)
            {
                Color blended = Color.Lerp(c.baseColor, tintColor, Mathf.Clamp01(tintStrength));
                _mpb.SetColor(c.basePid, blended);
            }

            if (useEmission && c.hasEmis)
                _mpb.SetColor(c.emisPid, tintColor * Mathf.Max(0f, emissionIntensity));

            c.renderer.SetPropertyBlock(_mpb);
        }
    }

    private void Restore()
    {
        for (int i = 0; i < _cache.Length; i++)
        {
            var c = _cache[i];
            if (c.renderer == null) continue;
            c.renderer.GetPropertyBlock(_mpb);

            if (c.hasBase) _mpb.SetColor(c.basePid, c.baseColor);
            if (useEmission && c.hasEmis) _mpb.SetColor(c.emisPid, c.emisColor);

            c.renderer.SetPropertyBlock(_mpb);
        }
    }
}
