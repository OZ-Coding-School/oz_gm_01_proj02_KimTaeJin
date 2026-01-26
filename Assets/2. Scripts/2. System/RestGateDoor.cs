using UnityEngine;
using DG.Tweening;

[DisallowMultipleComponent]
public sealed class RestGateDoor : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private RestStopSystem restStopSystem;
    [SerializeField] private Transform door;

    [Header("Door Find (Optional)")]
    [SerializeField] private bool autoFindDoor = true;
    [SerializeField] private string doorName = "Big Gate";

    [Header("Move")]
    [SerializeField] private bool useLocalPosition = true;
    [SerializeField] private Vector3 closedOffset = new Vector3(0f, -3f, 0f);
    [SerializeField] private Transform closedTarget;
    [SerializeField] private float moveDuration = 0.35f;
    [SerializeField] private Ease moveEase = Ease.OutCubic;
    [SerializeField] private bool openOnStart = true;

    [Header("사운드")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip openSfx;
    [SerializeField] private AudioClip closeSfx;
    [SerializeField, Range(0f, 1f)] private float openVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float closeVolume = 1f;

    private Vector3 _openPos;
    private Vector3 _closedPos;
    private Tween _moveTween;
    private bool _openPosCached;
    private bool _isOpen;

    private void Awake()
    {
        ResolveRefs();
        CachePositions();
        if (openOnStart) ApplyImmediate(true);
    }

    private void OnEnable()
    {
        ResolveRefs();
        if (restStopSystem != null)
            restStopSystem.RestStateChanged += OnRestStateChanged;
        ApplyImmediate(restStopSystem == null || !restStopSystem.IsResting);
    }

    private void OnDisable()
    {
        if (restStopSystem != null)
            restStopSystem.RestStateChanged -= OnRestStateChanged;
        KillTween();
    }

    private void ResolveRefs()
    {
        if (restStopSystem == null)
            restStopSystem = FindObjectOfType<RestStopSystem>();

        if (door == null && autoFindDoor)
        {
            Transform found = transform.Find(doorName);
            if (found != null) door = found;
        }

        if (door == null)
            door = transform;
    }

    private void CachePositions()
    {
        if (door == null) return;
        if (!_openPosCached)
        {
            _openPos = useLocalPosition ? door.localPosition : door.position;
            _openPosCached = true;
        }
        if (closedTarget != null)
            _closedPos = useLocalPosition ? closedTarget.localPosition : closedTarget.position;
        else
            _closedPos = _openPos + closedOffset;
    }

    private void OnRestStateChanged(bool resting)
    {
        SetOpen(!resting);
    }

    public void SetOpen(bool open)
    {
        CachePositions();
        if (_isOpen == open) return;
        _isOpen = open;
        Vector3 target = open ? _openPos : _closedPos;
        KillTween();

        if (useLocalPosition)
            _moveTween = door.DOLocalMove(target, moveDuration).SetEase(moveEase).SetUpdate(true);
        else
            _moveTween = door.DOMove(target, moveDuration).SetEase(moveEase).SetUpdate(true);

        PlayDoorSfx(open);
    }

    private void ApplyImmediate(bool open)
    {
        if (door == null) return;
        CachePositions();
        Vector3 target = open ? _openPos : _closedPos;
        _isOpen = open;
        if (useLocalPosition)
            door.localPosition = target;
        else
            door.position = target;
    }

    private void KillTween()
    {
        if (_moveTween == null) return;
        _moveTween.Kill();
        _moveTween = null;
    }

    private void PlayDoorSfx(bool open)
    {
        var clip = open ? openSfx : closeSfx;
        if (clip == null) return;
        if (sfxSource == null)
            sfxSource = GetComponent<AudioSource>();
        if (sfxSource == null) return;

        float vol = open ? openVolume : closeVolume;
        sfxSource.PlayOneShot(clip, Mathf.Clamp01(vol));
    }
}
