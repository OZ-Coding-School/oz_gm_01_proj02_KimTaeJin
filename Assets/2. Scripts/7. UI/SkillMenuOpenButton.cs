using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class SkillMenuOpenButton : MonoBehaviour
{
    [SerializeField] private SkillMenuPanel panel;
    [SerializeField] private UIPanelQueue panelQueue;
    [SerializeField] private KeyCode openKey = KeyCode.None;

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(Open);
    }

    private void Update()
    {
        if (openKey != KeyCode.None && Input.GetKeyDown(openKey))
            Open();
    }

    private void Open()
    {
        if (panelQueue == null) panelQueue = FindObjectOfType<UIPanelQueue>(true);
        if (panelQueue != null)
        {
            panelQueue.RequestSkillMenu();
            return;
        }

        if (panel == null) panel = FindObjectOfType<SkillMenuPanel>(true);
        if (panel == null || panel.IsOpen) return;
        panel.Open();
    }
}
