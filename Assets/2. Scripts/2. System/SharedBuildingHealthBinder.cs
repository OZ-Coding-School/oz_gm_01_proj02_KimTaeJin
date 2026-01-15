using UnityEngine;

[DisallowMultipleComponent]
public sealed class SharedBuildingHealthBinder : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private SharedBuildingHealth sharedHealth;
    [SerializeField] private Transform centerRoot;

    [Header("Tuning")]
    [SerializeField] private bool bindCenter = true;
    [SerializeField] private bool bindTowers = true;
    [SerializeField] private float refreshInterval = 0.25f;

    private RunScope _scope;
    private float _timer;

    private void OnEnable()
    {
        RunScopeLocator.Changed += OnScopeChanged;
        TryBind();
    }

    private void OnDisable()
    {
        RunScopeLocator.Changed -= OnScopeChanged;
        _scope = null;
    }

    private void OnScopeChanged(RunScope scope)
    {
        TryBind();
    }

    private void TryBind()
    {
        _scope = RunScopeLocator.Current;
        if (sharedHealth == null)
        {
            sharedHealth = GetComponent<SharedBuildingHealth>();
            if (sharedHealth == null && _scope != null)
                sharedHealth = _scope.GetComponent<SharedBuildingHealth>();
        }

        if (centerRoot == null && _scope != null && _scope.Grid != null)
            centerRoot = _scope.Grid.Anchor;

        _timer = 0f;
    }

    private void Update()
    {
        if (sharedHealth == null) return;

        _timer -= Time.deltaTime;
        if (_timer > 0f) return;
        _timer = Mathf.Max(0.05f, refreshInterval);

        if (bindCenter)
        {
            if (centerRoot == null && _scope != null && _scope.Grid != null)
                centerRoot = _scope.Grid.Anchor;
            EnsureProxy(centerRoot);
        }

        if (bindTowers && _scope != null && _scope.Entities != null)
        {
            var towers = _scope.Entities.Towers;
            for (int i = 0; i < towers.Count; i++)
            {
                var tower = towers[i];
                if (tower == null) continue;
                EnsureProxy(tower.transform);
            }
        }
    }

    private void EnsureProxy(Transform target)
    {
        if (target == null) return;
        var proxy = target.GetComponent<SharedBuildingHealthProxy>();
        if (proxy == null)
            proxy = target.gameObject.AddComponent<SharedBuildingHealthProxy>();
        proxy.SetShared(sharedHealth);
    }
}
