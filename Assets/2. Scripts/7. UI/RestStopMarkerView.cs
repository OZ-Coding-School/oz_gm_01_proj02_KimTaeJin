using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public sealed class RestStopMarkerView : MonoBehaviour
{
    [Header("UI 표시")]
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text label;
    [SerializeField] private bool hideWhenNoIcon = true;
    [SerializeField] private bool hideWhenNoLabel = true;

    public void Apply(RestStopMarker marker)
    {
        if (marker == null)
        {
            SetIcon(null);
            SetLabel(null);
            return;
        }

        SetIcon(marker.HudIcon);
        SetLabel(marker.HudLabel);
    }

    public void Apply(RunProgressPoint marker)
    {
        if (marker == null)
        {
            SetIcon(null);
            SetLabel(null);
            return;
        }

        SetIcon(marker.HudIcon);
        SetLabel(marker.HudLabel);
    }

    private void SetIcon(Sprite sprite)
    {
        if (icon == null) return;
        icon.sprite = sprite;
        if (hideWhenNoIcon)
            icon.gameObject.SetActive(sprite != null);
    }

    private void SetLabel(string text)
    {
        if (label == null) return;
        bool has = !string.IsNullOrEmpty(text);
        label.text = text ?? string.Empty;
        if (hideWhenNoLabel)
            label.gameObject.SetActive(has);
    }
}
