using UnityEngine;

public sealed class Harvestable : MonoBehaviour
{
    [Header("HP")]
    [SerializeField] private int maxHp = 3;
    [SerializeField] private int maxDamagePerHit = 1;

    [Header("Drop")]
    [SerializeField] private DropItem dropPrefab;
    [SerializeField] private int dropCount = 3;
    [SerializeField] private float dropScatterRadius = 0.6f;

    [Header("분류")]
    [SerializeField] private ResourceType resourceTypeOverride = ResourceType.None;
    [SerializeField] private bool useTagFallback = true;
    [SerializeField] private string woodTag = "Wood";
    [SerializeField] private string stoneTag = "Stone";

    private int _hp;
    private JellyPunch _jelly;
    private HitFlashURP _flash;

    public ResourceType DropResourceType => ResolveResourceType();

    private void Awake()
    {
        _jelly = GetComponent<JellyPunch>();
        _flash = GetComponent<HitFlashURP>();
    }

    private void OnEnable()
    {
        _hp = maxHp;               
        //_flash?.StopAndRestore();   
    }

    public void TakeHit(int damage, Vector3 from)
    {
        if (_hp <= 0) return;

        _jelly?.Play();
        _flash?.Play();
        GameAudio.Instance?.PlayHarvestHit(DropResourceType);

        int finalDamage = Mathf.Max(1, damage);
        if (maxDamagePerHit > 0)
            finalDamage = Mathf.Min(finalDamage, maxDamagePerHit);
        _hp -= finalDamage;
        if (_hp <= 0) Die();
    }

    private void Die()
    {
        if (dropPrefab != null)
        {
            var root = GameRoot.Instance;
            var pool = root != null ? root.Pool : null;
            float countMul = root != null ? root.HarvestDropCountMultiplier : 1f;
            float amountMul = root != null ? root.HarvestDropAmountMultiplier : 1f;

            int spawnCount = Mathf.Max(0, Mathf.RoundToInt(dropCount * Mathf.Max(0f, countMul)));
            if (spawnCount <= 0)
            {
                if (root != null && root.Pool != null)
                    root.Pool.Despawn(gameObject);
                else
                    gameObject.SetActive(false);
                return;
            }

            int baseAmount = Mathf.Max(1, dropPrefab.Amount);
            int finalAmount = Mathf.Max(1, Mathf.RoundToInt(baseAmount * Mathf.Max(0f, amountMul)));

            for (int i = 0; i < spawnCount; i++)
            {
                Vector2 r = UnityEngine.Random.insideUnitCircle * dropScatterRadius;
                Vector3 pos = transform.position + new Vector3(r.x, 0f, r.y);
                if (pool != null)
                {
                    var item = pool.Spawn(dropPrefab, pos, Quaternion.identity);
                    if (item != null) item.SetAmount(finalAmount);
                }
                else
                {
                    var item = Instantiate(dropPrefab, pos, Quaternion.identity);
                    if (item != null) item.SetAmount(finalAmount);
                }
            }
        }

        if (GameRoot.Instance != null && GameRoot.Instance.Pool != null)
            GameRoot.Instance.Pool.Despawn(gameObject);
        else
            gameObject.SetActive(false);
    }

    private ResourceType ResolveResourceType()
    {
        if (resourceTypeOverride != ResourceType.None)
            return resourceTypeOverride;

        if (dropPrefab != null && dropPrefab.ResourceType != ResourceType.None)
            return dropPrefab.ResourceType;

        if (!useTagFallback) return ResourceType.None;
        if (IsTagged(woodTag)) return ResourceType.Wood;
        if (IsTagged(stoneTag)) return ResourceType.Stone;
        return ResourceType.None;
    }

    private bool IsTagged(string tag)
    {
        if (string.IsNullOrEmpty(tag)) return false;
        try
        {
            if (CompareTag(tag)) return true;
            var parent = transform.parent;
            return parent != null && parent.CompareTag(tag);
        }
        catch
        {
            return false;
        }
    }
}
