using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public sealed class ResourceProgressHUD : MonoBehaviour
{
    [Header("Base EXP")]
    [SerializeField] private TMP_Text expText;
    [SerializeField] private Image expFill;
    [SerializeField] private Slider expSlider;
    [SerializeField] private GameObject expBarRoot;
    [SerializeField] private string expFormat = "{0}";

    [Header("Wood Skill")]
    [SerializeField] private TMP_Text woodLevelText;
    [SerializeField] private Image woodFill;
    [SerializeField] private Slider woodSlider;
    [SerializeField] private string woodLevelFormat = "Lv : {0} +({1:0}%)";

    [Header("Stone Skill")]
    [SerializeField] private TMP_Text stoneCountText;
    [SerializeField] private TMP_Text stoneLevelText;
    [SerializeField] private string stoneCountFormat = "{0}";
    [SerializeField] private string stoneLevelFormat = "Stone Lv.{0}";
    [SerializeField] private bool useStoneCountTween = true;
    [SerializeField] private float stoneCountTweenDuration = 0.35f;
    [SerializeField] private Ease stoneCountTweenEase = Ease.OutExpo;

    [Header("Tuning")]
    [SerializeField] private bool clampFill01 = true;
    [SerializeField] private bool hideExpBarOnBuildMode = true;
    [SerializeField] private float expFillTweenDuration = 0.3f;
    [SerializeField] private Ease expFillTweenEase = Ease.OutExpo;
    [SerializeField] private float expLevelDropDuration = 0.08f;
    [SerializeField] private float expLevelHoldDuration = 0.04f;

    [Header("Panels")]
    [SerializeField] private bool hideExpBarOnSkillMenu = true;
    [SerializeField] private SkillMenuPanel skillMenuPanel;

    private RunScope _scope;
    private ResourceProgression _progression;
    private RunEventBus _events;
    private Tween _expFillTween;
    private Sequence _expFillSequence;
    private float _expFillValue;
    private bool _expFillInitialized;
    private bool _expLevelInitialized;
    private int _expLevel;
    private Tween _stoneCountTween;
    private float _stoneCountValue;
    private bool _stoneCountInitialized;
    private bool _buildModeOn;
    private bool _skillMenuOn;

    private void Awake()
    {
        DisableSliderInput(expSlider);
        DisableSliderInput(woodSlider);
    }

    private void OnEnable()
    {
        RunScopeLocator.Changed += OnScopeChanged;
        TryBind();
    }

    private void OnDisable()
    {
        RunScopeLocator.Changed -= OnScopeChanged;
        Unbind();
        KillExpFillTween();
        KillExpFillSequence();
    }

    private void OnScopeChanged(RunScope scope)
    {
        TryBind();
    }

    private void TryBind()
    {
        Unbind();

        _scope = RunScopeLocator.Current;
        if (_scope == null) return;

        _progression = _scope.Progression;
        if (_progression == null) return;

        _progression.BaseExpChanged += OnBaseExpChanged;
        _progression.WoodProgressChanged += OnWoodProgressChanged;
        _progression.StoneCountChanged += OnStoneCountChanged;

        _events = _scope.Events;
        if (_events != null)
        {
            _events.BuildModeChanged += OnBuildModeChanged;
            _buildModeOn = _events.IsBuildMode;
        }
        else
        {
            _buildModeOn = false;
        }

        BindSkillMenu();
        UpdateExpBarVisibility();

        OnBaseExpChanged(_progression.BaseLevel, _progression.BaseExp, _progression.BaseExpToLevel);
        OnWoodProgressChanged(_progression.WoodLevel, _progression.WoodStored, _progression.WoodPerLevel);
        OnStoneCountChanged(_progression.StoneLevel, _progression.StoneCount);
    }

    private void Unbind()
    {
        if (_progression != null)
        {
            _progression.BaseExpChanged -= OnBaseExpChanged;
            _progression.WoodProgressChanged -= OnWoodProgressChanged;
            _progression.StoneCountChanged -= OnStoneCountChanged;
        }

        if (_events != null)
            _events.BuildModeChanged -= OnBuildModeChanged;

        UnbindSkillMenu();
        _progression = null;
        _scope = null;
        _events = null;
        _expFillInitialized = false;
        _expFillValue = 0f;
        _expLevelInitialized = false;
        _expLevel = 0;
        _stoneCountInitialized = false;
        _stoneCountValue = 0f;
        _buildModeOn = false;
        _skillMenuOn = false;
        KillExpFillTween();
        KillExpFillSequence();
        KillStoneCountTween();
    }

    private void OnBaseExpChanged(int level, int exp, int expToLevel)
    {
        if (expText != null)
            expText.text = string.Format(expFormat, level, exp, expToLevel);

        float denom = Mathf.Max(1, expToLevel);
        float fill = expToLevel <= 0 ? 0f : exp / denom;
        SetExpFill(fill, level);
    }

    private void OnWoodProgressChanged(int level, int stored, int perLevel)
    {
        if (woodLevelText != null)
        {
            float bonusPercent = 0f;
            if (_progression != null)
                bonusPercent = Mathf.Max(0f, _progression.WoodAttackSpeedBonusPerLevel) * Mathf.Max(0, level) * 100f;
            woodLevelText.text = string.Format(woodLevelFormat, level, bonusPercent, stored, perLevel);
        }

        float denom = Mathf.Max(1, perLevel);
        float fill = perLevel <= 0 ? 0f : stored / denom;
        ApplyFill(woodFill, woodSlider, fill);
    }

    private void OnStoneCountChanged(int level, int count)
    {
        if (stoneCountText != null)
        {
            if (!useStoneCountTween || stoneCountTweenDuration <= 0f)
            {
                stoneCountText.text = string.Format(stoneCountFormat, count, level);
            }
            else if (!_stoneCountInitialized)
            {
                _stoneCountInitialized = true;
                _stoneCountValue = count;
                SetStoneCountText(Mathf.RoundToInt(_stoneCountValue), level);
            }
            else
            {
                KillStoneCountTween();
                _stoneCountTween = DOTween.To(() => _stoneCountValue, v =>
                {
                    _stoneCountValue = v;
                    SetStoneCountText(Mathf.RoundToInt(v), level);
                }, count, stoneCountTweenDuration).SetEase(stoneCountTweenEase).SetUpdate(true);
            }
        }

        if (stoneLevelText != null)
            stoneLevelText.text = string.Format(stoneLevelFormat, level, count);
    }

    private void ApplyFill(Image fillImage, Slider slider, float value)
    {
        if (clampFill01) value = Mathf.Clamp01(value);
        if (slider != null)
            slider.SetValueWithoutNotify(value);
        if (fillImage != null && !IsSliderFillImage(slider, fillImage))
            fillImage.fillAmount = value;
    }

    private void SetExpFill(float value, int level)
    {
        if (!_expFillInitialized)
        {
            _expFillInitialized = true;
            _expLevelInitialized = true;
            _expLevel = level;
            _expFillValue = value;
            KillExpFillTween();
            KillExpFillSequence();
            ApplyFill(expFill, expSlider, value);
            return;
        }

        if (!_expLevelInitialized)
        {
            _expLevelInitialized = true;
            _expLevel = level;
        }

        if (level > _expLevel)
        {
            PlayExpLevelUpSequence(level - _expLevel, value);
            _expLevel = level;
            return;
        }

        _expLevel = level;

        if (Mathf.Approximately(_expFillValue, value) || expFillTweenDuration <= 0f)
        {
            ApplyFill(expFill, expSlider, value);
            return;
        }

        TweenExpFill(value);
    }

    private void KillExpFillTween()
    {
        if (_expFillTween == null) return;
        _expFillTween.Kill();
        _expFillTween = null;
    }

    private void KillExpFillSequence()
    {
        if (_expFillSequence == null) return;
        _expFillSequence.Kill();
        _expFillSequence = null;
    }

    private void KillStoneCountTween()
    {
        if (_stoneCountTween == null) return;
        _stoneCountTween.Kill();
        _stoneCountTween = null;
    }

    private void TweenExpFill(float value)
    {
        KillExpFillSequence();
        KillExpFillTween();
        _expFillTween = DOTween.To(() => _expFillValue, v =>
        {
            _expFillValue = v;
            ApplyFill(expFill, expSlider, v);
        }, value, expFillTweenDuration).SetEase(expFillTweenEase).SetUpdate(true);
    }

    private void PlayExpLevelUpSequence(int levelUps, float targetValue)
    {
        if (levelUps <= 0)
        {
            TweenExpFill(targetValue);
            return;
        }

        if (expFillTweenDuration <= 0f)
        {
            _expFillValue = targetValue;
            ApplyFill(expFill, expSlider, targetValue);
            return;
        }

        KillExpFillTween();
        KillExpFillSequence();

        float fillDuration = expFillTweenDuration;
        float dropDuration = Mathf.Max(0.02f, expLevelDropDuration);
        _expFillSequence = DOTween.Sequence().SetUpdate(true);

        for (int i = 0; i < levelUps; i++)
        {
            _expFillSequence.Append(DOTween.To(() => _expFillValue, v =>
            {
                _expFillValue = v;
                ApplyFill(expFill, expSlider, v);
            }, 1f, fillDuration).SetEase(expFillTweenEase));

            if (expLevelHoldDuration > 0f)
                _expFillSequence.AppendInterval(expLevelHoldDuration);

            _expFillSequence.Append(DOTween.To(() => _expFillValue, v =>
            {
                _expFillValue = v;
                ApplyFill(expFill, expSlider, v);
            }, 0f, dropDuration).SetEase(Ease.OutQuad));
        }

        _expFillSequence.Append(DOTween.To(() => _expFillValue, v =>
        {
            _expFillValue = v;
            ApplyFill(expFill, expSlider, v);
        }, targetValue, fillDuration).SetEase(expFillTweenEase));
    }

    private static bool IsSliderFillImage(Slider slider, Image image)
    {
        if (slider == null || image == null) return false;
        if (slider.fillRect == null) return false;
        return slider.fillRect.GetComponent<Image>() == image;
    }

    private void SetStoneCountText(int count, int level)
    {
        if (stoneCountText == null) return;
        stoneCountText.text = string.Format(stoneCountFormat, count, level);
    }

    private void OnBuildModeChanged(bool on)
    {
        _buildModeOn = on;
        UpdateExpBarVisibility();
    }

    private void BindSkillMenu()
    {
        UnbindSkillMenu();
        if (!hideExpBarOnSkillMenu) return;
        if (skillMenuPanel == null)
            skillMenuPanel = FindObjectOfType<SkillMenuPanel>(true);
        if (skillMenuPanel == null) return;
        skillMenuPanel.OpenStateChanged += OnSkillMenuChanged;
        _skillMenuOn = skillMenuPanel.IsOpen;
    }

    private void UnbindSkillMenu()
    {
        if (skillMenuPanel != null)
            skillMenuPanel.OpenStateChanged -= OnSkillMenuChanged;
        _skillMenuOn = false;
    }

    private void OnSkillMenuChanged(bool on)
    {
        _skillMenuOn = on;
        UpdateExpBarVisibility();
    }

    private void UpdateExpBarVisibility()
    {
        bool hide = (hideExpBarOnBuildMode && _buildModeOn) || (hideExpBarOnSkillMenu && _skillMenuOn);
        SetExpBarVisible(!hide);
    }

    private void SetExpBarVisible(bool visible)
    {
        if (expBarRoot != null)
        {
            expBarRoot.SetActive(visible);
            return;
        }

        if (expSlider != null)
            expSlider.gameObject.SetActive(visible);
        if (expFill != null)
            expFill.gameObject.SetActive(visible);
    }

    private void DisableSliderInput(Slider slider)
    {
        if (slider == null) return;
        slider.interactable = false;
        var nav = slider.navigation;
        nav.mode = Navigation.Mode.None;
        slider.navigation = nav;
    }
}
