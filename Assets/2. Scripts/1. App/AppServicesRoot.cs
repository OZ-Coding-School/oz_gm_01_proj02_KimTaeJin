using System;

public sealed class AppServicesRoot : IDisposable
{
    public PoolService Pool { get; private set; }

    public void Initialize()
    {
        Pool = new PoolService();
    }

    public void Dispose() { }
}
