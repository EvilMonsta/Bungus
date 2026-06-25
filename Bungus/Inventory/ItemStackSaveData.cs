using Raylib_cs;

namespace Bungus.Game;

public sealed class ItemStackSaveData
{
    public ItemType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ArmorRarity Rarity { get; set; }
    public byte ColorR { get; set; }
    public byte ColorG { get; set; }
    public byte ColorB { get; set; }
    public byte ColorA { get; set; } = 255;
    public WeaponClass? WeaponKind { get; set; }
    public WeaponPattern Pattern { get; set; }
    public ConsumableType? ConsumableKind { get; set; }
    public bool IsStarter { get; set; }
    public ArmorKind ArmorKind { get; set; }
    public float Defense { get; set; }
    public float ResiliencePercent { get; set; }
    public float SpeedBonusPercent { get; set; }
    public float ExplosionResistancePercent { get; set; }
    public float HealingBonusPercent { get; set; }
    public float DashRecoveryPercent { get; set; }
    public float ShieldMax { get; set; }
    public float RegenPercentPerSecond { get; set; }
    public float MovementSpreadPercent { get; set; }
    public float DashDistancePercent { get; set; }
    public float WeaponDamage { get; set; }
    public float PowerBonus { get; set; }
    public float AmmoPercent { get; set; }
    public int Quantity { get; set; } = 1;
}
