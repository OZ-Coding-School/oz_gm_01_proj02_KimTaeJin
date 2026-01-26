using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerEntity : MonoBehaviour
{

    [Header("Base Stats (ONLY HERE)")]
    [SerializeField] private float _baseMoveSpeed = 12f;
    [SerializeField] private int _maxHp = 100;

    private RunScope _scope;

    public PlayerController Controller { get; private set; }
    public AbilitySystem Ability { get; private set; }
    public PlayerMeleeAutoAttack Melee { get; private set; }
    public PlayerHarvestAutoAttack Harvest { get; private set; }
    public PlayerExperience Experience { get; private set; }
    public PlayerSkillSystem Skills { get; private set; }
    public HealthComponent Health { get; private set; }

    public void Construct(RunScope scope)
    {
        _scope = scope;

        Controller = GetOrAdd<PlayerController>();
        Controller.SetBaseMoveSpeed(_baseMoveSpeed); 

        Ability = GetOrAdd<AbilitySystem>();
        Ability.Construct(_scope);

        Melee = GetOrAdd<PlayerMeleeAutoAttack>();
        Melee.Construct(_scope);
        Harvest = GetOrAdd<PlayerHarvestAutoAttack>();
        Harvest.Construct(_scope);
        Experience = GetOrAdd<PlayerExperience>();
        Skills = GetOrAdd<PlayerSkillSystem>();
        Skills.Construct(_scope);

        Health = GetOrAdd<HealthComponent>();
        Health.Initialize(Mathf.Max(1, _maxHp), null);

        EnsureBoundaryDamage();
    }
    public void SetMoveSpeedMultiplier(float mul) => Controller?.SetMoveSpeedMultiplier(mul);
    public void AddMoveSpeedMultiplier(float add) => Controller?.AddMoveSpeedMultiplier(add);

    private T GetOrAdd<T>() where T : Component
    {
        var c = GetComponent<T>();
        if (c == null) c = gameObject.AddComponent<T>();
        return c;
    }

    private void EnsureBoundaryDamage()
    {
        var boundary = GetComponent<BoundaryDamage>();
        if (boundary != null) return;
        boundary = gameObject.AddComponent<BoundaryDamage>();
        boundary.Configure(30, 0.5f, 0f);
    }
}
