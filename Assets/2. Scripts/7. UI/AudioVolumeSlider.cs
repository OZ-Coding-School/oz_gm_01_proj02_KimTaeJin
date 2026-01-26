using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class AudioVolumeSlider : MonoBehaviour
{
    public enum VolumeType
    {
        Bgm = 0,
        Sfx = 1
    }

    [Header("타입")]
    [SerializeField] private VolumeType volumeType = VolumeType.Bgm;

    [Header("UI")]
    [SerializeField] private Slider slider;
    [SerializeField] private bool applyOnEnable = true;

    private void Awake()
    {
        if (slider == null) slider = GetComponent<Slider>();
    }

    private void OnEnable()
    {
        if (slider == null) return;
        if (applyOnEnable)
            slider.SetValueWithoutNotify(GetCurrentVolume());
        slider.onValueChanged.AddListener(OnSliderChanged);
    }

    private void OnDisable()
    {
        if (slider == null) return;
        slider.onValueChanged.RemoveListener(OnSliderChanged);
    }

    private void OnSliderChanged(float value)
    {
        var audio = GameAudio.Instance != null ? GameAudio.Instance : GameAudio.Ensure();
        if (audio == null) return;

        if (volumeType == VolumeType.Bgm)
            audio.SetBgmVolume(value);
        else
            audio.SetSfxVolume(value);
    }

    private float GetCurrentVolume()
    {
        var audio = GameAudio.Instance;
        if (audio == null) return 1f;
        return volumeType == VolumeType.Bgm ? audio.BgmVolume : audio.SfxVolume;
    }
}
