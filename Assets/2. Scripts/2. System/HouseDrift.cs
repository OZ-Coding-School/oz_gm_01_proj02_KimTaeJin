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

    private static readonly RaycastHit[] _hits = new RaycastHit[8];

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
        Vector3 origin = (checkOrigin != null ? checkOrigin.position : transform.position)
            + checkOffset + (forward * checkForwardOffset);
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
            if (hit.collider.transform.IsChildOf(transform)) continue;
            return true;
        }

        return false;
    }
}
