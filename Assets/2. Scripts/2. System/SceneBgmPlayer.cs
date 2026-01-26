using UnityEngine;

[DisallowMultipleComponent]
public sealed class SceneBgmPlayer : MonoBehaviour
{
    [Header("BGM")]
    [SerializeField] private AudioClip clip;
    [SerializeField] private bool loop = true;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private bool stopOnDisable = true;

    private void OnEnable()
    {
        if (playOnEnable) Play();
    }

    private void OnDisable()
    {
        if (!stopOnDisable) return;
        GameAudio.Instance?.StopBgm();
    }

    public void Play()
    {
        var audio = GameAudio.Ensure();
        if (audio == null) return;
        audio.PlayBgm(clip, loop, volume);
    }
}
