using UnityEngine;

[DisallowMultipleComponent]
public sealed class TowerPassiveBuff : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] private TowerEntity tower;

    private RunScope _scope;
    private TowerDefinitionSO _def;
    private bool _applied;
    private float _expGainBonus;
    private float _towerDamageBonus;
    private float _towerAttackSpeedBonus;

    private void Awake()
    {
        if (tower == null) tower = GetComponent<TowerEntity>();
    }

    private void OnEnable()
    {
        RunScopeLocator.Changed += OnScopeChanged;
        TryApply();
    }

    private void OnDisable()
    {
        RunScopeLocator.Changed -= OnScopeChanged;
        Remove();
    }

    private void Start()
    {
        TryApply();
    }

    public void Configure(TowerDefinitionSO def)
    {
        _def = def;
        if (!isActiveAndEnabled) return;
        if (_applied) Remove();
        TryApply();
    }

    private void OnScopeChanged(RunScope scope)
    {
        Remove();
        _scope = scope;
        TryApply();
    }

    private void TryApply()
    {
        if (_applied) return;
        _scope = _scope != null ? _scope : RunScopeLocator.Current;
        if (_scope == null) return;

        TowerDefinitionSO def = _def;
        if (def == null && tower != null)
            def = tower.Definition;
        if (def == null) return;

        _expGainBonus = def.passiveExpGainBonus;
        _towerDamageBonus = def.passiveTowerDamageBonus;
        _towerAttackSpeedBonus = def.passiveTowerAttackSpeedBonus;

        _scope.AddExpGainBonus(_expGainBonus);
        _scope.AddTowerDamageBonus(_towerDamageBonus);
        _scope.AddTowerAttackSpeedBonus(_towerAttackSpeedBonus);
        _applied = true;
    }

    private void Remove()
    {
        if (!_applied) return;
        if (_scope != null)
        {
            _scope.AddExpGainBonus(-_expGainBonus);
            _scope.AddTowerDamageBonus(-_towerDamageBonus);
            _scope.AddTowerAttackSpeedBonus(-_towerAttackSpeedBonus);
        }
        _applied = false;
    }
}
