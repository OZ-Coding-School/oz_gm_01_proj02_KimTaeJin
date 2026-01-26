using UnityEngine;

[DisallowMultipleComponent]
public sealed class RunProgressPoint : MonoBehaviour
{
    [SerializeField] private Transform point;
    [Header("HUD 표시")]
    [SerializeField] private Sprite hudIcon;
    [SerializeField] private string hudLabel;

    public Transform Point => point != null ? point : transform;
    public Sprite HudIcon => hudIcon;
    public string HudLabel => hudLabel;
}
