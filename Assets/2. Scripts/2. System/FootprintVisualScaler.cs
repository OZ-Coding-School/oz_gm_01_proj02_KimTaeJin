using UnityEngine;

[DisallowMultipleComponent]
public sealed class FootprintVisualScaler : MonoBehaviour
{
    [Header("Sources")]
    [SerializeField] private GridSystem grid;
    [SerializeField] private BaseFootprintReserver baseFootprint;

    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector2Int footprint = new Vector2Int(1, 1);
    [SerializeField] private bool useBaseFootprint = true;
    [SerializeField] private bool keepYScale = true;

    [Header("Apply")]
    [SerializeField] private bool applyOnStart = true;
    [SerializeField] private bool applyOnValidate = true;

    private Vector3 _baseScale;
    private Bounds _baseBounds;
    private bool _cached;

    private void Awake()
    {
        CacheBase();
        if (applyOnStart)
            Apply();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!applyOnValidate) return;
        CacheBase(force: true);
        if (!Application.isPlaying)
            Apply();
    }
#endif

    public void Apply()
    {
        if (target == null) return;

        GridSystem gridSystem = grid != null ? grid : RunScopeLocator.Current?.Grid;
        if (gridSystem == null) return;

        Vector2Int size = footprint;
        if (useBaseFootprint && baseFootprint != null && baseFootprint.UseFixedFootprint)
        {
            if (baseFootprint.UseFootprintMask && baseFootprint.FixedFootprintMask != null)
                size = baseFootprint.FixedFootprintMask.Size;
            else
                size = baseFootprint.FixedFootprintSize;
        }

        size.x = Mathf.Max(1, size.x);
        size.y = Mathf.Max(1, size.y);

        if (_baseBounds.size.x <= 0.0001f || _baseBounds.size.z <= 0.0001f)
            return;

        float targetX = size.x * gridSystem.CellSize;
        float targetZ = size.y * gridSystem.CellSize;

        float sx = targetX / _baseBounds.size.x;
        float sz = targetZ / _baseBounds.size.z;

        Vector3 scale = _baseScale;
        scale.x *= sx;
        scale.z *= sz;
        if (!keepYScale)
            scale.y *= Mathf.Min(sx, sz);

        target.localScale = scale;
    }

    private void CacheBase(bool force = false)
    {
        if (target == null)
            target = transform;
        if (_cached && !force) return;

        _baseScale = target.localScale;
        var renderers = target.GetComponentsInChildren<Renderer>(true);
        _baseBounds = GetBounds(renderers);
        _cached = true;
    }

    private static Bounds GetBounds(Renderer[] renderers)
    {
        bool has = false;
        Bounds b = new Bounds(Vector3.zero, Vector3.zero);
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            if (!has)
            {
                b = r.bounds;
                has = true;
            }
            else
            {
                b.Encapsulate(r.bounds);
            }
        }
        return b;
    }
}
