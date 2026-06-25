using Raylib_cs;

namespace Bungus.Game;

public sealed class MetaProfile
{
    public const int StorageCapacity = 100;

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

    public List<ItemStack?> StorageSlots { get; } = Enumerable.Repeat<ItemStack?>(null, StorageCapacity).ToList();
    public List<ItemStack?> RunBackpackSlots { get; } = Enumerable.Repeat<ItemStack?>(null, Inventory.BackpackCapacity).ToList();
    public List<ArmoryOffer> ArmoryOffers { get; } = [];
    public List<TokenStoreOffer> TokenStoreOffers { get; } = [];
    public ItemStack? Armor { get; set; }
    public ItemStack? RangedWeapon { get; set; }
    public ItemStack? HeavyWeapon { get; set; }
    public ItemStack? MeleeWeapon { get; set; }
    public ItemStack? QuickSlotQ { get; set; }
    public ItemStack? QuickSlotR { get; set; }
    public ItemStack? Trash { get; set; }

    public bool AddToStorage(ItemStack item)
    {
        if (item.IsHeavyAmmo) return TryAddHeavyAmmo(item.AmmoPercent, out _);
        if (item.IsPersistentStackableKey) return ItemStack.TryAddStackableKeyToSlots(StorageSlots, item, out _);

        for (var i = 0; i < StorageSlots.Count; i++)
        {
            if (StorageSlots[i] is not null) continue;
            StorageSlots[i] = item;
            return true;
        }

        return false;
    }

    public bool HasFreeStorageSlot() => StorageSlots.Any(item => item is null);

    public bool TryAddHeavyAmmo(float percent, out float remainingPercent)
        => ItemStack.TryAddHeavyAmmoToSlots(StorageSlots, percent, out remainingPercent);

    public bool CanStoreHeavyAmmo(float percent)
        => ItemStack.GetHeavyAmmoFreeCapacity(StorageSlots) + 0.0001f >= percent;

    public int GetCradleTrack(CradleTrack track) => track switch
    {
        CradleTrack.Health => CradleHealth,
        CradleTrack.Speed => CradleSpeed,
        CradleTrack.MeleeSpeed => CradleMeleeSpeed,
        CradleTrack.DashRecovery => CradleDashRecovery,
        CradleTrack.Stability => CradleStability,
        CradleTrack.Gunsmith => CradleGunsmith,
        CradleTrack.Fighter => CradleFighter,
        CradleTrack.Arcane => CradleArcane,
        _ => 0
    };

    public void SetCradleTrack(CradleTrack track, int value)
    {
        value = Math.Clamp(value, 0, 15);
        if (track == CradleTrack.Health) CradleHealth = value;
        if (track == CradleTrack.Speed) CradleSpeed = value;
        if (track == CradleTrack.MeleeSpeed) CradleMeleeSpeed = value;
        if (track == CradleTrack.DashRecovery) CradleDashRecovery = value;
        if (track == CradleTrack.Stability) CradleStability = value;
        if (track == CradleTrack.Gunsmith) CradleGunsmith = value;
        if (track == CradleTrack.Fighter) CradleFighter = value;
        if (track == CradleTrack.Arcane) CradleArcane = value;
    }

    public int SpentCradleCells()
        => CradleHealth + CradleSpeed + CradleMeleeSpeed + CradleDashRecovery + CradleStability + CradleGunsmith + CradleFighter + CradleArcane;
}
