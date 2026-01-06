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
        var playerPos = _scope.Entities.Player.transform.position;
        float radius = (GameRoot.Instance != null) ? GameRoot.Instance.SpawnRadius : 10f;
        Vector2 r = Random.insideUnitCircle * radius;
        Vector3 xz = new Vector3(playerPos.x + r.x, 0f, playerPos.z + r.y);

        float groundY = 0f;
        if (GameRoot.Instance != null)
        {
            float rayH = GameRoot.Instance.GroundRayHeight;
            var origin = new Vector3(xz.x, rayH, xz.z);

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayH * 2f,
                    GameRoot.Instance.GroundMask, QueryTriggerInteraction.Ignore))
            {
                groundY = hit.point.y;
            }
        }

        float halfH = 0.5f;
        float extra = (GameRoot.Instance != null) ? GameRoot.Instance.GroundExtraOffset : 0.02f;

        if (GameRoot.Instance != null && GameRoot.Instance.EnemyPrefab != null)
        {
            var prefabCol = GameRoot.Instance.EnemyPrefab.GetComponent<Collider>();
            if (prefabCol != null)
            {
                float ext = prefabCol.bounds.extents.y;
                if (ext > 0.0001f) halfH = ext;
                else
                {
                    if (prefabCol is CapsuleCollider cap) halfH = (cap.height * 0.5f) * cap.transform.lossyScale.y;
                    else if (prefabCol is BoxCollider box) halfH = (box.size.y * 0.5f) * box.transform.lossyScale.y;
                }
            }
        }

        Vector3 pos = new Vector3(xz.x, groundY + halfH + extra, xz.z);

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


    private bool TryGetGroundY(Vector3 xzPos, out float groundY)
    {
        float rayStartHeight = GameRoot.Instance.GroundRayHeight;
        var origin = new Vector3(xzPos.x, rayStartHeight, xzPos.z);

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayStartHeight * 2f,
                GameRoot.Instance.GroundMask, QueryTriggerInteraction.Ignore))
        {
            groundY = hit.point.y;
            return true;
        }

        groundY = 0f;
        return false;
    }

    private Vector3 GetSpawnPosOnGround(Vector3 playerPos, float radius, Collider enemyCol)
    {
        Vector2 r = Random.insideUnitCircle * radius;
        Vector3 xz = new Vector3(playerPos.x + r.x, 0f, playerPos.z + r.y);

        if (!TryGetGroundY(xz, out float y))
            y = 0f;

        float halfH = (enemyCol != null) ? enemyCol.bounds.extents.y : 0.5f;
        return new Vector3(xz.x, y + halfH + GameRoot.Instance.GroundExtraOffset, xz.z);
    }

}
