using UnityEngine;

[DisallowMultipleComponent]
public sealed class SharedBuildingHealthGameOverPause : MonoBehaviour
{
    [SerializeField] private SharedBuildingHealth sharedHealth;
    [SerializeField] private float pauseTimeScale = 0f;
    [SerializeField] private bool usePause = false;
    [SerializeField] private bool restoreOnDisable = true;

    private bool _paused;
    private float _prevTimeScale = 1f;

    private void OnEnable()
    {
        ResolveShared();
        if (sharedHealth != null)
            sharedHealth.Dead += OnDead;
    }

    private void OnDisable()
    {
        if (sharedHealth != null)
            sharedHealth.Dead -= OnDead;
        if (_paused && restoreOnDisable)
            Time.timeScale = _prevTimeScale;
        sharedHealth = null;
        _paused = false;
    }

    private void ResolveShared()
    {
        if (sharedHealth != null) return;
        var scope = RunScopeLocator.Current;
        if (scope != null)
            sharedHealth = scope.GetComponent<SharedBuildingHealth>();
        if (sharedHealth == null)
            sharedHealth = FindObjectOfType<SharedBuildingHealth>();
    }

    private void OnDead()
    {
        if (_paused) return;
        if (!usePause) return;
        _prevTimeScale = Time.timeScale;
        Time.timeScale = pauseTimeScale;
        _paused = true;
    }
}
