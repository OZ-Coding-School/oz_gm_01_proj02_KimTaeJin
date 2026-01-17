using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed class ResourceProgressHUD : MonoBehaviour
{
    [Header("Base EXP")]
    [SerializeField] private TMP_Text expText;
    [SerializeField] private Image expFill;
    [SerializeField] private string expFormat = "EXP {0}/{1}";

    [Header("Wood Skill")]
    [SerializeField] private TMP_Text woodLevelText;
    [SerializeField] private Image woodFill;
    [SerializeField] private string woodLevelFormat = "Wood Lv.{0}";

    [Header("Stone Skill")]
    [SerializeField] private TMP_Text stoneCountText;
    [SerializeField] private TMP_Text stoneLevelText;
    [SerializeField] private string stoneCountFormat = "Stone {0}";
    [SerializeField] private string stoneLevelFormat = "Stone Lv.{0}";

    [Header("Tuning")]
    [SerializeField] private bool clampFill01 = true;

    private RunScope _scope;
    private ResourceProgression _progression;

    private void OnEnable()
    {
        RunScopeLocator.Changed += OnScopeChanged;
        TryBind();
    }

    private void OnDisable()
    {
        RunScopeLocator.Changed -= OnScopeChanged;
        Unbind();
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

        _progression = null;
        _scope = null;
    }

    private void OnBaseExpChanged(int level, int exp, int expToLevel)
    {
        if (expText != null)
            expText.text = string.Format(expFormat, exp, expToLevel);

        if (expFill != null)
        {
            float denom = Mathf.Max(1, expToLevel);
            float fill = expToLevel <= 0 ? 0f : exp / denom;
            if (clampFill01) fill = Mathf.Clamp01(fill);
            expFill.fillAmount = fill;
        }
    }

    private void OnWoodProgressChanged(int level, int stored, int perLevel)
    {
        if (woodLevelText != null)
            woodLevelText.text = string.Format(woodLevelFormat, level);

        if (woodFill != null)
        {
            float denom = Mathf.Max(1, perLevel);
            float fill = perLevel <= 0 ? 0f : stored / denom;
            if (clampFill01) fill = Mathf.Clamp01(fill);
            woodFill.fillAmount = fill;
        }
    }

    private void OnStoneCountChanged(int level, int count)
    {
        if (stoneCountText != null)
            stoneCountText.text = string.Format(stoneCountFormat, count);

        if (stoneLevelText != null)
            stoneLevelText.text = string.Format(stoneLevelFormat, level);
    }
}
