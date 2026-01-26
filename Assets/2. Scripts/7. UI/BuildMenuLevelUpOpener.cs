using UnityEngine;

[DisallowMultipleComponent]
public sealed class BuildMenuLevelUpOpener : MonoBehaviour
{
    [SerializeField] private float pauseTimeScale = 0f;
    [SerializeField] private UIPanelQueue panelQueue;

    private RunScope _scope;
    private ResourceProgression _progression;
    private BuildMenuPanel _menu;
    private SkillMenuPanel _skillMenu;
    private bool _blockedByDeposit;
    private bool _waitingForOpen;
    private bool _menuBound;
    private int _pendingOpenCount;
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
        UnbindMenu();
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

        ResolveMenu();
    }

    private void Unbind()
    {
        if (_progression != null)
            _progression.BaseLevelUp -= OnBaseLevelUp;

        _scope = null;
        _progression = null;
        _pendingOpenCount = 0;
        _blockedByDeposit = false;
        _waitingForOpen = false;
    }

    private void OnBaseLevelUp(int level)
    {
        _pendingOpenCount = Mathf.Max(0, _pendingOpenCount + 1);
        ResolveMenu();

        if (PickupTrain.IsDepositAnimating)
        {
            _blockedByDeposit = true;
            PauseForLevelUp();
            return;
        }

        TryRequestOpen();
    }

    private void OnDepositAnimationChanged(bool active)
    {
        if (active) return;
        if (!_blockedByDeposit) return;
        _blockedByDeposit = false;

        TryRequestOpen();
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
        if (!buildModeOn && !IsSkillMenuOpen())
            Time.timeScale = Mathf.Approximately(_prevTimeScale, 0f) ? 1f : _prevTimeScale;
        _pausedForLevelUp = false;
    }

    private void TryRequestOpen()
    {
        if (_pendingOpenCount <= 0) return;
        ResolveMenu();
        if (_menu != null && _menu.IsOpen) return;
        if (_waitingForOpen) return;
        if (panelQueue == null && _menu == null) return;

        _waitingForOpen = true;
        RequestOpen();
    }

    private void RequestOpen()
    {
        ResolvePanelQueue();
        if (panelQueue != null)
        {
            panelQueue.RequestBuildMenu();
            return;
        }

        if (_menu == null)
            _menu = FindObjectOfType<BuildMenuPanel>(true);
        if (_menu != null && !_menu.IsOpen)
            _menu.Open();
    }

    private void ResolveMenu()
    {
        if (_menu == null)
            _menu = FindObjectOfType<BuildMenuPanel>(true);
        if (_menu == null) return;
        if (_menuBound) return;
        _menu.OpenStateChanged += OnMenuStateChanged;
        _menuBound = true;
    }

    private void UnbindMenu()
    {
        if (_menu == null) return;
        if (!_menuBound) return;
        _menu.OpenStateChanged -= OnMenuStateChanged;
        _menuBound = false;
    }

    private void OnMenuStateChanged(bool open)
    {
        if (open)
        {
            _waitingForOpen = false;
            if (_pendingOpenCount > 0)
                _pendingOpenCount -= 1;
            return;
        }

        _waitingForOpen = false;
        TryRequestOpen();
    }

    private void ResolvePanelQueue()
    {
        if (panelQueue != null) return;
        panelQueue = FindObjectOfType<UIPanelQueue>(true);
    }

    private bool IsSkillMenuOpen()
    {
        if (_skillMenu == null)
            _skillMenu = FindObjectOfType<SkillMenuPanel>(true);
        return _skillMenu != null && _skillMenu.IsOpen;
    }
}
