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

    public List<ItemStack?> StorageSlots { get; } = Enumerable.Repeat<ItemStack?>(null, StorageCapacity).ToList();
    public ItemStack? Armor { get; set; }
    public ItemStack? RangedWeapon { get; set; }
    public ItemStack? MeleeWeapon { get; set; }
    public ItemStack? QuickSlotQ { get; set; }
    public ItemStack? QuickSlotR { get; set; }
    public ItemStack? Trash { get; set; }

    public bool AddToStorage(ItemStack item)
    {
        for (var i = 0; i < StorageSlots.Count; i++)
        {
            if (StorageSlots[i] is not null) continue;
            StorageSlots[i] = item;
            return true;
        }

        return false;
    }

    public bool HasFreeStorageSlot() => StorageSlots.Any(item => item is null);
}

public sealed class PersistentStateData
{
    public int ThemeIndex { get; set; }
    public DisplayMode DisplayMode { get; set; } = DisplayMode.Windowed;
    public string SelectedMapName { get; set; } = "Baselands";
    public MetaProfileSaveData Meta { get; set; } = new();
}

public sealed class ProtectedSaveFile
{
    public int Version { get; set; } = 1;
    public string Iv { get; set; } = string.Empty;
    public string ProtectedPayload { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
}

public sealed class MetaProfileSaveData
{
    public int Level { get; set; } = 1;
    public int Score { get; set; }
    public int BaseStrength { get; set; } = 4;
    public int BaseDexterity { get; set; } = 4;
    public int BaseSpeed { get; set; } = 4;
    public int BaseGuns { get; set; } = 4;
    public List<ItemStackSaveData?> StorageSlots { get; set; } = [];
    public ItemStackSaveData? Armor { get; set; }
    public ItemStackSaveData? RangedWeapon { get; set; }
    public ItemStackSaveData? MeleeWeapon { get; set; }
    public ItemStackSaveData? QuickSlotQ { get; set; }
    public ItemStackSaveData? QuickSlotR { get; set; }
    public ItemStackSaveData? Trash { get; set; }
}

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
    public float Defense { get; set; }
    public float WeaponDamage { get; set; }
    public float PowerBonus { get; set; }
}

public sealed class Inventory
{
    public const int BackpackCapacity = 30;

    public List<ItemStack?> BackpackSlots { get; } = Enumerable.Repeat<ItemStack?>(null, BackpackCapacity).ToList();
    public ItemStack? QuickSlotQ { get; set; }
    public ItemStack? QuickSlotR { get; set; }

    public ItemStack? Trash { get; set; }

    public bool AddToBackpack(ItemStack item)
    {
        if (TryPlaceIntoConsumableSlot(item)) return true;

        for (var i = 0; i < BackpackSlots.Count; i++)
        {
            if (BackpackSlots[i] is not null) continue;
            BackpackSlots[i] = item;
            return true;
        }

        return false;
    }

    public bool HasFreeBackpackSlot() => BackpackSlots.Any(item => item is null);

    public void AutoFillConsumableSlots()
    {
        if (QuickSlotQ is null) QuickSlotQ = TakeFirstConsumableFromBackpack();
        if (QuickSlotR is null) QuickSlotR = TakeFirstConsumableFromBackpack();
    }

    private bool TryPlaceIntoConsumableSlot(ItemStack item)
    {
        if (item.Type != ItemType.Consumable) return false;

        if (QuickSlotQ is null)
        {
            QuickSlotQ = item;
            return true;
        }

        if (QuickSlotR is null)
        {
            QuickSlotR = item;
            return true;
        }

        return false;
    }

    public bool TryReceiveGroundConsumableWhenBackpackFull(ItemStack item)
    {
        if (item.Type != ItemType.Consumable) return false;
        if (HasFreeBackpackSlot()) return false;
        if (QuickSlotQ is not null || QuickSlotR is not null) return false;
        if (BackpackSlots.Any(slot => slot?.Type == ItemType.Consumable)) return false;

        QuickSlotQ = item;
        return true;
    }

    private ItemStack? TakeFirstConsumableFromBackpack()
    {
        for (var i = 0; i < BackpackSlots.Count; i++)
        {
            var item = BackpackSlots[i];
            if (item?.Type != ItemType.Consumable) continue;
            BackpackSlots[i] = null;
            return item;
        }

        return null;
    }
}

