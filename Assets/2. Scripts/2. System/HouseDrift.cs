using System.Collections.Generic;
using UnityEngine;

public sealed class HouseDrift : MonoBehaviour
{
    [SerializeField] float speed = 2f;
    [SerializeField] Vector3 dir = Vector3.back;

    [Header("Obstacle Stop")]
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private float checkDistance = 1.2f;
    [SerializeField] private float checkRadius = 0.4f;
    [SerializeField] private Vector3 checkOffset = new Vector3(0f, 0.3f, 0f);
    [SerializeField] private Transform checkOrigin;
    [SerializeField] private float checkForwardOffset = 0f;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
    [SerializeField] private bool useBoundsProbe = true;
    [SerializeField] private Transform boundsRoot;
    [SerializeField] private float boundsPadding = 0.05f;

    [Header("Tile Filter")]
    [SerializeField] private bool useTileRealOnly = true;
    [SerializeField] private string[] tileNamePrefixes = { "Tile_", "RoadTile_" };

    [Header("Gizmos")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private bool drawGizmosSelectedOnly = true;
    [SerializeField] private Color gizmoBoundsColor = new Color(1f, 0.8f, 0.2f, 0.25f);
    [SerializeField] private Color gizmoCastColor = new Color(1f, 0.2f, 0.2f, 0.5f);

    private static readonly RaycastHit[] _hits = new RaycastHit[8];
    private static readonly Collider[] _overlapHits = new Collider[16];
    private readonly List<Bounds> _probeBounds = new List<Bounds>(32);

    public Vector3 Direction => dir;
    public float Speed => speed;

    private void Awake()
    {
        if (obstacleMask.value != 0) return;
        int harvest = LayerMask.NameToLayer("Harvest");
        if (harvest >= 0 && harvest < 32)
            obstacleMask = 1 << harvest;
    }

    void Update()
    {
        if (IsBlocked()) return;
        transform.position += dir.normalized * (speed * Time.deltaTime);
    }

    private bool IsBlocked()
    {
        Vector3 forward = dir.normalized;
        if (forward.sqrMagnitude <= 0.0001f) return false;

        if (useBoundsProbe)
        {
            Transform root = ResolveBoundsRoot();
            if (root != null)
            {
                bool hasProbe;
                if (IsBoundsProbeBlocked(root, forward, out hasProbe))
                    return true;
                if (useTileRealOnly && hasProbe)
                    return false;
            }
            if (useTileRealOnly)
                return false;
        }

        Vector3 fallbackOrigin = (checkOrigin != null ? checkOrigin.position : transform.position)
            + checkOffset + (forward * checkForwardOffset);
        return ProbeBlocked(fallbackOrigin, forward, transform);
    }

    private bool IsBoundsOverlapping(Bounds bounds, Transform ignoreRoot)
    {
        int mask = obstacleMask.value != 0 ? obstacleMask.value : ~0;
        int hitCount = Physics.OverlapBoxNonAlloc(
            bounds.center,
            bounds.extents,
            _overlapHits,
            Quaternion.identity,
            mask,
            triggerInteraction);

        for (int i = 0; i < hitCount; i++)
        {
            var col = _overlapHits[i];
            if (col == null) continue;
            if (ignoreRoot != null && col.transform.IsChildOf(ignoreRoot)) continue;
            return true;
        }

        return false;
    }

    private bool IsBoundsAheadBlocked(Bounds bounds, Vector3 forward, Transform ignoreRoot)
    {
        int mask = obstacleMask.value != 0 ? obstacleMask.value : ~0;
        float dist = Mathf.Max(0.01f, checkDistance);
        Vector3 origin = bounds.center + checkOffset + (forward * checkForwardOffset);
        float pad = Mathf.Max(0f, checkRadius);
        Vector3 halfExtents = bounds.extents + new Vector3(pad, pad, pad);

        int hitCount = Physics.BoxCastNonAlloc(
            origin,
            halfExtents,
            forward,
            _hits,
            Quaternion.identity,
            dist,
            mask,
            triggerInteraction);

        for (int i = 0; i < hitCount; i++)
        {
            var hit = _hits[i];
            if (hit.collider == null) continue;
            if (ignoreRoot != null && hit.collider.transform.IsChildOf(ignoreRoot)) continue;
            return true;
        }

        return false;
    }

    private bool ProbeBlocked(Vector3 origin, Vector3 forward, Transform ignoreRoot)
    {
        int mask = obstacleMask.value != 0 ? obstacleMask.value : ~0;

        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            Mathf.Max(0.01f, checkRadius),
            forward,
            _hits,
            Mathf.Max(0.01f, checkDistance),
            mask,
            triggerInteraction);

        for (int i = 0; i < hitCount; i++)
        {
            var hit = _hits[i];
            if (hit.collider == null) continue;
            if (ignoreRoot != null && hit.collider.transform.IsChildOf(ignoreRoot)) continue;
            return true;
        }

        return false;
    }

