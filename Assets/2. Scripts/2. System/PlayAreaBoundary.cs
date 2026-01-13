using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayAreaBoundary : MonoBehaviour
{
    [Header("Polygon (local XZ)")]
    [SerializeField] private List<Vector2> points = new List<Vector2>();

    [Header("Volume")]
    [SerializeField] private float bottomY = -1f;
    [SerializeField] private float topY = 6f;

    [Header("Collider (auto)")]
    [SerializeField] private bool autoCreateCollider = true;
    [SerializeField] private bool colliderIsTrigger = true;
    [SerializeField] private MeshCollider meshCollider;

    [Header("Rebuild")]
    [SerializeField] private bool rebuildOnEnable = true;
    [SerializeField] private bool rebuildOnValidate = true;

    private Mesh _mesh;
    private bool _isCCW;
    private Vector2[] _edgeNormals;

    public event Action BoundaryChanged;

    public IReadOnlyList<Vector2> Points => points;
    public MeshCollider Collider => meshCollider;

    private void Reset()
    {
        if (points == null || points.Count < 3)
        {
            points = new List<Vector2>
            {
                new Vector2(-6f, -4f),
                new Vector2(-2f, -6f),
                new Vector2(2f, -6f),
                new Vector2(6f, -4f),
                new Vector2(6f, 4f),
                new Vector2(2f, 6f),
                new Vector2(-2f, 6f),
                new Vector2(-6f, 4f)
            };
        }
        Rebuild();
    }

    private void OnEnable()
    {
        if (rebuildOnEnable)
            Rebuild();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (rebuildOnValidate)
            Rebuild();
    }
#endif

    private void OnDestroy()
    {
        if (_mesh == null) return;
        if (Application.isPlaying)
            Destroy(_mesh);
        else
            DestroyImmediate(_mesh);
    }

    public void Rebuild()
    {
        EnsureCollider();
        NormalizeHeights();

        if (!BuildMesh())
        {
            ApplyMesh(null);
            BoundaryChanged?.Invoke();
            return;
        }

        ApplyMesh(_mesh);
        BoundaryChanged?.Invoke();
    }

    public bool IsInsideXZ(Vector3 worldPos)
    {
        if (points == null || points.Count < 3) return false;
        Vector3 local = transform.InverseTransformPoint(worldPos);
        Vector2 p = new Vector2(local.x, local.z);
        return IsInsideLocal(p);
    }

    public Vector3 ClampInsideXZ(Vector3 worldPos, float radius)
    {
        if (points == null || points.Count < 3) return worldPos;

        Vector3 local = transform.InverseTransformPoint(worldPos);
        Vector2 p = new Vector2(local.x, local.z);

        if (IsInsideLocal(p))
            return worldPos;

        Vector2 insideNormal;
        Vector2 closest = GetClosestPointOnEdges(p, out insideNormal);
        Vector2 clamped = closest + insideNormal * Mathf.Max(0f, radius);
        Vector3 localClamped = new Vector3(clamped.x, local.y, clamped.y);
        return transform.TransformPoint(localClamped);
    }

    private void EnsureCollider()
    {
        if (meshCollider == null && autoCreateCollider)
            meshCollider = GetComponent<MeshCollider>();

        if (meshCollider == null && autoCreateCollider)
            meshCollider = gameObject.AddComponent<MeshCollider>();

        if (meshCollider != null)
            meshCollider.isTrigger = colliderIsTrigger;
    }

    private void NormalizeHeights()
    {
        if (topY < bottomY)
        {
            float tmp = topY;
            topY = bottomY;
            bottomY = tmp;
        }
    }

    private void ApplyMesh(Mesh mesh)
    {
        if (meshCollider == null) return;
        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = mesh;
    }

    private bool BuildMesh()
    {
        if (points == null || points.Count < 3) return false;

        float area = ComputeSignedArea(points);
        if (Mathf.Abs(area) < 0.0001f) return false;
        _isCCW = area > 0f;

        CacheEdgeNormals();
        EnsureMesh();

        int count = points.Count;
        var verts = new List<Vector3>(count * 2);
        for (int i = 0; i < count; i++)
        {
            Vector2 p = points[i];
            verts.Add(new Vector3(p.x, bottomY, p.y));
            verts.Add(new Vector3(p.x, topY, p.y));
        }

        var tris = new List<int>(count * 12);

        for (int i = 0; i < count; i++)
        {
            int next = (i + 1) % count;
            int i0 = i * 2;
            int i1 = i * 2 + 1;
            int j0 = next * 2;
            int j1 = next * 2 + 1;

            tris.Add(i0);
            tris.Add(i1);
            tris.Add(j1);

            tris.Add(i0);
            tris.Add(j1);
            tris.Add(j0);
        }

        var topTriangles = new List<int>();
        if (Triangulate(points, _isCCW, topTriangles))
        {
            for (int i = 0; i < topTriangles.Count; i += 3)
            {
                int a = topTriangles[i];
                int b = topTriangles[i + 1];
                int c = topTriangles[i + 2];

                tris.Add(a * 2 + 1);
                tris.Add(b * 2 + 1);
                tris.Add(c * 2 + 1);

                tris.Add(c * 2);
                tris.Add(b * 2);
                tris.Add(a * 2);
            }
        }

        _mesh.Clear();
        _mesh.SetVertices(verts);
        _mesh.SetTriangles(tris, 0);
        _mesh.RecalculateBounds();
        return true;
    }

    private void EnsureMesh()
    {
        if (_mesh != null) return;
        _mesh = new Mesh { name = "PlayAreaBoundaryMesh" };
        _mesh.MarkDynamic();
    }

    private void CacheEdgeNormals()
    {
        int count = points.Count;
        if (_edgeNormals == null || _edgeNormals.Length != count)
            _edgeNormals = new Vector2[count];

        for (int i = 0; i < count; i++)
        {
            Vector2 a = points[i];
            Vector2 b = points[(i + 1) % count];
            Vector2 edge = (b - a).normalized;
            Vector2 left = new Vector2(-edge.y, edge.x);
            _edgeNormals[i] = _isCCW ? left : -left;
        }
    }

    private bool IsInsideLocal(Vector2 p)
    {
        int count = points.Count;
        if (count < 3) return false;

        for (int i = 0; i < count; i++)
        {
            Vector2 a = points[i];
            Vector2 b = points[(i + 1) % count];
            if (IsPointOnSegment(p, a, b)) return true;
        }

        bool inside = false;
        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            Vector2 a = points[i];
            Vector2 b = points[j];
            bool intersect = ((a.y > p.y) != (b.y > p.y)) &&
                (p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y + 0.000001f) + a.x);
            if (intersect) inside = !inside;
        }

        return inside;
    }

    private Vector2 GetClosestPointOnEdges(Vector2 p, out Vector2 insideNormal)
    {
        insideNormal = Vector2.zero;
        float best = float.MaxValue;
        Vector2 bestPoint = p;

        int count = points.Count;
        for (int i = 0; i < count; i++)
        {
            Vector2 a = points[i];
            Vector2 b = points[(i + 1) % count];
            Vector2 closest = ClosestPointOnSegment(p, a, b);
            float d = (p - closest).sqrMagnitude;
            if (d < best)
            {
                best = d;
                bestPoint = closest;
                insideNormal = (_edgeNormals != null && _edgeNormals.Length == count) ? _edgeNormals[i] : Vector2.zero;
            }
        }

        if (insideNormal.sqrMagnitude < 0.0001f)
            insideNormal = Vector2.up;

        return bestPoint;
    }

    private static Vector2 ClosestPointOnSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float abSqr = ab.sqrMagnitude;
        if (abSqr <= 0.000001f) return a;
        float t = Vector2.Dot(p - a, ab) / abSqr;
        t = Mathf.Clamp01(t);
        return a + ab * t;
    }

    private static bool IsPointOnSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ap = p - a;
        Vector2 ab = b - a;
        float cross = ab.x * ap.y - ab.y * ap.x;
        if (Mathf.Abs(cross) > 0.0005f) return false;
        float dot = Vector2.Dot(ap, ab);
        if (dot < -0.0005f) return false;
        if (dot > ab.sqrMagnitude + 0.0005f) return false;
        return true;
    }

    private static float ComputeSignedArea(IReadOnlyList<Vector2> pts)
    {
        float area = 0f;
        int count = pts.Count;
        for (int i = 0; i < count; i++)
        {
            Vector2 a = pts[i];
            Vector2 b = pts[(i + 1) % count];
            area += a.x * b.y - b.x * a.y;
        }
        return area * 0.5f;
    }

    private static bool Triangulate(IReadOnlyList<Vector2> pts, bool isCCW, List<int> result)
    {
        result.Clear();
        int count = pts.Count;
        if (count < 3) return false;

        var indices = new List<int>(count);
        for (int i = 0; i < count; i++)
            indices.Add(i);

        int guard = 0;
        while (indices.Count > 2 && guard < count * count)
        {
            bool earFound = false;
            for (int i = 0; i < indices.Count; i++)
            {
                int i0 = indices[(i + indices.Count - 1) % indices.Count];
                int i1 = indices[i];
                int i2 = indices[(i + 1) % indices.Count];

                if (!IsConvex(pts[i0], pts[i1], pts[i2], isCCW))
                    continue;

                if (ContainsAnyPoint(pts, indices, i0, i1, i2))
                    continue;

                result.Add(i0);
                result.Add(i1);
                result.Add(i2);
                indices.RemoveAt(i);
                earFound = true;
                break;
            }

            if (!earFound)
                break;

            guard++;
        }

        return result.Count >= 3;
    }

    private static bool IsConvex(Vector2 a, Vector2 b, Vector2 c, bool isCCW)
    {
        Vector2 ab = b - a;
        Vector2 bc = c - b;
        float cross = ab.x * bc.y - ab.y * bc.x;
        return isCCW ? cross > 0.000001f : cross < -0.000001f;
    }

    private static bool ContainsAnyPoint(IReadOnlyList<Vector2> pts, List<int> indices, int i0, int i1, int i2)
    {
        Vector2 a = pts[i0];
        Vector2 b = pts[i1];
        Vector2 c = pts[i2];
        for (int i = 0; i < indices.Count; i++)
        {
            int idx = indices[i];
            if (idx == i0 || idx == i1 || idx == i2) continue;
            if (PointInTriangle(pts[idx], a, b, c)) return true;
        }
        return false;
    }

    private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float area = Sign(p, a, b);
        float area2 = Sign(p, b, c);
        float area3 = Sign(p, c, a);

        bool hasNeg = (area < 0f) || (area2 < 0f) || (area3 < 0f);
        bool hasPos = (area > 0f) || (area2 > 0f) || (area3 > 0f);
        return !(hasNeg && hasPos);
    }

    private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
    }

    private void OnDrawGizmosSelected()
    {
        if (points == null || points.Count < 2) return;
        Gizmos.color = new Color(0f, 0.8f, 0.9f, 0.9f);
        for (int i = 0; i < points.Count; i++)
        {
            Vector2 a = points[i];
            Vector2 b = points[(i + 1) % points.Count];
            Vector3 wa = transform.TransformPoint(new Vector3(a.x, 0f, a.y));
            Vector3 wb = transform.TransformPoint(new Vector3(b.x, 0f, b.y));
            Gizmos.DrawLine(wa, wb);
        }
    }
}
