using UnityEngine;
using Cinemachine;

public sealed class VCamAutoFollow : MonoBehaviour
{
    [SerializeField] private string targetTag = "Player";
    [SerializeField] private bool setLookAt = true;
    [SerializeField] private bool useRunScopeFallback = true;
    private CinemachineVirtualCamera _vcam;

    private void Awake()
    {
        _vcam = GetComponent<CinemachineVirtualCamera>();
    }

    private void LateUpdate()
    {
        if (_vcam == null) return;
        if (_vcam.Follow == null)
        {
            var target = ResolveTarget();
            if (target != null)
                _vcam.Follow = target;
        }

        if (setLookAt && _vcam.LookAt == null && _vcam.Follow != null)
            _vcam.LookAt = _vcam.Follow;
    }

    private Transform ResolveTarget()
    {
        if (!string.IsNullOrEmpty(targetTag))
        {
            var go = GameObject.FindWithTag(targetTag);
            if (go != null) return go.transform;
        }

        if (useRunScopeFallback)
        {
            var p = RunScopeLocator.Current?.Entities?.Player;
            if (p != null) return p.transform;
        }

        return null;
    }
}
