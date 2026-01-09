using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Button))]
public sealed class TowerBuildButton : MonoBehaviour
{
    [SerializeField] private TowerDefinitionSO tower;
    [SerializeField] private TMP_Text label;

    private Button _btn;

    private void Awake()
    {
        _btn = GetComponent<Button>();
        _btn.onClick.AddListener(OnClick);
        Refresh();
    }

    private void Refresh()
    {
        if (label != null && tower != null)
            label.text = $"{tower.displayName}\n{tower.cost}G";

        if (_btn != null)
            _btn.interactable = (tower != null);
    }

    private void OnClick()
    {
        var scope = RunScopeLocator.Current;
        if (scope == null || tower == null) return;

        scope.Events.RequestPlaceTower(tower);
    }
}
