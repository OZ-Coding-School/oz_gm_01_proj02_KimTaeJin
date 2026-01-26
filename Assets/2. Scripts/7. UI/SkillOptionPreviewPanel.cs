using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SkillOptionPreviewPanel : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject root;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text damageText;
    [SerializeField] private TMP_Text speedText;
    [SerializeField] private TMP_Text rangeText;
    [SerializeField] private TMP_Text typeText;

    [Header("표시 옵션")]
    [SerializeField] private bool showIcon = false;
    [SerializeField] private bool showName = false;
    [SerializeField] private bool showLevel = false;
    [SerializeField] private bool showType = false;
    [SerializeField] private bool showOnlyOnUpgrade = true;

    [Header("표시 색상")]
    [SerializeField] private Color changedValueColor = new Color(0.3f, 0.95f, 0.3f, 1f);
    [SerializeField] private Color deltaValueColor = new Color(1f, 0.9f, 0.25f, 1f);

    [Header("포맷")]
    [SerializeField] private string levelFormat = "Lv {0}";
    [SerializeField] private string newLabel = "신규";
    [SerializeField] private string upgradeLabel = "강화";
    [SerializeField] private string noneText = "-";
    [SerializeField] private string damageLabel = "피해량";
    [SerializeField] private string speedLabel = "공격속도";
    [SerializeField] private string rangeLabel = "사거리";

    [Header("참조")]
    [SerializeField] private PlayerSkillSystem skillSystem;

    private RunScope _scope;

    private void Awake()
    {
        if (root == null) root = gameObject;
        SetVisible(false);
    }

    public void Bind(PlayerSkillSystem system)
    {
        skillSystem = system;
    }

    public void ShowOption(PlayerSkillOptionSO option)
    {
        if (option == null || option.targetSkill == null)
        {
            SetVisible(false);
            return;
        }

        EnsureSkillSystem();
        if (skillSystem == null)
        {
            SetVisible(false);
            return;
        }

        if (!skillSystem.TryGetPreview(option, out PlayerSkillSystem.SkillSnapshot current, out PlayerSkillSystem.SkillSnapshot preview))
        {
            SetVisible(false);
            return;
        }

        if (showOnlyOnUpgrade && option.optionType != PlayerSkillOptionSO.OptionType.UpgradeSkill)
        {
            SetVisible(false);
            return;
        }

        var def = option.targetSkill;
        if (icon != null && showIcon)
        {
            icon.sprite = def.icon;
            icon.enabled = icon.sprite != null;
        }
        if (nameText != null)
        {
            nameText.gameObject.SetActive(showName);
            nameText.text = showName ? def.displayName : string.Empty;
        }
        if (levelText != null)
        {
            levelText.gameObject.SetActive(showLevel);
            levelText.text = showLevel ? string.Format(levelFormat, preview.Level) : string.Empty;
        }
        if (typeText != null)
        {
            typeText.gameObject.SetActive(showType);
            typeText.text = showType ? (option.optionType == PlayerSkillOptionSO.OptionType.AddSkill ? newLabel : upgradeLabel) : string.Empty;
        }

        if (option.optionType == PlayerSkillOptionSO.OptionType.AddSkill)
        {
            SetStatText(damageText, damageLabel, preview.Damage, true);
            SetStatText(speedText, speedLabel, preview.AttackSpeed, false);
            SetStatText(rangeText, rangeLabel, preview.Range, false);
        }
        else
        {
            SetStatCompare(damageText, damageLabel, current.Damage, preview.Damage, true);
            SetStatCompare(speedText, speedLabel, current.AttackSpeed, preview.AttackSpeed, false);
            SetStatCompare(rangeText, rangeLabel, current.Range, preview.Range, false);
        }

        SetVisible(true);
    }

    public void Hide()
    {
        SetVisible(false);
    }

    private void EnsureSkillSystem()
    {
        if (skillSystem != null) return;
        _scope = _scope != null ? _scope : RunScopeLocator.Current;
        if (_scope != null && _scope.Entities != null && _scope.Entities.Player != null)
            skillSystem = _scope.Entities.Player.Skills;
        if (skillSystem == null)
            skillSystem = FindObjectOfType<PlayerSkillSystem>(true);
    }

    private void SetVisible(bool on)
    {
        if (root != null) root.SetActive(on);
    }

    private void SetStatText(TMP_Text text, string label, float value, bool integer)
    {
        if (text == null) return;
        string valueText = integer ? Mathf.RoundToInt(value).ToString() : value.ToString("0.0");
        text.text = string.IsNullOrEmpty(label) ? valueText : $"{label} {valueText}";
    }

    private void SetStatCompare(TMP_Text text, string label, float cur, float next, bool integer)
    {
        if (text == null) return;
        string curText = integer ? Mathf.RoundToInt(cur).ToString() : cur.ToString("0.0");
        string nextText = integer ? Mathf.RoundToInt(next).ToString() : next.ToString("0.0");
        float delta = next - cur;

        if (Mathf.Abs(delta) < 0.0001f)
        {
            text.text = string.IsNullOrEmpty(label) ? curText : $"{label} {curText}";
            return;
        }

        string deltaText = integer
            ? $"{(delta >= 0 ? "+" : string.Empty)}{Mathf.RoundToInt(delta)}"
            : $"{(delta >= 0 ? "+" : string.Empty)}{delta:0.0}";

        string body = $"{curText} -> {Colorize(nextText, changedValueColor)} ({Colorize(deltaText, deltaValueColor)})";
        text.text = string.IsNullOrEmpty(label) ? body : $"{label} {body}";
    }

    private static string Colorize(string text, Color color)
    {
        return $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";
    }

    public void ClearTexts()
    {
        if (icon != null && showIcon) icon.sprite = null;
        if (nameText != null) nameText.text = string.Empty;
        if (levelText != null) levelText.text = string.Empty;
        if (damageText != null) damageText.text = noneText;
        if (speedText != null) speedText.text = noneText;
        if (rangeText != null) rangeText.text = noneText;
        if (typeText != null) typeText.text = string.Empty;
    }
}