public sealed class ItemStack
{
    public ItemType Type { get; }
    public string Name { get; }
    public string Description { get; }
    public ArmorRarity Rarity { get; }
    public Color Color { get; }

    public WeaponClass? WeaponKind { get; }
    public WeaponPattern Pattern { get; }
    public ConsumableType? ConsumableKind { get; }
    public bool IsStarter { get; }

    public float Defense { get; }
    public float BaseDamage { get; }

    private ItemStack(ItemType type, string name, string description, ArmorRarity rarity, Color color, WeaponClass? weaponClass, WeaponPattern pattern, ConsumableType? consumableType, float defense, float baseDamage, bool isStarter)
    {
        Type = type;
        Name = name;
        Description = description;
        Rarity = rarity;
        Color = color;
        WeaponKind = weaponClass;
        Pattern = pattern;
        ConsumableKind = consumableType;
        IsStarter = isStarter;
        Defense = defense;
        BaseDamage = baseDamage;
    }

    public static ItemStackSaveData? ToSaveData(ItemStack? item)
    {
        if (item is null) return null;

        return new ItemStackSaveData
        {
            Type = item.Type,
            Name = item.Name,
            Description = item.Description,
            Rarity = item.Rarity,
            ColorR = item.Color.R,
            ColorG = item.Color.G,
            ColorB = item.Color.B,
            ColorA = item.Color.A,
            WeaponKind = item.WeaponKind,
            Pattern = item.Pattern,
            ConsumableKind = item.ConsumableKind,
            IsStarter = item.IsStarter,
            Defense = item.Defense,
            WeaponDamage = item.BaseDamage,
            PowerBonus = item.BaseDamage
        };
    }

    public static ItemStack? FromSaveData(ItemStackSaveData? data)
    {
        if (data is null) return null;

        return new ItemStack(
            data.Type,
            data.Name,
            data.Description,
            data.Rarity,
            new Color(data.ColorR, data.ColorG, data.ColorB, data.ColorA),
            data.WeaponKind,
            data.Pattern,
            data.ConsumableKind,
            data.Defense,
            data.WeaponDamage > 0f ? data.WeaponDamage : data.PowerBonus,
            data.IsStarter);
    }

    public static ItemStack Armor(ArmorRarity rarity, Random rng)
    {
        var baseDef = rarity switch
        {
            ArmorRarity.Damaged => 5f,
            ArmorRarity.Common => 10f,
            ArmorRarity.Rare => 14f,
            ArmorRarity.Epic => 19f,
            ArmorRarity.Legendary => 25f,
            _ => 33f
        };

        var name = rarity switch
        {
            ArmorRarity.Damaged => "Damaged Scrap Vest",
            ArmorRarity.Common => "Scrap Vest",
            ArmorRarity.Rare => "Titan Weave",
            ArmorRarity.Epic => "Aegis Fiber",
            ArmorRarity.Legendary => "Nova Bulwark",
            _ => "Crimson Bastion"
        };

        var variance = rarity == ArmorRarity.Damaged ? 2f : 4f;
        return new ItemStack(ItemType.Armor, name, "Armor. Drag into armor slot.", rarity, Palette.Rarity(rarity), null, WeaponPattern.Standard, null, baseDef + rng.NextSingle() * variance, 0f, rarity == ArmorRarity.Damaged);
    }

