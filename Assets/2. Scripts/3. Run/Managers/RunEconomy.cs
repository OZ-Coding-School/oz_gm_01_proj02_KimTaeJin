using System;

public sealed class RunEconomy : IDisposable
{
    public int Gold { get; private set; }
    public event Action<int> OnGoldChanged;

    public RunEconomy(int startGold)
    {
        Gold = Math.Max(0, startGold);
    }

    public bool CanAfford(int cost) => Gold >= Math.Max(0, cost);

    public void AddGold(int amount)
    {
        if (amount <= 0) return;
        Gold += amount;
        OnGoldChanged?.Invoke(Gold);
    }

    public bool Spend(int cost)
    {
        cost = Math.Max(0, cost);
        if (Gold < cost) return false;
        Gold -= cost;
        OnGoldChanged?.Invoke(Gold);
        return true;
    }

    public void Dispose()
    {
        OnGoldChanged = null;
    }
}
