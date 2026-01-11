using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class BuildMenuOpenButton : MonoBehaviour
{
    [SerializeField] private BuildMenuPanel menu;
    private Button _btn;

    private void Awake()
    {
        _btn = GetComponent<Button>();
        _btn.onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        Debug.Log("[BuildMenuOpenButton] Click!");
        if (menu == null) menu = FindObjectOfType<BuildMenuPanel>(true);
        Debug.Log("[BuildMenuOpenButton] menu = " + (menu ? menu.name : "NULL"));
        if (menu != null) menu.Open();
    }

}
