using Raylib_cs;

namespace Bungus.Game;

public sealed class MetaProfileSaveData
{
    public int Level { get; set; } = 1;
    public int Score { get; set; }
    public int BaseStrength { get; set; } = 4;
    public int BaseDexterity { get; set; } = 4;
    public int BaseSpeed { get; set; } = 4;
    public int BaseGuns { get; set; } = 4;
    public int CradleHealth { get; set; }
    public int CradleSpeed { get; set; }
    public int CradleMeleeSpeed { get; set; }
    public int CradleDashRecovery { get; set; }
    public int CradleStability { get; set; }
    public int CradleGunsmith { get; set; }
    public int CradleFighter { get; set; }
    public int CradleArcane { get; set; }
    public int SynthCoins { get; set; }
    public int CryptoTokens { get; set; }
    public int FailedRunsSinceStoreRefresh { get; set; }
    public List<ItemStackSaveData?> StorageSlots { get; set; } = [];
    public List<ItemStackSaveData?> RunBackpackSlots { get; set; } = [];
    public List<ArmoryOfferSaveData> ArmoryOffers { get; set; } = [];
    public List<TokenStoreOfferSaveData> TokenStoreOffers { get; set; } = [];
    public ItemStackSaveData? Armor { get; set; }
    public ItemStackSaveData? RangedWeapon { get; set; }
    public ItemStackSaveData? HeavyWeapon { get; set; }
    public ItemStackSaveData? MeleeWeapon { get; set; }
    public ItemStackSaveData? QuickSlotQ { get; set; }
    public ItemStackSaveData? QuickSlotR { get; set; }
    public ItemStackSaveData? Trash { get; set; }
}
