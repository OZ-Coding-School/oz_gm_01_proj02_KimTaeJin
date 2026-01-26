using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SkillSlotUI : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text statsText;
    [SerializeField] private GameObject emptyRoot;
    [SerializeField] private GameObject backRoot;
    [SerializeField] private string levelFormat = "lvl {0}";
    [SerializeField] private Color previewLevelColor = new Color(0.2f, 1f, 0.4f);

    public void Clear()
    {
        if (emptyRoot != null) emptyRoot.SetActive(true);
        if (backRoot != null) backRoot.SetActive(false);
        if (icon != null)
        {
            icon.sprite = null;
            icon.enabled = false;
        }
        if (nameText != null) nameText.text = string.Empty;
        if (levelText != null) levelText.text = string.Empty;
        if (statsText != null) statsText.text = string.Empty;
    }

    public void SetSkill(PlayerSkillSystem.SkillSnapshot current, bool hasPreview, PlayerSkillSystem.SkillSnapshot preview, bool showLevelPreview, int previewLevel)
    {
        if (current.Definition == null)
        {
            Clear();
            return;
        }

        if (emptyRoot != null) emptyRoot.SetActive(false);
        if (icon != null)
        {
            icon.sprite = current.Definition.icon;
            icon.enabled = icon.sprite != null;
        }
        if (backRoot != null)
        {
            bool hasIcon = icon != null && icon.sprite != null;
            backRoot.SetActive(hasIcon);
        }
        if (nameText != null) nameText.text = current.Definition.displayName;

        int levelValue = current.Level;
        bool usePreviewColor = false;
        if (showLevelPreview && previewLevel > 0)
        {
            levelValue = previewLevel;
            usePreviewColor = true;
        }
        else if (hasPreview && preview.Level > 0 && preview.Level != current.Level)
        {
            levelValue = preview.Level;
            usePreviewColor = true;
        }

        if (levelText != null) levelText.text = BuildLevelText(levelValue, usePreviewColor);
        if (statsText != null) statsText.text = BuildStats(current, hasPreview, preview);
    }

    private string BuildLevelText(int levelValue, bool usePreviewColor)
    {
        if (!usePreviewColor)
            return string.Format(levelFormat, levelValue);

        string color = ColorUtility.ToHtmlStringRGB(previewLevelColor);
        return string.Format(levelFormat, $"<color=#{color}>{levelValue}</color>");
    }



    private string BuildStats(PlayerSkillSystem.SkillSnapshot current, bool hasPreview, PlayerSkillSystem.SkillSnapshot preview)
    {
        var sb = new StringBuilder(128);
        AppendStat(sb, "공격력", current.Damage, hasPreview ? preview.Damage : current.Damage, true, hasPreview);
        sb.AppendLine();
        AppendStat(sb, "공격속도", current.AttackSpeed, hasPreview ? preview.AttackSpeed : current.AttackSpeed, false, hasPreview);
        sb.AppendLine();
        AppendStat(sb, "공격범위", current.Range, hasPreview ? preview.Range : current.Range, false, hasPreview);
        return sb.ToString();
    }

    private void AppendStat(StringBuilder sb, string label, float cur, float next, bool integer, bool showPreview)
    {
        string curText = integer ? Mathf.RoundToInt(cur).ToString() : cur.ToString("0.0");
        sb.Append(label).Append(' ').Append(curText);
        if (!showPreview) return;

        string nextText = integer ? Mathf.RoundToInt(next).ToString() : next.ToString("0.0");
        float delta = next - cur;
        string deltaText = integer
            ? $"{(delta >= 0 ? "+" : string.Empty)}{Mathf.RoundToInt(delta)}"
            : $"{(delta >= 0 ? "+" : string.Empty)}{delta:0.0}";
        sb.Append(" -> ").Append(nextText).Append(" (").Append(deltaText).Append(')');
    }
}
