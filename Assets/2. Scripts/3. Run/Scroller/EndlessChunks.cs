//using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class EndlessChunks : MonoBehaviour
{

    public enum ScrollDir { PlusZ, MinusZ }
    [SerializeField] private ScrollDir scrollDir = ScrollDir.PlusZ;

    [Header("Refs")]
    [SerializeField] private Transform core;
    [SerializeField] private Transform[] chunkRoots;
    [SerializeField] private Transform[] nodesParents;

    [Header("Socket Folder Names (Nodes 아래)")]
    [SerializeField] private string centerSocketsName = "Sockets_Center";
    [SerializeField] private string sideLSocketsName = "Sockets_SideL";
    [SerializeField] private string sideRSocketsName = "Sockets_SideR";

    [Header("Group Prefabs")]
    [SerializeField] private GameObject[] sideGroupPrefabs;
    [SerializeField] private GameObject[] laneBlockerPrefabs;

    [Header("Chunk Size")]
    [SerializeField] private float chunkLenZ = 0f;
    [SerializeField] private float chunkWidthX = 0f;
    [SerializeField] private bool autoComputeFromRenderer = false;
    [SerializeField] private bool autoComputeLenFromChunkPositions = true;
    [SerializeField] private Vector3 chunkCenterOffset = Vector3.zero;

    [Header("Recycle")]
    [SerializeField] private float recycleZOffset = 0f;

    [Header("Spawn Amount")]
    [SerializeField] private Vector2 sideFillRange = new Vector2(0.75f, 0.95f);
    [SerializeField] private float laneChance1 = 0.30f;
    [SerializeField] private float laneChance2 = 0.10f;

    [Header("No-Overlap (Socket 간 최소거리)")]
    [SerializeField] private float sideMinSpacing = 2.3f;
    [SerializeField] private float laneMinSpacing = 3.0f;

    [Header("Optional Ground Snap")]
    [SerializeField] private bool snapToGround = false;
    [SerializeField] private float rayHeight = 50f;
    [SerializeField] private LayerMask groundMask;

    [Header("Gizmos")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private float gizmoY = 0.05f;

    private struct Spawned
    {
        public GameObject go;
        public GameObject prefab;
    }

    private List<Spawned>[] _spawnedByChunk;
    private List<Transform>[] _centerSockets;
    private List<Transform>[] _sideLSockets;
    private List<Transform>[] _sideRSockets;

    private PoolService Pool => (GameRoot.Instance != null) ? GameRoot.Instance.Pool : null;

    private void Awake()
    {
        if (chunkRoots == null || chunkRoots.Length == 0)
        {
            Debug.LogError("[EndlessChunks] chunkRoots not set!");
            enabled = false;
            return;
        }
        if (nodesParents == null || nodesParents.Length != chunkRoots.Length)
            nodesParents = new Transform[chunkRoots.Length];

        for (int i = 0; i < chunkRoots.Length; i++)
        {
            if (nodesParents[i] == null && chunkRoots[i] != null)
                nodesParents[i] = chunkRoots[i].Find("Nodes Visual");
        }

        if (nodesParents == null || nodesParents.Length != chunkRoots.Length)
            Debug.LogError("[EndlessChunks] nodesParents length mismatch with chunkRoots.");

        if (autoComputeFromRenderer)
        {
            var r = chunkRoots[0] ? chunkRoots[0].GetComponentInChildren<Renderer>() : null;
            if (r != null)
            {
                var size = r.bounds.size;
                if (chunkWidthX <= 0f) chunkWidthX = size.x;
                if (chunkLenZ <= 0f) chunkLenZ = size.z;
            }
        }

        if (autoComputeLenFromChunkPositions && chunkLenZ <= 0f && chunkRoots.Length >= 2)
        {
            var zs = new float[chunkRoots.Length];
            for (int i = 0; i < chunkRoots.Length; i++)
                zs[i] = GetChunkCenter(chunkRoots[i]).z;

            System.Array.Sort(zs);
            float sum = 0f;
            int n = 0;
            for (int i = 1; i < zs.Length; i++)
            {
                float d = Mathf.Abs(zs[i] - zs[i - 1]);
                if (d > 0.0001f) { sum += d; n++; }
            }
            if (n > 0) chunkLenZ = sum / n;
        }

        if (chunkLenZ <= 0f) chunkLenZ = 20f;
        if (chunkWidthX <= 0f) chunkWidthX = 20f;
        if (recycleZOffset <= 0f) recycleZOffset = chunkLenZ;

        if (groundMask.value == 0 && GameRoot.Instance != null)
            groundMask = GameRoot.Instance.GroundMask;

        _spawnedByChunk = new List<Spawned>[chunkRoots.Length];
        _centerSockets = new List<Transform>[chunkRoots.Length];
        _sideLSockets = new List<Transform>[chunkRoots.Length];
        _sideRSockets = new List<Transform>[chunkRoots.Length];

        for (int i = 0; i < chunkRoots.Length; i++)
        {
            _spawnedByChunk[i] = new List<Spawned>(128);
            _centerSockets[i] = new List<Transform>(64);
            _sideLSockets[i] = new List<Transform>(128);
            _sideRSockets[i] = new List<Transform>(128);
        }
    }
    private void ArrangeInitial()
    {
        float coreZ = core ? core.position.z : 0f;

        int half = chunkRoots.Length / 2;
        float startZ = coreZ - half * chunkLenZ;

        for (int i = 0; i < chunkRoots.Length; i++)
        {
            float centerZ = startZ + i * chunkLenZ;
            SetChunkCenterZ(chunkRoots[i], centerZ);
        }
    }
    private void AutoSortByZ()
    {
        if (chunkRoots == null || chunkRoots.Length == 0) return;

        var list = new List<(Transform chunk, Transform nodes)>(chunkRoots.Length);
        for (int i = 0; i < chunkRoots.Length; i++)
        {
            Transform n = (nodesParents != null && i < nodesParents.Length) ? nodesParents[i] : null;
            list.Add((chunkRoots[i], n));
        }

        list.Sort((a, b) => GetChunkCenter(a.chunk).z.CompareTo(GetChunkCenter(b.chunk).z));

        for (int i = 0; i < list.Count; i++)
        {
            chunkRoots[i] = list[i].chunk;
            if (nodesParents != null && i < nodesParents.Length)
                nodesParents[i] = list[i].nodes;
        }
    }
    private void Start()
    {
        AutoSortByZ();
        ArrangeInitial();
        CacheSockets();

        for (int i = 0; i < chunkRoots.Length; i++)
            ReshuffleResources(i);
    }

    private void Update()
    {
        float coreZ = core ? core.position.z : 0f;

        int safety = 0;
        while (safety++ < chunkRoots.Length + 1)
        {
            int maxIdx = -1, minIdx = -1;
            float maxZ = float.NegativeInfinity, minZ = float.PositiveInfinity;

            for (int i = 0; i < chunkRoots.Length; i++)
            {
                float z = GetChunkCenter(chunkRoots[i]).z;
                if (z > maxZ) { maxZ = z; maxIdx = i; }
                if (z < minZ) { minZ = z; minIdx = i; }
            }

            if (maxIdx < 0 || minIdx < 0) break;

            if (scrollDir == ScrollDir.PlusZ)
            {
                if (minZ >= coreZ - recycleZOffset) break;

                float newCenterZ = maxZ + chunkLenZ;
                SetChunkCenterZ(chunkRoots[minIdx], newCenterZ);
                ReshuffleResources(minIdx);
            }
            else // MinusZ
            {
                if (maxZ <= coreZ + recycleZOffset) break;

                float newCenterZ = minZ - chunkLenZ;
                SetChunkCenterZ(chunkRoots[maxIdx], newCenterZ);
                ReshuffleResources(maxIdx);
            }
        }
    }




    [ContextMenu("Cache Sockets")]
    private void CacheSockets()
    {
        for (int i = 0; i < chunkRoots.Length; i++)
        {
            _centerSockets[i].Clear();
            _sideLSockets[i].Clear();
            _sideRSockets[i].Clear();

            if (nodesParents == null || i >= nodesParents.Length || nodesParents[i] == null) continue;

            var nodes = nodesParents[i];

            var c = nodes.Find(centerSocketsName);
            var l = nodes.Find(sideLSocketsName);
            var r = nodes.Find(sideRSocketsName);

            CollectChildren(c, _centerSockets[i]);
            CollectChildren(l, _sideLSockets[i]);
            CollectChildren(r, _sideRSockets[i]);
        }
    }

    private void ReshuffleResources(int chunkIndex)
    {
        DespawnAll(chunkIndex);

        var used = new List<Vector3>(128);

        SpawnLaneBlockers(chunkIndex, used);
        SpawnSideDense(chunkIndex, used, _sideLSockets[chunkIndex]);
        SpawnSideDense(chunkIndex, used, _sideRSockets[chunkIndex]);
    }

    private void DespawnAll(int chunkIndex)
    {
        var pool = Pool;
        var list = _spawnedByChunk[chunkIndex];
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var s = list[i];
            if (s.go == null) continue;
            if (pool != null) pool.Despawn(s.go, s.prefab);
            else Destroy(s.go);
        }
        list.Clear();
    }

    private void SpawnLaneBlockers(int chunkIndex, List<Vector3> used)
    {
        if (laneBlockerPrefabs == null || laneBlockerPrefabs.Length == 0) return;
        if (_centerSockets[chunkIndex].Count == 0) return;

        int count = 0;
        float r = Random.value;
        if (r < laneChance2) count = 2;
        else if (r < laneChance2 + laneChance1) count = 1;

        SpawnFromSockets(chunkIndex, used, _centerSockets[chunkIndex], laneBlockerPrefabs, count, laneMinSpacing);
    }

    private void SpawnSideDense(int chunkIndex, List<Vector3> used, List<Transform> sockets)
    {
        if (sideGroupPrefabs == null || sideGroupPrefabs.Length == 0) return;
        if (sockets == null || sockets.Count == 0) return;

        float fill = Random.Range(sideFillRange.x, sideFillRange.y);
        int target = Mathf.Clamp(Mathf.RoundToInt(sockets.Count * fill), 0, sockets.Count);

        SpawnFromSockets(chunkIndex, used, sockets, sideGroupPrefabs, target, sideMinSpacing);
    }

    private void SpawnFromSockets(int chunkIndex, List<Vector3> used, List<Transform> sockets, GameObject[] prefabs, int targetCount, float minSpacing)
    {
        if (targetCount <= 0) return;
        var parent = (nodesParents != null && chunkIndex < nodesParents.Length) ? nodesParents[chunkIndex] : null;
        if (parent == null) return;

        var picked = new HashSet<Transform>();
        int attempts = sockets.Count * 6;

        while (targetCount > 0 && attempts-- > 0)
        {
            var s = sockets[Random.Range(0, sockets.Count)];
            if (s == null) continue;
            if (!picked.Add(s)) continue;

            Vector3 pos = s.position;

            if (IsTooClose(pos, used, minSpacing))
                continue;

            var prefab = prefabs[Random.Range(0, prefabs.Length)];
            if (prefab == null) continue;

            if (snapToGround)
            {
                float gy = GetGroundY(pos.x, pos.z, pos.y);
                pos = new Vector3(pos.x, gy, pos.z);
            }

            Transform spawnedTr = null;
            var pool = Pool;

            if (pool != null)
                spawnedTr = pool.Spawn(prefab.transform, pos, s.rotation);
            else
                spawnedTr = Instantiate(prefab, pos, s.rotation).transform;

            spawnedTr.SetParent(parent, true);
            if (snapToGround)
            {
                var col = spawnedTr.GetComponentInChildren<Collider>();
                GroundSnap.TrySnapToGround(spawnedTr, col, groundMask, rayHeight, 0.02f);
            }


            _spawnedByChunk[chunkIndex].Add(new Spawned { go = spawnedTr.gameObject, prefab = prefab });
            used.Add(pos);

            targetCount--;
        }
    }

    private static bool IsTooClose(Vector3 pos, List<Vector3> used, float minSpacing)
    {
        float r2 = minSpacing * minSpacing;
        for (int i = 0; i < used.Count; i++)
        {
            var d = pos - used[i];
            d.y = 0f;
            if (d.sqrMagnitude < r2) return true;
        }
        return false;
    }

    private float GetGroundY(float x, float z, float fallbackY)
    {
        if (groundMask.value == 0) return fallbackY;

        var origin = new Vector3(x, rayHeight, z);
        if (Physics.Raycast(origin, Vector3.down, out var hit, rayHeight * 2f, groundMask, QueryTriggerInteraction.Ignore))
            return hit.point.y;

        return fallbackY;
    }

    private static void CollectChildren(Transform root, List<Transform> dst)
    {
        if (root == null) return;
        for (int i = 0; i < root.childCount; i++)
        {
            var c = root.GetChild(i);
            if (c != null) dst.Add(c);
        }
    }

    private Vector3 GetChunkCenter(Transform chunk) => chunk.position + chunkCenterOffset;

    private void SetChunkCenterZ(Transform chunk, float centerZ)
    {
        var p = chunk.position;
        chunk.position = new Vector3(p.x, p.y, centerZ - chunkCenterOffset.z);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        if (chunkRoots == null || chunkRoots.Length == 0) return;

        for (int i = 0; i < chunkRoots.Length; i++)
        {
            var c = chunkRoots[i];
            if (!c) continue;

            Vector3 center = GetChunkCenter(c);
            center.y = gizmoY;

            DrawRectXZ(center, chunkWidthX > 0f ? chunkWidthX : 20f, chunkLenZ > 0f ? chunkLenZ : 20f, Color.cyan);
        }

        if (core)
        {
            float zLine = (core.position.z) + ((recycleZOffset <= 0f) ? (chunkLenZ > 0f ? chunkLenZ : 20f) : recycleZOffset);
            Gizmos.color = Color.red;
            Gizmos.DrawLine(new Vector3(core.position.x - 5f, gizmoY, zLine), new Vector3(core.position.x + 5f, gizmoY, zLine));

            Handles.color = Color.red;
            Handles.Label(new Vector3(core.position.x, gizmoY, zLine), $"Recycle Z={zLine:0.00}");
        }
    }

    private static void DrawRectXZ(Vector3 center, float widthX, float lengthZ, Color col)
    {
        Gizmos.color = col;
        float hx = widthX * 0.5f;
        float hz = lengthZ * 0.5f;

        Vector3 p1 = new Vector3(center.x - hx, center.y, center.z - hz);
        Vector3 p2 = new Vector3(center.x + hx, center.y, center.z - hz);
        Vector3 p3 = new Vector3(center.x + hx, center.y, center.z + hz);
        Vector3 p4 = new Vector3(center.x - hx, center.y, center.z + hz);

        Gizmos.DrawLine(p1, p2);
        Gizmos.DrawLine(p2, p3);
        Gizmos.DrawLine(p3, p4);
        Gizmos.DrawLine(p4, p1);
    }
#endif
}
