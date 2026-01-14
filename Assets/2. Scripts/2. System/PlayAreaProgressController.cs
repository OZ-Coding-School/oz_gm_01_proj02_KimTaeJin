using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayAreaProgressController : MonoBehaviour
{
    public enum Shape
    {
        Circle = 0,
        Ellipse = 1,
        Capsule = 2
    }

    public enum CapsuleAxis
    {
        X = 0,
        Z = 1
    }

    [Header("Progress")]
    [SerializeField] private bool autoMove = true;
    [SerializeField] private Vector3 moveDirection = new Vector3(0f, 0f, -1f);
    [SerializeField] private float moveSpeed = 0.5f;
    [SerializeField] private bool useUnscaledTime = false;

    [Header("Shape")]
    [SerializeField] private Shape shape = Shape.Circle;
    [SerializeField] private CapsuleAxis capsuleAxis = CapsuleAxis.Z;
    [SerializeField] private Vector2 centerOffset = Vector2.zero;
    [SerializeField] private float radius = 15f;
    [SerializeField] private Vector2 aspect = Vector2.one;
    [SerializeField] private float capsuleHalfLength = 8f;

    [Header("Gizmo")]
    [SerializeField] private bool drawGizmo = true;
    [SerializeField] private bool drawOnlyWhenSelected = true;
    [SerializeField] private int gizmoSegments = 48;
    [SerializeField] private float gizmoYOffset = 0.05f;
    [SerializeField] private Color gizmoColor = new Color(0f, 0.9f, 0.7f, 0.9f);

    public Vector3 Center => transform.TransformPoint(new Vector3(centerOffset.x, 0f, centerOffset.y));
    public float Radius => Mathf.Max(0f, radius);
    public Shape ShapeType => shape;
    public CapsuleAxis CapsuleAxisMode => capsuleAxis;
    public Vector2 CenterOffset => centerOffset;
    public Vector2 Aspect => aspect;
    public float CapsuleHalfLength => capsuleHalfLength;

    private void Update()
    {
        if (!autoMove) return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (dt <= 0f || Mathf.Abs(moveSpeed) <= 0.0001f) return;

        Vector3 dir = moveDirection;
        if (dir.sqrMagnitude < 0.0001f)
            dir = new Vector3(0f, 0f, -1f);

        transform.position += dir.normalized * moveSpeed * dt;
    }

    public bool IsInsideXZ(Vector3 worldPos, float padding = 0f)
    {
        return IsInsideXZ(worldPos, new Vector2(padding, padding));
    }

    public bool IsInsideXZ(Vector3 worldPos, Vector2 padding)
    {
        Vector3 local = transform.InverseTransformPoint(worldPos);
        Vector2 p = new Vector2(local.x - centerOffset.x, local.z - centerOffset.y);
        return IsInsideLocal(p, padding);
    }

    public Vector3 ClampPointXZ(Vector3 worldPos, float padding = 0f)
    {
        return ClampPointXZ(worldPos, new Vector2(padding, padding));
    }

    public Vector3 ClampPointXZ(Vector3 worldPos, Vector2 padding)
    {
        Vector3 local = transform.InverseTransformPoint(worldPos);
        float localY = local.y;
        Vector2 p = new Vector2(local.x - centerOffset.x, local.z - centerOffset.y);
        Vector2 clamped = ClampLocal(p, padding, out _);
        Vector3 localClamped = new Vector3(clamped.x + centerOffset.x, localY, clamped.y + centerOffset.y);
        return transform.TransformPoint(localClamped);
    }

    private bool IsInsideLocal(Vector2 p, Vector2 padding)
    {
        switch (shape)
        {
            case Shape.Ellipse:
                return IsInsideEllipse(p, padding);
            case Shape.Capsule:
                return IsInsideCapsule(p, padding);
            default:
                return IsInsideCircle(p, padding);
        }
    }

    private Vector2 ClampLocal(Vector2 p, Vector2 padding, out bool inside)
    {
        switch (shape)
        {
            case Shape.Ellipse:
                return ClampEllipse(p, padding, out inside);
            case Shape.Capsule:
                return ClampCapsule(p, padding, out inside);
            default:
                return ClampCircle(p, padding, out inside);
        }
    }

    private bool IsInsideCircle(Vector2 p, Vector2 padding)
    {
        Vector2 r = GetCircleRadii(padding);
        float nx = p.x / r.x;
        float nz = p.y / r.y;
        return (nx * nx + nz * nz) <= 1f;
    }

    private Vector2 ClampCircle(Vector2 p, Vector2 padding, out bool inside)
    {
        Vector2 r = GetCircleRadii(padding);
        float nx = p.x / r.x;
        float nz = p.y / r.y;
        float m = nx * nx + nz * nz;
        inside = m <= 1f || m <= 0.0001f;
        if (inside) return p;
        float scale = 1f / Mathf.Sqrt(m);
        return new Vector2(p.x * scale, p.y * scale);
    }

    private bool IsInsideEllipse(Vector2 p, Vector2 padding)
    {
        Vector2 r = GetEllipseRadii(padding);
        float nx = p.x / r.x;
        float nz = p.y / r.y;
        return (nx * nx + nz * nz) <= 1f;
    }

    private Vector2 ClampEllipse(Vector2 p, Vector2 padding, out bool inside)
    {
        Vector2 r = GetEllipseRadii(padding);
        float nx = p.x / r.x;
        float nz = p.y / r.y;
        float m = nx * nx + nz * nz;
        inside = m <= 1f || m <= 0.0001f;
        if (inside) return p;
        float scale = 1f / Mathf.Sqrt(m);
        return new Vector2(p.x * scale, p.y * scale);
    }

    private Vector2 GetCircleRadii(Vector2 padding)
    {
        float padX = Mathf.Max(0f, padding.x);
        float padZ = Mathf.Max(0f, padding.y);
        float rx = Mathf.Max(0.0001f, radius - padX);
        float rz = Mathf.Max(0.0001f, radius - padZ);
        return new Vector2(rx, rz);
    }

    private Vector2 GetEllipseRadii(Vector2 padding)
    {
        float ax = Mathf.Max(0.0001f, aspect.x);
        float az = Mathf.Max(0.0001f, aspect.y);
        float padX = Mathf.Max(0f, padding.x);
        float padZ = Mathf.Max(0f, padding.y);
        float rx = Mathf.Max(0.0001f, radius * ax - padX);
        float rz = Mathf.Max(0.0001f, radius * az - padZ);
        return new Vector2(rx, rz);
    }

    private bool IsInsideCapsule(Vector2 p, Vector2 padding)
    {
        GetCapsuleParams(padding, out float r, out float halfLen);
        return CapsuleContainsPoint(p, r, halfLen, capsuleAxis);
    }

    private Vector2 ClampCapsule(Vector2 p, Vector2 padding, out bool inside)
    {
        GetCapsuleParams(padding, out float r, out float halfLen);
        return CapsuleClampPoint(p, r, halfLen, capsuleAxis, out inside);
    }

    private void GetCapsuleParams(Vector2 padding, out float r, out float halfLen)
    {
        float padX = Mathf.Max(0f, padding.x);
        float padZ = Mathf.Max(0f, padding.y);
        if (capsuleAxis == CapsuleAxis.Z)
        {
            r = Mathf.Max(0.0001f, radius - padX);
            halfLen = Mathf.Max(0f, capsuleHalfLength - padZ);
        }
        else
        {
            r = Mathf.Max(0.0001f, radius - padZ);
            halfLen = Mathf.Max(0f, capsuleHalfLength - padX);
        }
    }

    private static bool CapsuleContainsPoint(Vector2 p, float r, float halfLen, CapsuleAxis axis)
    {
        Vector2 q = (axis == CapsuleAxis.Z) ? p : new Vector2(p.y, p.x);
        float ax = q.x;
        float az = q.y;
        float absZ = Mathf.Abs(az);
        if (absZ <= halfLen)
            return Mathf.Abs(ax) <= r;

        float dz = absZ - halfLen;
        return (ax * ax + dz * dz) <= r * r;
    }

    private static Vector2 CapsuleClampPoint(Vector2 p, float r, float halfLen, CapsuleAxis axis, out bool inside)
    {
        Vector2 q = (axis == CapsuleAxis.Z) ? p : new Vector2(p.y, p.x);
        float ax = q.x;
        float az = q.y;
        float absZ = Mathf.Abs(az);

        if (absZ <= halfLen)
        {
            inside = Mathf.Abs(ax) <= r;
            if (inside) return p;
            ax = Mathf.Clamp(ax, -r, r);
            Vector2 res = new Vector2(ax, az);
            return (axis == CapsuleAxis.Z) ? res : new Vector2(res.y, res.x);
        }

        Vector2 center = new Vector2(0f, Mathf.Sign(az) * halfLen);
        Vector2 d = new Vector2(ax, az) - center;
        float len = d.magnitude;
        inside = len <= r;
        if (!inside && len > 0.0001f)
            d = d / len * r;
        else if (!inside)
            d = new Vector2(0f, r);

        Vector2 outQ = center + d;
        return (axis == CapsuleAxis.Z) ? outQ : new Vector2(outQ.y, outQ.x);
    }

    private void DrawRadiusGizmo()
    {
        if (!drawGizmo) return;
        int seg = Mathf.Clamp(gizmoSegments, 6, 256);
        Gizmos.color = gizmoColor;

        Matrix4x4 prev = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        Vector3 center = new Vector3(centerOffset.x, gizmoYOffset, centerOffset.y);

        switch (shape)
        {
            case Shape.Ellipse:
                DrawEllipseGizmo(center, seg);
                break;
            case Shape.Capsule:
                DrawCapsuleGizmo(center, seg);
                break;
            default:
                DrawCircleGizmo(center, seg);
                break;
        }

        Gizmos.matrix = prev;
    }

    private void DrawCircleGizmo(Vector3 center, int seg)
    {
        DrawCircleGizmo(center, seg, Vector2.zero);
    }

    private void DrawCircleGizmo(Vector3 center, int seg, Vector2 padding)
    {
        Vector2 r = GetCircleRadii(padding);
        if (r.x <= 0.0001f || r.y <= 0.0001f) return;
        float step = Mathf.PI * 2f / seg;
        Vector3 prev = center + new Vector3(Mathf.Cos(0f) * r.x, 0f, Mathf.Sin(0f) * r.y);
        for (int i = 1; i <= seg; i++)
        {
            float a = step * i;
            Vector3 next = center + new Vector3(Mathf.Cos(a) * r.x, 0f, Mathf.Sin(a) * r.y);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }

    private void DrawEllipseGizmo(Vector3 center, int seg)
    {
        DrawEllipseGizmo(center, seg, Vector2.zero);
    }

    private void DrawEllipseGizmo(Vector3 center, int seg, Vector2 padding)
    {
        Vector2 r = GetEllipseRadii(padding);
        float step = Mathf.PI * 2f / seg;
        Vector3 prev = center + new Vector3(Mathf.Cos(0f) * r.x, 0f, Mathf.Sin(0f) * r.y);
        for (int i = 1; i <= seg; i++)
        {
            float a = step * i;
            Vector3 next = center + new Vector3(Mathf.Cos(a) * r.x, 0f, Mathf.Sin(a) * r.y);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }

    private void DrawCapsuleGizmo(Vector3 center, int seg)
    {
        DrawCapsuleGizmo(center, seg, Vector2.zero);
    }

    private void DrawCapsuleGizmo(Vector3 center, int seg, Vector2 padding)
    {
        GetCapsuleParams(padding, out float r, out float halfLen);
        if (r <= 0.0001f) return;

        int arcSeg = Mathf.Max(6, seg / 2);
        if (capsuleAxis == CapsuleAxis.Z)
        {
            Vector3 leftTop = center + new Vector3(-r, 0f, halfLen);
            Vector3 leftBottom = center + new Vector3(-r, 0f, -halfLen);
            Vector3 rightTop = center + new Vector3(r, 0f, halfLen);
            Vector3 rightBottom = center + new Vector3(r, 0f, -halfLen);
            Gizmos.DrawLine(leftTop, leftBottom);
            Gizmos.DrawLine(rightTop, rightBottom);

            DrawArc(center + new Vector3(0f, 0f, halfLen), r, 0f, 180f, arcSeg);
            DrawArc(center + new Vector3(0f, 0f, -halfLen), r, 180f, 360f, arcSeg);
        }
        else
        {
            Vector3 top = center + new Vector3(halfLen, 0f, r);
            Vector3 bottom = center + new Vector3(halfLen, 0f, -r);
            Vector3 topL = center + new Vector3(-halfLen, 0f, r);
            Vector3 bottomL = center + new Vector3(-halfLen, 0f, -r);
            Gizmos.DrawLine(top, topL);
            Gizmos.DrawLine(bottom, bottomL);

            DrawArc(center + new Vector3(halfLen, 0f, 0f), r, -90f, 90f, arcSeg);
            DrawArc(center + new Vector3(-halfLen, 0f, 0f), r, 90f, 270f, arcSeg);
        }
    }

    public void DrawGizmo(Vector2 padding, Color color, float yOffset, int segments)
    {
        int seg = Mathf.Clamp(segments, 6, 256);
        Gizmos.color = color;

        Matrix4x4 prev = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        Vector3 center = new Vector3(centerOffset.x, yOffset, centerOffset.y);
        switch (shape)
        {
            case Shape.Ellipse:
                DrawEllipseGizmo(center, seg, padding);
                break;
            case Shape.Capsule:
                DrawCapsuleGizmo(center, seg, padding);
                break;
            default:
                DrawCircleGizmo(center, seg, padding);
                break;
        }

        Gizmos.matrix = prev;
    }

    private void DrawArc(Vector3 center, float radius, float startDeg, float endDeg, int seg)
    {
        float step = (endDeg - startDeg) / Mathf.Max(1, seg);
        float a0 = startDeg * Mathf.Deg2Rad;
        Vector3 prev = center + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
        for (int i = 1; i <= seg; i++)
        {
            float a = (startDeg + step * i) * Mathf.Deg2Rad;
            Vector3 next = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }

    private void OnDrawGizmos()
    {
        if (drawOnlyWhenSelected) return;
        DrawRadiusGizmo();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawOnlyWhenSelected) return;
        DrawRadiusGizmo();
    }
}
