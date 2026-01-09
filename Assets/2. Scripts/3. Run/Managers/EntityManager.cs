using System;
using System.Collections.Generic;

public sealed class EntityManager : IDisposable
{
    public PlayerEntity Player { get; private set; }
    public readonly List<EnemyEntity> Enemies = new();
    public readonly List<TowerEntity> Towers = new();

    public void RegisterPlayer(PlayerEntity p) => Player = p;

    public void RegisterEnemy(EnemyEntity e)
    {
        if (e == null) return;
        if (!Enemies.Contains(e)) Enemies.Add(e);
    }

    public void UnregisterEnemy(EnemyEntity e)
    {
        if (e == null) return;
        Enemies.Remove(e);
    }
    public void RegisterTower(TowerEntity t)
    {
        if (t == null) return;
        if (!Towers.Contains(t)) Towers.Add(t);
    }

    public void UnregisterTower(TowerEntity t)
    {
        if (t == null) return;
        Towers.Remove(t);
    }

    public void Dispose()
    {
        Enemies.Clear();
        Towers.Clear();
        Player = null;
    }

}
