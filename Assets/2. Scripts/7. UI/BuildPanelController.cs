using UnityEngine;

public sealed class BuildPanelController : MonoBehaviour
{
    [SerializeField] private GameObject root;
    private RunScope _scope;

    private void Awake()
    {
        if (root == null) root = gameObject;
        root.SetActive(false);
    }

    public void Open()
    {
        _scope = RunScopeLocator.Current;
        if (_scope == null) return;

        root.SetActive(true);
        _scope.Events.PushBuildMode(this); 
    }

    public void Close()
    {
        if (_scope == null) _scope = RunScopeLocator.Current;

        root.SetActive(false);
        if (_scope != null) _scope.Events.PopBuildMode(this);
    }
}
