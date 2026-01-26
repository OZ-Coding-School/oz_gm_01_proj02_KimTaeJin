using UnityEngine;

[DisallowMultipleComponent]
public sealed class SharedBuildingStoneRepair : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private SharedBuildingHealth sharedHealth;
    [SerializeField] private ResourceProgression progression;
    [SerializeField] private bool autoFind = true;

    [Header("회복")]
    [SerializeField] private int hpPerStone = 1;

    private bool _handling;
    private bool _dead;

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
        _dead = false;
        Resolve();

        if (sharedHealth != null)
        {
            sharedHealth.HpChanged += OnHpChanged;
            sharedHealth.Dead += OnDead;
        }

        if (progression != null)
            progression.StoneCountChanged += OnStoneCountChanged;

        TryHeal();
    }

    private void Unbind()
    {
        if (sharedHealth != null)
        {
            sharedHealth.HpChanged -= OnHpChanged;
            sharedHealth.Dead -= OnDead;
        }

        if (progression != null)
            progression.StoneCountChanged -= OnStoneCountChanged;
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

    private void OnHpChanged(int current, int max)
    {
        TryHeal();
    }

    private void OnStoneCountChanged(int level, int count)
    {
        TryHeal();
    }

    private void OnDead()
    {
        _dead = true;
    }

    private void TryHeal()
    {
        if (_dead || _handling) return;
        if (sharedHealth == null || progression == null) return;

        int missing = sharedHealth.Max - sharedHealth.Current;
        if (missing <= 0) return;

        int stones = progression.StoneCount;
        if (stones <= 0) return;

        int perStone = Mathf.Max(1, hpPerStone);
        int need = Mathf.CeilToInt(missing / (float)perStone);
        int consume = Mathf.Min(stones, need);
        if (consume <= 0) return;

        _handling = true;
        progression.ConsumeStone(consume);
        sharedHealth.Heal(consume * perStone);
        _handling = false;
    }
}
