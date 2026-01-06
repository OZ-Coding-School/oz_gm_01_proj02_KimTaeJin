using System;
using System.Collections.Generic;

public sealed class EntityManager : IDisposable
{
    public PlayerEntity Player { get; private set; }
    public readonly List<EnemyEntity> Enemies = new();

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

    public void Dispose()
    {
        Enemies.Clear();
        Player = null;
    }
}
