using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ResourceProgression : MonoBehaviour
{
    [Header("Base EXP")]
    [SerializeField] private int baseLevel = 1;
    [SerializeField] private int baseExp = 0;
    [SerializeField] private int baseExpToLevel = 10;
    [SerializeField] private int baseExpToLevelStep = 5;
    [SerializeField] private int baseExpPerWood = 1;
    [SerializeField] private int baseExpPerStone = 1;

    [Header("Wood Skill")]
    [SerializeField] private int woodLevel = 0;
    [SerializeField] private int woodStored = 0;
    [SerializeField] private int woodPerLevel = 10;
    [SerializeField] private float towerAttackSpeedBonusPerLevel = 0.03f;

    [Header("Stone Skill")]
    [SerializeField] private int stoneLevel = 0;
    [SerializeField] private int stoneStored = 0;
    [SerializeField] private int stoneCount = 0;
    [SerializeField] private int stonePerLevel = 10;

    private RunScope _scope;

    public int BaseLevel => baseLevel;
    public int BaseExp => baseExp;
    public int BaseExpToLevel => baseExpToLevel;
    public int WoodLevel => woodLevel;
    public int WoodStored => woodStored;
    public int WoodPerLevel => woodPerLevel;
    public float WoodAttackSpeedBonusPerLevel => towerAttackSpeedBonusPerLevel;
    public int StoneLevel => stoneLevel;
    public int StoneStored => stoneStored;
    public int StoneCount => stoneCount;
    public int StonePerLevel => stonePerLevel;

    public event Action<int, int, int> BaseExpChanged;
    public event Action<int> BaseLevelUp;
    public event Action<int> WoodLevelUp;
    public event Action<int> StoneLevelUp;
    public event Action<int, int, int> WoodProgressChanged;
    public event Action<int, int> StoneCountChanged;

    public void Construct(RunScope scope)
    {
        _scope = scope;
        ApplyTowerSpeedBonus();
        RaiseBaseExpChanged();
        RaiseWoodProgressChanged();
        RaiseStoneCountChanged();
    }

    public void AddResource(ResourceType type, int amount)
    {
        if (amount <= 0) return;

        switch (type)
        {
            case ResourceType.Wood:
                AddBaseExp(amount * Mathf.Max(0, baseExpPerWood));
                AddWood(amount);
                return;
            case ResourceType.Stone:
                AddBaseExp(amount * Mathf.Max(0, baseExpPerStone));
                AddStone(amount);
                return;
            default:
                return;
        }
    }

    private void AddBaseExp(int amount)
    {
        if (amount <= 0) return;
        baseExp += amount;

        int guard = 0;
        while (baseExpToLevel > 0 && baseExp >= baseExpToLevel && guard < 1000)
        {
            baseExp -= baseExpToLevel;
            baseLevel += 1;
            baseExpToLevel += Mathf.Max(0, baseExpToLevelStep);
            BaseLevelUp?.Invoke(baseLevel);
            guard++;
        }

        RaiseBaseExpChanged();
    }

    private void AddWood(int amount)
    {
        if (amount <= 0) return;
        woodStored += amount;
        if (woodPerLevel <= 0)
        {
            RaiseWoodProgressChanged();
            return;
        }

        while (woodStored >= woodPerLevel)
        {
            woodStored -= woodPerLevel;
            woodLevel += 1;
            ApplyTowerSpeedBonus();
            WoodLevelUp?.Invoke(woodLevel);
        }

        RaiseWoodProgressChanged();
    }

    private void AddStone(int amount)
    {
        if (amount <= 0) return;
        stoneStored += amount;
        stoneCount += amount;
        if (stonePerLevel <= 0)
        {
            RaiseStoneCountChanged();
            return;
        }

        while (stoneStored >= stonePerLevel)
        {
            stoneStored -= stonePerLevel;
            stoneLevel += 1;
            StoneLevelUp?.Invoke(stoneLevel);
        }

        RaiseStoneCountChanged();
    }

    private void ApplyTowerSpeedBonus()
    {
        if (_scope == null) return;
        float bonus = Mathf.Max(0f, towerAttackSpeedBonusPerLevel) * Mathf.Max(0, woodLevel);
        _scope.SetTowerAttackSpeedMultiplier(1f + bonus);
    }

    private void RaiseBaseExpChanged()
    {
        BaseExpChanged?.Invoke(baseLevel, baseExp, baseExpToLevel);
    }

    private void RaiseWoodProgressChanged()
    {
        WoodProgressChanged?.Invoke(woodLevel, woodStored, woodPerLevel);
    }

    private void RaiseStoneCountChanged()
    {
        StoneCountChanged?.Invoke(stoneLevel, stoneCount);
    }
}