    public static ItemStack Weapon(WeaponClass kind, ArmorRarity rarity, Random rng)
    {
        var baseDamage = rarity switch
        {
            ArmorRarity.Damaged => 4f,
            ArmorRarity.Common => 11f,
            ArmorRarity.Rare => 16f,
            ArmorRarity.Epic => 22f,
            ArmorRarity.Legendary => 30f,
            _ => 150f
        };

        baseDamage += rng.NextSingle() * (rarity == ArmorRarity.Damaged ? 1f : 3f);

        WeaponPattern pattern;
        string name;
        string description;

        if (rarity != ArmorRarity.Damaged && kind == WeaponClass.Ranged && rng.NextSingle() < 0.35f)
        {
            pattern = WeaponPattern.PulseRifle;
            name = "Pulse Rifle";
            description = "Ranged weapon. Fires a 3-round burst.";
            baseDamage += 2f;
        }
        else if (rarity != ArmorRarity.Damaged && kind == WeaponClass.Melee && rng.NextSingle() < 0.35f)
        {
            pattern = WeaponPattern.EnergySpear;
            name = "Energy Spear";
            description = "Melee weapon. Cleaves forward in a line.";
            baseDamage += 1.5f;
        }
        else
        {
            pattern = WeaponPattern.Standard;
            name = rarity == ArmorRarity.Damaged
                ? kind == WeaponClass.Ranged ? "Damaged Rail Pistol" : "Damaged Plasma Blade"
                : kind == WeaponClass.Ranged ? "Rail Pistol" : "Plasma Blade";
            description = rarity == ArmorRarity.Damaged ? "Damaged weapon. Emergency deployment issue." : "Weapon. Drag to matching slot.";
        }

        return new ItemStack(ItemType.Weapon, name, description, rarity, Palette.Rarity(rarity), kind, pattern, null, 0f, baseDamage, rarity == ArmorRarity.Damaged);
    }

    public static ItemStack StartingPistol()
    {
        return new ItemStack(
            ItemType.Weapon,
            "Damaged Rail Pistol",
            "Damaged weapon. Emergency deployment issue.",
            ArmorRarity.Damaged,
            Palette.Rarity(ArmorRarity.Damaged),
            WeaponClass.Ranged,
            WeaponPattern.Standard,
            null,
            0f,
            4f,
            true);
    }

    public static ItemStack StartingMelee()
    {
        return new ItemStack(
            ItemType.Weapon,
            "Damaged Plasma Blade",
            "Damaged weapon. Emergency deployment issue.",
            ArmorRarity.Damaged,
            Palette.Rarity(ArmorRarity.Damaged),
            WeaponClass.Melee,
            WeaponPattern.Standard,
            null,
            0f,
            4f,
            true);
    }

    public static ItemStack StartingArmor()
    {
        return new ItemStack(
            ItemType.Armor,
            "Damaged Scrap Vest",
            "Armor. Drag into armor slot.",
            ArmorRarity.Damaged,
            Palette.Rarity(ArmorRarity.Damaged),
            null,
            WeaponPattern.Standard,
            null,
            6f,
            0f,
            true);
    }

    public static ItemStack BossGrenadeLauncher()
    {
        return new ItemStack(
            ItemType.Weapon,
            "Destroyer Grenade Launcher",
            "Boss weapon. Explosive shell deals 150 blast damage and 350 on direct hit.",
            ArmorRarity.Red,
            Palette.Rarity(ArmorRarity.Red),
            WeaponClass.Ranged,
            WeaponPattern.GrenadeLauncher,
            null,
            0f,
            0f,
            false);
    }

    public static ItemStack Consumable(ConsumableType t)
    {
        return t == ConsumableType.Medkit
            ? new ItemStack(ItemType.Consumable, "Medkit", "Restore HP. Hotkey Q/R.", ArmorRarity.Common, Palette.C(130, 210, 120), null, WeaponPattern.Standard, t, 0f, 0f, false)
            : new ItemStack(ItemType.Consumable, "Stim", "Move speed boost. Hotkey Q/R.", ArmorRarity.Common, Palette.C(220, 220, 120), null, WeaponPattern.Standard, t, 0f, 0f, false);
    }
}

public enum SlotKind
{
    RangedWeapon,
    MeleeWeapon,
    Armor,
    Trash,
    Storage,
    Backpack,
    QuickSlotQ,
    QuickSlotR,
    Chest
}

public sealed class UiSlot(Rectangle rect, SlotKind kind, int? index, ItemStack? item, int slotId)
{
    public Rectangle Rect { get; } = rect;
    public SlotKind Kind { get; } = kind;
    public int Index { get; } = index ?? -1;
    public ItemStack? Item { get; } = item;
    public int SlotId { get; } = slotId;
}

public sealed class DragPayload(SlotKind kind, int index, ItemStack item)
{
    public SlotKind Kind { get; } = kind;
    public int Index { get; } = index;
    public ItemStack Item { get; } = item;
}
