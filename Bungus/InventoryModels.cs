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
    public int SynthCoins { get; set; }

    public List<ItemStack?> StorageSlots { get; } = Enumerable.Repeat<ItemStack?>(null, StorageCapacity).ToList();
    public List<ItemStack?> RunBackpackSlots { get; } = Enumerable.Repeat<ItemStack?>(null, Inventory.BackpackCapacity).ToList();
    public List<ArmoryOffer> ArmoryOffers { get; } = [];
    public ItemStack? Armor { get; set; }
    public ItemStack? RangedWeapon { get; set; }
    public ItemStack? HeavyWeapon { get; set; }
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
    public bool IsFunnyNextRun { get; set; }
    public Dictionary<string, int> PromoCodeUses { get; set; } = [];
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
    public int SynthCoins { get; set; }
    public List<ItemStackSaveData?> StorageSlots { get; set; } = [];
    public List<ItemStackSaveData?> RunBackpackSlots { get; set; } = [];
    public List<ArmoryOfferSaveData> ArmoryOffers { get; set; } = [];
    public ItemStackSaveData? Armor { get; set; }
    public ItemStackSaveData? RangedWeapon { get; set; }
    public ItemStackSaveData? HeavyWeapon { get; set; }
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
    public float ResiliencePercent { get; set; }
    public float SpeedBonusPercent { get; set; }
    public float ExplosionResistancePercent { get; set; }
    public float HealingBonusPercent { get; set; }
    public float DashRecoveryPercent { get; set; }
    public float ShieldMax { get; set; }
    public float RegenPercentPerSecond { get; set; }
    public float WeaponDamage { get; set; }
    public float PowerBonus { get; set; }
}

public sealed class ArmoryOffer
{
    public ItemStack Item { get; set; } = ItemStack.Weapon(WeaponClass.Ranged, ArmorRarity.Rare, new Random());
    public bool Purchased { get; set; }
}

public sealed class ArmoryOfferSaveData
{
    public ItemStackSaveData? Item { get; set; }
    public bool Purchased { get; set; }
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
        if (item.IsStationKey) return false;

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
        if (item.IsStationKey) return false;
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
            if (item.IsStationKey) continue;
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
    public bool IsStationKey
        => Type == ItemType.KeyItem && Name.Equals("S.T.A.T.I.O.N", StringComparison.OrdinalIgnoreCase)
           || Type == ItemType.Consumable && ConsumableKind == ConsumableType.StationKey;
    public bool IsPrimaryWeapon => Type == ItemType.Weapon && WeaponKind == WeaponClass.Ranged && Pattern is WeaponPattern.Standard or WeaponPattern.PulseRifle or WeaponPattern.Pulsar or WeaponPattern.Toxikus;
    public bool IsHeavyWeapon => Type == ItemType.Weapon && WeaponKind == WeaponClass.Ranged && Pattern is WeaponPattern.GrenadeLauncher or WeaponPattern.LinearRifle or WeaponPattern.RocketLauncher or WeaponPattern.SniperRifle or WeaponPattern.TraceRifle;

    public float Defense { get; }
    public float ResiliencePercent { get; }
    public float SpeedBonusPercent { get; }
    public float ExplosionResistancePercent { get; }
    public float HealingBonusPercent { get; }
    public float DashRecoveryPercent { get; }
    public float ShieldMax { get; }
    public float RegenPercentPerSecond { get; }
    public float BaseDamage { get; }

