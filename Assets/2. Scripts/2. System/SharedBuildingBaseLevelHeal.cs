using UnityEngine;

[DisallowMultipleComponent]
public sealed class SharedBuildingBaseLevelHeal : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private SharedBuildingHealth sharedHealth;
    [SerializeField] private ResourceProgression progression;
    [SerializeField] private bool autoFind = true;

    [Header("회복")]
    [SerializeField, Range(0f, 1f)] private float healPercentPerLevel = 0.2f;
    [SerializeField] private bool ignoreIfDead = true;

    private void OnEnable()
    {
        RunScopeLocator.Changed += OnScopeChanged;
        Bind();
    }

    private void OnDisable()
    {
        RunScopeLocator.Changed -= OnScopeChanged;
        Unbind();
    }

    private void OnScopeChanged(RunScope scope)
    {
        Bind();
    }

    private void Bind()
    {
        Unbind();
        Resolve();

        if (progression != null)
            progression.BaseLevelUp += OnBaseLevelUp;
    }

    private void Unbind()
    {
        if (progression != null)
            progression.BaseLevelUp -= OnBaseLevelUp;
    }

    private void Resolve()
    {
        if (!autoFind) return;

        if (sharedHealth == null)
        {
            var scope = RunScopeLocator.Current;
            if (scope != null) sharedHealth = scope.GetComponent<SharedBuildingHealth>();
            if (sharedHealth == null) sharedHealth = FindObjectOfType<SharedBuildingHealth>();
        }

        if (progression == null)
        {
            var scope = RunScopeLocator.Current;
            if (scope != null) progression = scope.Progression;
            if (progression == null) progression = FindObjectOfType<ResourceProgression>();
        }
    }

    private void OnBaseLevelUp(int level)
    {
        TryHeal();
    }

    private void TryHeal()
    {
        if (sharedHealth == null) return;
        if (ignoreIfDead && sharedHealth.IsDead) return;

        float percent = Mathf.Clamp01(healPercentPerLevel);
        if (percent <= 0f) return;

        int amount = Mathf.RoundToInt(sharedHealth.Max * percent);
        if (amount <= 0) return;

        sharedHealth.Heal(amount);
    }
}
