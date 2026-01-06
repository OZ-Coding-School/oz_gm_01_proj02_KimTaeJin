using System;
using UnityEngine;

public sealed class HealthComponent : MonoBehaviour
{
    [SerializeField] private int maxHp = 20;   
    [SerializeField] private bool resetOnEnable = true;

    public int Max => maxHp;
    public int Current { get; private set; }

    private Action _onDead;
    private bool _dead;

    private void OnEnable()
    {
        if (resetOnEnable)
        {
            _dead = false;
            Current = maxHp;
        }
    }

    public void Initialize(int hp, Action onDead)
    {
        maxHp = hp;           
        Current = maxHp;
        _onDead = onDead;
        _dead = false;
    }

    public void ApplyDamage(int amount)
    {
        if (_dead) return;

        Current -= amount;
        if (Current <= 0)
        {
            _dead = true;
            _onDead?.Invoke();
        }
    }
}
