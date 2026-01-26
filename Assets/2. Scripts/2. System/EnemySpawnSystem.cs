using System.Collections.Generic;
using UnityEngine;

public sealed class EnemySpawnSystem : MonoBehaviour
{
    public enum SpawnMode
    {
        AroundPlayer = 0,
        GridEdge = 1,
        PlayAreaBoundary = 2
    }

    [Header("Spawn")]
    [SerializeField] private SpawnMode spawnMode = SpawnMode.GridEdge;
    [SerializeField] private float spawnEdgePadding = 2f;
    [SerializeField] private PlayAreaBoundary playAreaBoundary;
    [SerializeField] private EnemySpawnCatalogSO spawnCatalog;
    [SerializeField] private float spawnBoundaryInset = 0f;

    [Header("스폰 디버그")]
    [SerializeField] private bool drawSpawnGizmos = true;
    [SerializeField] private bool drawSpawnGizmosOnlyWhenSelected = true;
    [SerializeField] private Color spawnGizmoColor = new Color(1f, 0.2f, 0.2f, 0.85f);
    [SerializeField] private float spawnGizmoSize = 0.25f;
    [SerializeField] private int spawnGizmoSamples = 24;

    [Header("Difficulty")]
    [SerializeField] private int baseEnemyHp = 20;
    [SerializeField] private float baseEnemySpeed = 2.5f;
    [SerializeField] private int hpPerStage = 2;
    [SerializeField] private float speedPerStage = 0.1f;
    [SerializeField] private float distancePerStage = 50f;
    [SerializeField] private float timePerStage = 30f;
    [SerializeField] private WorldScroller progressSource;

    [Header("스폰 수량")]
    [SerializeField] private int spawnCountBase = 1;
    [SerializeField] private int spawnCountPerStage = 0;
    [SerializeField] private int spawnCountMax = 6;

    [Header("종류 진행")]
    [SerializeField] private float distancePerVarietyStage = 0f;

    [Header("진행 거리 대상")]
    [SerializeField] private Transform progressTarget;
    [SerializeField] private bool useAxisDistance = true;
    [SerializeField] private Vector3 progressAxis = Vector3.forward;
    [SerializeField] private bool useAxisAbsoluteDistance = true;

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
    private float _startTime;
    private Vector3 _progressStartPos;
    private bool _progressStartCaptured;

    public bool ThreatActive => _threatActive;
    public float ThreatTimeRemaining => _threatTimer;
    public int ThreatStage => _threatStage;
    public event System.Action ThreatWaveChanged;

    public void Construct(RunScope scope)
    {
        _scope = scope;
        ResolveProgressSource();
    }

