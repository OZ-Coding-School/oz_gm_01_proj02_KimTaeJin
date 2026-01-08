using UnityEngine;

public static class GroundSnap
{

    public static bool TrySnapToGround(
        Transform tr,
        Collider col,
        LayerMask groundMask,
        float rayStartHeight = 30f,
        float extraOffset = 0.02f,
        bool debugLog = false)
    {
        if (tr == null) return false;
        if (groundMask.value == 0) return false;

        Vector3 basePos = tr.position;
        Vector3 origin = basePos + Vector3.up * rayStartHeight;

        var hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            rayStartHeight * 2f,
            groundMask,
            QueryTriggerInteraction.Ignore);

        if (hits == null || hits.Length == 0)
        {
        }

        bool found = false;
        RaycastHit best = default;

        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h.collider == null) continue;

            if (h.collider.transform.IsChildOf(tr)) continue;
            if (!found || h.point.y > best.point.y)
            {
                best = h;
                found = true;
            }
        }

        if (col != null)
        {
            float delta = (best.point.y + extraOffset) - col.bounds.min.y;
            tr.position = basePos + Vector3.up * delta;
        }
        else
        {
            tr.position = new Vector3(basePos.x, best.point.y + extraOffset, basePos.z);
        }

        return true;
    }
}
