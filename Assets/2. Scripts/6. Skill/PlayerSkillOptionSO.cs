using UnityEngine;

[CreateAssetMenu(menuName = "Game/Skill Option", fileName = "SkillOption_")]
public sealed class PlayerSkillOptionSO : ScriptableObject
{
    public enum OptionType
    {
        AddSkill = 0,
        UpgradeSkill = 1
    }

    [Header("Identity")]
    public string id = "skill_option";
    public string displayName = "발도 강화";

    [Header("UI")]
    public Sprite icon;
    [TextArea(2, 4)] public string description;

    [Header("Target")]
    public OptionType optionType = OptionType.UpgradeSkill;
    public PlayerSkillDefinitionSO targetSkill;

    [Header("Delta")]
    public int damageDelta = 2;
    public float attackSpeedDelta = 0.2f;
    public float rangeDelta = 0.3f;
    public int levelDelta = 1;
}
