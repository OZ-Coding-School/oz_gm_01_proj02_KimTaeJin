using System;
using UnityEngine;

public sealed class AppServicesRoot : IDisposable
{
    public PoolService Pool { get; private set; }

    public void Initialize(Transform poolRoot)
    {
        Pool = new PoolService(poolRoot);
    }


    public void Dispose() { }
}
