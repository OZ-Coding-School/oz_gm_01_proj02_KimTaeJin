using UnityEngine;

[CreateAssetMenu(menuName = "Game/Enemy Spawn Catalog", fileName = "EnemySpawnCatalog_")]
public sealed class EnemySpawnCatalogSO : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        public EnemyEntity prefab;
        public int minStage;
        public float weight;
        public float hpMultiplier;
        public float speedMultiplier;
    }

    [SerializeField] private Entry[] entries;

    public bool TryPick(int stage, out Entry result)
    {
        result = default;
        if (entries == null || entries.Length == 0) return false;

        float total = 0f;
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i].prefab == null) continue;
            if (entries[i].minStage > stage) continue;
            total += Mathf.Max(0f, entries[i].weight);
        }

        if (total <= 0f) return false;

        float roll = Random.value * total;
        for (int i = 0; i < entries.Length; i++)
        {
            var e = entries[i];
            if (e.prefab == null) continue;
            if (e.minStage > stage) continue;
            roll -= Mathf.Max(0f, e.weight);
            if (roll <= 0f)
            {
                result = e;
                return true;
            }
        }

        return false;
    }
}
