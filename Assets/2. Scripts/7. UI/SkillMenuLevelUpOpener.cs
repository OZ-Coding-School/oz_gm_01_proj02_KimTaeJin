using UnityEngine;

[DisallowMultipleComponent]
public sealed class SkillMenuLevelUpOpener : MonoBehaviour
{
    [SerializeField] private GameObject skillMenuRoot;

    private RunScope _scope;
    private ResourceProgression _progression;

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
            _progression.StoneLevelUp += OnStoneLevelUp;
    }

    private void Unbind()
    {
        if (_progression != null)
            _progression.StoneLevelUp -= OnStoneLevelUp;

        _progression = null;
        _scope = null;
    }

    private void OnStoneLevelUp(int level)
    {
        if (skillMenuRoot == null) return;
        if (!skillMenuRoot.activeSelf)
            skillMenuRoot.SetActive(true);
    }
}