    private Transform ResolveBoundsRoot()
    {
        if (boundsRoot != null) return boundsRoot;
        var scope = RunScopeLocator.Current;
        if (scope != null && scope.Grid != null && scope.Grid.Anchor != null)
            return scope.Grid.Anchor;
        return transform;
    }

    private bool IsBoundsProbeBlocked(Transform root, Vector3 forward, out bool hasProbe)
    {
        if (!TryGetProbeBounds(root, _probeBounds))
        {
            hasProbe = false;
            return false;
        }
        hasProbe = true;

        for (int i = 0; i < _probeBounds.Count; i++)
        {
            Bounds bounds = _probeBounds[i];
            if (boundsPadding > 0f)
                bounds.Expand(boundsPadding * 2f);

            if (IsBoundsOverlapping(bounds, root))
                return true;
            if (IsBoundsAheadBlocked(bounds, forward, root))
                return true;
        }

        return false;
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmosSelectedOnly)
            DrawGizmosInternal();
    }

    private void OnDrawGizmosSelected()
    {
        if (drawGizmosSelectedOnly)
            DrawGizmosInternal();
    }

    private void DrawGizmosInternal()
    {
        if (!drawGizmos) return;

        Vector3 forward = dir.normalized;
        if (forward.sqrMagnitude <= 0.0001f) return;

        if (useBoundsProbe)
        {
            Transform root = ResolveBoundsRoot();
            if (root != null && TryGetProbeBounds(root, _probeBounds))
            {
                float dist = Mathf.Max(0.01f, checkDistance);
                float pad = Mathf.Max(0f, checkRadius);

                for (int i = 0; i < _probeBounds.Count; i++)
                {
                    Bounds bounds = _probeBounds[i];
                    if (boundsPadding > 0f)
                        bounds.Expand(boundsPadding * 2f);

                    Gizmos.color = gizmoBoundsColor;
                    Gizmos.DrawWireCube(bounds.center, bounds.size);

                    Vector3 origin = bounds.center + checkOffset + (forward * checkForwardOffset);
                    Vector3 halfExtents = bounds.extents + new Vector3(pad, pad, pad);
                    Vector3 end = origin + forward * dist;

                    Gizmos.color = gizmoCastColor;
                    Gizmos.DrawWireCube(origin, halfExtents * 2f);
                    Gizmos.DrawWireCube(end, halfExtents * 2f);
                    Gizmos.DrawLine(origin, end);
                }
                return;
            }
        }

        Vector3 fallbackOrigin = (checkOrigin != null ? checkOrigin.position : transform.position)
            + checkOffset + (forward * checkForwardOffset);
        Vector3 fallbackEnd = fallbackOrigin + forward * Mathf.Max(0.01f, checkDistance);

        Gizmos.color = gizmoCastColor;
        Gizmos.DrawWireSphere(fallbackOrigin, Mathf.Max(0.01f, checkRadius));
        Gizmos.DrawWireSphere(fallbackEnd, Mathf.Max(0.01f, checkRadius));
        Gizmos.DrawLine(fallbackOrigin, fallbackEnd);
    }

    private bool IsTileRealChild(Transform tr)
    {
        if (!useTileRealOnly) return true;
        if (tr == null) return false;

        if (tileNamePrefixes == null || tileNamePrefixes.Length == 0)
            return false;

        Transform current = tr;
        while (current != null)
        {
            string name = current.name;
            if (!string.IsNullOrEmpty(name))
            {
                for (int i = 0; i < tileNamePrefixes.Length; i++)
                {
                    string prefix = tileNamePrefixes[i];
                    if (string.IsNullOrEmpty(prefix)) continue;
                    if (name.StartsWith(prefix))
                        return true;
                }
            }
            current = current.parent;
        }

        return false;
    }

    private bool TryGetProbeBounds(Transform root, List<Bounds> results)
    {
        results.Clear();
        if (root == null) return false;

        var colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            var col = colliders[i];
            if (col == null || !col.enabled) continue;
            if (!IsTileRealChild(col.transform)) continue;
            results.Add(col.bounds);
        }

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null || !r.enabled) continue;
            if (r.TryGetComponent<Collider>(out _)) continue;
            if (!IsTileRealChild(r.transform)) continue;
            results.Add(r.bounds);
        }

        return results.Count > 0;
    }

}
