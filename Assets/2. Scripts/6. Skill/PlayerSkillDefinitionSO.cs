using UnityEngine;

[CreateAssetMenu(menuName = "Game/Skill Definition", fileName = "Skill_")]
public sealed class PlayerSkillDefinitionSO : ScriptableObject
{
    [Header("Identity")]
    public string id = "skill_basic";
    public string displayName = "발도";

    [Header("UI")]
    public Sprite icon;
    [TextArea(2, 4)] public string description;

    [Header("Base Stats")]
    public int baseDamage = 10;
    public float baseAttackSpeed = 1.2f;
    public float baseRange = 2f;

    [Header("사운드")]
    public AudioClip castSfx;
    [Range(0f, 1f)] public float castSfxVolume = 1f;
}
