using UnityEngine;

[DisallowMultipleComponent]
public sealed class SkillMenuLevelUpOpener : MonoBehaviour
{
    [SerializeField] private SkillMenuPanel panel;
    [SerializeField] private UIPanelQueue panelQueue;
    [SerializeField] private bool forceCloseOnBind = true;

    private RunScope _scope;
    private ResourceProgression _progression;

    private void OnEnable()
    {
        RunScopeLocator.Changed += OnScopeChanged;
        ForceCloseMenu();
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

        ForceCloseMenu();

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
        RequestOpen();
    }

    private void ForceCloseMenu()
    {
        if (!forceCloseOnBind) return;
        ResolvePanel();
        if (panel != null)
        {
            if (panel.IsOpen) panel.Close();
            return;
        }
    }

    private void RequestOpen()
    {
        ResolvePanelQueue();
        if (panelQueue != null)
        {
            panelQueue.RequestSkillMenu();
            return;
        }

        ResolvePanel();
        if (panel == null || panel.IsOpen) return;
        panel.Open();
    }

    private void ResolvePanel()
    {
        if (panel != null) return;
        panel = FindObjectOfType<SkillMenuPanel>(true);
    }

    private void ResolvePanelQueue()
    {
        if (panelQueue != null) return;
        panelQueue = FindObjectOfType<UIPanelQueue>(true);
    }
}
