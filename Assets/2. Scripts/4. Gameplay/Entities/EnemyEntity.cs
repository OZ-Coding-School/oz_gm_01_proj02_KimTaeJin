using UnityEngine;

public sealed class EnemyEntity : MonoBehaviour
{
    private RunScope _scope;
    private bool _constructed;

    public void Construct(RunScope scope)
    {
        _scope = scope;
        _constructed = true;

        var hp = GetComponent<HealthComponent>();
        if (hp == null) hp = gameObject.AddComponent<HealthComponent>();
        hp.Initialize(20, OnDead);

        var brain = GetComponent<EnemyBrain>();
        if (brain == null) brain = gameObject.AddComponent<EnemyBrain>();
        brain.Construct(_scope, speed: 2.5f);
    }

    private void OnDead()
    {
        _scope.Entities.UnregisterEnemy(this);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (_constructed && _scope != null)
            _scope.Entities.UnregisterEnemy(this);
    }
}
