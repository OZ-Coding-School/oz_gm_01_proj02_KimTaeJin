using System.Collections.Generic;
using UnityEngine;

public sealed class BaseFootprintOverlay : MonoBehaviour
{
    [SerializeField] private Material overlayMaterial;
    [SerializeField] private float y = 0.04f;
    [SerializeField] private float cellShrink = 0.08f;
    [SerializeField] private Color color = new Color(0.2f, 0.75f, 1f, 0.35f);

    private RunScope _scope;
    private BaseFootprintReserver _reserver;

    private GameObject _go;
    private MeshRenderer _mr;
    private Mesh _mesh;
    private bool _visible;

    private readonly List<Vector3> _verts = new();
    private readonly List<int> _tris = new();
    private readonly List<Vector2> _uvs = new();

    public void Construct(RunScope scope)
    {
        _scope = scope;
        _reserver = GetComponent<BaseFootprintReserver>();

        if (overlayMaterial == null)
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            overlayMaterial = new Material(shader);
        }

        if (_scope?.Events != null)
            _scope.Events.BuildModeChanged += on => SetVisible(on);

        SetVisible(false);
    }

    private void SetVisible(bool on)
    {
        _visible = on;
        if (!on)
        {
            if (_go != null) _go.SetActive(false);
            return;
        }

        EnsureObjects();
        _go.SetActive(true);
        RebuildMesh();
    }

    private void EnsureObjects()
    {
        if (_go != null) return;

        _go = new GameObject("[BaseFootprintOverlay]");
        _go.transform.SetParent(transform, false);

        var mf = _go.AddComponent<MeshFilter>();
        _mr = _go.AddComponent<MeshRenderer>();
        _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _mr.receiveShadows = false;
        _mr.material = overlayMaterial;

        _mesh = new Mesh { name = "BaseFootprintOverlayMesh" };
        _mesh.MarkDynamic();
        mf.sharedMesh = _mesh;
    }

    private void RebuildMesh()
    {
        if (_mesh == null || _scope == null || _scope.Grid == null) return;
        if (_reserver == null) return;

        if (!_reserver.TryGetCellRect(out Vector2Int min, out Vector2Int max))
        {
            _mesh.Clear();
            return;
        }

        _verts.Clear(); _tris.Clear(); _uvs.Clear();

        float size = _scope.Grid.CellSize;
        float half = size * 0.5f;
        float inset = half * Mathf.Clamp01(cellShrink);

        int v = 0;
        for (int yCell = min.y; yCell <= max.y; yCell++)
            for (int xCell = min.x; xCell <= max.x; xCell++)
            {
                Vector3 c = _scope.Grid.CellToWorldCenter(new Vector2Int(xCell, yCell));

                float x0 = c.x - half + inset;
                float x1 = c.x + half - inset;
                float z0 = c.z - half + inset;
                float z1 = c.z + half - inset;

                _verts.Add(new Vector3(x0, y, z0));
                _verts.Add(new Vector3(x0, y, z1));
                _verts.Add(new Vector3(x1, y, z1));
                _verts.Add(new Vector3(x1, y, z0));

                _uvs.Add(new Vector2(0, 0));
                _uvs.Add(new Vector2(0, 1));
                _uvs.Add(new Vector2(1, 1));
                _uvs.Add(new Vector2(1, 0));

                _tris.Add(v + 0); _tris.Add(v + 1); _tris.Add(v + 2);
                _tris.Add(v + 0); _tris.Add(v + 2); _tris.Add(v + 3);
                v += 4;
            }

        _mesh.Clear();
        _mesh.SetVertices(_verts);
        _mesh.SetTriangles(_tris, 0);
        _mesh.SetUVs(0, _uvs);
        _mesh.RecalculateBounds();

        if (_mr != null && _mr.material != null)
        {
            var m = _mr.material;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        }
    }
}
