using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SharedBuildingHealth : MonoBehaviour
{
    [SerializeField] private int maxHp = 100;
    [SerializeField] private int currentHp = 100;
    [SerializeField] private bool resetOnEnable = true;

    public int Max => maxHp;
    public int Current => currentHp;
    public bool IsDead => currentHp <= 0;

    public event Action<int, int> HpChanged;
    public event Action Dead;

    private bool _deadInvoked;

    private void OnEnable()
    {
        if (!resetOnEnable) return;
        currentHp = Mathf.Max(1, maxHp);
        _deadInvoked = false;
        HpChanged?.Invoke(currentHp, maxHp);
    }

    public void Initialize(int maxHealth, int currentHealth = -1)
    {
        maxHp = Mathf.Max(1, maxHealth);
        currentHp = currentHealth < 0 ? maxHp : Mathf.Clamp(currentHealth, 0, maxHp);
        _deadInvoked = currentHp <= 0;
        HpChanged?.Invoke(currentHp, maxHp);
    }

    public void ApplyDamage(int amount)
    {
        if (amount <= 0 || IsDead) return;
        currentHp = Mathf.Max(0, currentHp - amount);
        HpChanged?.Invoke(currentHp, maxHp);
        if (currentHp <= 0 && !_deadInvoked)
        {
            _deadInvoked = true;
            Dead?.Invoke();
        }
    }

    public void Heal(int amount)
    {
        if (amount <= 0 || IsDead) return;
        currentHp = Mathf.Min(maxHp, currentHp + amount);
        HpChanged?.Invoke(currentHp, maxHp);
    }
}
