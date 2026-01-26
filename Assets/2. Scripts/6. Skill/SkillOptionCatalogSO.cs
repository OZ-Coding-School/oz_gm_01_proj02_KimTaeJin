using UnityEngine;

[CreateAssetMenu(menuName = "Game/Skill Option Catalog")]
public sealed class SkillOptionCatalogSO : ScriptableObject
{
    [SerializeField] private PlayerSkillOptionSO[] options;

    public PlayerSkillOptionSO[] Options => options;
}
