using UnityEngine;

public sealed class BuildModePauseSystem : MonoBehaviour
{
    [SerializeField] private float pausedTimeScale = 0f;

    private RunScope _scope;
    private float _prev;

    public void Construct(RunScope scope)
    {
        _scope = scope;
        if (_scope != null && _scope.Events != null)
            _scope.Events.BuildModeChanged += OnBuildModeChanged;
    }

    private void OnDestroy()
    {
        if (_scope != null && _scope.Events != null)
            _scope.Events.BuildModeChanged -= OnBuildModeChanged;

        Time.timeScale = 1f;
    }

    private void OnBuildModeChanged(bool on)
    {
        if (on)
        {
            _prev = Time.timeScale;
            Time.timeScale = pausedTimeScale;
        }
        else
        {
            Time.timeScale = Mathf.Approximately(_prev, 0f) ? 1f : _prev;
        }
    }
}
