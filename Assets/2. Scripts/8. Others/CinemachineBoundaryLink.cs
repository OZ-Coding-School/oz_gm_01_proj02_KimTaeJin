using UnityEngine;
using Cinemachine;

[DisallowMultipleComponent]
public sealed class CinemachineBoundaryLink : MonoBehaviour
{
    [SerializeField] private PlayAreaBoundary boundary;
    [SerializeField] private CinemachineConfiner confiner;
    [SerializeField] private bool autoFindBoundary = true;
    [SerializeField] private bool autoFindConfiner = true;

    private void Awake()
    {
        ResolveRefs();
        Sync();
    }

    private void OnEnable()
    {
        if (boundary != null)
            boundary.BoundaryChanged += Sync;
    }

    private void OnDisable()
    {
        if (boundary != null)
            boundary.BoundaryChanged -= Sync;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveRefs();
        Sync();
    }
#endif

    private void ResolveRefs()
    {
        if (confiner == null && autoFindConfiner)
            confiner = GetComponent<CinemachineConfiner>();

        if (boundary == null && autoFindBoundary)
            boundary = FindObjectOfType<PlayAreaBoundary>();
    }

    private void Sync()
    {
        if (confiner == null || boundary == null) return;
        confiner.m_BoundingVolume = boundary.Collider;
        confiner.m_ConfineMode = CinemachineConfiner.Mode.Confine3D;
        confiner.InvalidatePathCache();
    }
}
