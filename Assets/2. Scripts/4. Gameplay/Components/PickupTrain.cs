using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

[DisallowMultipleComponent]
public sealed class PickupTrain : MonoBehaviour
{
    [Header("Follow Tuning")]
    [SerializeField] private float spacing = 0.55f;          
    [SerializeField] private float recordStep = 0.10f;       
    [SerializeField] private float followSharpness = 14f;    
    [SerializeField] private float followHeight = 0.25f;     
    [SerializeField] private bool lockYToPlayer = true;

    [Header("Capacity")]
    [SerializeField] private int maxFollowers = 9;
    [SerializeField] private bool stackExtraIntoLast = true;

    [Header("Snake Feel")]
    [SerializeField] private float wiggleAmplitude = 0.25f; 
    [SerializeField] private float wiggleSpeed = 6.0f; 
    [SerializeField] private float wigglePhase = 0.65f;     


    private readonly List<DropItem> _items = new();
    private readonly List<Vector3> _trail = new(); 
    private Vector3 _lastRecorded;
    private bool _depositing;

    public int Count => _items.Count;

    [SerializeField] private float speedForFullWiggle = 6f; 
    private Vector3 _prevPos;
    private float _move01;
    private float _wiggleT;


    private void Awake()
    {
        _trail.Clear();
        _trail.Add(transform.position);
        _lastRecorded = transform.position;
        _prevPos = transform.position;
    }

    private void LateUpdate()
    {
        if (_depositing) return;
        Vector3 dp = transform.position - _prevPos;
        dp.y = 0f;
        float speed = dp.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        _move01 = Mathf.Clamp01(speed / Mathf.Max(0.01f, speedForFullWiggle));

        if (_move01 > 0.01f)
            _wiggleT += Time.deltaTime * wiggleSpeed;

        _prevPos = transform.position;
        RecordTrail();
        UpdateFollowers(Time.deltaTime);
    }

    private void RecordTrail()
    {
        Vector3 current = transform.position;
        float dist = Vector3.Distance(current, _lastRecorded);
        if (dist < recordStep)
            return;

        int steps = Mathf.FloorToInt(dist / recordStep);
        Vector3 dir = (current - _lastRecorded).normalized;

        for (int i = 1; i <= steps; i++)
        {
            Vector3 p = _lastRecorded + dir * (recordStep * i);
            _trail.Add(p);
        }

        _lastRecorded = _trail[_trail.Count - 1];

        int maxNeeded = Mathf.CeilToInt((_items.Count + 2) * (spacing / recordStep)) + 30;
        if (_trail.Count > maxNeeded)
            _trail.RemoveRange(0, _trail.Count - maxNeeded); 
    }

    private void UpdateFollowers(float dt)
    {
        if (_items.Count == 0) return;

        float t = 1f - Mathf.Exp(-followSharpness * dt);

        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            if (item == null) continue;

            Transform tr = item.transform;

            int stepIndex = Mathf.RoundToInt((i + 1) * (spacing / recordStep));
            int trailIndex = Mathf.Clamp(_trail.Count - 1 - stepIndex, 0, _trail.Count - 1);

            Vector3 target = _trail[trailIndex];

            Vector3 dir;
            if (trailIndex < _trail.Count - 1)
                dir = _trail[trailIndex + 1] - _trail[trailIndex];
            else
                dir = transform.forward;

            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;

            Vector3 right = Vector3.Cross(Vector3.up, dir.normalized);

            float wiggle = Mathf.Sin(_wiggleT - i * wigglePhase) * wiggleAmplitude * _move01;
            target += right * wiggle;

            if (lockYToPlayer) target.y = transform.position.y;
            target.y += followHeight;

            tr.position = Vector3.Lerp(tr.position, target, t);

            Vector3 to = target - tr.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.0001f)
            {
                Quaternion q = Quaternion.LookRotation(to.normalized, Vector3.up);
                tr.rotation = Quaternion.Slerp(tr.rotation, q, t);
            }
        }
    }

    public void Capture(DropItem item)
    {
        if (_items.Count >= maxFollowers)
        {
            if (stackExtraIntoLast && _items.Count > 0 && item != null)
            {
                _items[_items.Count - 1].AddAmount(item.Amount);
                Destroy(item.gameObject);
                return;
            }
            Destroy(item.gameObject);
            return;
        }

        if (_depositing || item == null) return;

        _items.Add(item);

        item.transform.position = transform.position + Vector3.up * followHeight;

        item.BecomeFollower();
    }

    public void DepositTo(Transform building, float perItemDelay = 0.05f)
    {
        if (_depositing) return;
        if (_items.Count == 0) return;
        if (building == null) return;

        _depositing = true;

        Vector3 baseTarget = building.position + Vector3.up * 1.0f;

        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            if (item == null) continue;

            Transform tr = item.transform;
            tr.DOKill();

            float delay = i * perItemDelay;

            Vector3 center = building.position + Vector3.up * 1.0f;
            Vector3 start = tr.position;

            Vector3 flat = start - center; flat.y = 0f;
            if (flat.sqrMagnitude < 0.01f)
            {
                Vector2 rnd2 = Random.insideUnitCircle.normalized;
                flat = new Vector3(rnd2.x, 0f, rnd2.y);
            }

            Vector3 dir = flat.normalized;
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;

            float radius = Mathf.Clamp(flat.magnitude + 3.0f, 3.0f, 6.0f);
            Vector3 p1 = center + dir * radius + Vector3.up * Random.Range(0.3f, 1.0f);
            Vector3 p2 = center + side * radius * Random.Range(0.7f, 1.2f) + Vector3.up * Random.Range(0.3f, 1.2f);
            Vector3 p3 = center + new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(-0.1f, 0.4f), Random.Range(-0.2f, 0.2f));

            float dur = 0.65f;

            Sequence seq = DOTween.Sequence();
            seq.AppendInterval(delay);
            seq.Append(tr.DOPath(new[] { p1, p2, p3 }, dur, PathType.CatmullRom).SetEase(Ease.InOutSine));
            seq.Join(tr.DORotate(new Vector3(0f, 720f, 0f), dur, RotateMode.FastBeyond360).SetEase(Ease.OutQuad));
            seq.Join(tr.DOScale(0f, dur).SetEase(Ease.InQuad));
            seq.OnComplete(() => { if (item != null) Destroy(item.gameObject); });
        }


        _items.Clear();

        _trail.Clear();
        _trail.Add(transform.position);
        _lastRecorded = transform.position;

        DOVirtual.DelayedCall(0.35f, () => _depositing = false);
    }
}