    private ItemStack(
        ItemType type,
        string name,
        string description,
        ArmorRarity rarity,
        Color color,
        WeaponClass? weaponClass,
        WeaponPattern pattern,
        ConsumableType? consumableType,
        float defense,
        float resiliencePercent,
        float speedBonusPercent,
        float explosionResistancePercent,
        float healingBonusPercent,
        float dashRecoveryPercent,
        float shieldMax,
        float regenPercentPerSecond,
        float baseDamage,
        bool isStarter)
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
        ResiliencePercent = resiliencePercent;
        SpeedBonusPercent = speedBonusPercent;
        ExplosionResistancePercent = explosionResistancePercent;
        HealingBonusPercent = healingBonusPercent;
        DashRecoveryPercent = dashRecoveryPercent;
        ShieldMax = shieldMax;
        RegenPercentPerSecond = regenPercentPerSecond;
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
            ResiliencePercent = item.ResiliencePercent,
            SpeedBonusPercent = item.SpeedBonusPercent,
            ExplosionResistancePercent = item.ExplosionResistancePercent,
            HealingBonusPercent = item.HealingBonusPercent,
            DashRecoveryPercent = item.DashRecoveryPercent,
            ShieldMax = item.ShieldMax,
            RegenPercentPerSecond = item.RegenPercentPerSecond,
            WeaponDamage = item.BaseDamage,
            PowerBonus = item.BaseDamage
        };
    }

    public static ItemStack? FromSaveData(ItemStackSaveData? data)
    {
        if (data is null) return null;
        if (data.Type == ItemType.Consumable && data.ConsumableKind == ConsumableType.StationKey) return StationKey();

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
            data.ResiliencePercent,
            data.SpeedBonusPercent,
            data.ExplosionResistancePercent,
            data.HealingBonusPercent,
            data.DashRecoveryPercent,
            data.ShieldMax,
            data.RegenPercentPerSecond,
            NormalizeSavedWeaponDamage(data),
            data.IsStarter);
    }

    private static float NormalizeSavedWeaponDamage(ItemStackSaveData data)
    {
        if (data.Type != ItemType.Weapon) return data.WeaponDamage > 0f ? data.WeaponDamage : data.PowerBonus;

        return data.Pattern switch
        {
            WeaponPattern.TraceRifle => 13f,
            WeaponPattern.LinearRifle => 325f,
            WeaponPattern.RocketLauncher => 225f,
            WeaponPattern.Pulsar => 30f,
            WeaponPattern.GrenadeLauncher => 90f,
            _ => data.WeaponDamage > 0f ? data.WeaponDamage : data.PowerBonus
        };
    }

    public static ItemStack Armor(ArmorRarity rarity, Random rng)
    {
        var defense = rarity switch
        {
            ArmorRarity.Damaged => 1f,
            ArmorRarity.Common => rng.Next(3, 5),
            ArmorRarity.Rare => rng.Next(5, 7),
            ArmorRarity.Epic => rng.Next(7, 9),
            ArmorRarity.Legendary => rng.Next(10, 13),
            _ => 15f
        };

        var resiliencePercent = rarity switch
        {
            ArmorRarity.Common => RollPercentRange(rng, 2, 4),
            ArmorRarity.Rare => RollPercentRange(rng, 3, 5),
            ArmorRarity.Epic => RollPercentRange(rng, 5, 12),
            ArmorRarity.Legendary => RollPercentRange(rng, 12, 20),
            ArmorRarity.Red => RollPercentRange(rng, 15, 25),
            _ => 0f
        };

        var speedBonusPercent = 0f;
        var explosionResistancePercent = 0f;
        var healingBonusPercent = 0f;
        var dashRecoveryPercent = 0f;
        var shieldMax = 0f;
        var regenPercentPerSecond = 0f;

        var name = rarity switch
        {
            ArmorRarity.Damaged => "Damaged Scrap Vest",
            ArmorRarity.Common => "Scrap Vest",
            ArmorRarity.Rare => "Titan Weave",
            ArmorRarity.Epic => "Aegis Fiber",
            ArmorRarity.Legendary => "Nova Bulwark",
            _ => "Crimson Bastion"
        };

        var modifierPool = new List<int> { 0, 1, 2, 3, 4, 5 };
        var modifierCount = rarity switch
        {
            ArmorRarity.Damaged => 0,
            ArmorRarity.Common => 0,
            ArmorRarity.Rare => rng.Next(0, 2),
            ArmorRarity.Epic => rng.Next(0, 3),
            ArmorRarity.Legendary => rng.Next(1, 4),
            _ => rng.Next(2, 4)
        };

        for (var i = 0; i < modifierCount && modifierPool.Count > 0; i++)
        {
            var selectedIndex = rng.Next(modifierPool.Count);
            var modifier = modifierPool[selectedIndex];
            modifierPool.RemoveAt(selectedIndex);

            switch (modifier)
            {
                case 0:
                    speedBonusPercent = rarity == ArmorRarity.Red ? 0.10f : rarity switch
                    {
                        ArmorRarity.Rare => RollPercentRange(rng, 1, 4),
                        ArmorRarity.Epic => RollPercentRange(rng, 2, 5),
                        _ => RollPercentRange(rng, 3, 7)
                    };
                    break;
                case 1:
                    explosionResistancePercent = rarity switch
                    {
                        ArmorRarity.Rare => RollPercentRange(rng, 10, 12),
                        ArmorRarity.Epic => RollPercentRange(rng, 10, 15),
                        ArmorRarity.Legendary => RollPercentRange(rng, 13, 25),
                        _ => RollPercentRange(rng, 20, 30)
                    };
                    break;
                case 2:
                    healingBonusPercent = rarity switch
                    {
                        ArmorRarity.Rare => RollPercentRange(rng, 10, 12),
                        ArmorRarity.Epic => RollPercentRange(rng, 10, 15),
                        ArmorRarity.Legendary => RollPercentRange(rng, 13, 25),
                        _ => RollPercentRange(rng, 20, 30)
                    };
                    break;
                case 3:
                    dashRecoveryPercent = rarity switch
                    {
                        ArmorRarity.Rare => RollPercentRange(rng, 10, 12),
                        ArmorRarity.Epic => RollPercentRange(rng, 10, 15),
                        ArmorRarity.Legendary => RollPercentRange(rng, 10, 18),
                        _ => RollPercentRange(rng, 10, 20)
                    };
                    break;
                case 4:
                    shieldMax = rarity switch
                    {
                        ArmorRarity.Rare => rng.Next(50, 101),
                        ArmorRarity.Epic => rng.Next(50, 121),
                        ArmorRarity.Legendary => rng.Next(75, 151),
                        _ => rng.Next(100, 151)
                    };
                    break;
                case 5:
                    regenPercentPerSecond = rarity switch
                    {
                        ArmorRarity.Rare => RollTenthPercentRange(rng, 1, 3),
                        ArmorRarity.Epic => RollTenthPercentRange(rng, 1, 5),
                        ArmorRarity.Legendary => RollTenthPercentRange(rng, 3, 5),
                        _ => 0.005f
                    };
                    break;
            }
        }

        return new ItemStack(
            ItemType.Armor,
            name,
            "Armor. Drag into armor slot.",
            rarity,
            Palette.Rarity(rarity),
            null,
            WeaponPattern.Standard,
            null,
            defense,
            resiliencePercent,
            speedBonusPercent,
            explosionResistancePercent,
            healingBonusPercent,
            dashRecoveryPercent,
            shieldMax,
            regenPercentPerSecond,
            0f,
            rarity == ArmorRarity.Damaged);
    }

    public static ItemStack Weapon(WeaponClass kind, ArmorRarity rarity, Random rng)
    {
        var pattern = RollWeaponPattern(kind, rarity, rng);
        return CreatePatternWeapon(kind, pattern, rarity, rng);
    }

    public static ItemStack PatternWeapon(WeaponClass kind, WeaponPattern pattern, ArmorRarity rarity, Random rng)
        => CreatePatternWeapon(kind, pattern, rarity, rng);

    private static WeaponPattern RollWeaponPattern(WeaponClass kind, ArmorRarity rarity, Random rng)
    {
        if (rarity == ArmorRarity.Damaged) return WeaponPattern.Standard;

        if (kind == WeaponClass.Ranged)
        {
            var rangedRoll = rng.NextSingle();
            if (rangedRoll < 0.20f) return WeaponPattern.SniperRifle;
            if (rangedRoll < 0.55f) return WeaponPattern.PulseRifle;
            return WeaponPattern.Standard;
        }

        if (kind == WeaponClass.Melee && rng.NextSingle() < 0.35f) return WeaponPattern.EnergySpear;
        return WeaponPattern.Standard;
    }

    private static ItemStack CreatePatternWeapon(WeaponClass kind, WeaponPattern pattern, ArmorRarity rarity, Random rng)
    {
        var (baseDamage, variance) = rarity switch
        {
            ArmorRarity.Damaged => (4f, 1f),
            ArmorRarity.Common => (8f, 4f),
            ArmorRarity.Rare => (13f, 3f),
            ArmorRarity.Epic => (20f, 5f),
            ArmorRarity.Legendary => (27f, 4f),
            _ => (150f, 0f)
        };

        baseDamage += rng.NextSingle() * variance;

        string name;
        string description;

        if (kind == WeaponClass.Ranged && pattern == WeaponPattern.Toxikus)
        {
            name = "Toxikus";
            description = "Unique pulse rifle. Slower 2-round burst; bullets poison enemies.";
            baseDamage = 22.857143f + rng.NextSingle() * 3.809524f;
        }
        else if (kind == WeaponClass.Ranged && pattern == WeaponPattern.TraceRifle)
        {
            name = "Trace Rifle";
            description = "Unique beam rifle. Rapidly paints a limited-range energy stream toward the cursor.";
            baseDamage = 13f;
        }
        else if (kind == WeaponClass.Ranged && pattern == WeaponPattern.LinearRifle)
        {
            name = "Linear Rifle";
            description = "Unique charge rifle. Hold to charge, then release to fire a heavy linear shot.";
            baseDamage = 325f;
        }
        else if (kind == WeaponClass.Ranged && pattern == WeaponPattern.RocketLauncher)
        {
            name = "Rocket Launcher";
            description = "Unique launcher. Fast rocket deals heavy direct and blast damage.";
            baseDamage = 225f;
        }
        else if (kind == WeaponClass.Ranged && pattern == WeaponPattern.Pulsar)
        {
            name = "Pulsar";
            description = "Unique automatic rifle. Impacts scatter delayed micro-explosions.";
            baseDamage = 30f;
        }
        else if (kind == WeaponClass.Melee && pattern == WeaponPattern.Lancelot)
        {
            name = "Lancelot";
            description = "Unique spear. Attacks lunge forward and hit harder than a legendary spear.";
            baseDamage = (27f + rng.NextSingle() * 4f) * 1.05f;
        }
        else if (kind == WeaponClass.Ranged && pattern == WeaponPattern.SniperRifle)
        {
            name = "Sniper Rifle";
            description = rarity == ArmorRarity.Legendary
                ? "Legendary sniper rifle. Standing still arms a devastating charged shot."
                : "Ranged weapon. High damage, long reload and a targeting beam.";
        }
        else if (kind == WeaponClass.Ranged && pattern == WeaponPattern.PulseRifle)
        {
            name = "Pulse Rifle";
            description = rarity == ArmorRarity.Legendary
                ? "Legendary ranged weapon. Fires a 4-round burst."
                : "Ranged weapon. Fires a 3-round burst.";
        }
        else if (kind == WeaponClass.Melee && pattern == WeaponPattern.EnergySpear)
        {
            name = "Energy Spear";
            description = rarity == ArmorRarity.Legendary
                ? "Legendary melee weapon. Longer thrust reach."
                : "Melee weapon. Cleaves forward in a line.";
        }
        else
        {
            name = rarity == ArmorRarity.Damaged
                ? kind == WeaponClass.Ranged ? "Damaged Rail Pistol" : "Damaged Plasma Blade"
                : kind == WeaponClass.Ranged ? "Rail Pistol" : "Plasma Blade";
            description = rarity == ArmorRarity.Damaged
                ? "Damaged weapon. Emergency deployment issue."
                : rarity == ArmorRarity.Legendary
                    ? kind == WeaponClass.Ranged
                        ? "Legendary pistol. 33% chance to fire two bullets with slight spread."
                        : "Legendary blade. Slash arc is wider."
                    : "Weapon. Drag to matching slot.";
        }

        return new ItemStack(ItemType.Weapon, name, description, rarity, Palette.Rarity(rarity), kind, pattern, null, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, baseDamage, rarity == ArmorRarity.Damaged);
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
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
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
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
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
            1f,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            true);
    }

    public static ItemStack BossGrenadeLauncher()
    {
        return new ItemStack(
            ItemType.Weapon,
            "Destroyer Grenade Launcher",
            "Boss weapon. Explosive shell deals 90 blast damage and 225 on direct hit.",
            ArmorRarity.Red,
            Palette.Rarity(ArmorRarity.Red),
            WeaponClass.Ranged,
            WeaponPattern.GrenadeLauncher,
            null,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            90f,
            false);
    }

    public static ItemStack Toxikus(Random rng)
        => CreatePatternWeapon(WeaponClass.Ranged, WeaponPattern.Toxikus, ArmorRarity.Red, rng);

    public static ItemStack Lancelot(Random rng)
        => CreatePatternWeapon(WeaponClass.Melee, WeaponPattern.Lancelot, ArmorRarity.Red, rng);

    public static ItemStack TraceRifle(Random rng)
        => CreatePatternWeapon(WeaponClass.Ranged, WeaponPattern.TraceRifle, ArmorRarity.Red, rng);

    public static ItemStack LinearRifle(Random rng)
        => CreatePatternWeapon(WeaponClass.Ranged, WeaponPattern.LinearRifle, ArmorRarity.Red, rng);

    public static ItemStack RocketLauncher(Random rng)
        => CreatePatternWeapon(WeaponClass.Ranged, WeaponPattern.RocketLauncher, ArmorRarity.Red, rng);

    public static ItemStack Pulsar(Random rng)
        => CreatePatternWeapon(WeaponClass.Ranged, WeaponPattern.Pulsar, ArmorRarity.Red, rng);

    public static ItemStack StationKey()
        => new(
            ItemType.KeyItem,
            "S.T.A.T.I.O.N",
            "Opens the Dead Zone station entrance without destroying generators.",
            ArmorRarity.Epic,
            Palette.Rarity(ArmorRarity.Epic),
            null,
            WeaponPattern.Standard,
            null,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            false);

    public static ItemStack Consumable(ConsumableType t)
    {
        return t switch
        {
            ConsumableType.Medkit => new ItemStack(ItemType.Consumable, "Medkit", "Restore HP. Hotkey Q/R.", ArmorRarity.Common, Palette.C(130, 210, 120), null, WeaponPattern.Standard, t, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, false),
            ConsumableType.Stim => new ItemStack(ItemType.Consumable, "Stim", "Move speed boost. Hotkey Q/R.", ArmorRarity.Common, Palette.C(220, 220, 120), null, WeaponPattern.Standard, t, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, false),
            ConsumableType.ProtectiveDome => new ItemStack(ItemType.Consumable, "Protective Dome", "Deploy a dome that blocks enemy shots and absorbs 200 damage. Hotkey Q/R.", ArmorRarity.Common, Palette.C(120, 190, 255), null, WeaponPattern.Standard, t, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, false),
            ConsumableType.StationKey => StationKey(),
            _ => new ItemStack(ItemType.Consumable, "Sticky Bullets", "For 15 seconds your damage slows enemies by 30% for 1 second. Hotkey Q/R.", ArmorRarity.Common, Palette.C(235, 235, 235), null, WeaponPattern.Standard, t, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, false)
        };
    }

    private static float RollPercentRange(Random rng, int minPercent, int maxPercent)
        => rng.Next(minPercent, maxPercent + 1) / 100f;

    private static float RollTenthPercentRange(Random rng, int minTenthsPercent, int maxTenthsPercent)
        => rng.Next(minTenthsPercent, maxTenthsPercent + 1) / 1000f;
}

public enum SlotKind
{
    RangedWeapon,
    HeavyWeapon,
    MeleeWeapon,
    Armor,
    Trash,
    Storage,
    RunBackpack,
    Backpack,
    QuickSlotQ,
    QuickSlotR,
    Chest,
    Armory
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
