using UnityEngine;

[DisallowMultipleComponent]
public sealed class BuildMenuLevelUpOpener : MonoBehaviour
{
    private RunScope _scope;
    private ResourceProgression _progression;
    private BuildMenuPanel _menu;

    private void OnEnable()
    {
        RunScopeLocator.Changed += OnScopeChanged;
        TryBind();
    }

    private void OnDisable()
    {
        RunScopeLocator.Changed -= OnScopeChanged;
        Unbind();
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
        _menu.Open();
    }
}
