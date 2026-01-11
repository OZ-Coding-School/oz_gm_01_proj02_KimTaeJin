using UnityEngine;
using Cinemachine;

public sealed class VCamAutoFollow : MonoBehaviour
{
    [SerializeField] private string targetTag = "Player";
    private CinemachineVirtualCamera _vcam;

    private void Awake()
    {
        _vcam = GetComponent<CinemachineVirtualCamera>();
    }

    private void LateUpdate()
    {
        if (_vcam == null) return;
        if (_vcam.Follow != null) return;

        var go = GameObject.FindWithTag(targetTag);
        if (go != null) _vcam.Follow = go.transform;
    }
}
