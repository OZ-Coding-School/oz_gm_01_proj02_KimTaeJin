using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerExperience : MonoBehaviour
{
    [SerializeField] private int level = 1;
    [SerializeField] private int exp = 0;
    [SerializeField] private int expToLevel = 10;
    [SerializeField] private int expToLevelStep = 5;

    public int Level => level;
    public int Exp => exp;
    public int ExpToLevel => expToLevel;

    public event Action<int> LevelUp;
    public event Action<int, int, int> ExpChanged;

    public void AddExp(int amount)
    {
        if (amount <= 0) return;
        exp += amount;

        int guard = 0;
        while (expToLevel > 0 && exp >= expToLevel && guard < 1000)
        {
            exp -= expToLevel;
            level += 1;
            expToLevel += Mathf.Max(0, expToLevelStep);
            LevelUp?.Invoke(level);
            guard++;
        }

        ExpChanged?.Invoke(level, exp, expToLevel);
    }
}
