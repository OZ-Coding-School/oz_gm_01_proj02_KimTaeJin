using UnityEngine;

public sealed class EnemyBrain : MonoBehaviour
{
    private RunScope _scope;
    private float _speed;

    public void Construct(RunScope scope, float speed)
    {
        _scope = scope;
        _speed = speed;
    }

    private void Update()
    {
        var player = _scope?.Entities?.Player;
        if (player == null) return;

        var dir = player.transform.position - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.01f) return;

        transform.position += dir.normalized * (_speed * Time.deltaTime);
    }
}
