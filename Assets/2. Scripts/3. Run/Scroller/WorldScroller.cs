using UnityEngine;

public sealed class WorldScroller : MonoBehaviour
{
    [SerializeField] private float scrollSpeed = 2f;
    [SerializeField] private bool useUnscaledTime = false;

    public float ProgressDistance { get; private set; }

    private void Update()
    {
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float dz = scrollSpeed * dt;
        transform.position += Vector3.forward * dz; 

        ProgressDistance += dz;
    }
}
