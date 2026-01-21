using UnityEngine;

[DisallowMultipleComponent]
public sealed class RestStopGate : MonoBehaviour
{
    public enum GateType
    {
        Enter = 0,
        Exit = 1
    }

    [SerializeField] private GateType gateType = GateType.Enter;
    [SerializeField] private RestStopSystem restStopSystem;
    [SerializeField] private bool requirePlayer = true;
    [SerializeField] private string playerTag = "Player";

    private void Awake()
    {
        if (restStopSystem == null)
            restStopSystem = FindObjectOfType<RestStopSystem>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (requirePlayer && !IsPlayer(other)) return;
        if (restStopSystem == null) return;

        if (gateType == GateType.Enter)
            restStopSystem.EnterRestStop();
        else
            restStopSystem.ExitRestStop();
    }

    private bool IsPlayer(Collider other)
    {
        if (other == null) return false;
        if (!string.IsNullOrEmpty(playerTag) && other.CompareTag(playerTag)) return true;
        return other.GetComponentInParent<PlayerController>() != null;
    }
}
