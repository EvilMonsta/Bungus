using Raylib_cs;

namespace Bungus.Game;

public sealed class Inventory
{
    public const int BackpackCapacity = 30;

    public List<ItemStack?> BackpackSlots { get; } = Enumerable.Repeat<ItemStack?>(null, BackpackCapacity).ToList();
    public ItemStack? QuickSlotQ { get; set; }
    public ItemStack? QuickSlotR { get; set; }

    public ItemStack? Trash { get; set; }

    public bool AddToBackpack(ItemStack item)
    {
        if (item.IsHeavyAmmo) return TryAddHeavyAmmo(item.AmmoPercent, out _);
        if (item.IsPersistentStackableKey) return ItemStack.TryAddStackableKeyToSlots(BackpackSlots, item, out _);

        for (var i = 0; i < BackpackSlots.Count; i++)
        {
            if (BackpackSlots[i] is not null) continue;
            BackpackSlots[i] = item;
            return true;
        }

        return false;
    }

    public bool HasFreeBackpackSlot() => BackpackSlots.Any(item => item is null);

    public bool TryAddHeavyAmmo(float percent, out float remainingPercent)
        => ItemStack.TryAddHeavyAmmoToSlots(BackpackSlots, percent, out remainingPercent);

    public bool CanStoreHeavyAmmo(float percent)
        => ItemStack.GetHeavyAmmoFreeCapacity(BackpackSlots) + 0.0001f >= percent;

    public float HeavyAmmoPercent => BackpackSlots.Where(item => item?.IsHeavyAmmo == true).Sum(item => item!.AmmoPercent);

    public int GetHeavyAmmoShotCount(ItemStack? weapon)
    {
        var cost = ItemStack.GetHeavyAmmoCostPercent(weapon);
        if (cost <= 0f) return 0;
        return (int)MathF.Floor(HeavyAmmoPercent / cost);
    }

    public bool TryConsumeHeavyAmmo(ItemStack? weapon)
    {
        var cost = ItemStack.GetHeavyAmmoCostPercent(weapon);
        if (cost <= 0f) return true;
        if (HeavyAmmoPercent + 0.0001f < cost) return false;

        var remaining = cost;
        for (var i = 0; i < BackpackSlots.Count && remaining > 0f; i++)
        {
            var item = BackpackSlots[i];
            if (item?.IsHeavyAmmo != true) continue;

            var consumed = MathF.Min(item.AmmoPercent, remaining);
            var left = MathF.Round(item.AmmoPercent - consumed, 4);
            remaining = MathF.Round(remaining - consumed, 4);
            BackpackSlots[i] = left > 0f ? ItemStack.HeavyAmmo(left) : null;
        }

        return true;
    }

    public void AutoFillConsumableSlots()
    {
        QuickSlotQ = null;
        QuickSlotR = null;
    }

    public bool TryReceiveGroundConsumableWhenBackpackFull(ItemStack item)
    {
        return false;
    }
}
