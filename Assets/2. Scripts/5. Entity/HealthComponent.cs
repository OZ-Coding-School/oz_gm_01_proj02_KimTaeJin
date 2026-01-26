using System;
using UnityEngine;

public sealed class HealthComponent : MonoBehaviour
{
    [SerializeField] private int maxHp = 20;   
    [SerializeField] private bool resetOnEnable = true;

    public int Max => maxHp;
    public int Current { get; private set; }

    public event Action<int, int> HpChanged;

    private Action _onDead;
    private bool _dead;

    private void OnEnable()
    {
        if (resetOnEnable)
        {
            _dead = false;
            Current = maxHp;
            HpChanged?.Invoke(Current, maxHp);
        }
    }

    public void Initialize(int hp, Action onDead)
    {
        maxHp = hp;           
        Current = maxHp;
        _onDead = onDead;
        _dead = false;
        HpChanged?.Invoke(Current, maxHp);
    }

    public void ApplyDamage(int amount)
    {
        if (_dead) return;

        Current -= amount;
        HpChanged?.Invoke(Current, maxHp);
        if (Current <= 0)
        {
            _dead = true;
            _onDead?.Invoke();
        }
    }

    public void Heal(int amount)
    {
        if (_dead || amount <= 0) return;
        Current = Mathf.Min(maxHp, Current + amount);
        HpChanged?.Invoke(Current, maxHp);
    }
}
