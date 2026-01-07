using UnityEngine;

[DisallowMultipleComponent]
public sealed class DepositZone : MonoBehaviour
{
    [SerializeField] private Transform depositTarget; 

    private void Awake()
    {
        if (depositTarget == null) depositTarget = transform;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryDeposit(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryDeposit(other);
    }

    private void TryDeposit(Collider other)
    {
        var train = other.GetComponentInParent<PickupTrain>();
        if (train == null) return;

        train.DepositTo(depositTarget);
    }
}
