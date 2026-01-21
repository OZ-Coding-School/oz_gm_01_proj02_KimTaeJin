using UnityEngine;

[DisallowMultipleComponent]
public sealed class RestStopMarker : MonoBehaviour
{
    [SerializeField] private Transform point;

    public Transform Point => point != null ? point : transform;
}
