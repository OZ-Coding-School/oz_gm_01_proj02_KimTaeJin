using UnityEngine;
using UnityEngine.EventSystems;

public sealed class UIPanelToggle : MonoBehaviour
{
    [SerializeField] private GameObject target;
    [SerializeField] private bool applyOnStart = true;
    [SerializeField] private bool openOnStart = false;
    [SerializeField] private bool clearSelectionOnClose = true;
    [SerializeField] private UIButtonSelectTween[] resetTweensOnClose;

    private void Start()
    {
        if (!applyOnStart) return;
        SetOpen(openOnStart);
    }

    public void SetOpen(bool open)
    {
        if (target == null) return;
        target.SetActive(open);
        if (!open)
            HandleCloseEffects();
    }

    public void Open()
    {
        SetOpen(true);
    }

    public void Close()
    {
        SetOpen(false);
    }

    public void Toggle()
    {
        if (target == null) return;
        SetOpen(!target.activeSelf);
    }

    private void HandleCloseEffects()
    {
        if (clearSelectionOnClose && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        if (resetTweensOnClose == null) return;
        for (int i = 0; i < resetTweensOnClose.Length; i++)
        {
            var tween = resetTweensOnClose[i];
            if (tween != null) tween.ResetState(true);
        }
    }
}
