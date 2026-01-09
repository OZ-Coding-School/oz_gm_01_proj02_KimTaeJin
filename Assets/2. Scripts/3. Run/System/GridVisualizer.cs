using UnityEngine;

public sealed class GridVisualizer : MonoBehaviour
{
    [Header("Line Look")]
    [SerializeField] private Material lineMaterial;
    [SerializeField] private float lineWidth = 0.03f;
    [SerializeField] private float y = 0.05f;

    private RunScope _scope;
    private LineRenderer[] _lines;
    private bool _visible;

    public void Construct(RunScope scope)
    {
        _scope = scope;

        if (lineMaterial == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            lineMaterial = new Material(shader);
        }

        if (_scope?.Events != null)
            _scope.Events.BuildModeChanged += OnBuildModeChanged;

        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (_scope?.Events != null)
            _scope.Events.BuildModeChanged -= OnBuildModeChanged;
    }

    private void OnBuildModeChanged(bool on)
    {
        SetVisible(on);
    }

    private void SetVisible(bool on)
    {
        _visible = on;

        if (!on)
        {
            if (_lines != null)
                for (int i = 0; i < _lines.Length; i++)
                    if (_lines[i] != null) _lines[i].gameObject.SetActive(false);

            return;
        }

        EnsureLines();
        for (int i = 0; i < _lines.Length; i++)
            if (_lines[i] != null) _lines[i].gameObject.SetActive(true);
    }

    private void Update()
    {
        if (!_visible) return;
        if (_scope == null || _scope.Grid == null) return;

        Vector3 origin = _scope.Grid.Origin;
        float size = _scope.Grid.CellSize;
        int w = _scope.Grid.Width;
        int h = _scope.Grid.Height;

        float x0 = origin.x;
        float z0 = origin.z;
        float x1 = x0 + w * size;
        float z1 = z0 + h * size;

        int idx = 0;

        // 세로줄 (w+1)
        for (int i = 0; i <= w; i++)
        {
            float x = x0 + i * size;
            SetLine(_lines[idx++], new Vector3(x, y, z0), new Vector3(x, y, z1));
        }

        // 가로줄 (h+1)
        for (int j = 0; j <= h; j++)
        {
            float z = z0 + j * size;
            SetLine(_lines[idx++], new Vector3(x0, y, z), new Vector3(x1, y, z));
        }
    }

    private void EnsureLines()
    {
        if (_lines != null && _lines.Length > 0) return;
        if (_scope == null || _scope.Grid == null) return;

        int w = _scope.Grid.Width;
        int h = _scope.Grid.Height;

        int count = (w + 1) + (h + 1);
        _lines = new LineRenderer[count];

        var root = new GameObject("[GridLines]").transform;
        root.SetParent(transform, false);

        for (int i = 0; i < count; i++)
        {
            var go = new GameObject($"L{i}");
            go.transform.SetParent(root, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.material = lineMaterial;
            lr.widthMultiplier = lineWidth;
            lr.useWorldSpace = true;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            _lines[i] = lr;
        }
    }

    private static void SetLine(LineRenderer lr, Vector3 a, Vector3 b)
    {
        if (lr == null) return;
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);
    }
}
