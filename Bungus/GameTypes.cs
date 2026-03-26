using Raylib_cs;

namespace Bungus.Game;

public enum GameState { MainMenu, MapSelect, Storage, Character, Settings, Playing, Paused, Death }
public enum WeaponClass { Melee, Ranged }
public enum ItemType { Weapon, Armor, Consumable }
public enum ConsumableType { Medkit, Stim }
public enum ArmorRarity { Common = 0, Rare = 1, Epic = 2, Legendary = 3, Red = 4, Damaged = 5 }
public enum StatType { Strength, Dexterity, Speed, Gunsmith }
public enum WeaponPattern { Standard, PulseRifle, EnergySpear, GrenadeLauncher }
public enum ProjectileKind { Bullet, Grenade }
public enum DisplayMode { Windowed, Fullscreen }

public static class Palette
{
    public static Color C(int r, int g, int b, int a = 255) => new((byte)r, (byte)g, (byte)b, (byte)a);

    public static Color Rarity(ArmorRarity r) => r switch
    {
        ArmorRarity.Common => Color.LightGray,
        ArmorRarity.Rare => Color.SkyBlue,
        ArmorRarity.Epic => C(191, 120, 255),
        ArmorRarity.Legendary => Color.Gold,
        ArmorRarity.Red => C(230, 45, 45),
        ArmorRarity.Damaged => C(160, 160, 160),
        _ => Color.White
    };
}

public sealed record VisualTheme(
    string Name,
    Color Background,
    Color Grid,
    Color BuildingFill,
    Color BuildingLine,
    Color OutpostFill,
    Color OutpostLine,
    Color ObstacleFill,
    Color ObstacleLine,
    Color Player,
    Color Enemy,
    Color EnemyStrong,
    Color Boss);
