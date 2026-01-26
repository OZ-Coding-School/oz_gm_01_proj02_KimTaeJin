using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemySlowEffect : MonoBehaviour
{
    private struct Slow
    {
        public float Multiplier;
        public float EndTime;
    }

    [SerializeField] private float minMultiplier = 0.1f;
    [SerializeField] private bool autoAddTint = true;

    private readonly List<Slow> _slows = new();
    private EnemyBrain _brain;
    private EnemySlowTint _tint;

    public bool HasSlow => _slows.Count > 0;

    private void Awake()
    {
        _brain = GetComponent<EnemyBrain>();
        if (autoAddTint)
        {
            _tint = GetComponent<EnemySlowTint>();
            if (_tint == null)
                _tint = gameObject.AddComponent<EnemySlowTint>();
        }
    }

    public void ApplySlow(float multiplier, float duration)
    {
        if (duration <= 0f) return;
        float slowRatio = Mathf.Clamp01(multiplier);
        float speedMultiplier = Mathf.Clamp(1f - slowRatio, minMultiplier, 1f);
        _slows.Add(new Slow { Multiplier = speedMultiplier, EndTime = Time.time + duration });
        RefreshSpeed();
    }

    private void Update()
    {
        if (_slows.Count == 0) return;

        float now = Time.time;
        for (int i = _slows.Count - 1; i >= 0; i--)
        {
            if (_slows[i].EndTime <= now)
                _slows.RemoveAt(i);
        }

        RefreshSpeed();
    }

    private void RefreshSpeed()
    {
        if (_brain == null) _brain = GetComponent<EnemyBrain>();
        if (_brain == null) return;

        float mult = 1f;
        for (int i = 0; i < _slows.Count; i++)
            mult = Mathf.Min(mult, _slows[i].Multiplier);

        _brain.SetSpeedMultiplier(mult);
    }
}
