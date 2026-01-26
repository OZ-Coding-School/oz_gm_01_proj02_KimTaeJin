using UnityEngine;

[CreateAssetMenu(menuName = "Game/Audio/Sfx Catalog", fileName = "SfxCatalog_")]
public sealed class GameSfxCatalogSO : ScriptableObject
{
    [System.Serializable]
    public struct SfxClip
    {
        public AudioClip clip;
        [Range(0f, 1f)] public float volume;
    }

    [Header("배치/건설")]
    public SfxClip towerPlaceConfirm;
    public SfxClip towerPlaceBlocked;

    [Header("수확/드롭")]
    public SfxClip harvestHit;
    public SfxClip harvestPickup;
    public SfxClip harvestHitWood;
    public SfxClip harvestHitStone;
    public SfxClip harvestPickupWood;
    public SfxClip harvestPickupStone;

    [Header("휴식 지점")]
    public SfxClip restStopEnter;
    public SfxClip restStopExit;

    [Header("피격/경고")]
    public SfxClip playerHit;
    public SfxClip lowHpPulse;

    [Header("길막힘")]
    public SfxClip pathBlocked;
}
