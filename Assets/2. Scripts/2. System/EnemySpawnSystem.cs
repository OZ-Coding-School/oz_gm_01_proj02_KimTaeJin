using UnityEngine;

public sealed class EnemySpawnSystem : MonoBehaviour
{
    public enum SpawnMode
    {
        AroundPlayer = 0,
        GridEdge = 1
    }

    [Header("Spawn")]
    [SerializeField] private SpawnMode spawnMode = SpawnMode.GridEdge;
    [SerializeField] private float spawnEdgePadding = 2f;

    [Header("Difficulty")]
    [SerializeField] private int baseEnemyHp = 20;
    [SerializeField] private float baseEnemySpeed = 2.5f;
    [SerializeField] private int hpPerStage = 2;
    [SerializeField] private float speedPerStage = 0.1f;
    [SerializeField] private float distancePerStage = 50f;
    [SerializeField] private WorldScroller progressSource;

    [Header("Threat Wave")]
    [SerializeField] private bool useThreatWaves = true;
    [SerializeField] private float threatWaveInterval = 45f;
    [SerializeField] private float threatWaveDuration = 30f;
    [SerializeField] private float threatWaveDurationStep = 5f;
    [SerializeField] private float threatSpawnMultiplier = 3f;
    [SerializeField] private int threatStageBonus = 1;

    private RunScope _scope;
    private float _t;
    private bool _running;
    private bool _threatActive;
    private float _threatTimer;
    private float _nextThreatTime;
    private int _threatStage;

    public bool ThreatActive => _threatActive;
    public float ThreatTimeRemaining => _threatTimer;
    public int ThreatStage => _threatStage;
    public event System.Action ThreatWaveChanged;

    public void Construct(RunScope scope)
    {
        _scope = scope;
        if (progressSource == null)
            progressSource = FindObjectOfType<WorldScroller>();
    }

    public void Begin()
    {
        _t = 0f;
        _running = true;
        _threatActive = false;
        _threatTimer = 0f;
        _threatStage = 0;
        _nextThreatTime = Time.time + Mathf.Max(0f, threatWaveInterval);
        Debug.Log("[EnemySpawnSystem] Begin");
    }

    private void Update()
    {
        if (!_running) return;
        if (_scope?.Entities?.Player == null) return;

        UpdateThreatWave();

        float interval = (GameRoot.Instance != null) ? GameRoot.Instance.SpawnInterval : 1.5f;
        if (_threatActive)
            interval /= Mathf.Max(0.01f, threatSpawnMultiplier);

        _t += Time.deltaTime;
        if (_t >= interval)
        {
            _t = 0f;
            if (GameRoot.Instance != null && _scope.Entities.Enemies.Count >= GameRoot.Instance.MaxEnemiesAlive)
                return;

            SpawnOne();
        }
    }
    private void SpawnOne()
    {
        Vector3 xz = GetSpawnXZ();

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

        float extra = (GameRoot.Instance != null) ? GameRoot.Instance.GroundExtraOffset : 0.02f;

        float bottomOffset = 0.5f; // fallback
        if (GameRoot.Instance != null && GameRoot.Instance.EnemyPrefab != null)
        {
            var prefabCol = GameRoot.Instance.EnemyPrefab.GetComponent<Collider>();
            if (prefabCol != null)
                bottomOffset = GetColliderBottomOffset(prefabCol, GameRoot.Instance.EnemyPrefab.transform);
        }
        Vector3 pos = new Vector3(xz.x, groundY + bottomOffset + extra, xz.z);


        EnemyEntity enemy;

        if (GameRoot.Instance != null && GameRoot.Instance.EnemyPrefab != null)
        {
            enemy = _scope.App.Pool.Spawn(GameRoot.Instance.EnemyPrefab, pos, Quaternion.identity);

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

        int stage = Mathf.Max(0, GetProgressStage());
        if (_threatActive) stage += Mathf.Max(0, threatStageBonus);
        int hp = Mathf.Max(1, baseEnemyHp + stage * hpPerStage);
        float speed = Mathf.Max(0.1f, baseEnemySpeed + stage * speedPerStage);

        enemy.Construct(_scope, hp, speed);
        _scope.Entities.RegisterEnemy(enemy);
    }

    private static float GetColliderBottomOffset(Collider col, Transform tr)
    {
        float sy = tr.lossyScale.y;

        switch (col)
        {
            case CapsuleCollider cap:
                return (cap.height * 0.5f - cap.center.y) * sy;
            case BoxCollider box:
                return (box.size.y * 0.5f - box.center.y) * sy;
            case SphereCollider sph:
                return (sph.radius - sph.center.y) * sy;
            default:
                return col.bounds.extents.y;
        }
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

    private Vector3 GetSpawnXZ()
    {
        if (spawnMode == SpawnMode.GridEdge && _scope != null && _scope.Grid != null)
            return GetGridEdgeSpawnXZ(_scope.Grid);

        return GetRadialSpawnXZ();
    }

    private Vector3 GetRadialSpawnXZ()
    {
        var playerPos = _scope.Entities.Player.transform.position;
        float radius = (GameRoot.Instance != null) ? GameRoot.Instance.SpawnRadius : 10f;
        Vector2 r = Random.insideUnitCircle * radius;
        return new Vector3(playerPos.x + r.x, 0f, playerPos.z + r.y);
    }

    private Vector3 GetGridEdgeSpawnXZ(GridSystem grid)
    {
        Vector3 origin = grid.Origin;
        float sizeX = grid.CellSizeX * grid.Width;
        float sizeZ = grid.CellSizeZ * grid.Height;
        float minX = origin.x;
        float maxX = origin.x + sizeX;
        float minZ = origin.z;
        float maxZ = origin.z + sizeZ;
        float pad = Mathf.Max(0f, spawnEdgePadding);

        float x;
        float z;
        int side = Random.Range(0, 4);
        switch (side)
        {
            case 0:
                x = minX - pad;
                z = Random.Range(minZ, maxZ);
                break;
            case 1:
                x = maxX + pad;
                z = Random.Range(minZ, maxZ);
                break;
            case 2:
                x = Random.Range(minX, maxX);
                z = minZ - pad;
                break;
            default:
                x = Random.Range(minX, maxX);
                z = maxZ + pad;
                break;
        }

        return new Vector3(x, 0f, z);
    }

    private int GetProgressStage()
    {
        float dist = progressSource != null ? progressSource.ProgressDistance : 0f;
        if (distancePerStage <= 0f) return 0;
        return Mathf.FloorToInt(dist / distancePerStage);
    }

    private void UpdateThreatWave()
    {
        if (!useThreatWaves) return;

        if (_threatActive)
        {
            _threatTimer -= Time.deltaTime;
            if (_threatTimer <= 0f)
            {
                _threatActive = false;
                _threatTimer = 0f;
                _nextThreatTime = Time.time + Mathf.Max(0f, threatWaveInterval);
                ThreatWaveChanged?.Invoke();
            }
            return;
        }

        if (_nextThreatTime <= 0f)
            _nextThreatTime = Time.time + Mathf.Max(0f, threatWaveInterval);

        if (Time.time >= _nextThreatTime)
        {
            _threatStage += 1;
            float dur = Mathf.Max(0f, threatWaveDuration)
                + Mathf.Max(0f, threatWaveDurationStep) * Mathf.Max(0, _threatStage - 1);
            _threatTimer = dur;
            _threatActive = true;
            ThreatWaveChanged?.Invoke();
        }
    }
}
