using UnityEngine;

[DisallowMultipleComponent]
public sealed class SharedBuildingHealthProxy : MonoBehaviour
{
    [SerializeField] private SharedBuildingHealth sharedHealth;
    [SerializeField] private bool autoFindShared = true;

    public SharedBuildingHealth SharedHealth => sharedHealth;

    private void Awake()
    {
        if (autoFindShared && sharedHealth == null)
            ResolveShared();
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
