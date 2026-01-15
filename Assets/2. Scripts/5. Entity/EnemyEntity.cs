using UnityEngine;

public sealed class EnemyEntity : MonoBehaviour
{
    private RunScope _scope;
    private bool _constructed;

    public void Construct(RunScope scope, int maxHp = 20, float moveSpeed = 2.5f)
    { 
        _scope = scope;
        _constructed = true;

        var hp = GetComponent<HealthComponent>();
        if (hp == null) hp = gameObject.AddComponent<HealthComponent>();
        hp.Initialize(Mathf.Max(1, maxHp), OnDead);

        var brain = GetComponent<EnemyBrain>();
        if (brain == null) brain = gameObject.AddComponent<EnemyBrain>();
        brain.Construct(_scope, speed: Mathf.Max(0.1f, moveSpeed));
    }

    private void OnDead()
    {
        if (_scope != null && _scope.Entities != null)
            _scope.Entities.UnregisterEnemy(this);

        if (GameRoot.Instance != null && GameRoot.Instance.EnemyExpDropPrefab != null)
            Instantiate(GameRoot.Instance.EnemyExpDropPrefab, transform.position, Quaternion.identity);

        if (_scope != null && _scope.App != null && _scope.App.Pool != null
            && GameRoot.Instance != null && GameRoot.Instance.EnemyPrefab != null)
            _scope.App.Pool.Despawn(gameObject, GameRoot.Instance.EnemyPrefab.gameObject);
        else
            Destroy(gameObject);
    }


    private void OnDestroy()
    {
        if (_constructed && _scope != null && _scope.Entities != null)
            _scope.Entities.UnregisterEnemy(this);
    }
}
