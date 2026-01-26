using UnityEngine;
using DG.Tweening;

public sealed class DropItem : MonoBehaviour
{
    [Header("Value")]
    [SerializeField] private int amount = 1;
    [SerializeField] private ResourceType resourceType = ResourceType.Wood;

    [Header("??(??)")]
    [SerializeField] private bool useTagFallback = false;
    [SerializeField] private string woodTag = "Wood";
    [SerializeField] private string stoneTag = "Stone";

    [Header("Spawn Pop")]
    [SerializeField] private float popDuration = 0.2f;
    [SerializeField] private float jumpDuration = 0.35f;
    [SerializeField] private float jumpPower = 0.6f;
    [SerializeField] private float scatter = 0.5f;

    [Header("Pickup Magnet")]
    [SerializeField] private float magnetRange = 3.0f;
    [SerializeField] private float flyTime = 0.22f;

    [Header("Visual (비워두면 자기 자신)")]
    [SerializeField] private Transform visual;

    [SerializeField] private float groundScaleMul = 1f;

    [SerializeField] private float followerScaleMul = 1.8f;

    private Transform _player;
    private Vector3 _baseScale;
    private int _baseAmount;
    private bool _flying;
    private Collider _col;
    private Rigidbody _rb;
    public int Amount => amount;
    public ResourceType ResourceType => resourceType;
    private bool _isFollower;
    private bool _countedOnPickup;
    public bool CountedOnPickup => _countedOnPickup;

    public void AddAmount(int add)
    {
        amount += Mathf.Max(0, add);

        if (visual != null)
        {
            visual.DOKill();
            visual.DOPunchScale(new Vector3(0.12f, -0.08f, 0.12f), 0.15f, 8, 1f);
        }
    }
    public void SetAmount(int value)
    {
        amount = Mathf.Max(1, value);
    }

    public void Despawn()
    {
        if (GameRoot.Instance != null && GameRoot.Instance.Pool != null)
        {
            GameRoot.Instance.Pool.Despawn(gameObject);
            return;
        }

        Destroy(gameObject);
    }

    private void Awake()
    {
        if (visual == null) visual = transform;
        _baseScale = visual.localScale;
        _baseAmount = amount;

        _col = GetComponent<Collider>();
        _rb = GetComponent<Rigidbody>();
    }
    public void SetGroundScaleMul(float mul)
    {
        groundScaleMul = Mathf.Max(0.01f, mul);
        ApplyScale(groundScaleMul);
    }

    public void SetFollowerScaleMul(float mul)
    {
        followerScaleMul = Mathf.Max(0.01f, mul);
    }

    private void ApplyScale(float mul)
    {
        if (visual == null) return;
        visual.localScale = _baseScale * mul;
    }

    private void Update()
    {
        if (_isFollower) return;

        if (_flying) return;

        if (_player == null)
        {
            var go = GameObject.FindWithTag("Player");
            if (go == null) return;
            _player = go.transform;
        }

        Vector3 diff = _player.position - transform.position;
        diff.y = 0f; 
        float d2 = diff.sqrMagnitude;

        if (d2 <= magnetRange * magnetRange)
            FlyToPlayer();
    }



    private void FlyToPlayer()
    {
        if (_player == null) return;

        _flying = true;

        transform.DOKill();
        if (visual != null) visual.DOKill();

        if (_col != null) _col.enabled = false;
        if (_rb != null)
        {
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic = true;
            _rb.useGravity = false;
        }

        Vector3 target = _player.position + Vector3.up * 1.0f;

        visual.DOScale((_baseScale * groundScaleMul) * 0.2f, flyTime).SetEase(Ease.InQuad);

        transform.DOMove(target, flyTime)
            .SetEase(Ease.InQuad)
            .OnComplete(Collect);
    }


    private void Collect()
    {
        if (_player == null)
        {
            Despawn();
            return;
        }

        if (resourceType == ResourceType.Exp)
        {
            var progression = RunScopeLocator.Current != null ? RunScopeLocator.Current.Progression : null;
            if (progression != null)
            {
                progression.AddResource(ResourceType.Exp, amount);
                Despawn();
                return;
            }

            var xp = _player.GetComponentInParent<PlayerExperience>();
            if (xp != null) xp.AddExp(amount);
            Despawn();
            return;
        }

        var sfxType = ResolveSfxResourceType();
        if (sfxType == ResourceType.Wood || sfxType == ResourceType.Stone)
        {
            GameAudio.Instance?.PlayHarvestPickup(sfxType);
        }


        if (resourceType == ResourceType.Stone && !_countedOnPickup)
        {
            var progression = RunScopeLocator.Current != null ? RunScopeLocator.Current.Progression : null;
            if (progression != null)
            {
                progression.AddStoneSkillExp(amount);
                _countedOnPickup = true;
            }
        }

        var train = _player.GetComponentInParent<PickupTrain>();
        if (train != null)
        {
            train.Capture(this); 
            return;
        }

        Despawn();
    }

    private ResourceType ResolveSfxResourceType()
    {
        if (resourceType != ResourceType.None)
            return resourceType;

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
    private void OnEnable()
    {
        _isFollower = false;
        _countedOnPickup = false;
        _player = null;
        amount = _baseAmount;

        _flying = false;
        enabled = true;

        if (_col != null) _col.enabled = true;

        if (_rb != null)
        {
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic = false;  
            _rb.useGravity = false;
        }

        PlaySpawnPop();
    }

    private void OnDisable()
    {
        transform.DOKill();
        if (visual != null) visual.DOKill();
    }

    public void BecomeFollower()
    {
        _isFollower = true;

        transform.DOKill();
        if (visual != null)
        {
            visual.DOKill();
            visual.DOScale(_baseScale * followerScaleMul, 0.12f).SetEase(Ease.OutBack);
        }

        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.useGravity = false;
        }

    }

    private void PlaySpawnPop()
    {
        transform.DOKill();
        if (visual != null) visual.DOKill();

        if (visual != null)
        {
            visual.localScale = Vector3.zero;
            visual.DOScale(_baseScale * groundScaleMul, popDuration).SetEase(Ease.OutBack);
        }

        Vector3 end = transform.position + new Vector3(Random.Range(-scatter, scatter), 0f, Random.Range(-scatter, scatter));
        transform.DOJump(end, jumpPower, 1, jumpDuration).SetEase(Ease.OutQuad);
    }

}