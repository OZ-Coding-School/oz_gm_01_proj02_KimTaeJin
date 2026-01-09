using UnityEngine;

public sealed class PlacementSystem : MonoBehaviour
{
    [Header("Ray")]
    [SerializeField] private Camera cam;

    private RunScope _scope;
    private TowerDefinitionSO _selected;
    private bool _placing;

    private GameObject _preview;
    private Renderer[] _previewRenderers;
    private MaterialPropertyBlock _mpb;
    private Quaternion _rot = Quaternion.identity;

    public void Construct(RunScope scope)
    {
        _scope = scope;
        _mpb = new MaterialPropertyBlock();

        if (_scope != null && _scope.Events != null)
            _scope.Events.PlaceTowerRequested += BeginPlace;
    }

    private void OnDestroy()
    {
        if (_scope != null && _scope.Events != null)
            _scope.Events.PlaceTowerRequested -= BeginPlace;

        if (_scope != null && _scope.Events != null)
            _scope.Events.SetBuildMode(false);

        DestroyPreview();
    }

    private void Update()
    {
        if (_scope == null) return;

        if (!_placing && Input.GetKeyDown(KeyCode.Alpha1))
        {
            var cat = GameRoot.Instance != null ? GameRoot.Instance.TowerCatalog : null;
            if (cat != null && cat.Length > 0 && cat[0] != null)
                BeginPlace(cat[0]);
        }

        if (!_placing) return;

        if (Input.GetKeyDown(KeyCode.R))
            _rot = Quaternion.Euler(0f, (_rot.eulerAngles.y + 90f) % 360f, 0f);

        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            Cancel();
            return;
        }

        if (!TryGetMouseGroundPoint(out Vector3 p)) return;

        Vector2Int cell = _scope.Grid.WorldToCell(p);

        if (_scope.TowerBuild.TryGetPlacementPos(_selected, cell, out Vector3 placePos))
        {
            EnsurePreview();
            if (_preview != null)
                _preview.transform.SetPositionAndRotation(placePos, _rot);
        }

        bool can = _scope.TowerBuild.CanPlace(_selected, cell);
        TintPreview(can);

        if (Input.GetMouseButtonDown(0) && can)
        {
            bool placed = _scope.TowerBuild.TryPlaceTower(_selected, cell, _rot);
            if (placed)
            {
            }
        }
    }

    private void BeginPlace(TowerDefinitionSO def)
    {
        if (def == null || def.prefab == null) return;

        _selected = def;
        _placing = true;
        _rot = Quaternion.identity;

        RebuildPreview();

        if (_scope != null && _scope.Events != null)
            _scope.Events.SetBuildMode(true);
    }

    private void Cancel()
    {
        _placing = false;
        _selected = null;

        if (_scope != null && _scope.Events != null)
            _scope.Events.SetBuildMode(false);

        DestroyPreview();
    }

    private bool TryGetMouseGroundPoint(out Vector3 point)
    {
        point = default;

        if (cam == null) cam = Camera.main;
        if (cam == null) return false;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        int mask = (GameRoot.Instance != null) ? GameRoot.Instance.GroundMask.value : ~0;
        if (Physics.Raycast(ray, out RaycastHit hit, 500f, mask, QueryTriggerInteraction.Ignore))
        {
            point = hit.point;
            return true;
        }

        return false;
    }

    private void RebuildPreview()
    {
        DestroyPreview();
        EnsurePreview();
    }

    private void EnsurePreview()
    {
        if (_preview != null || _selected == null) return;

        _preview = Instantiate(_selected.prefab.gameObject);
        _preview.name = "[TowerPreview]";
        DisableGameplayOnPreview(_preview);

        _previewRenderers = _preview.GetComponentsInChildren<Renderer>(true);
    }

    private void DisableGameplayOnPreview(GameObject go)
    {
        foreach (var c in go.GetComponentsInChildren<Collider>(true))
            c.enabled = false;

        foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            mb.enabled = false;

        int ignore = LayerMask.NameToLayer("Ignore Raycast");
        if (ignore >= 0)
        {
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = ignore;
        }
    }

    private void TintPreview(bool canPlace)
    {
        if (_previewRenderers == null) return;

        Color c = canPlace
            ? new Color(0.2f, 1f, 0.2f, 0.6f)
            : new Color(1f, 0.2f, 0.2f, 0.6f);

        int baseColor = Shader.PropertyToID("_BaseColor");
        int color = Shader.PropertyToID("_Color");

        foreach (var r in _previewRenderers)
        {
            if (r == null) continue;

            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(baseColor, c);
            _mpb.SetColor(color, c);
            r.SetPropertyBlock(_mpb);
        }
    }

    private void DestroyPreview()
    {
        if (_preview != null) Destroy(_preview);
        _preview = null;
        _previewRenderers = null;
    }
}