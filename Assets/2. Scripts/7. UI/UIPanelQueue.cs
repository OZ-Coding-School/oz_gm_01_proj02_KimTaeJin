using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class UIPanelQueue : MonoBehaviour
{
    public enum PanelType
    {
        None,
        BuildMenu,
        SkillMenu
    }

    [Header("Panels")]
    [SerializeField] private BuildMenuPanel buildMenu;
    [SerializeField] private SkillMenuPanel skillMenu;
    [SerializeField] private bool autoFindPanels = true;
    [SerializeField] private bool ignoreDuplicateRequests = true;

    private readonly Queue<PanelType> _queue = new();
    private PanelType _current = PanelType.None;

    private void Awake()
    {
        ResolvePanels();
        BindPanels();
    }

    private void OnDestroy()
    {
        UnbindPanels();
    }

    public void RequestBuildMenu()
    {
        RequestOpen(PanelType.BuildMenu);
    }

    public void RequestSkillMenu()
    {
        RequestOpen(PanelType.SkillMenu);
    }

    public void RequestOpen(PanelType type)
    {
        ResolvePanels();

        if (type == PanelType.None) return;
        if (IsPanelOpen(type)) return;

        if (_current != PanelType.None || AnyPanelOpen())
        {
            if (ignoreDuplicateRequests && IsQueued(type)) return;
            _queue.Enqueue(type);
            return;
        }

        OpenPanel(type);
    }

    private void ResolvePanels()
    {
        if (!autoFindPanels) return;
        if (buildMenu == null) buildMenu = FindObjectOfType<BuildMenuPanel>(true);
        if (skillMenu == null) skillMenu = FindObjectOfType<SkillMenuPanel>(true);
    }

    private void BindPanels()
    {
        if (buildMenu != null) buildMenu.OpenStateChanged += OnBuildMenuState;
        if (skillMenu != null) skillMenu.OpenStateChanged += OnSkillMenuState;
    }

    private void UnbindPanels()
    {
        if (buildMenu != null) buildMenu.OpenStateChanged -= OnBuildMenuState;
        if (skillMenu != null) skillMenu.OpenStateChanged -= OnSkillMenuState;
    }

    private void OnBuildMenuState(bool open)
    {
        if (open)
        {
            _current = PanelType.BuildMenu;
            return;
        }

        if (_current == PanelType.BuildMenu)
            _current = PanelType.None;

        TryOpenNext();
    }

    private void OnSkillMenuState(bool open)
    {
        if (open)
        {
            _current = PanelType.SkillMenu;
            return;
        }

        if (_current == PanelType.SkillMenu)
            _current = PanelType.None;

        TryOpenNext();
    }

    private void OpenPanel(PanelType type)
    {
        _current = type;
        switch (type)
        {
            case PanelType.BuildMenu:
                if (buildMenu == null)
                {
                    _current = PanelType.None;
                    TryOpenNext();
                    return;
                }
                buildMenu.Open();
                break;
            case PanelType.SkillMenu:
                if (skillMenu == null)
                {
                    _current = PanelType.None;
                    TryOpenNext();
                    return;
                }
                skillMenu.Open();
                break;
        }
    }

    private void TryOpenNext()
    {
        if (_current != PanelType.None) return;
        if (AnyPanelOpen()) return;

        while (_queue.Count > 0)
        {
            var next = _queue.Dequeue();
            if (IsPanelOpen(next)) continue;
            OpenPanel(next);
            break;
        }
    }

    private bool AnyPanelOpen()
    {
        return (buildMenu != null && buildMenu.IsOpen) ||
               (skillMenu != null && skillMenu.IsOpen);
    }

    private bool IsPanelOpen(PanelType type)
    {
        return type switch
        {
            PanelType.BuildMenu => buildMenu != null && buildMenu.IsOpen,
            PanelType.SkillMenu => skillMenu != null && skillMenu.IsOpen,
            _ => false
        };
    }

    private bool IsQueued(PanelType type)
    {
        foreach (var t in _queue)
            if (t == type) return true;
        return false;
    }
}
