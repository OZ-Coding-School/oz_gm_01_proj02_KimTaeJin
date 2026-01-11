using System;
using System.Collections.Generic;

public sealed class RunEventBus : IDisposable
{
    public event Action<TowerDefinitionSO> PlaceTowerRequested;
    public event Action<bool> BuildModeChanged;

    private readonly HashSet<object> _buildOwners = new();

    public bool IsBuildMode => _buildOwners.Count > 0;

    public void RequestPlaceTower(TowerDefinitionSO def)
        => PlaceTowerRequested?.Invoke(def);

    public void PushBuildMode(object owner)
    {
        owner ??= this;
        bool wasOff = _buildOwners.Count == 0;
        _buildOwners.Add(owner);
        if (wasOff && _buildOwners.Count > 0)
            BuildModeChanged?.Invoke(true);
    }

    public void PopBuildMode(object owner)
    {
        owner ??= this;
        bool removed = _buildOwners.Remove(owner);
        if (removed && _buildOwners.Count == 0)
            BuildModeChanged?.Invoke(false);
    }

    public void SetBuildMode(bool on)
    {
        if (on) PushBuildMode(this);
        else PopBuildMode(this);
    }

    public void Dispose()
    {
        PlaceTowerRequested = null;
        BuildModeChanged = null;
        _buildOwners.Clear();
    }
}
