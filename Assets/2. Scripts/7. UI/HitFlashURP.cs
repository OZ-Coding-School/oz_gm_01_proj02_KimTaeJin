using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class HitFlashURP : MonoBehaviour
{
    [Header("Flash")]
    [SerializeField] private int pulses = 2;
    [SerializeField] private float onTime = 0.05f;
    [SerializeField] private float offTime = 0.03f;

    [Header("BaseColor (URP Lit)")]
    [SerializeField] private Color flashBaseColor = Color.white;

    [Header("Emission (needs Emission enabled on material for best results)")]
    [SerializeField] private bool useEmission = true;
    [SerializeField] private float emissionIntensity = 3.0f; 

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private Renderer[] _renderers;
    private MaterialPropertyBlock _mpb;
    private Coroutine _co;

    private struct Cache
    {
        public int basePid;
        public int emisPid;
        public Color baseColor;
        public Color emisColor;
        public bool hasBase;
        public bool hasEmis;
    }

    private Cache[] _cache;

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        CacheAll();
    }

    private void OnEnable()
    {
        if (_renderers == null || _renderers.Length == 0) CacheAll();
    }

    private void CacheAll()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        _cache = new Cache[_renderers.Length];

        for (int i = 0; i < _renderers.Length; i++)
        {
            var r = _renderers[i];
            var mat = r ? r.sharedMaterial : null;
            if (useEmission && mat != null && mat.HasProperty("_EmissionColor"))
                mat.EnableKeyword("_EMISSION");
            var c = new Cache
            {
                basePid = BaseColorId,
                emisPid = EmissionColorId,
                baseColor = Color.white,
                emisColor = Color.black,
                hasBase = false,
                hasEmis = false
            };

            if (mat != null)
            {
                if (mat.HasProperty(BaseColorId))
                {
                    c.hasBase = true;
                    c.baseColor = mat.GetColor(BaseColorId);
                }
                if (mat.HasProperty(EmissionColorId))
                {
                    c.hasEmis = true;
                    c.emisColor = mat.GetColor(EmissionColorId);
                }
            }

            _cache[i] = c;
        }
    }

    public void Play()
    {
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(CoFlash());
    }

    private IEnumerator CoFlash()
    {
        if (_renderers == null || _renderers.Length == 0) CacheAll();

        Color baseC = flashBaseColor;
        Color emisC = flashBaseColor * emissionIntensity;

        for (int p = 0; p < pulses; p++)
        {
            SetAll(baseC, emisC);
            if (onTime > 0f) yield return new WaitForSeconds(onTime);

            RestoreAll();
            if (offTime > 0f) yield return new WaitForSeconds(offTime);
        }

        RestoreAll();
        _co = null;
    }
    private void OnDisable()
    {
        StopAndRestore();
    }

    private void SetAll(Color baseColor, Color emissionColor)
    {
        for (int i = 0; i < _renderers.Length; i++)
        {
            var r = _renderers[i];
            if (!r) continue;

            var c = _cache[i];

            r.GetPropertyBlock(_mpb);

            if (c.hasBase) _mpb.SetColor(c.basePid, baseColor);
            if (useEmission && c.hasEmis) _mpb.SetColor(c.emisPid, emissionColor);

            r.SetPropertyBlock(_mpb);
        }
    }

    private void RestoreAll()
    {
        if (_renderers == null || _cache == null || _mpb == null) return;
        for (int i = 0; i < _renderers.Length; i++)
        {
            var r = _renderers[i];
            if (!r) continue;

            var c = _cache[i];

            r.GetPropertyBlock(_mpb);

            if (c.hasBase) _mpb.SetColor(c.basePid, c.baseColor);
            if (useEmission && c.hasEmis) _mpb.SetColor(c.emisPid, c.emisColor);

            r.SetPropertyBlock(_mpb);
        }
    }
    public void StopAndRestore()
    {
        if (_co != null)
        {
            StopCoroutine(_co);
            _co = null;
        }
        RestoreAll();
    }

}
