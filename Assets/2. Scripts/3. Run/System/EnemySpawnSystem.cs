using UnityEngine;

public sealed class EnemySpawnSystem : MonoBehaviour
{
    private RunScope _scope;
    private float _t;
    private bool _running;

    public void Construct(RunScope scope) => _scope = scope;

    public void Begin()
    {
        _t = 0f;
        _running = true;
        Debug.Log("[EnemySpawnSystem] Begin");
    }

    private void Update()
    {
        if (!_running) return;
        if (_scope?.Entities?.Player == null) return;

        float interval = (GameRoot.Instance != null) ? GameRoot.Instance.SpawnInterval : 1.5f;

        _t += Time.deltaTime;
        if (_t >= interval)
        {
            _t = 0f;
            SpawnOne();
        }
    }
    private void SpawnOne()
    {
        var center = _scope.Entities.Player.transform.position;

        float radius = (GameRoot.Instance != null) ? GameRoot.Instance.SpawnRadius : 10f;
        var offset = Random.onUnitSphere * radius;
        offset.y = 0f;

        var pos = center + offset;

        EnemyEntity enemy;

        if (GameRoot.Instance != null && GameRoot.Instance.EnemyPrefab != null)
        {
            enemy = Instantiate(GameRoot.Instance.EnemyPrefab, pos, Quaternion.identity);
        }
        else
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Enemy";
            go.transform.position = pos;

            var col = go.GetComponent<Collider>();
            col.isTrigger = true;

            var rb = go.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezeRotation;

            enemy = go.AddComponent<EnemyEntity>();
        }
        if (GameRoot.Instance != null)
        {
            var col = enemy.GetComponent<Collider>();
            GroundSnap.TrySnapToGround(
                enemy.transform,
                col,
                GameRoot.Instance.GroundMask,
                GameRoot.Instance.GroundRayHeight,
                GameRoot.Instance.GroundExtraOffset
            );
        }
        enemy.Construct(_scope);
        _scope.Entities.RegisterEnemy(enemy);
    }
}
