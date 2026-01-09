using System;
using UnityEngine;

public sealed class RunEventBus : IDisposable
{
    public event Action<TowerDefinitionSO> PlaceTowerRequested;
    public event Action<bool> BuildModeChanged; 

    public void RequestPlaceTower(TowerDefinitionSO def)
        => PlaceTowerRequested?.Invoke(def);

    public void SetBuildMode(bool on)
        => BuildModeChanged?.Invoke(on);

    public void Dispose()
    {
        PlaceTowerRequested = null;
        BuildModeChanged = null;
    }
}
