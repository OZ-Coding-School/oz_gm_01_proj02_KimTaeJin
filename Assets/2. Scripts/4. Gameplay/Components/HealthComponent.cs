using System;
using UnityEngine;

public sealed class HealthComponent : MonoBehaviour
{
    public int Current { get; private set; }
    private Action _onDead;
    private bool _dead;

    public void Initialize(int hp, Action onDead)
    {
        Current = hp;
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
            Debug.Log("[Health] Dead");
            _onDead?.Invoke();
        }
    }
}
