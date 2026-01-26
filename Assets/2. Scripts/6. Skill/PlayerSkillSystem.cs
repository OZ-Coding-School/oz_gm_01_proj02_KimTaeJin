using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerSkillSystem : MonoBehaviour
{
    [Header("Base Skill")]
    [SerializeField] private PlayerSkillDefinitionSO baseSkill;

    private RunScope _scope;
    private PlayerMeleeAutoAttack _melee;
    private readonly List<SkillState> _skills = new();
    private readonly Dictionary<string, SkillState> _skillById = new();
    private bool _initialized;

    public IReadOnlyList<SkillState> Skills => _skills;
    public PlayerSkillDefinitionSO BaseSkill => baseSkill;

    public event System.Action SkillsChanged;

    public void Construct(RunScope scope)
    {
        _scope = scope;
        _melee = _scope != null ? _scope.Entities?.Player?.Melee : null;
        if (_melee == null)
            _melee = GetComponent<PlayerMeleeAutoAttack>();
        Initialize();
    }

    private void Awake()
    {
        if (_melee == null)
            _melee = GetComponent<PlayerMeleeAutoAttack>();
        Initialize();
    }

    private void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        EnsureBaseSkill();
    }

    private void EnsureBaseSkill()
    {
        if (baseSkill == null) return;
        if (_skillById.ContainsKey(baseSkill.id)) return;
        SkillState state = BuildBaseState(baseSkill);
        AddSkillState(state);
        ApplyBaseSkillToMelee(state);
        SkillsChanged?.Invoke();
    }

    public bool HasSkill(PlayerSkillDefinitionSO def)
    {
        if (def == null || string.IsNullOrEmpty(def.id)) return false;
        return _skillById.ContainsKey(def.id);
    }

    public bool TryGetSkill(PlayerSkillDefinitionSO def, out SkillState state)
    {
        state = null;
        if (def == null || string.IsNullOrEmpty(def.id)) return false;
        return _skillById.TryGetValue(def.id, out state);
    }

    public bool CanApplyOption(PlayerSkillOptionSO option)
    {
        if (option == null || option.targetSkill == null) return false;
        bool has = HasSkill(option.targetSkill);
        return option.optionType == PlayerSkillOptionSO.OptionType.AddSkill ? !has : has;
    }

    public bool ApplyOption(PlayerSkillOptionSO option)
    {
        if (!CanApplyOption(option)) return false;

        if (option.optionType == PlayerSkillOptionSO.OptionType.AddSkill)
        {
            SkillState state = BuildNewSkillState(option.targetSkill);
            AddSkillState(state);
            SkillsChanged?.Invoke();
            return true;
        }

        if (TryGetSkill(option.targetSkill, out SkillState target))
        {
            target.ApplyDelta(option.damageDelta, option.attackSpeedDelta, option.rangeDelta, option.levelDelta);
            if (target.Definition == baseSkill)
                ApplyBaseSkillToMelee(target);
            SkillsChanged?.Invoke();
            return true;
        }

        return false;
    }

    public bool TryGetSnapshot(PlayerSkillDefinitionSO def, out SkillSnapshot snapshot)
    {
        snapshot = default;
        if (!TryGetSkill(def, out SkillState state)) return false;
        snapshot = SkillSnapshot.FromState(state);
        return true;
    }

    public bool TryGetPreview(PlayerSkillOptionSO option, out SkillSnapshot current, out SkillSnapshot preview)
    {
        current = default;
        preview = default;
        if (option == null || option.targetSkill == null) return false;

        if (option.optionType == PlayerSkillOptionSO.OptionType.AddSkill)
        {
            if (HasSkill(option.targetSkill)) return false;
            preview = SkillSnapshot.FromDefinition(option.targetSkill);
            preview.Level = Mathf.Max(1, option.levelDelta);
            return true;
        }

        if (!TryGetSkill(option.targetSkill, out SkillState state)) return false;
        current = SkillSnapshot.FromState(state);
        preview = current;
        preview.Level = Mathf.Max(1, preview.Level + option.levelDelta);
        preview.Damage = Mathf.Max(0, preview.Damage + option.damageDelta);
        preview.AttackSpeed = Mathf.Max(0.01f, preview.AttackSpeed + option.attackSpeedDelta);
        preview.Range = Mathf.Max(0.01f, preview.Range + option.rangeDelta);
        return true;
    }

    private SkillState BuildBaseState(PlayerSkillDefinitionSO def)
    {
        if (_melee != null)
        {
            _melee.GetCombatStats(out float range, out float hitRadius, out float aps, out int damage);
            return new SkillState(def, 1, damage, aps, hitRadius);
        }

        return new SkillState(def, 1, def.baseDamage, def.baseAttackSpeed, def.baseRange);
    }

    private SkillState BuildNewSkillState(PlayerSkillDefinitionSO def)
    {
        return new SkillState(def, 1, def.baseDamage, def.baseAttackSpeed, def.baseRange);
    }

    private void AddSkillState(SkillState state)
    {
        if (state == null || state.Definition == null) return;
        _skills.Add(state);
        _skillById[state.Definition.id] = state;
    }

    private void ApplyBaseSkillToMelee(SkillState state)
    {
        if (_melee == null || state == null) return;
        float range = Mathf.Max(0.01f, state.Range);
        float aps = Mathf.Max(0.01f, state.AttackSpeed);
        int dmg = Mathf.Max(0, state.Damage);
        _melee.SetCombatStats(range, range, aps, dmg);
    }

    public sealed class SkillState
    {
        public PlayerSkillDefinitionSO Definition { get; }
        public int Level { get; private set; }
        public int Damage { get; private set; }
        public float AttackSpeed { get; private set; }
        public float Range { get; private set; }

        public SkillState(PlayerSkillDefinitionSO def, int level, int damage, float attackSpeed, float range)
        {
            Definition = def;
            Level = Mathf.Max(1, level);
            Damage = Mathf.Max(0, damage);
            AttackSpeed = Mathf.Max(0.01f, attackSpeed);
            Range = Mathf.Max(0.01f, range);
        }

        public void ApplyDelta(int damageDelta, float attackSpeedDelta, float rangeDelta, int levelDelta)
        {
            Level = Mathf.Max(1, Level + levelDelta);
            Damage = Mathf.Max(0, Damage + damageDelta);
            AttackSpeed = Mathf.Max(0.01f, AttackSpeed + attackSpeedDelta);
            Range = Mathf.Max(0.01f, Range + rangeDelta);
        }
    }

    public struct SkillSnapshot
    {
        public PlayerSkillDefinitionSO Definition;
        public int Level;
        public int Damage;
        public float AttackSpeed;
        public float Range;

        public static SkillSnapshot FromState(SkillState state)
        {
            return new SkillSnapshot
            {
                Definition = state.Definition,
                Level = state.Level,
                Damage = state.Damage,
                AttackSpeed = state.AttackSpeed,
                Range = state.Range
            };
        }

        public static SkillSnapshot FromDefinition(PlayerSkillDefinitionSO def)
        {
            return new SkillSnapshot
            {
                Definition = def,
                Level = 1,
                Damage = def.baseDamage,
                AttackSpeed = def.baseAttackSpeed,
                Range = def.baseRange
            };
        }
    }
}
