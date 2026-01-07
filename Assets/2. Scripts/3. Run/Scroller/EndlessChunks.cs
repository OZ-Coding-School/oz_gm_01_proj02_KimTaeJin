using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class EndlessChunks : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform core;
    [SerializeField] private Transform[] chunkRoots;
    [SerializeField] private Transform[] nodesParents;

    [Header("Prefabs")]
    [SerializeField] private GameObject[] treePrefabs;
    [SerializeField] private GameObject[] rockPrefabs;

    [Header("Chunk Size")]
    [SerializeField] private float chunkLenZ = 0f;
    [SerializeField] private float chunkWidthX = 0f;

    [Header("Manual Override")]
    [SerializeField] private bool autoComputeChunkSize = false;
    [SerializeField] private bool autoArrangeOnStart = false;
    [SerializeField] private Vector3 chunkCenterOffset = Vector3.zero;

    [Header("Recycle")]
    [SerializeField] private float recycleZOffset = 0f;          // 0ÀÌ¸é step »ç¿ë
    [SerializeField] private bool deriveSpacingFromScene = true; // ¾À¿¡¼­ Ã»Å© °£°ÝÀ» ÀÐ¾î stepÀ¸·Î ¾¸
    [SerializeField] private float spacingZ = 0f;                // 0ÀÌ¸é Start¿¡¼­ Ä¸ÃÄ
    [SerializeField] private float seamOverlap = 0.05f;          // »ìÂ¦ °ãÄ¡°Ô(ºóÆ´ ¹æÁö)

    [Header("Lane Rule")]
    [SerializeField] private float laneHalfWidth = 4f;
    [SerializeField] private float laneBlockChance1 = 0.30f;
    [SerializeField] private float laneBlockChance2 = 0.10f;

    [Header("Counts (Side Dense)")]
    [SerializeField] private Vector2Int treeCountRange = new(15, 25);
    [SerializeField] private Vector2Int rockCountRange = new(10, 18);

    [Header("Placement")]
    [SerializeField] private float edgeMargin = 2f;
    [SerializeField] private float rayHeight = 50f;
    [SerializeField] private LayerMask groundMask;

    [Header("Gizmos")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private float gizmoY = 0.05f;

    private void Awake()
    {
        if (chunkRoots == null || chunkRoots.Length == 0)
        {
            Debug.LogError("[EndlessChunks] chunkRoots not set!");
            enabled = false;
            return;
        }

        if (nodesParents == null || nodesParents.Length != chunkRoots.Length)
            Debug.LogError("[EndlessChunks] nodesParents length mismatch with chunkRoots.");

        if (autoComputeChunkSize)
        {
            var r = chunkRoots[0].GetComponentInChildren<Renderer>();
            if (r != null)
            {
                var size = r.bounds.size;
                if (chunkWidthX <= 0f) chunkWidthX = size.x;
                if (chunkLenZ <= 0f) chunkLenZ = size.z;
            }
        }

        if (chunkLenZ <= 0f) chunkLenZ = 20f;
        if (chunkWidthX <= 0f) chunkWidthX = 20f;

        if (groundMask.value == 0 && GameRoot.Instance != null)
            groundMask = GameRoot.Instance.GroundMask;
    }

    private void Start()
    {
        if (autoArrangeOnStart) ArrangeInitial();

        if (deriveSpacingFromScene && spacingZ <= 0f)
            CaptureSpacingFromScene();

        for (int i = 0; i < chunkRoots.Length; i++)
            ReshuffleResources(i);
    }

    private void Update()
    {
        float coreZ = core ? core.position.z : 0f;

        float step = GetStepZ();
        float recycleOffset = (recycleZOffset > 0f) ? recycleZOffset : step;

        float minCenterZ = float.PositiveInfinity;
        for (int i = 0; i < chunkRoots.Length; i++)
            minCenterZ = Mathf.Min(minCenterZ, GetChunkCenter(chunkRoots[i]).z);

        for (int i = 0; i < chunkRoots.Length; i++)
        {
            var c = chunkRoots[i];
            float centerZ = GetChunkCenter(c).z;

            if (centerZ > coreZ + recycleOffset)
            {
                float newCenterZ = minCenterZ - (step - seamOverlap);
                SetChunkCenterZ(c, newCenterZ);
                ReshuffleResources(i);
                minCenterZ = newCenterZ;
            }
        }
    }

    private float GetStepZ()
    {
        if (deriveSpacingFromScene && spacingZ > 0.001f) return spacingZ;
        return chunkLenZ;
    }

    private void ArrangeInitial()
    {
        float step = GetStepZ();
        float coreZ = core ? core.position.z : 0f;

        for (int i = 0; i < chunkRoots.Length; i++)
        {
            float centerZ = coreZ + (i - 1) * step;
            SetChunkCenterZ(chunkRoots[i], centerZ);
        }
    }

    private void ReshuffleResources(int chunkIndex)
    {
        if (nodesParents == null || chunkIndex >= nodesParents.Length) return;
        var parent = nodesParents[chunkIndex];
        if (parent == null) return;

        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);

        SpawnLaneBlockers(parent, chunkIndex);

        int treeCount = Random.Range(treeCountRange.x, treeCountRange.y + 1);
        int rockCount = Random.Range(rockCountRange.x, rockCountRange.y + 1);

        SpawnSideDense(treePrefabs, treeCount, parent, chunkIndex);
        SpawnSideDense(rockPrefabs, rockCount, parent, chunkIndex);
    }

    private void SpawnLaneBlockers(Transform parent, int chunkIndex)
    {
        int count = 0;
        if (Random.value < laneBlockChance1) count = 1;
        if (Random.value < laneBlockChance2) count = 2;

        for (int i = 0; i < count; i++)
        {
            var pick = (Random.value < 0.5f) ? treePrefabs : rockPrefabs;
            SpawnOneInLane(pick, parent, chunkIndex);
        }
    }

    private void SpawnOneInLane(GameObject[] prefabs, Transform parent, int chunkIndex)
    {
        if (prefabs == null || prefabs.Length == 0) return;

        var chunk = chunkRoots[chunkIndex];
        Vector3 center = GetChunkCenter(chunk);

        float halfZ = GetStepZ() * 0.5f - edgeMargin;

        float x = Random.Range(center.x - laneHalfWidth, center.x + laneHalfWidth);
        float z = Random.Range(center.z - halfZ, center.z + halfZ);
        float y = GetGroundY(x, z);

        var prefab = prefabs[Random.Range(0, prefabs.Length)];
        var go = Instantiate(prefab, new Vector3(x, y, z), Quaternion.identity);
        go.transform.SetParent(parent, true);
    }

    private void SpawnSideDense(GameObject[] prefabs, int count, Transform parent, int chunkIndex)
    {
        if (prefabs == null || prefabs.Length == 0) return;

        var chunk = chunkRoots[chunkIndex];
        Vector3 center = GetChunkCenter(chunk);

        float halfX = chunkWidthX * 0.5f - edgeMargin;
        float halfZ = GetStepZ() * 0.5f - edgeMargin;

        int safety = 0;
        while (count > 0 && safety++ < 3000)
        {
            float x = Random.Range(center.x - halfX, center.x + halfX);
            if (Mathf.Abs(x - center.x) < laneHalfWidth) continue;

            float z = Random.Range(center.z - halfZ, center.z + halfZ);
            float y = GetGroundY(x, z);

            var prefab = prefabs[Random.Range(0, prefabs.Length)];
            var go = Instantiate(prefab, new Vector3(x, y, z), Quaternion.identity);
            go.transform.SetParent(parent, true);

            count--;
        }
    }

    private float GetGroundY(float x, float z)
    {
        if (groundMask.value == 0) return 0f;

        var origin = new Vector3(x, rayHeight, z);
        if (Physics.Raycast(origin, Vector3.down, out var hit, rayHeight * 2f, groundMask, QueryTriggerInteraction.Ignore))
            return hit.point.y;

        return 0f;
    }

    private Vector3 GetChunkCenter(Transform chunk) => chunk.position + chunkCenterOffset;

    private void SetChunkCenterZ(Transform chunk, float centerZ)
    {
        var p = chunk.position;
        chunk.position = new Vector3(p.x, p.y, centerZ - chunkCenterOffset.z);
    }

    private void CaptureSpacingFromScene()
    {
        if (chunkRoots == null || chunkRoots.Length < 2) return;

        float[] zs = new float[chunkRoots.Length];
        for (int i = 0; i < chunkRoots.Length; i++)
            zs[i] = GetChunkCenter(chunkRoots[i]).z;

        System.Array.Sort(zs);

        float sum = 0f;
        int n = 0;
        for (int i = 1; i < zs.Length; i++)
        {
            float d = Mathf.Abs(zs[i] - zs[i - 1]);
            if (d > 0.001f) { sum += d; n++; }
        }

        if (n > 0) spacingZ = sum / n;
    }

    [ContextMenu("Capture Spacing From Scene")]
    private void CaptureSpacingMenu() => CaptureSpacingFromScene();

    [ContextMenu("Reshuffle All Chunks")]
    private void ReshuffleAll()
    {
        if (chunkRoots == null) return;
        for (int i = 0; i < chunkRoots.Length; i++)
            ReshuffleResources(i);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        if (chunkRoots == null || chunkRoots.Length == 0) return;

        float len = GetStepZ();
        float wid = chunkWidthX > 0f ? chunkWidthX : 20f;

        for (int i = 0; i < chunkRoots.Length; i++)
        {
            var c = chunkRoots[i];
            if (!c) continue;

            Vector3 center = GetChunkCenter(c);
            center.y = gizmoY;

            DrawRectXZ(center, wid, len, Color.cyan);

            float laneWidth = laneHalfWidth * 2f;
            DrawRectXZ(center, laneWidth, len, new Color(1f, 0.9f, 0.1f, 1f));
        }

        if (core)
        {
            float coreZ = core.position.z;
            float recycleOffset = (recycleZOffset > 0f) ? recycleZOffset : len;
            float zLine = coreZ + recycleOffset;

            Vector3 a = new Vector3(core.position.x - wid * 0.6f, gizmoY, zLine);
            Vector3 b = new Vector3(core.position.x + wid * 0.6f, gizmoY, zLine);

            Gizmos.color = Color.red;
            Gizmos.DrawLine(a, b);

            Handles.color = Color.red;
            Handles.Label(new Vector3(core.position.x, gizmoY, zLine), $"Recycle Z={zLine:0.00}  Step={len:0.00}");
        }
    }

    private void DrawRectXZ(Vector3 center, float widthX, float lengthZ, Color col)
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
