using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class BuildMenuOpenButton : MonoBehaviour
{
    [SerializeField] private BuildMenuPanel menu;
    [SerializeField] private UIPanelQueue panelQueue;
    private Button _btn;

    private void Awake()
    {
        _btn = GetComponent<Button>();
        _btn.onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        if (panelQueue == null) panelQueue = FindObjectOfType<UIPanelQueue>(true);
        if (panelQueue != null)
        {
            panelQueue.RequestBuildMenu();
            return;
        }

        if (menu == null) menu = FindObjectOfType<BuildMenuPanel>(true);
        if (menu != null && !menu.IsOpen) menu.Open();
    }

}
