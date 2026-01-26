using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerExpLevelHeal : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private PlayerExperience experience;
    [SerializeField] private HealthComponent health;
    [SerializeField] private bool autoFind = true;

    [Header("회복")]
    [SerializeField, Range(0f, 1f)] private float healPercentPerLevel = 0.4f;
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
        ResolveRefs();

        if (experience != null)
            experience.LevelUp += OnLevelUp;
    }

    private void Unbind()
    {
        if (experience != null)
            experience.LevelUp -= OnLevelUp;
    }

    private void ResolveRefs()
    {
        if (!autoFind) return;

        if (experience == null || health == null)
        {
            var player = RunScopeLocator.Current?.Entities?.Player;
            if (player != null)
            {
                experience = player.Experience;
                health = player.Health;
            }
        }
    }

    private void OnLevelUp(int level)
    {
        TryHeal();
    }

    private void TryHeal()
    {
        if (health == null) return;
        if (ignoreIfDead && health.Current <= 0) return;

        float percent = Mathf.Clamp01(healPercentPerLevel);
        if (percent <= 0f) return;

        int amount = Mathf.RoundToInt(health.Max * percent);
        if (amount <= 0) return;

        health.Heal(amount);
    }
}
