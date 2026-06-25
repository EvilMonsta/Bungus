using Raylib_cs;

namespace Bungus.Game;

public sealed class ArmoryOffer
{
    public ItemStack Item { get; set; } = ItemStack.Weapon(WeaponClass.Ranged, ArmorRarity.Rare, new Random());
    public bool Purchased { get; set; }
}
