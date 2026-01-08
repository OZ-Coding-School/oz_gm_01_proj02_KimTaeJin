using UnityEngine;

public sealed class HouseDrift : MonoBehaviour
{
    [SerializeField] float speed = 2f;
    [SerializeField] Vector3 dir = Vector3.back; 

    void Update()
    {
        transform.position += dir.normalized * (speed * Time.deltaTime);
    }
}