    public void Begin()
    {
        _t = 0f;
        _running = true;
        _threatActive = false;
        _threatTimer = 0f;
        _threatStage = 0;
        _startTime = Time.time;
        _nextThreatTime = Time.time + Mathf.Max(0f, threatWaveInterval);
        _progressStartCaptured = false;
        ResolveProgressSource();
        CaptureProgressStart();
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
            SpawnBatch();
        }
    }

    private void SpawnBatch()
    {
        if (_scope == null || _scope.Entities == null) return;

        int difficultyStage = Mathf.Max(0, GetDifficultyStage());
        if (_threatActive) difficultyStage += Mathf.Max(0, threatStageBonus);

        int varietyStage = Mathf.Max(0, GetVarietyStage());

        int spawnCount = GetSpawnCount(difficultyStage);
        int maxAlive = GameRoot.Instance != null ? GameRoot.Instance.MaxEnemiesAlive : int.MaxValue;

        for (int i = 0; i < spawnCount; i++)
        {
            if (GameRoot.Instance != null && _scope.Entities.Enemies.Count >= maxAlive)
                break;
            SpawnOne(difficultyStage, varietyStage);
        }
    }

    private int GetSpawnCount(int stage)
    {
        int count = Mathf.Max(1, spawnCountBase + stage * spawnCountPerStage);
        if (spawnCountMax > 0)
            count = Mathf.Min(spawnCountMax, count);
        return count;
    }

    private void SpawnOne(int difficultyStage, int varietyStage)
    {
        Vector3 xz = GetSpawnXZ();
        EnemyEntity prefab = ResolveEnemyPrefab(varietyStage, out float hpMul, out float speedMul);

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

        float bottomOffset = 0.5f;
        if (prefab != null)
        {
            var prefabCol = prefab.GetComponent<Collider>();
            if (prefabCol != null)
                bottomOffset = GetColliderBottomOffset(prefabCol, prefab.transform);
        }
        Vector3 pos = new Vector3(xz.x, groundY + bottomOffset + extra, xz.z);


        EnemyEntity enemy;

        if (prefab != null && _scope != null && _scope.App != null && _scope.App.Pool != null)
        {
            enemy = _scope.App.Pool.Spawn(prefab, pos, Quaternion.identity);
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

        int stage = Mathf.Max(0, difficultyStage);
        int hp = Mathf.Max(1, Mathf.RoundToInt((baseEnemyHp + stage * hpPerStage) * Mathf.Max(0.01f, hpMul)));
        float speed = Mathf.Max(0.1f, (baseEnemySpeed + stage * speedPerStage) * Mathf.Max(0.01f, speedMul));

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
        if (spawnMode == SpawnMode.PlayAreaBoundary)
            return GetBoundarySpawnXZ();

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

    private int GetDifficultyStage()
    {
        if (distancePerStage > 0f)
        {
            float dist = GetProgressDistance();
            return Mathf.FloorToInt(dist / distancePerStage);
        }

        if (timePerStage > 0f)
            return Mathf.FloorToInt((Time.time - _startTime) / timePerStage);

        return 0;
    }

    private int GetVarietyStage()
    {
        if (distancePerVarietyStage > 0f)
        {
            float dist = GetProgressDistance();
            return Mathf.FloorToInt(dist / distancePerVarietyStage);
        }

        return GetDifficultyStage();
    }

    private float GetProgressDistance()
    {
        if (progressSource != null && progressSource.isActiveAndEnabled)
            return Mathf.Max(0f, progressSource.ProgressDistance);

        if (progressTarget == null) return 0f;
        if (!_progressStartCaptured) CaptureProgressStart();

        Vector3 delta = progressTarget.position - _progressStartPos;
        delta.y = 0f;

        if (useAxisDistance)
        {
            Vector3 axis = progressAxis.sqrMagnitude > 0.0001f ? progressAxis.normalized : Vector3.forward;
            float dot = Vector3.Dot(delta, axis);
            if (useAxisAbsoluteDistance) dot = Mathf.Abs(dot);
            return Mathf.Max(0f, dot);
        }

        return Mathf.Max(0f, delta.magnitude);
    }

    private void CaptureProgressStart()
    {
        if (progressTarget == null) return;
        _progressStartPos = progressTarget.position;
        _progressStartCaptured = true;
    }

    private Vector3 GetBoundarySpawnXZ()
    {
        PlayAreaBoundary boundary = ResolveBoundary();
        if (boundary == null || boundary.Points == null || boundary.Points.Count < 2)
        {
            if (_scope != null && _scope.Grid != null)
                return GetGridEdgeSpawnXZ(_scope.Grid);
            return GetRadialSpawnXZ();
        }

        var pts = boundary.Points;
        int idx = Random.Range(0, pts.Count);
        int next = (idx + 1) % pts.Count;
        float t = Random.Range(0f, 1f);
        bool isCcw = ComputeSignedArea(pts) > 0f;
        Vector3 world = ComputeBoundarySpawnWorld(boundary, pts, idx, t, spawnBoundaryInset, isCcw);
        return new Vector3(world.x, 0f, world.z);
    }

    private EnemyEntity ResolveEnemyPrefab(int varietyStage, out float hpMul, out float speedMul)
    {
        hpMul = 1f;
        speedMul = 1f;

        int stage = Mathf.Max(0, varietyStage);
        if (spawnCatalog != null && spawnCatalog.TryPick(stage, out EnemySpawnCatalogSO.Entry entry))
        {
            hpMul = Mathf.Max(0.01f, entry.hpMultiplier <= 0f ? 1f : entry.hpMultiplier);
            speedMul = Mathf.Max(0.01f, entry.speedMultiplier <= 0f ? 1f : entry.speedMultiplier);
            if (entry.prefab != null) return entry.prefab;
        }

        return GameRoot.Instance != null ? GameRoot.Instance.EnemyPrefab : null;
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

    private void ResolveProgressSource()
    {
        if (progressSource == null)
            progressSource = FindObjectOfType<WorldScroller>();

        if (progressTarget == null)
        {
            var house = FindObjectOfType<HouseDrift>();
            if (house != null)
            {
                progressTarget = house.transform;
                if (useAxisDistance)
                {
                    Vector3 dir = house.Direction;
                    if (dir.sqrMagnitude > 0.0001f)
                        progressAxis = dir.normalized;
                }
                return;
            }

            if (_scope != null && _scope.Grid != null && _scope.Grid.Anchor != null)
                progressTarget = _scope.Grid.Anchor;
        }
    }

    private PlayAreaBoundary ResolveBoundary()
    {
        if (playAreaBoundary != null && playAreaBoundary.gameObject.scene.IsValid())
            return playAreaBoundary;
        playAreaBoundary = FindObjectOfType<PlayAreaBoundary>();
        return playAreaBoundary;
    }

    private static Vector3 ComputeBoundarySpawnWorld(PlayAreaBoundary boundary, IReadOnlyList<Vector2> pts, int edge, float t, float inset, bool isCcw)
    {
        int count = pts.Count;
        int next = (edge + 1) % count;
        Vector2 a = pts[edge];
        Vector2 b = pts[next];
        Vector2 local = Vector2.Lerp(a, b, Mathf.Clamp01(t));
        Vector3 world = boundary.transform.TransformPoint(new Vector3(local.x, 0f, local.y));
        if (inset <= 0.001f) return world;

        Vector2 edgeDir = b - a;
        if (edgeDir.sqrMagnitude <= 0.0001f) return world;
        edgeDir.Normalize();
        Vector2 left = new Vector2(-edgeDir.y, edgeDir.x);
        Vector2 inside = isCcw ? left : -left;
        Vector3 worldInside = boundary.transform.TransformDirection(new Vector3(inside.x, 0f, inside.y));
        if (worldInside.sqrMagnitude <= 0.0001f) return world;
        worldInside.Normalize();
        world += worldInside * inset;
        return world;
    }

    private static float ComputeSignedArea(IReadOnlyList<Vector2> pts)
    {
        float area = 0f;
        int count = pts.Count;
        for (int i = 0; i < count; i++)
        {
            Vector2 a = pts[i];
            Vector2 b = pts[(i + 1) % count];
            area += a.x * b.y - b.x * a.y;
        }
        return area * 0.5f;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (drawSpawnGizmosOnlyWhenSelected) return;
        DrawSpawnGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawSpawnGizmosOnlyWhenSelected) return;
        DrawSpawnGizmos();
    }

    private void DrawSpawnGizmos()
    {
        if (!drawSpawnGizmos) return;
        Gizmos.color = spawnGizmoColor;

        switch (spawnMode)
        {
            case SpawnMode.PlayAreaBoundary:
                DrawBoundarySpawnGizmos();
                break;
            case SpawnMode.GridEdge:
                DrawGridEdgeGizmos();
                break;
            default:
                DrawRadialGizmos();
                break;
        }
    }

    private void DrawBoundarySpawnGizmos()
    {
        PlayAreaBoundary boundary = ResolveBoundary();
        if (boundary == null) return;
        var pts = boundary.Points;
        if (pts == null || pts.Count < 2) return;

        float total = 0f;
        int count = pts.Count;
        for (int i = 0; i < count; i++)
        {
            int next = (i + 1) % count;
            total += Vector2.Distance(pts[i], pts[next]);
        }

        if (total < 0.001f) return;

        int samples = Mathf.Max(2, spawnGizmoSamples);
        bool isCcw = ComputeSignedArea(pts) > 0f;
        float step = total / samples;
        int edge = 0;
        float edgeLen = Vector2.Distance(pts[0], pts[1]);
        float accum = 0f;

        for (int s = 0; s < samples; s++)
        {
            float target = s * step;
            while (edge < count && accum + edgeLen < target)
            {
                accum += edgeLen;
                edge = (edge + 1) % count;
                edgeLen = Vector2.Distance(pts[edge], pts[(edge + 1) % count]);
                if (edgeLen <= 0.0001f) break;
            }

            float t = edgeLen > 0.0001f ? (target - accum) / edgeLen : 0f;
            Vector3 world = ComputeBoundarySpawnWorld(boundary, pts, edge, t, spawnBoundaryInset, isCcw);
            Gizmos.DrawSphere(world, spawnGizmoSize);
        }
    }

    private void DrawGridEdgeGizmos()
    {
        GridSystem grid = _scope != null ? _scope.Grid : null;
        if (grid == null)
            grid = FindObjectOfType<GridSystem>();
        if (grid == null) return;

        Vector3 origin = grid.Origin;
        float sizeX = grid.CellSizeX * grid.Width;
        float sizeZ = grid.CellSizeZ * grid.Height;
        float minX = origin.x - spawnEdgePadding;
        float maxX = origin.x + sizeX + spawnEdgePadding;
        float minZ = origin.z - spawnEdgePadding;
        float maxZ = origin.z + sizeZ + spawnEdgePadding;
        float y = origin.y;

        Vector3 a = new Vector3(minX, y, minZ);
        Vector3 b = new Vector3(maxX, y, minZ);
        Vector3 c = new Vector3(maxX, y, maxZ);
        Vector3 d = new Vector3(minX, y, maxZ);

        Gizmos.DrawLine(a, b);
        Gizmos.DrawLine(b, c);
        Gizmos.DrawLine(c, d);
        Gizmos.DrawLine(d, a);
    }

    private void DrawRadialGizmos()
    {
        Transform player = _scope != null && _scope.Entities != null && _scope.Entities.Player != null
            ? _scope.Entities.Player.transform
            : null;
        if (player == null) return;

        float radius = (GameRoot.Instance != null) ? GameRoot.Instance.SpawnRadius : 10f;
        int segments = Mathf.Clamp(spawnGizmoSamples, 8, 64);
        float step = Mathf.PI * 2f / segments;
        Vector3 center = player.position;
        Vector3 prev = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = step * i;
            Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
#endif
}
