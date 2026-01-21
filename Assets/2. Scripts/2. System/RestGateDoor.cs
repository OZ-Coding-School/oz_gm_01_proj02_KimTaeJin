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

    private Vector3 _openPos;
    private Vector3 _closedPos;
    private Tween _moveTween;

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
        _openPos = useLocalPosition ? door.localPosition : door.position;
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
        Vector3 target = open ? _openPos : _closedPos;
        KillTween();

        if (useLocalPosition)
            _moveTween = door.DOLocalMove(target, moveDuration).SetEase(moveEase).SetUpdate(true);
        else
            _moveTween = door.DOMove(target, moveDuration).SetEase(moveEase).SetUpdate(true);
    }

    private void ApplyImmediate(bool open)
    {
        if (door == null) return;
        CachePositions();
        Vector3 target = open ? _openPos : _closedPos;
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
}
