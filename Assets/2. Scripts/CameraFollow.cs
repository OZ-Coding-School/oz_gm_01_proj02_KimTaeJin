using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public string targetTag = "Player"; 
    public Vector3 offset = new Vector3(0f, 15f, -10f);
    public float smoothSpeed = 5f;

    private Transform target;

    void LateUpdate()
    {
        if (target == null)
        {
            GameObject playerObj = GameObject.FindWithTag(targetTag);
            if (playerObj != null) target = playerObj.transform;
            return;
        }

        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
    }
}