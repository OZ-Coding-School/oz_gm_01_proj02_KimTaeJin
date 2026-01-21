using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RestStopSystem : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private HouseDrift houseDrift;
    [SerializeField] private WorldScroller worldScroller;
    [SerializeField] private EnemySpawnSystem spawner;
    [SerializeField] private PlayAreaProgressController playArea;
    [SerializeField] private EndlessChunks chunks;

    [Header("Tuning")]
    [SerializeField] private bool stopHouseDrift = true;
    [SerializeField] private bool stopWorldScroller = true;
    [SerializeField] private bool stopSpawner = true;
    [SerializeField] private bool stopPlayArea = true;
    [SerializeField] private bool stopChunks = false;
    [SerializeField] private bool freezeEnemies = true;

    private RunScope _scope;
    private bool _isResting;

    private bool _houseDriftPrev;
    private bool _worldScrollerPrev;
    private bool _spawnerPrev;
    private bool _playAreaPrev;
    private bool _chunksPrev;

    private readonly HashSet<EnemyBrain> _pausedBrains = new();

    public bool IsResting => _isResting;
    public event System.Action<bool> RestStateChanged;

    private void Awake()
    {
        ResolveRefs();
    }

    public void EnterRestStop()
    {
        if (_isResting) return;
        _isResting = true;
        ResolveRefs();
        CaptureStates();
        ApplyResting(true);
        RestStateChanged?.Invoke(true);
    }

    public void ExitRestStop()
    {
        if (!_isResting) return;
        _isResting = false;
        ApplyResting(false);
        RestStateChanged?.Invoke(false);
    }

    private void ResolveRefs()
    {
        if (houseDrift == null) houseDrift = FindObjectOfType<HouseDrift>();
        if (worldScroller == null) worldScroller = FindObjectOfType<WorldScroller>();
        if (spawner == null) spawner = FindObjectOfType<EnemySpawnSystem>();
        if (playArea == null) playArea = FindObjectOfType<PlayAreaProgressController>();
        if (chunks == null) chunks = FindObjectOfType<EndlessChunks>();
        if (_scope == null) _scope = RunScopeLocator.Current;
    }

    private void CaptureStates()
    {
        _houseDriftPrev = houseDrift != null && houseDrift.enabled;
        _worldScrollerPrev = worldScroller != null && worldScroller.enabled;
        _spawnerPrev = spawner != null && spawner.enabled;
        _playAreaPrev = playArea != null && playArea.enabled;
        _chunksPrev = chunks != null && chunks.enabled;
    }

    private void ApplyResting(bool on)
    {
        if (on)
        {
            if (stopHouseDrift && houseDrift != null) houseDrift.enabled = false;
            if (stopWorldScroller && worldScroller != null) worldScroller.enabled = false;
            if (stopSpawner && spawner != null) spawner.enabled = false;
            if (stopPlayArea && playArea != null) playArea.enabled = false;
            if (stopChunks && chunks != null) chunks.enabled = false;
            if (freezeEnemies) PauseEnemies(true);
            return;
        }

        bool buildModeOn = _scope != null && _scope.Events != null && _scope.Events.IsBuildMode;
        if (!buildModeOn)
        {
            if (stopHouseDrift && houseDrift != null) houseDrift.enabled = _houseDriftPrev;
            if (stopWorldScroller && worldScroller != null) worldScroller.enabled = _worldScrollerPrev;
            if (stopSpawner && spawner != null) spawner.enabled = _spawnerPrev;
            if (stopPlayArea && playArea != null) playArea.enabled = _playAreaPrev;
            if (stopChunks && chunks != null) chunks.enabled = _chunksPrev;
        }

        if (freezeEnemies) PauseEnemies(false);
    }

    private void PauseEnemies(bool on)
    {
        var scope = _scope != null ? _scope : RunScopeLocator.Current;
        var enemies = scope?.Entities?.Enemies;
        if (enemies == null) return;

        if (on)
        {
            _pausedBrains.Clear();
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e == null) continue;

                var brain = e.GetComponent<EnemyBrain>();
                if (brain != null && brain.enabled)
                {
                    brain.enabled = false;
                    _pausedBrains.Add(brain);
                }

                var rb = e.GetComponent<Rigidbody>();
                if (rb != null) rb.velocity = Vector3.zero;
            }
            return;
        }

        foreach (var brain in _pausedBrains)
        {
            if (brain != null) brain.enabled = true;
        }
        _pausedBrains.Clear();
    }
}
