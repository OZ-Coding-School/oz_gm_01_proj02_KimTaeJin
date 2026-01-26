using UnityEngine;

[DisallowMultipleComponent]
public sealed class GameAudio : MonoBehaviour
{
    public static GameAudio Instance { get; private set; }

    [Header("소스")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource lowHpSource;

    [Header("설정")]
    [SerializeField] private GameSfxCatalogSO sfxCatalog;
    [SerializeField] private bool dontDestroyOnLoad = true;
    [SerializeField] private bool loadPrefs = true;
    [SerializeField] private bool savePrefs = true;
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
    [SerializeField] private bool lowHpUseBgmVolume = true;

    private float _bgmTrackVolume = 1f;
    private float _lowHpClipVolume = 1f;

    private const string BgmKey = "Audio.BgmVolume";
    private const string SfxKey = "Audio.SfxVolume";

    public float BgmVolume => bgmVolume;
    public float SfxVolume => sfxVolume;
    public GameSfxCatalogSO SfxCatalog => sfxCatalog;

    public static GameAudio Ensure()
    {
        if (Instance != null) return Instance;
        var found = FindObjectOfType<GameAudio>();
        if (found != null) return found;
        var go = new GameObject("[GameAudio]");
        return go.AddComponent<GameAudio>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);

        EnsureSources();
        LoadPrefsIfNeeded();
        ApplyBgmVolume();
        ApplyLowHpVolume();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void PlayBgm(AudioClip clip, bool loop, float volume = 1f)
    {
        EnsureSources();
        if (clip == null)
        {
            StopBgm();
            return;
        }

        if (bgmSource.clip == clip)
        {
            bgmSource.loop = loop;
            _bgmTrackVolume = Mathf.Clamp01(volume);
            ApplyBgmVolume();
            if (!bgmSource.isPlaying) bgmSource.Play();
            return;
        }

        bgmSource.clip = clip;
        bgmSource.loop = loop;
        _bgmTrackVolume = Mathf.Clamp01(volume);
        ApplyBgmVolume();
        bgmSource.Play();
    }

    public void StopBgm()
    {
        if (bgmSource == null) return;
        bgmSource.Stop();
        bgmSource.clip = null;
    }

    public void PlaySfx(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;
        EnsureSources();
        float vol = Mathf.Clamp01(volume) * Mathf.Clamp01(sfxVolume);
        if (vol <= 0f) return;
        sfxSource.PlayOneShot(clip, vol);
    }

    public void SetBgmVolume(float value)
    {
        bgmVolume = Mathf.Clamp01(value);
        ApplyBgmVolume();
        ApplyLowHpVolume();
        SavePrefsIfNeeded();
    }

    public void SetSfxVolume(float value)
    {
        sfxVolume = Mathf.Clamp01(value);
        ApplyLowHpVolume();
        SavePrefsIfNeeded();
    }

    public void PlayTowerPlaceConfirm()
    {
        if (sfxCatalog == null) return;
        PlayCatalogSfx(sfxCatalog.towerPlaceConfirm);
    }

    public void PlayTowerPlaceBlocked()
    {
        if (sfxCatalog == null) return;
        PlayCatalogSfx(sfxCatalog.towerPlaceBlocked);
    }

    public void PlayHarvestHit()
    {
        PlayHarvestHit(ResourceType.None);
    }

    public void PlayHarvestPickup()
    {
        PlayHarvestPickup(ResourceType.None);
    }

    public void PlayHarvestHit(ResourceType type)
    {
        if (sfxCatalog == null) return;
        var sfx = ResolveHarvestHit(type);
        PlayCatalogSfx(sfx);
    }

    public void PlayHarvestPickup(ResourceType type)
    {
        if (sfxCatalog == null) return;
        var sfx = ResolveHarvestPickup(type);
        PlayCatalogSfx(sfx);
    }

    public void PlayRestStopEnter()
    {
        if (sfxCatalog == null) return;
        PlayCatalogSfx(sfxCatalog.restStopEnter);
    }

    public void PlayRestStopExit()
    {
        if (sfxCatalog == null) return;
        PlayCatalogSfx(sfxCatalog.restStopExit);
    }

    public void PlayPlayerHit()
    {
        if (sfxCatalog == null) return;
        PlayCatalogSfx(sfxCatalog.playerHit);
    }

    public void PlayLowHpPulse()
    {
        if (sfxCatalog == null) return;
        PlayCatalogSfx(sfxCatalog.lowHpPulse);
    }

    public void SetLowHpLoopActive(bool active)
    {
        if (sfxCatalog == null) return;
        EnsureSources();

        if (!active)
        {
            StopLowHpLoop();
            return;
        }

        var clip = sfxCatalog.lowHpPulse.clip;
        if (clip == null)
        {
            StopLowHpLoop();
            return;
        }

        _lowHpClipVolume = Mathf.Clamp01(sfxCatalog.lowHpPulse.volume);
        if (lowHpSource.clip != clip)
            lowHpSource.clip = clip;
        lowHpSource.loop = true;
        ApplyLowHpVolume();
        if (!lowHpSource.isPlaying) lowHpSource.Play();
    }

    public void PlayPathBlocked()
    {
        if (sfxCatalog == null) return;
        PlayCatalogSfx(sfxCatalog.pathBlocked);
    }

    private void EnsureSources()
    {
        if (bgmSource == null)
        {
            bgmSource = GetComponent<AudioSource>();
            if (bgmSource == null) bgmSource = gameObject.AddComponent<AudioSource>();
        }

        if (sfxSource == null)
        {
            var sources = GetComponents<AudioSource>();
            if (sources.Length > 1)
            {
                sfxSource = sources[0] == bgmSource ? sources[1] : sources[0];
            }
            else
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
            }
        }

        if (lowHpSource == null)
            lowHpSource = gameObject.AddComponent<AudioSource>();

        ConfigureSource(bgmSource, true);
        ConfigureSource(sfxSource, false);
        ConfigureSource(lowHpSource, true);
    }

