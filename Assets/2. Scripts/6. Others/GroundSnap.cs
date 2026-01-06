using UnityEngine;

public static class GroundSnap
{
    public static bool TrySnapToGround(
        Transform tr,
        Collider col,
        LayerMask groundMask,
        float rayStartHeight = 30f,
        float extraOffset = 0.02f)
    {
        Vector3 basePos = tr.position;
        Vector3 origin = basePos + Vector3.up * rayStartHeight;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayStartHeight * 2f, groundMask, QueryTriggerInteraction.Ignore))
        {
            float halfHeight = 0f;
            if (col != null) halfHeight = col.bounds.extents.y;

            tr.position = new Vector3(basePos.x, hit.point.y + halfHeight + extraOffset, basePos.z);
            return true;
        }

        return false;
    }
}
