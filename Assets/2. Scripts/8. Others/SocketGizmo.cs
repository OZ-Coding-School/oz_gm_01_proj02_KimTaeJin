using UnityEngine;

public sealed class SocketGizmo : MonoBehaviour
{
    [SerializeField] private Color color = Color.cyan;

    [Header("Visibility")]
    [SerializeField] private float liftY = 0.5f;      
    [SerializeField] private float size = 0.6f;       
    [SerializeField] private bool drawLines = true;   
    [SerializeField] private bool includeInactive = true;
    [SerializeField] private bool onlyLeaf = true;

    private void OnDrawGizmos()
    {
        Gizmos.color = color;

        var trs = GetComponentsInChildren<Transform>(includeInactive);
        for (int i = 0; i < trs.Length; i++)
        {
            var t = trs[i];
            if (t == transform) continue;
            if (onlyLeaf && t.childCount > 0) continue;

            Vector3 p = t.position;
            Vector3 pLift = p + Vector3.up * liftY;

 
            Gizmos.DrawWireCube(pLift, Vector3.one * size);
            if (drawLines)
                Gizmos.DrawLine(pLift, p);
        }
    }
}
