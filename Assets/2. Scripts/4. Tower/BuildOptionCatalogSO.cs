using UnityEngine;

[CreateAssetMenu(menuName = "Game/Build Option Catalog")]
public sealed class BuildOptionCatalogSO : ScriptableObject
{
    [SerializeField] private TowerDefinitionSO[] options;

    public TowerDefinitionSO[] Options => options;
}
