using Raylib_cs;

namespace Bungus.Game;

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
    public bool IsDeviceDataFragment
        => Type == ItemType.KeyItem && Name.Equals("Device's Data Fragment", StringComparison.OrdinalIgnoreCase);
    public bool IsVexEye => Type == ItemType.KeyItem && Name.Equals("Vex's Eye", StringComparison.OrdinalIgnoreCase);
    public bool IsInfectedExemplar => Type == ItemType.KeyItem && Name.Equals("Infected Exemplar", StringComparison.OrdinalIgnoreCase);
    public bool IsPersistentStackableKey => IsDeviceDataFragment || IsVexEye || IsInfectedExemplar;
    public bool IsPrimaryWeapon => Type == ItemType.Weapon && WeaponKind == WeaponClass.Ranged && Pattern is WeaponPattern.Standard or WeaponPattern.PulseRifle or WeaponPattern.AutoRifle or WeaponPattern.Pulsar or WeaponPattern.Toxikus;
    public bool IsHeavyWeapon => Type == ItemType.Weapon && WeaponKind == WeaponClass.Ranged && Pattern is WeaponPattern.GrenadeLauncher or WeaponPattern.LinearRifle or WeaponPattern.RocketLauncher or WeaponPattern.SniperRifle or WeaponPattern.TraceRifle or WeaponPattern.RamBomber or WeaponPattern.RocketPulseRifle or WeaponPattern.Terror;
    public bool IsHeavyAmmo => Type == ItemType.Ammo;

    public ArmorKind ArmorKind { get; }
    public float Defense { get; }
    public float ResiliencePercent { get; }
    public float SpeedBonusPercent { get; }
    public float ExplosionResistancePercent { get; }
    public float HealingBonusPercent { get; }
    public float DashRecoveryPercent { get; }
    public float ShieldMax { get; }
    public float RegenPercentPerSecond { get; }
    public float MovementSpreadPercent { get; }
    public float DashDistancePercent { get; }
    public float BaseDamage { get; }
    public float AmmoPercent { get; }
    public int Quantity { get; }

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
        bool isStarter,
        float ammoPercent = 0f,
        ArmorKind armorKind = ArmorKind.Standard,
        float movementSpreadPercent = 0f,
        float dashDistancePercent = 0f,
        int quantity = 1)
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
        ArmorKind = type == ItemType.Armor ? armorKind : ArmorKind.Standard;
        MovementSpreadPercent = type == ItemType.Armor ? movementSpreadPercent : 0f;
        DashDistancePercent = type == ItemType.Armor ? dashDistancePercent : 0f;
        BaseDamage = baseDamage;
        AmmoPercent = MathF.Round(Math.Clamp(ammoPercent, 0f, 100f), 4);
        Quantity = Math.Clamp(quantity, 1, 999);
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
            ArmorKind = item.ArmorKind,
            Defense = item.Defense,
            ResiliencePercent = item.ResiliencePercent,
            SpeedBonusPercent = item.SpeedBonusPercent,
            ExplosionResistancePercent = item.ExplosionResistancePercent,
            HealingBonusPercent = item.HealingBonusPercent,
            DashRecoveryPercent = item.DashRecoveryPercent,
            ShieldMax = item.ShieldMax,
            RegenPercentPerSecond = item.RegenPercentPerSecond,
            MovementSpreadPercent = item.MovementSpreadPercent,
            DashDistancePercent = item.DashDistancePercent,
            WeaponDamage = item.BaseDamage,
            PowerBonus = item.BaseDamage,
            AmmoPercent = item.AmmoPercent,
            Quantity = item.Quantity
        };
    }

    public static ItemStack? FromSaveData(ItemStackSaveData? data)
    {
        if (data is null) return null;
        if (data.Type == ItemType.Consumable && data.ConsumableKind == ConsumableType.StationKey) return StationKey();

        var rarity = data.Rarity;
        var color = new Color(data.ColorR, data.ColorG, data.ColorB, data.ColorA);
        var baseDamage = NormalizeSavedWeaponDamage(data);
        if (data.Type == ItemType.Weapon && data.Pattern == WeaponPattern.LinearRifle && data.Rarity == ArmorRarity.Red)
        {
            rarity = ArmorRarity.Legendary;
            color = Palette.Rarity(rarity);
            baseDamage = Math.Clamp(baseDamage, 27f, 31f);
        }

        var armorKind = data.Type == ItemType.Armor && Enum.IsDefined(data.ArmorKind) ? data.ArmorKind : ArmorKind.Standard;
        return new ItemStack(
            data.Type,
            data.Type == ItemType.Armor ? GetArmorName(armorKind) : data.Name,
            data.Description,
            rarity,
            color,
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
            baseDamage,
            data.IsStarter,
            data.AmmoPercent,
            armorKind,
            data.MovementSpreadPercent,
            data.DashDistancePercent,
            data.Quantity);
    }

    public static bool TryAddDeviceDataFragmentsToSlots(List<ItemStack?> slots, int quantity, out int remaining)
    {
        remaining = Math.Clamp(quantity, 0, 999);
        if (remaining <= 0) return true;

        for (var i = 0; i < slots.Count && remaining > 0; i++)
        {
            var item = slots[i];
            if (item?.IsDeviceDataFragment != true || item.Quantity >= 999) continue;

            var add = Math.Min(999 - item.Quantity, remaining);
            slots[i] = DeviceDataFragment(item.Quantity + add);
            remaining -= add;
        }

        for (var i = 0; i < slots.Count && remaining > 0; i++)
        {
            if (slots[i] is not null) continue;

            var add = Math.Min(999, remaining);
            slots[i] = DeviceDataFragment(add);
            remaining -= add;
        }

        return remaining <= 0;
    }

    public static bool TryAddStackableKeyToSlots(List<ItemStack?> slots, ItemStack key, out int remaining)
    {
        remaining = key.Quantity;
        if (!key.IsPersistentStackableKey) return false;

        for (var i = 0; i < slots.Count && remaining > 0; i++)
        {
            var item = slots[i];
            if (item?.Type != ItemType.KeyItem
                || !item.Name.Equals(key.Name, StringComparison.OrdinalIgnoreCase)
                || item.Quantity >= 999) continue;
            var add = Math.Min(999 - item.Quantity, remaining);
            slots[i] = key.Name.Equals("Vex's Eye", StringComparison.OrdinalIgnoreCase)
                ? VexEye(item.Quantity + add)
                : key.Name.Equals("Infected Exemplar", StringComparison.OrdinalIgnoreCase)
                    ? InfectedExemplar(item.Quantity + add)
                    : DeviceDataFragment(item.Quantity + add);
            remaining -= add;
        }

        for (var i = 0; i < slots.Count && remaining > 0; i++)
        {
            if (slots[i] is not null) continue;
            var add = Math.Min(999, remaining);
            slots[i] = key.Name.Equals("Vex's Eye", StringComparison.OrdinalIgnoreCase)
                ? VexEye(add)
                : key.Name.Equals("Infected Exemplar", StringComparison.OrdinalIgnoreCase)
                    ? InfectedExemplar(add)
                    : DeviceDataFragment(add);
            remaining -= add;
        }
        return remaining <= 0;
    }

    public static bool TryAddHeavyAmmoToSlots(List<ItemStack?> slots, float percent, out float remainingPercent)
    {
        remainingPercent = MathF.Round(MathF.Max(0f, percent), 4);
        if (remainingPercent <= 0f) return true;

        for (var i = 0; i < slots.Count && remainingPercent > 0f; i++)
        {
            var item = slots[i];
            if (item?.IsHeavyAmmo != true || item.AmmoPercent >= 100f) continue;

            var add = MathF.Min(100f - item.AmmoPercent, remainingPercent);
            slots[i] = HeavyAmmo(item.AmmoPercent + add);
            remainingPercent = MathF.Round(remainingPercent - add, 4);
        }

        for (var i = 0; i < slots.Count && remainingPercent > 0f; i++)
        {
            if (slots[i] is not null) continue;

            var add = MathF.Min(100f, remainingPercent);
            slots[i] = HeavyAmmo(add);
            remainingPercent = MathF.Round(remainingPercent - add, 4);
        }

        return remainingPercent <= 0f;
    }

    public static float GetHeavyAmmoFreeCapacity(List<ItemStack?> slots)
    {
        var capacity = 0f;
        foreach (var item in slots)
        {
            if (item is null) capacity += 100f;
            else if (item.IsHeavyAmmo) capacity += 100f - item.AmmoPercent;
        }

        return capacity;
    }

    public static int GetHeavyAmmoRoundsPerFullStack(ItemStack? weapon)
        => weapon?.Pattern switch
        {
            WeaponPattern.TraceRifle => 500,
            WeaponPattern.GrenadeLauncher => 25,
            WeaponPattern.RocketLauncher => 11,
            WeaponPattern.SniperRifle => 33,
            WeaponPattern.LinearRifle => 25,
            WeaponPattern.RocketPulseRifle => 120,
            WeaponPattern.Terror => 75,
            _ => 0
        };

    public static float GetHeavyAmmoCostPercent(ItemStack? weapon)
    {
        if (weapon?.Pattern == WeaponPattern.Terror) return 1.3333f;
        var rounds = GetHeavyAmmoRoundsPerFullStack(weapon);
        if (rounds <= 0) return 0f;
        return MathF.Max(0.1f, MathF.Floor(1000f / rounds) / 10f);
    }

    private static float NormalizeSavedWeaponDamage(ItemStackSaveData data)
    {
        if (data.Type != ItemType.Weapon) return data.WeaponDamage > 0f ? data.WeaponDamage : data.PowerBonus;

        return data.Pattern switch
        {
            WeaponPattern.TraceRifle => 13f,
            WeaponPattern.RocketLauncher => 225f,
            WeaponPattern.Pulsar => 30f,
            WeaponPattern.GrenadeLauncher => 90f,
            _ => data.WeaponDamage > 0f ? data.WeaponDamage : data.PowerBonus
        };
    }

    public static ItemStack Armor(ArmorRarity rarity, Random rng)
    {
        var armorKind = RollArmorKind(rarity, rng);
        var movementSpreadPercent = 0f;
        var dashDistancePercent = 0f;

        var defense = armorKind switch
        {
            ArmorKind.Light => rarity switch
            {
                ArmorRarity.Common => rng.Next(1, 3),
                ArmorRarity.Rare => rng.Next(3, 5),
                ArmorRarity.Epic => rng.Next(5, 7),
                ArmorRarity.Legendary => rng.Next(8, 11),
                _ => 1f
            },
            ArmorKind.Heavy => rarity switch
            {
                ArmorRarity.Common => rng.Next(5, 7),
                ArmorRarity.Rare => rng.Next(7, 9),
                ArmorRarity.Epic => rng.Next(9, 11),
                ArmorRarity.Legendary => rng.Next(12, 16),
                _ => 15f
            },
            _ => rarity switch
            {
                ArmorRarity.Damaged => 1f,
                ArmorRarity.Common => rng.Next(3, 5),
                ArmorRarity.Rare => rng.Next(5, 7),
                ArmorRarity.Epic => rng.Next(7, 9),
                ArmorRarity.Legendary => rng.Next(10, 13),
                _ => 15f
            }
        };

        var resiliencePercent = armorKind switch
        {
            ArmorKind.Light => rarity switch
            {
                ArmorRarity.Common => RollPercentRange(rng, 2, 3),
                ArmorRarity.Rare => RollPercentRange(rng, 2, 4),
                ArmorRarity.Epic => RollPercentRange(rng, 5, 9),
                ArmorRarity.Legendary => RollPercentRange(rng, 10, 14),
                _ => 0f
            },
            ArmorKind.Heavy => rarity switch
            {
                ArmorRarity.Common => RollPercentRange(rng, 4, 5),
                ArmorRarity.Rare => RollPercentRange(rng, 6, 7),
                ArmorRarity.Epic => RollPercentRange(rng, 7, 15),
                ArmorRarity.Legendary => RollPercentRange(rng, 15, 25),
                _ => 0f
            },
            _ => rarity switch
            {
                ArmorRarity.Common => RollPercentRange(rng, 2, 4),
                ArmorRarity.Rare => RollPercentRange(rng, 3, 5),
                ArmorRarity.Epic => RollPercentRange(rng, 5, 12),
                ArmorRarity.Legendary => RollPercentRange(rng, 12, 20),
                ArmorRarity.Red => RollPercentRange(rng, 15, 25),
                _ => 0f
            }
        };

        if (armorKind == ArmorKind.Light)
        {
            movementSpreadPercent = rarity switch
            {
                ArmorRarity.Common => -RollPercentRange(rng, 5, 7),
                ArmorRarity.Rare => -RollPercentRange(rng, 6, 10),
                ArmorRarity.Epic => -RollPercentRange(rng, 8, 15),
                ArmorRarity.Legendary => -RollPercentRange(rng, 12, 20),
                _ => 0f
            };
            dashDistancePercent = rarity switch
            {
                ArmorRarity.Common => RollPercentRange(rng, 10, 15),
                ArmorRarity.Rare => RollPercentRange(rng, 13, 18),
                ArmorRarity.Epic => RollPercentRange(rng, 17, 23),
                ArmorRarity.Legendary => RollPercentRange(rng, 20, 25),
                _ => 0f
            };
        }
        else if (armorKind == ArmorKind.Heavy)
        {
            movementSpreadPercent = RollPercentRange(rng, 5, 10);
            dashDistancePercent = -0.25f;
        }

        var speedBonusPercent = 0f;
        var explosionResistancePercent = 0f;
        var healingBonusPercent = 0f;
        var dashRecoveryPercent = 0f;
        var shieldMax = 0f;
        var regenPercentPerSecond = 0f;

        var name = GetArmorName(armorKind);

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
            rarity == ArmorRarity.Damaged,
            armorKind: armorKind,
            movementSpreadPercent: movementSpreadPercent,
            dashDistancePercent: dashDistancePercent);
    }

    private static ArmorKind RollArmorKind(ArmorRarity rarity, Random rng)
    {
        if (rarity is ArmorRarity.Damaged or ArmorRarity.Red) return ArmorKind.Standard;
        return rng.Next(3) switch
        {
            1 => ArmorKind.Light,
            2 => ArmorKind.Heavy,
            _ => ArmorKind.Standard
        };
    }

    private static string GetArmorName(ArmorKind armorKind)
        => armorKind switch
        {
            ArmorKind.Light => "Phantom Weave",
            ArmorKind.Heavy => "Siege Plate",
            _ => "Field Armor"
        };

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
            var primary = new[] { WeaponPattern.Standard, WeaponPattern.PulseRifle, WeaponPattern.AutoRifle };
            var heavy = new[] { WeaponPattern.SniperRifle, WeaponPattern.LinearRifle, WeaponPattern.RocketPulseRifle };
            var pool = rng.NextSingle() < 0.60f ? primary : heavy;
            return pool[rng.Next(pool.Length)];
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

        if (kind == WeaponClass.Ranged && pattern == WeaponPattern.RamBomber)
        {
            name = "???";
            description = "???";
            baseDamage = 0f;
        }
        else if (kind == WeaponClass.Ranged && pattern == WeaponPattern.Toxikus)
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
            description = "Charge rifle. Hold to charge, then release to fire a heavy linear shot.";
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
        else if (kind == WeaponClass.Ranged && pattern == WeaponPattern.AutoRifle)
        {
            name = "Auto Rifle";
            description = "Primary automatic rifle. High fire rate with stable bullet speed.";
        }
        else if (kind == WeaponClass.Ranged && pattern == WeaponPattern.RocketPulseRifle)
        {
            name = "Rocket Pulse Rifle";
            description = "Heavy burst rifle. Fires three micro-rockets with small blast damage.";
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
            "Field Armor",
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
        => CreatePatternWeapon(WeaponClass.Ranged, WeaponPattern.LinearRifle, ArmorRarity.Legendary, rng);

    public static ItemStack RocketLauncher(Random rng)
        => CreatePatternWeapon(WeaponClass.Ranged, WeaponPattern.RocketLauncher, ArmorRarity.Red, rng);

    public static ItemStack Pulsar(Random rng)
        => CreatePatternWeapon(WeaponClass.Ranged, WeaponPattern.Pulsar, ArmorRarity.Red, rng);

    public static ItemStack RamBomber(Random rng)
        => CreatePatternWeapon(WeaponClass.Ranged, WeaponPattern.RamBomber, ArmorRarity.Red, rng);

    public static ItemStack Terror()
        => new(
            ItemType.Weapon,
            "Terror",
            "Unique heavy weapon. Sustained fire spins up from 2 to 15 rounds per second over 6 seconds. Bullets inflict radioactive decomposition.",
            ArmorRarity.Red,
            Palette.Rarity(ArmorRarity.Red),
            WeaponClass.Ranged,
            WeaponPattern.Terror,
            null,
            0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f,
            33f,
            false);

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

    public static ItemStack DeviceDataFragment(int quantity = 1)
        => new(
            ItemType.KeyItem,
            "Device's Data Fragment",
            "Grants access to secured objects.",
            ArmorRarity.Common,
            Palette.C(190, 150, 82),
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
            false,
            quantity: quantity);

    public static ItemStack VexEye(int quantity = 1)
        => new(
            ItemType.KeyItem,
            "Vex's Eye",
            "The eye of monstrous robots that used plasma as an energy source. I wonder where they are now...",
            ArmorRarity.Rare,
            Palette.C(100, 190, 255),
            null,
            WeaponPattern.Standard,
            null,
            0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f,
            false,
            quantity: quantity);

    public static ItemStack InfectedExemplar(int quantity = 1)
        => new(
            ItemType.KeyItem,
            "Infected Exemplar",
            "This specimen's DNA is dangerous, but some would pay dearly for it.",
            ArmorRarity.Epic,
            Palette.C(170, 95, 205),
            null,
            WeaponPattern.Standard,
            null,
            0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f,
            false,
            quantity: quantity);

    public static ItemStack Consumable(ConsumableType t)
    {
        return t switch
        {
            ConsumableType.Medkit => new ItemStack(ItemType.Consumable, "Medkit", "Restore HP. Hotkey Q/R.", ArmorRarity.Common, Palette.C(130, 210, 120), null, WeaponPattern.Standard, t, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, false),
            ConsumableType.Stim => new ItemStack(ItemType.Consumable, "Stim", "Move speed boost. Hotkey Q/R.", ArmorRarity.Common, Palette.C(220, 220, 120), null, WeaponPattern.Standard, t, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, false),
            ConsumableType.ProtectiveDome => new ItemStack(ItemType.Consumable, "Protective Dome", "Deploy a dome that blocks enemy shots and absorbs 200 damage. Hotkey Q/R.", ArmorRarity.Common, Palette.C(120, 190, 255), null, WeaponPattern.Standard, t, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, false),
            ConsumableType.TeslaBullets => new ItemStack(ItemType.Consumable, "Tesla Bullets", "For 15 seconds ranged hits chain lightning to up to two nearby enemies. Hotkey Q/R.", ArmorRarity.Common, Palette.C(120, 230, 255), null, WeaponPattern.Standard, t, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, false),
            ConsumableType.FreezeGrenade => new ItemStack(ItemType.Consumable, "Freeze Grenade", "Throw a freezing grenade. Enemies caught inside are frozen, then slowed. Hotkey Q/R.", ArmorRarity.Common, Palette.C(130, 220, 255), null, WeaponPattern.Standard, t, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, false),
            ConsumableType.HeGrenade => new ItemStack(ItemType.Consumable, "HE Grenade", "Throw a high explosive grenade. Hotkey Q/R.", ArmorRarity.Common, Palette.C(255, 150, 80), null, WeaponPattern.Standard, t, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, false),
            ConsumableType.MidaMiniTurret => new ItemStack(ItemType.Consumable, "MIDA Mini-Turret", "Deploys a temporary mini-turret that fires at nearby enemies. Hotkey Q/R.", ArmorRarity.Common, Palette.C(255, 220, 120), null, WeaponPattern.Standard, t, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, false),
            ConsumableType.StationKey => StationKey(),
            _ => new ItemStack(ItemType.Consumable, "Sticky Bullets", "For 15 seconds your damage slows enemies by 30% for 1 second. Hotkey Q/R.", ArmorRarity.Common, Palette.C(235, 235, 235), null, WeaponPattern.Standard, t, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, false)
        };
    }

    public static ItemStack HeavyAmmo(float percent)
        => new(
            ItemType.Ammo,
            "Heavy Ammo",
            "Ammo for heavy weapons. Stacks up to 100% per inventory cell.",
            ArmorRarity.Rare,
            Palette.C(90, 170, 255),
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
            false,
            percent);

    private static float RollPercentRange(Random rng, int minPercent, int maxPercent)
        => rng.Next(minPercent, maxPercent + 1) / 100f;

    private static float RollTenthPercentRange(Random rng, int minTenthsPercent, int maxTenthsPercent)
        => rng.Next(minTenthsPercent, maxTenthsPercent + 1) / 1000f;
}
