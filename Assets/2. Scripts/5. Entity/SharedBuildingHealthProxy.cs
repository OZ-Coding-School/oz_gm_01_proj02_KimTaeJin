using UnityEngine;

[DisallowMultipleComponent]
public sealed class SharedBuildingHealthProxy : MonoBehaviour
{
    private static readonly System.Collections.Generic.List<SharedBuildingHealthProxy> _active = new();

    [SerializeField] private SharedBuildingHealth sharedHealth;
    [SerializeField] private bool autoFindShared = true;

    public SharedBuildingHealth SharedHealth => sharedHealth;
    public static System.Collections.Generic.IReadOnlyList<SharedBuildingHealthProxy> Active => _active;

    private void Awake()
    {
        if (autoFindShared && sharedHealth == null)
            ResolveShared();
    }

    private void OnEnable()
    {
        if (!_active.Contains(this))
            _active.Add(this);
    }

    private void OnDisable()
    {
        _active.Remove(this);
    }

    public void SetShared(SharedBuildingHealth shared)
    {
        sharedHealth = shared;
    }

    public void ApplyDamage(int amount)
    {
        if (sharedHealth == null && autoFindShared)
            ResolveShared();
        if (sharedHealth == null) return;
        sharedHealth.ApplyDamage(amount);
        DamageNumberService.TryShow(amount, transform.position);
    }

    public void Heal(int amount)
    {
        if (sharedHealth == null && autoFindShared)
            ResolveShared();
        if (sharedHealth == null) return;
        sharedHealth.Heal(amount);
    }

    private void ResolveShared()
    {
        if (sharedHealth != null) return;

        var scope = RunScopeLocator.Current;
        if (scope != null)
            sharedHealth = scope.GetComponent<SharedBuildingHealth>();

        if (sharedHealth == null)
            sharedHealth = FindObjectOfType<SharedBuildingHealth>();
    }
}
