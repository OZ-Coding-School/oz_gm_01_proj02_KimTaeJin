using UnityEngine;

[DisallowMultipleComponent]
public sealed class BuildMenuLevelUpOpener : MonoBehaviour
{
    [SerializeField] private float pauseTimeScale = 0f;

    private RunScope _scope;
    private ResourceProgression _progression;
    private BuildMenuPanel _menu;
    private bool _pendingOpen;
    private bool _pausedForLevelUp;
    private float _prevTimeScale = 1f;

    private void OnEnable()
    {
        RunScopeLocator.Changed += OnScopeChanged;
        PickupTrain.DepositAnimationChanged += OnDepositAnimationChanged;
        TryBind();
    }

    private void OnDisable()
    {
        RunScopeLocator.Changed -= OnScopeChanged;
        PickupTrain.DepositAnimationChanged -= OnDepositAnimationChanged;
        Unbind();
        ClearLevelUpPause();
    }

    private void OnScopeChanged(RunScope scope)
    {
        TryBind();
    }

    private void TryBind()
    {
        Unbind();

        _scope = RunScopeLocator.Current;
        if (_scope == null) return;

        _progression = _scope.Progression;
        if (_progression != null)
            _progression.BaseLevelUp += OnBaseLevelUp;
    }

    private void Unbind()
    {
        if (_progression != null)
            _progression.BaseLevelUp -= OnBaseLevelUp;

        _scope = null;
        _progression = null;
    }

    private void OnBaseLevelUp(int level)
    {
        if (_menu == null)
            _menu = FindObjectOfType<BuildMenuPanel>(true);
        if (_menu == null || _menu.IsOpen) return;

        if (PickupTrain.IsDepositAnimating)
        {
            _pendingOpen = true;
            PauseForLevelUp();
            return;
        }

        _pendingOpen = false;
        _menu.Open();
    }

    private void OnDepositAnimationChanged(bool active)
    {
        if (active) return;
        if (!_pendingOpen) return;
        _pendingOpen = false;

        if (_menu == null)
            _menu = FindObjectOfType<BuildMenuPanel>(true);
        if (_menu == null)
        {
            ClearLevelUpPause();
            return;
        }

        _menu.Open();
        ClearLevelUpPause();
    }

    private void PauseForLevelUp()
    {
        if (_pausedForLevelUp) return;
        _prevTimeScale = Time.timeScale;
        Time.timeScale = pauseTimeScale;
        _pausedForLevelUp = true;
    }

    private void ClearLevelUpPause()
    {
        if (!_pausedForLevelUp) return;
        var scope = _scope != null ? _scope : RunScopeLocator.Current;
        bool buildModeOn = scope != null && scope.Events != null && scope.Events.IsBuildMode;
        if (!buildModeOn)
            Time.timeScale = Mathf.Approximately(_prevTimeScale, 0f) ? 1f : _prevTimeScale;
        _pausedForLevelUp = false;
    }
}