    private static void ConfigureSource(AudioSource source, bool isBgm)
    {
        if (source == null) return;
        source.playOnAwake = false;
        source.loop = isBgm;
        source.spatialBlend = 0f;
    }

    private void ApplyBgmVolume()
    {
        if (bgmSource == null) return;
        bgmSource.volume = Mathf.Clamp01(bgmVolume) * Mathf.Clamp01(_bgmTrackVolume);
    }

    private void ApplyLowHpVolume()
    {
        if (lowHpSource == null) return;
        float baseVol = lowHpUseBgmVolume ? bgmVolume : sfxVolume;
        lowHpSource.volume = Mathf.Clamp01(baseVol) * Mathf.Clamp01(_lowHpClipVolume);
    }

    private void StopLowHpLoop()
    {
        if (lowHpSource == null) return;
        lowHpSource.Stop();
        lowHpSource.clip = null;
    }

    private void PlayCatalogSfx(GameSfxCatalogSO.SfxClip sfx)
    {
        if (sfx.clip == null) return;
        float vol = Mathf.Clamp01(sfx.volume);
        if (vol <= 0f) return;
        PlaySfx(sfx.clip, vol);
    }

    private GameSfxCatalogSO.SfxClip ResolveHarvestHit(ResourceType type)
    {
        if (sfxCatalog == null) return default;
        if (type == ResourceType.Wood && sfxCatalog.harvestHitWood.clip != null)
            return sfxCatalog.harvestHitWood;
        if (type == ResourceType.Stone && sfxCatalog.harvestHitStone.clip != null)
            return sfxCatalog.harvestHitStone;
        return sfxCatalog.harvestHit;
    }

    private GameSfxCatalogSO.SfxClip ResolveHarvestPickup(ResourceType type)
    {
        if (sfxCatalog == null) return default;
        if (type == ResourceType.Wood && sfxCatalog.harvestPickupWood.clip != null)
            return sfxCatalog.harvestPickupWood;
        if (type == ResourceType.Stone && sfxCatalog.harvestPickupStone.clip != null)
            return sfxCatalog.harvestPickupStone;
        return sfxCatalog.harvestPickup;
    }

    private void LoadPrefsIfNeeded()
    {
        if (!loadPrefs) return;
        if (PlayerPrefs.HasKey(BgmKey)) bgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(BgmKey));
        if (PlayerPrefs.HasKey(SfxKey)) sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxKey));
    }

    private void SavePrefsIfNeeded()
    {
        if (!savePrefs) return;
        PlayerPrefs.SetFloat(BgmKey, bgmVolume);
        PlayerPrefs.SetFloat(SfxKey, sfxVolume);
    }
}
