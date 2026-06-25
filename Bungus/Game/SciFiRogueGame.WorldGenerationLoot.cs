using System.Numerics;
using System.Text.Json;
using Raylib_cs;

namespace Bungus.Game;

public sealed partial class SciFiRogueGame : IDisposable
{
    private IEnumerable<LootZone> AllZones()
    {
        foreach (var zone in _buildings) yield return zone;
        foreach (var zone in _outposts) yield return zone;
        foreach (var zone in _generatorZones) yield return zone;
        foreach (var zone in _hangars) yield return zone;
        if (_stationZone is not null) yield return _stationZone;
    }

    private void GenerateSpecialZones()
    {
        _generatorZones = [];
        _hangars = [];
        _stationZone = null;
        if (!_currentMap.IsDeadZone) return;

        var station = GetDeadZoneStationRect();
        _stationZone = new LootZone(10000, station, LootZoneKind.Station);

        var all = _buildings.Concat(_outposts).ToList();
        all.Add(_stationZone);
        PlaceSpecialZones(all, 5, LootZoneKind.Generator);
        PlaceSpecialZones(all, _rng.Next(2, 4), LootZoneKind.Hangar);
    }

    private void PlaceSpecialZones(List<LootZone> all, int count, LootZoneKind kind)
    {
        var created = 0;
        var attempts = 0;
        while (created < count && attempts < count * 240)
        {
            attempts++;
            var size = kind == LootZoneKind.Hangar
                ? new Vector2(_rng.Next(1400, 1650), _rng.Next(1400, 1650))
                : new Vector2(_rng.Next(360, 460), _rng.Next(360, 460));
            var pos = new Vector2(_rng.Next(80, _worldSize - (int)size.X - 80), _rng.Next(80, _worldSize - (int)size.Y - 80));
            var rect = new Rectangle(pos, size);
            if (!IsZonePlacementValid(rect, all)) continue;

            var zone = new LootZone(10000 + all.Count, rect, kind);
            all.Add(zone);
            if (kind == LootZoneKind.Hangar) _hangars.Add(zone);
            else _generatorZones.Add(zone);
            created++;
        }
    }

    private void PlaceZones(List<LootZone> all, int count, bool outpost)
    {
        var created = 0;
        var attempts = 0;
        while (created < count && attempts < count * 180)
        {
            attempts++;
            var scale = _currentMap.IsDeadZone ? MathF.Sqrt(1.4f) : 1f;
            var size = outpost
                ? new Vector2(_rng.Next(520, 780) * scale, _rng.Next(520, 780) * scale)
                : new Vector2(_rng.Next(420, 620) * scale, _rng.Next(420, 620) * scale);
            var pos = new Vector2(_rng.Next(80, _worldSize - (int)size.X - 80), _rng.Next(80, _worldSize - (int)size.Y - 80));
            var rect = new Rectangle(pos, size);
            if (!IsZonePlacementValid(rect, all)) continue;
            all.Add(new LootZone(all.Count, rect, outpost));
            created++;
        }
    }

    private bool IsZonePlacementValid(Rectangle rect, List<LootZone> existing)
    {
        var center = new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);
        var worldCenter = new Vector2(_worldSize / 2f, _worldSize / 2f);
        if (Vector2.Distance(center, worldCenter) < CenterNoZoneRadius + MathF.Max(rect.Width, rect.Height) * 0.5f)
        {
            return false;
        }

        if (_currentMap.IsDeadZone && RectDistance(rect, GetDeadZoneStationRect()) < MinZoneGap)
        {
            return false;
        }

        foreach (var zone in existing)
        {
            if (RectDistance(rect, zone.Rect) < MinZoneGap) return false;
        }

        return true;
    }

    private void UpdateStorageSellHold(List<UiSlot> slots, Vector2 mouse)
    {
        if (_drag is not null || !Raylib.IsKeyDown(KeyboardKey.X))
        {
            ResetInventoryUseHold();
            return;
        }

        var slot = slots.FirstOrDefault(s => Raylib.CheckCollisionPointRec(mouse, s.Rect));
        if (slot?.Item is null || slot.Kind == SlotKind.Trash)
        {
            ResetInventoryUseHold();
            return;
        }

        if (slot.Kind != SlotKind.Storage && slot.Kind != SlotKind.RunBackpack && !IsMetaLoadoutSlot(slot.Kind))
        {
            ResetInventoryUseHold();
            return;
        }

        var sellingSelection = _selectedStorageSlots.Contains(GetStorageSelectionKey(slot));

        if (_inventoryUseHoldKind != slot.Kind || _inventoryUseHoldIndex != slot.Index)
        {
            _inventoryUseHoldKind = slot.Kind;
            _inventoryUseHoldIndex = slot.Index;
            _inventoryUseHoldTimer = 0f;
        }

        _inventoryUseHoldTimer += Raylib.GetFrameTime();
        if (_inventoryUseHoldTimer < InventoryConsumableUseHoldDuration) return;

        if (sellingSelection) SellSelectedStorageSlots();
        else SellStorageSlot(slot);
        ResetInventoryUseHold();
    }

    private void SellSelectedStorageSlots()
    {
        var soldCount = 0;
        var totalValue = 0;

        foreach (var key in _selectedStorageSlots.ToList())
        {
            var item = GetSelectedStorageItem(key);
            if (item is null) continue;

            totalValue += GetSellValue(item);
            ClearSelectedStorageItem(key);
            soldCount++;
        }

        ClearStorageSelection();
        if (soldCount <= 0) return;

        _meta.SynthCoins += totalValue;
        SavePersistentState();
        ShowNotice($"Sold {soldCount} items for {totalValue} SynthCoins.");
    }

    private void SellStorageSlot(UiSlot slot)
    {
        if (slot.Item is null) return;

        _selectedStorageSlots.Remove(GetStorageSelectionKey(slot));
        var value = GetSellValue(slot.Item);
        _meta.SynthCoins += value;
        ReplaceStorageSourceWith(new DragPayload(slot.Kind, slot.Index, slot.Item), null);
        SavePersistentState();
        ShowNotice($"Sold {slot.Item.Name} for {value} SynthCoins.");
    }

    private Rectangle GetDeadZoneStationRect()
        => new(_worldSize / 2f - 1500f, _worldSize / 2f - 1000f, 3000f, 2000f);

    private List<LootChest> GenerateChestsInZones()
    {
        var list = new List<LootChest>();

        foreach (var zone in _buildings.Concat(_outposts))
        {
            var chestCount = _rng.Next(1, 4);
            for (var i = 0; i < chestCount; i++)
            {
                var pos = RandomPointInZoneSafe(zone.Rect, 20f);
                var lootCount = _rng.Next(1, 6);
                var loot = new List<ItemStack>();
                for (var l = 0; l < lootCount; l++) loot.Add(RollLoot(zone.IsOutpost));
                list.Add(new LootChest(pos, loot, zone.Id, LootContainerKind.Chest));
            }

            var crateSpawnChance = zone.IsOutpost ? 0.60f : 0.40f;
            var crateCount = 0;
            if (_rng.NextSingle() < crateSpawnChance)
            {
                var cratePos = RandomPointInZoneSafe(zone.Rect, 20f);
                list.Add(new LootChest(cratePos, RollCrateLoot(zone.IsOutpost), zone.Id, LootContainerKind.Crate));
                crateCount++;
            }

            if (crateCount > 0 && _rng.NextSingle() < 0.10f)
            {
                var cratePos = RandomPointInZoneSafe(zone.Rect, 20f);
                list.Add(new LootChest(cratePos, RollCrateLoot(zone.IsOutpost), zone.Id, LootContainerKind.Crate));
            }
        }

        foreach (var hangar in _hangars)
        {
            var chestCount = _rng.Next(2, 4);
            for (var i = 0; i < chestCount; i++)
            {
                var lootCount = _rng.Next(1, 4);
                var loot = new List<ItemStack>();
                for (var l = 0; l < lootCount; l++) loot.Add(RollHangarLoot());
                list.Add(new LootChest(RandomPointInZoneSafe(hangar.Rect, 20f), loot, hangar.Id, LootContainerKind.Chest));
            }
        }

        return list;
    }

    private ItemStack RollLoot(bool isOutpost)
    {
        var r = _rng.NextSingle();
        if (_currentMap.IsDeadZone)
        {
            if (isOutpost)
            {
                if (r < 0.20f) return ItemStack.Consumable(RollConsumableType());
                if (r < 0.50f) return RollEquipmentOfRarity(ArmorRarity.Common);
                if (r < 0.83f) return RollEquipmentOfRarity(ArmorRarity.Rare);
                if (r < 0.98f) return RollHeavyAmmo(20, 30);
                return RollEquipmentOfRarity(ArmorRarity.Epic);
            }

            if (r < 0.30f) return ItemStack.Consumable(RollConsumableType());
            if (r < 0.70f) return RollEquipmentOfRarity(ArmorRarity.Common);
            if (r < 0.90f) return RollHeavyAmmo(10, 20);
            return RollEquipmentOfRarity(ArmorRarity.Rare);
        }

        if (_selectedMapName.Equals("Baselands", StringComparison.OrdinalIgnoreCase))
        {
            if (isOutpost)
            {
                if (r < 0.25f) return ItemStack.Consumable(RollConsumableType());
                if (r < 0.55f) return RollEquipmentOfRarity(ArmorRarity.Common);
                if (r < 0.75f) return RollHeavyAmmo(15, 25);
                if (r < 0.995f) return RollEquipmentOfRarity(ArmorRarity.Rare);
                return RollEquipmentOfRarity(ArmorRarity.Epic);
            }

            if (r < 0.395f) return ItemStack.Consumable(RollConsumableType());
            if (r < 0.795f) return RollEquipmentOfRarity(ArmorRarity.Common);
            if (r < 0.97f) return RollHeavyAmmo(5, 15);
            return RollEquipmentOfRarity(ArmorRarity.Rare);
        }

        if (r < 0.35f) return ItemStack.Consumable(RollConsumableType());

        var rarity = RollRarity(isOutpost);
        return RollEquipmentOfRarity(rarity);
    }

    private ItemStack RollHangarLoot()
    {
        var r = _rng.NextSingle();
        if (r < 0.22f) return ItemStack.Consumable(RollConsumableType());
        if (r < 0.42f) return RollEquipmentOfRarity(ArmorRarity.Common);
        if (r < 0.92f) return RollEquipmentOfRarity(ArmorRarity.Rare);
        if (r < 0.96f) return RollEquipmentOfRarity(ArmorRarity.Epic);
        if (r < 0.98f) return ItemStack.Toxikus(_rng);
        return RollEquipmentOfRarity(ArmorRarity.Rare);
    }

    private ItemStack RollStationCrateLoot()
    {
        var r = _rng.NextSingle();
        if (r < 0.40f) return ItemStack.Consumable(RollConsumableType());
        if (r < 0.70f) return RollEquipmentOfRarity(ArmorRarity.Common);
        return RollEquipmentOfRarity(ArmorRarity.Rare);
    }

    private ArmorRarity RollRarity(bool isOutpost)
    {
        var r = _rng.NextSingle();

        if (!isOutpost)
        {
            if (r < 0.55f) return ArmorRarity.Common;
            if (r < 0.84f) return ArmorRarity.Rare;
            if (r < 0.98f) return ArmorRarity.Epic;
            return ArmorRarity.Legendary;
        }

        if (r < 0.20f) return ArmorRarity.Rare;
        if (r < 0.75f) return ArmorRarity.Epic;
        return ArmorRarity.Legendary;
    }

    private ItemStack RollEquipmentOfRarity(ArmorRarity rarity)
    {
        if (_rng.NextSingle() < 0.35f) return ItemStack.Armor(rarity, _rng);
        return ItemStack.Weapon(_rng.NextSingle() < 0.5f ? WeaponClass.Ranged : WeaponClass.Melee, rarity, _rng);
    }

    private ConsumableType RollConsumableType()
    {
        var roll = _rng.NextSingle();
        if (roll < 0.125f) return ConsumableType.Medkit;
        if (roll < 0.25f) return ConsumableType.Stim;
        if (roll < 0.375f) return ConsumableType.ProtectiveDome;
        if (roll < 0.5f) return ConsumableType.StickyBullets;
        if (roll < 0.625f) return ConsumableType.TeslaBullets;
        if (roll < 0.75f) return ConsumableType.FreezeGrenade;
        if (roll < 0.875f) return ConsumableType.HeGrenade;
        return ConsumableType.MidaMiniTurret;
    }

    private ItemStack RollHeavyAmmo(int minPercent, int maxPercent)
        => ItemStack.HeavyAmmo(_rng.Next(minPercent, maxPercent + 1));

    private List<ItemStack> RollBossLoot()
    {
        var loot = new List<ItemStack> { RollEquipmentOfRarity(ArmorRarity.Epic) };
        if (_rng.NextSingle() < 0.01f) loot.Add(RollEquipmentOfRarity(ArmorRarity.Legendary));
        if (_rng.NextSingle() < 0.025f) loot.Add(ItemStack.BossGrenadeLauncher());
        return loot;
    }

    private ItemStack RollBunkerTyrantLoot()
        => RollEquipmentOfRarity(ArmorRarity.Legendary);

    private List<ItemStack> RollStationBossLoot()
    {
        var secondRarity = _rng.NextSingle() < 0.05f ? ArmorRarity.Legendary : ArmorRarity.Epic;
        var loot = new List<ItemStack>
        {
            RollEquipmentOfRarity(ArmorRarity.Epic),
            RollEquipmentOfRarity(secondRarity)
        };
        if (_rng.NextSingle() < 0.20f) loot.Add(ItemStack.Lancelot(_rng));
        return loot;
    }

    private List<ItemStack> RollMiniBossLoot()
    {
        var loot = new List<ItemStack> { RollEquipmentOfRarity(_rng.NextSingle() < 0.5f ? ArmorRarity.Rare : ArmorRarity.Common) };
        if (_rng.NextSingle() < 0.25f) loot.Add(ItemStack.Consumable(RollConsumableType()));
        return loot;
    }

    private List<ItemStack> RollCrateLoot(bool isOutpost)
    {
        var loot = new List<ItemStack>();

        if (_currentMap.IsDeadZone)
        {
            loot.Add(RollDeadZoneCityCrateLoot());
            return loot;
        }

        if (isOutpost)
        {
            var r = _rng.NextSingle();
            if (r < 0.01f) loot.Add(RollEquipmentOfRarity(ArmorRarity.Rare));
            else if (r < 0.51f) loot.Add(RollEquipmentOfRarity(ArmorRarity.Common));
            if (_rng.NextSingle() < 0.25f) loot.Add(RollHeavyAmmo(10, 25));

            loot.Add(ItemStack.Consumable(RollConsumableType()));
            loot.Add(ItemStack.Consumable(RollConsumableType()));
            return loot;
        }

        if (_rng.NextSingle() < 0.10f) loot.Add(RollEquipmentOfRarity(ArmorRarity.Common));
        if (_rng.NextSingle() < 0.10f) loot.Add(RollHeavyAmmo(5, 15));
        loot.Add(ItemStack.Consumable(RollConsumableType()));
        if (_rng.NextSingle() < 0.20f) loot.Add(ItemStack.Consumable(RollConsumableType()));
        return loot;
    }

    private ItemStack RollDeadZoneCityCrateLoot()
    {
        var r = _rng.NextSingle();
        if (r < 0.30f) return ItemStack.Consumable(RollConsumableType());
        if (r < 0.70f) return RollEquipmentOfRarity(ArmorRarity.Common);
        if (r < 0.90f) return RollHeavyAmmo(10, 20);
        return RollEquipmentOfRarity(ArmorRarity.Rare);
    }

    private bool IsZoneCleared(int zoneId)
    {
        if (_enemies.Any(enemy => enemy.Alive && enemy.ZoneId == zoneId)) return false;
        if (_turrets.Any(turret => turret.Alive && turret.ZoneId == zoneId)) return false;
        if (_miniBosses.Any(boss => boss.Alive && boss.ZoneId == zoneId)) return false;
        if (_generatorGuards.Any(guard => guard.Alive && guard.ZoneId == zoneId)) return false;
        if (_toxicEnemies.Any(enemy => enemy.Alive && enemy.ZoneId == zoneId)) return false;
        return true;
    }

    private void TryDropEnemyCache(Vector2 position)
    {
        if (_challengeMode) return;
        if (_rng.NextSingle() >= 0.25f) return;

        var loot = new List<ItemStack> { ItemStack.DeviceDataFragment() };
        if (_rng.NextSingle() < 0.01f) loot.Add(ItemStack.Consumable(RollConsumableType()));
        _chests.Add(new LootChest(position, loot, null, LootContainerKind.EnemyCache));
    }

    private void TryDropStationKey(Vector2 position)
    {
        if (_rng.NextSingle() >= 0.05f) return;
        _groundConsumables.Add(new GroundConsumablePickup(position, ItemStack.StationKey()));
    }

    private void GenerateSecuredTerminalContent()
    {
        ResetSecuredTerminalContent();
        if (_challengeMode) return;

        var password = _rng.Next(0, 1_000_000).ToString("D6");
        _securedTerminalZone = new SecuredTerminalZone(RandomSecuredTerminalZonePoint(), password);
        _secondaryBunkerHatchPosition = RandomSecondaryBunkerHatchPoint();

        var selectedOutposts = _outposts
            .OrderBy(_ => _rng.Next())
            .Take(Math.Min(2, _outposts.Count))
            .ToList();

        if (selectedOutposts.Count >= 1)
        {
            _terminalNotes.Add(new TerminalNote(
                RandomPointInZoneSafe(selectedOutposts[0].Rect, 12f),
                0,
                "XXX" + password[3..]));
        }

        if (selectedOutposts.Count >= 2)
        {
            _terminalNotes.Add(new TerminalNote(
                RandomPointInZoneSafe(selectedOutposts[1].Rect, 12f),
                1,
                password[..3] + "XXX"));
        }
    }

    private void ResetSecuredTerminalContent()
    {
        _securedTerminalZone = null;
        _terminalNotes.Clear();
        _terminalNotesRead[0] = false;
        _terminalNotesRead[1] = false;
        _terminalOpen = false;
        _openTerminalNoteIndex = null;
        _terminalInput = string.Empty;
        _terminalScreenText = "ACCESS DENIED";
        _secondaryBunkerHatchPosition = Vector2.Zero;
        _secondaryBunkerHatchUnlocked = false;
    }

    private Vector2 RandomSecondaryBunkerHatchPoint()
    {
        var primaryHatch = _securedTerminalZone?.HatchPosition ?? Vector2.Zero;
        for (var i = 0; i < 300; i++)
        {
            var point = RandomOutdoorPoint(100f);
            var clearance = new Rectangle(point.X - 90f, point.Y - 90f, 180f, 180f);
            if (Vector2.Distance(point, primaryHatch) < 600f) continue;
            if (clearance.X < 80f || clearance.Y < 80f
                || clearance.X + clearance.Width > _worldSize - 80f
                || clearance.Y + clearance.Height > _worldSize - 80f) continue;
            if (AllZones().Any(zone => Raylib.CheckCollisionRecs(clearance, ExpandRect(zone.Rect, 24f)))) continue;
            if (_obstacles.Any(obstacle => Raylib.CheckCollisionRecs(clearance, obstacle.Rect))) continue;
            return point;
        }

        return RandomOutdoorPoint(100f);
    }

    private Vector2 RandomSecuredTerminalZonePoint()
    {
        for (var i = 0; i < 300; i++)
        {
            var point = RandomOutdoorPoint(24f);
            var rect = new Rectangle(point.X - 150f, point.Y - 150f, 300f, 300f);
            if (Vector2.Distance(point, new Vector2(_worldSize / 2f, _worldSize / 2f)) < CenterNoZoneRadius) continue;
            if (rect.X < 80f || rect.Y < 80f || rect.X + rect.Width > _worldSize - 80f || rect.Y + rect.Height > _worldSize - 80f) continue;
            if (AllZones().Any(zone => Raylib.CheckCollisionRecs(rect, ExpandRect(zone.Rect, 24f)))) continue;
            if (_obstacles.Any(obstacle => Raylib.CheckCollisionRecs(rect, obstacle.Rect))) continue;
            return point;
        }

        for (var i = 0; i < 100; i++)
        {
            var point = RandomOutdoorPoint(24f);
            if (Vector2.Distance(point, new Vector2(_worldSize / 2f, _worldSize / 2f)) >= CenterNoZoneRadius) return point;
        }

        return RandomOutdoorPoint(24f);
    }

    private bool TryPickGroundItem(ItemStack item)
    {
        if (item.IsHeavyAmmo) return _player.Inventory.CanStoreHeavyAmmo(item.AmmoPercent) && _player.Inventory.TryAddHeavyAmmo(item.AmmoPercent, out _);

        if (_player.Inventory.HasFreeBackpackSlot()) return _player.Inventory.AddToBackpack(item);

        if (item.Type == ItemType.Consumable && _player.Inventory.TryReceiveGroundConsumableWhenBackpackFull(item))
        {
            return true;
        }

        return false;
    }

    private Vector2 RandomPointIn(Rectangle r)
        => new(_rng.Next((int)r.X + 18, (int)(r.X + r.Width - 18)), _rng.Next((int)r.Y + 18, (int)(r.Y + r.Height - 18)));

    private Vector2 RandomPointInZoneSafe(Rectangle zoneRect, float radius)
    {
        for (var i = 0; i < 100; i++)
        {
            var point = RandomPointIn(zoneRect);
            if (!MovementUtils.CircleHitsObstacle(point, radius, _obstacles)) return point;
        }

        var step = Math.Max(18f, radius);
        for (var y = zoneRect.Y + radius; y <= zoneRect.Y + zoneRect.Height - radius; y += step)
        {
            for (var x = zoneRect.X + radius; x <= zoneRect.X + zoneRect.Width - radius; x += step)
            {
                var point = new Vector2(x, y);
                if (!MovementUtils.CircleHitsObstacle(point, radius, _obstacles)) return point;
            }
        }

        return new Vector2(zoneRect.X + zoneRect.Width / 2f, zoneRect.Y + zoneRect.Height / 2f);
    }


    private Vector2 RandomMapPointSafe(float radius)
    {
        for (var i = 0; i < 200; i++)
        {
            var point = new Vector2(_rng.Next(50, _worldSize - 50), _rng.Next(50, _worldSize - 50));
            if (MovementUtils.CircleHitsObstacle(point, radius, _obstacles)) continue;
            return point;
        }

        return new Vector2(_worldSize / 2f, _worldSize / 2f);
    }

    private Vector2 RandomOutdoorPoint(float radius = 14f)
    {
        while (true)
        {
            var point = new Vector2(_rng.Next(100, _worldSize - 100), _rng.Next(100, _worldSize - 100));
            if (AllZones().Any(z => Raylib.CheckCollisionPointRec(point, z.Rect))) continue;
            if (MovementUtils.CircleHitsObstacle(point, radius, _obstacles)) continue;
            return point;
        }
    }

    private Vector2 RandomExtractionPortalPoint(float radius)
    {
        for (var i = 0; i < 400; i++)
        {
            var point = RandomOutdoorPoint(radius);
            if (IsPointInAnyZone(point, radius + 20f)) continue;
            if (!IsOutsideCurrentScreen(point, 80f)) continue;
            return point;
        }

        return RandomOutdoorPoint(radius);
    }

    private bool IsPointInAnyZone(Vector2 point, float margin)
        => AllZones().Any(zone => Raylib.CheckCollisionPointRec(point, ExpandRect(zone.Rect, margin)));

    private static Rectangle ExpandRect(Rectangle rect, float margin)
        => new(rect.X - margin, rect.Y - margin, rect.Width + margin * 2f, rect.Height + margin * 2f);

    private bool IsOutsideCurrentScreen(Vector2 point, float margin)
    {
        var screen = Raylib.GetWorldToScreen2D(point, _camera);
        return screen.X < -margin
            || screen.Y < -margin
            || screen.X > GetUiScreenWidth() + margin
            || screen.Y > GetUiScreenHeight() + margin;
    }

    private List<Obstacle> GenerateObstacles()
    {
        var list = new List<Obstacle>();

        foreach (var zone in _buildings.Concat(_outposts).Concat(_hangars))
        {
            var count = zone.Kind switch
            {
                LootZoneKind.Hangar => _rng.Next(14, 20),
                LootZoneKind.Outpost => _rng.Next(_currentMap.IsDeadZone ? 9 : 6, _currentMap.IsDeadZone ? 14 : 10),
                _ => _rng.Next(_currentMap.IsDeadZone ? 6 : 4, _currentMap.IsDeadZone ? 10 : 7)
            };
            for (var i = 0; i < count; i++)
            {
                var tries = 0;
                while (tries++ < 60)
                {
                    var w = zone.Kind == LootZoneKind.Hangar
                        ? _rng.Next(96, 180)
                        : zone.IsOutpost ? _rng.Next(70, 128) : _rng.Next(52, 96);
                    var h = zone.Kind == LootZoneKind.Hangar
                        ? _rng.Next(96, 180)
                        : zone.IsOutpost ? _rng.Next(70, 128) : _rng.Next(52, 96);
                    var x = _rng.Next((int)zone.Rect.X + 18, (int)(zone.Rect.X + zone.Rect.Width - w - 18));
                    var y = _rng.Next((int)zone.Rect.Y + 18, (int)(zone.Rect.Y + zone.Rect.Height - h - 18));
                    var rect = new Rectangle(x, y, w, h);

                    if (list.Any(o => RectDistance(rect, o.Rect) < 10f)) continue;

                    list.Add(new Obstacle(rect));
                    break;
                }
            }
        }

        return list;
    }

    private void GenerateDeadZoneSetPieces()
    {
        if (!_currentMap.IsDeadZone) return;

        foreach (var zone in _generatorZones)
        {
            _generators.Add(new GeneratorNode(zone.Center, zone.Id));
        }

        foreach (var hangar in _hangars)
        {
            AddHangarWalls(hangar.Rect);
            var clusterCenters = new List<Vector2>();
            var clusters = _rng.Next(8, 12);
            for (var i = 0; i < clusters; i++)
            {
                var center = RandomToxicClusterCenter(hangar.Rect, clusterCenters);
                clusterCenters.Add(center);
                var blobs = _rng.Next(9, 13);
                for (var b = 0; b < blobs; b++)
                {
                    var offset = new Vector2(_rng.Next(-46, 47), _rng.Next(-34, 35));
                    _toxicPools.Add(new ToxicPool(center + offset, _rng.Next(44, 86), _rng.Next(30, 74)));
                }
            }
        }

        if (_stationZone is not null)
        {
            AddStationLayout(_stationZone.Rect);
        }
    }

    private Vector2 RandomToxicClusterCenter(Rectangle rect, List<Vector2> existing)
    {
        const float minDistance = 260f;
        for (var i = 0; i < 80; i++)
        {
            var point = RandomPointInZoneSafe(rect, 130f);
            if (existing.All(other => Vector2.DistanceSquared(point, other) >= minDistance * minDistance)) return point;
        }

        return RandomPointInZoneSafe(rect, 130f);
    }

    private void AddHangarWalls(Rectangle rect)
    {
        const float wall = 28f;
        var gap = rect.Width * 0.22f;
        var gapX = rect.X + rect.Width * 0.5f - gap * 0.5f;

        _obstacles.Add(new Obstacle(new Rectangle(rect.X, rect.Y, wall, rect.Height)));
        _obstacles.Add(new Obstacle(new Rectangle(rect.X + rect.Width - wall, rect.Y, wall, rect.Height)));
        _obstacles.Add(new Obstacle(new Rectangle(rect.X, rect.Y, gapX - rect.X, wall)));
        _obstacles.Add(new Obstacle(new Rectangle(gapX + gap, rect.Y, rect.X + rect.Width - gapX - gap, wall)));
        _obstacles.Add(new Obstacle(new Rectangle(rect.X, rect.Y + rect.Height - wall, gapX - rect.X, wall)));
        _obstacles.Add(new Obstacle(new Rectangle(gapX + gap, rect.Y + rect.Height - wall, rect.X + rect.Width - gapX - gap, wall)));
    }

    private void AddStationLayout(Rectangle rect)
    {
        const float wall = 26f;
        var x = rect.X;
        var y = rect.Y;
        var w = rect.Width;
        var h = rect.Height;

        void AddWall(float wx, float wy, float ww, float wh)
            => _obstacles.Add(new Obstacle(new Rectangle(wx, wy, ww, wh)));

        var rawRows = new[]
        {
            "#############################################################################################",
            "# G                           #                #                                            #",
            "#                             #                #                                            #",
            "#                                                                                           #",
            "####   ######                                                                               #",
            "#           #                 #                #                                            #",
            "# G         #                 #                #                                            #",
            "##################################             #                                            #",
            "#          #          #       G  #             #                                            #",
            "#          #          #          #             #                                            #",
            "#                     #          #             #                                            #",
            "#          #          ###     #########   ######                                            #",
            "# G        #          #                        #                                            #",
            "###############     ###                        #                                            #",
            "#          #                     #######   #####                                            #",
            "#                                #             #                                            #",
            "#                                #             #                                            #",
            "# G        #                     #             #                  BOSS                      #",
            "###############    ###############   ###########                                            #",
            "#          #           # G       #             #                                            #",
            "#          #           #                       #                                            #",
            "#                      #                       #                                            #",
            "#                      #         #           G #                                            #",
            "###############    ###############   ###########                                            #",
            "#          #           #G        #             #                                            #",
            "#          #           #         #             #                                            #",
            "#                      #         #             #                                            #",
            "#                      #         #           G #                                            #",
            "#          #           ####   ##################                                            #",
            "#          #                     #             #                                            #",
            "#        G #                     #             #                                            #",
            "####   #####                                   #                                            #",
            "#          #                                   #                                            #",
            "#          #                     #             #                                            #",
            "# G        # G                   #             #                                            #",
            "###################################    ######################################################"
        };

        var cols = rawRows.Max(row => row.Length);
        var rows = rawRows
            .Select(row => (row.EndsWith('#') ? row[..^1] : row).PadRight(cols - 1) + "#")
            .ToArray();
        var cellW = w / (cols - 1);
        var cellH = h / (rows.Length - 1);
        var bossRow = Array.FindIndex(rows, row => row.Contains("BOSS", StringComparison.Ordinal));
        var bossTextCol = bossRow >= 0 ? rows[bossRow].IndexOf("BOSS", StringComparison.Ordinal) : -1;
        var bossWallCol = bossTextCol > 0 ? rows[bossRow].LastIndexOf('#', bossTextCol - 1) : -1;
        bossWallCol = Math.Clamp(bossWallCol > 1 ? bossWallCol : cols / 2, 1, cols - 2);

        float GridX(int col) => x + col * cellW;
        float GridY(int row) => y + row * cellH;
        float WallX(int col) => Math.Clamp(GridX(col) - wall * 0.5f, x, x + w - wall);
        float WallY(int row) => Math.Clamp(GridY(row) - wall * 0.5f, y, y + h - wall);

        void AddGridHorizontal(int row, int startCol, int endCol)
        {
            var left = GridX(startCol);
            var right = GridX(Math.Min(endCol + 1, cols - 1));
            AddWall(left, WallY(row), MathF.Max(wall, right - left), wall);
        }

        void AddGridVertical(int col, int startRow, int endRow)
        {
            var top = GridY(startRow);
            var bottom = GridY(Math.Min(endRow + 1, rows.Length - 1));
            AddWall(WallX(col), top, wall, MathF.Max(wall, bottom - top));
        }

        for (var row = 0; row < rows.Length; row++)
        {
            var start = -1;
            for (var col = 0; col < cols; col++)
            {
                var isWall = rows[row][col] == '#';
                if (isWall && start < 0) start = col;
                if ((!isWall || col == cols - 1) && start >= 0)
                {
                    var end = isWall && col == cols - 1 ? col : col - 1;
                    if (end - start >= 1) AddGridHorizontal(row, start, end);
                    start = -1;
                }
            }
        }

        for (var col = 0; col < cols; col++)
        {
            var start = -1;
            for (var row = 0; row < rows.Length; row++)
            {
                var isWall = rows[row][col] == '#';
                if (isWall && start < 0) start = row;
                if ((!isWall || row == rows.Length - 1) && start >= 0)
                {
                    var end = isWall && row == rows.Length - 1 ? row : row - 1;
                    if (end - start >= 1) AddGridVertical(col, start, end);
                    start = -1;
                }
            }
        }

        var bottomRow = rows[^1];
        var entranceStartCol = bottomRow.IndexOf(' ');
        var entranceEndCol = entranceStartCol;
        while (entranceEndCol < cols && bottomRow[entranceEndCol] == ' ') entranceEndCol++;

        var entranceX = GridX(entranceStartCol);
        var entranceWidth = GridX(entranceEndCol) - entranceX;
        _stationEntranceDoor = new Rectangle(entranceX, y + h - wall, entranceWidth, wall);
        _obstacles.Add(new Obstacle(_stationEntranceDoor.Value));

        var bossDoorRow = 3;
        var bossDoorHeightRows = 2.2f;
        var bossWallX = WallX(bossWallCol);
        var bossDoorY = GridY(bossDoorRow);
        var bossDoorHeight = cellH * bossDoorHeightRows;
        AddWall(bossWallX, y, wall, MathF.Max(wall, bossDoorY - y));
        AddWall(bossWallX, bossDoorY + bossDoorHeight, wall, MathF.Max(wall, y + h - bossDoorY - bossDoorHeight));
        _stationBossDoor = new Rectangle(bossWallX, bossDoorY, wall, bossDoorHeight);
        _stationBossArena = new Rectangle(bossWallX + wall, y + wall, x + w - bossWallX - wall * 2f, h - wall * 2f);

        var potentialCrates = new List<Vector2>();
        for (var row = 0; row < rows.Length; row++)
        {
            for (var col = 0; col < cols; col++)
            {
                if (rows[row][col] == 'G') potentialCrates.Add(new Vector2(GridX(col), GridY(row)));
            }
        }

        foreach (var pos in potentialCrates.OrderBy(_ => _rng.Next()).Take(_rng.Next(4, 8)))
        {
            _chests.Add(new LootChest(pos, new List<ItemStack> { RollStationCrateLoot() }, null, LootContainerKind.Crate));
        }
    }

    private List<Enemy> GenerateEnemies()
    {
        var list = new List<Enemy>();
        var enhanced = _currentMap.IsDeadZone;

        foreach (var b in _buildings)
        {
            var count = ScaleDeadZoneEnemyCount(_rng.Next(2, 4));
            for (var i = 0; i < count; i++)
            {
                var patrolA = RandomPointInZoneSafe(b.Rect, 14f);
                var patrolB = RandomPointInZoneSafe(b.Rect, 14f);
                list.Add(Enemy.CreatePatrol(patrolA, patrolB, false, b.Id, enhanced));
            }

            var strongCount = ScaleDeadZoneEnemyCount(_rng.Next(1, 3));
            for (var i = 0; i < strongCount; i++)
            {
                list.Add(Enemy.CreateStrong(RandomPointInZoneSafe(b.Rect, 14f), b.Id, enhanced));
            }
        }

        foreach (var o in _outposts)
        {
            var count = ScaleDeadZoneEnemyCount(_rng.Next(5, 8));
            for (var i = 0; i < count; i++)
            {
                var patrolA = RandomPointInZoneSafe(o.Rect, 14f);
                var patrolB = RandomPointInZoneSafe(o.Rect, 14f);
                list.Add(Enemy.CreatePatrol(patrolA, patrolB, true, o.Id, enhanced));
            }
            var strong = ScaleDeadZoneEnemyCount(_rng.Next(3, 5));
            for (var i = 0; i < strong; i++) list.Add(Enemy.CreateStrong(RandomPointInZoneSafe(o.Rect, 14f), o.Id, enhanced));
        }

        var outdoorPatrols = _rng.Next(12, 19);
        for (var i = 0; i < outdoorPatrols; i++)
        {
            var patrolA = RandomOutdoorPoint();
            var patrolB = patrolA + new Vector2(_rng.Next(-160, 161), _rng.Next(-160, 161));
            patrolB = Vector2.Clamp(patrolB, new Vector2(40f, 40f), new Vector2(_worldSize - 40f, _worldSize - 40f));
            if (MovementUtils.CircleHitsObstacle(patrolB, 14f, _obstacles)) patrolB = patrolA;
            list.Add(Enemy.CreatePatrol(patrolA, patrolB, false, enhanced: enhanced));
        }

        var outdoorStrong = _currentMap.IsDeadZone ? _rng.Next(18, 29) : _rng.Next(6, 11);
        for (var i = 0; i < outdoorStrong; i++) list.Add(Enemy.CreateStrong(RandomOutdoorPoint(), enhanced: enhanced));

        var outdoorGuards = _rng.Next(10, 17);
        for (var i = 0; i < outdoorGuards; i++)
        {
            var point = RandomOutdoorPoint();
            list.Add(Enemy.CreatePatrol(point, point, false, enhanced: enhanced));
        }

        return list;
    }

    private int ScaleDeadZoneEnemyCount(int count)
        => _currentMap.IsDeadZone ? Math.Max(1, (int)MathF.Ceiling(count * 1.5f)) : count;


    private List<TurretEnemy> GenerateTurrets()
    {
        var list = new List<TurretEnemy>();
        foreach (var outpost in _outposts)
        {
            var count = _rng.Next(1, 3);
            for (var i = 0; i < count; i++)
            {
                list.Add(new TurretEnemy(RandomPointInZoneSafe(outpost.Rect, 18f), _rng.NextSingle() * MathF.Tau, outpost.Id));
            }
        }

        return list;
    }

    private List<GeneratorGuardianEnemy> GenerateGeneratorGuards()
    {
        var list = new List<GeneratorGuardianEnemy>();
        foreach (var zone in _generatorZones)
        {
            list.Add(new GeneratorGuardianEnemy(zone.Center + new Vector2(70f, 0f), zone.Id));
        }

        return list;
    }

    private List<ToxicTriangleEnemy> GenerateToxicEnemies()
    {
        var list = new List<ToxicTriangleEnemy>();
        foreach (var hangar in _hangars)
        {
            var count = _rng.Next(5, 11);
            for (var i = 0; i < count; i++)
            {
                list.Add(new ToxicTriangleEnemy(RandomPointInZoneSafe(hangar.Rect, 16f), hangar.Id));
            }
        }

        return list;
    }

    private StationBossEnemy? GenerateStationBoss()
    {
        if (!_currentMap.IsDeadZone || _stationBossArena is not Rectangle arena) return null;
        return new StationBossEnemy(new Vector2(arena.X + arena.Width * 0.5f, arena.Y + arena.Height * 0.5f), arena);
    }

    private Vector2 GeneratePlayerSpawnPoint()
    {
        var center = new Vector2(_worldSize / 2f, _worldSize / 2f);
        var minCenterDistance = MathF.Max(CenterNoZoneRadius + 650f, _worldSize * 0.25f);
        var relaxedCenterDistance = CenterNoZoneRadius + 250f;
        var minEnemyDistanceSq = PlayerSpawnMinEnemyDistance * PlayerSpawnMinEnemyDistance;
        var best = Vector2.Zero;
        var bestScore = float.NegativeInfinity;
        var hasBest = false;

        for (var i = 0; i < 900; i++)
        {
            var point = RandomOutdoorPoint(16f);
            var centerDistance = Vector2.Distance(point, center);
            var nearestEnemyDistanceSq = GetNearestEnemyDistanceSquared(point);
            var score = nearestEnemyDistanceSq + centerDistance * centerDistance * 0.15f;
            if (!hasBest || score > bestScore)
            {
                best = point;
                bestScore = score;
                hasBest = true;
            }

            if (centerDistance >= minCenterDistance && nearestEnemyDistanceSq >= minEnemyDistanceSq)
            {
                return point;
            }
        }

        for (var i = 0; i < 400; i++)
        {
            var point = RandomOutdoorPoint(16f);
            if (Vector2.Distance(point, center) < relaxedCenterDistance) continue;
            if (GetNearestEnemyDistanceSquared(point) >= minEnemyDistanceSq) return point;
        }

        return hasBest ? best : new Vector2(_worldSize / 2f, CenterNoZoneRadius + 250f);
    }

    private float GetNearestEnemyDistanceSquared(Vector2 point)
    {
        var nearest = float.PositiveInfinity;
        void Consider(Vector2 enemyPosition)
        {
            nearest = MathF.Min(nearest, Vector2.DistanceSquared(point, enemyPosition));
        }

        foreach (var enemy in _enemies) Consider(enemy.Position);
        foreach (var hex in _hexEnemies) Consider(hex.Position);
        foreach (var turret in _turrets) Consider(turret.Position);
        foreach (var boss in _miniBosses) Consider(boss.Position);
        if (_destroyerBoss is not null) Consider(_destroyerBoss.Position);
        foreach (var guard in _generatorGuards) Consider(guard.Position);
        foreach (var toxic in _toxicEnemies) Consider(toxic.Position);
        if (_stationBoss is not null) Consider(_stationBoss.Position);
        foreach (var boss in _pitStationBosses) Consider(boss.Position);

        return nearest;
    }

    private List<MiniBossEnemySquare> GenerateMiniBosses()
    {
        var list = new List<MiniBossEnemySquare>();
        foreach (var o in _outposts)
        {
            list.Add(new MiniBossEnemySquare(RandomPointInZoneSafe(o.Rect, 28f), o.Id));
        }

        return list;
    }

    private BossEnemyDestroyer GenerateDestroyerBoss()
        => new(new Vector2(_worldSize / 2f, _worldSize / 2f));


    private static List<VisualTheme> BuildThemes()
    {
        return
        [
            new VisualTheme("Neon Night", Palette.C(13, 17, 28), Palette.C(26, 32, 44), Palette.C(45, 85, 180, 45), Palette.C(60, 110, 220, 130), Palette.C(180, 45, 45, 40), Palette.C(220, 80, 80, 110), Palette.C(52, 56, 68, 245), Palette.C(88, 96, 116, 255), Color.SkyBlue, Palette.C(235, 95, 95), Palette.C(240, 110, 110), Palette.C(180, 60, 60)),
            new VisualTheme("Amber Dusk", Palette.C(35, 21, 16), Palette.C(64, 42, 28), Palette.C(112, 74, 38, 55), Palette.C(180, 118, 62, 130), Palette.C(140, 52, 34, 50), Palette.C(198, 94, 60, 120), Palette.C(84, 58, 46, 245), Palette.C(124, 90, 70, 255), Palette.C(240, 202, 120), Palette.C(205, 84, 65), Palette.C(230, 112, 78), Palette.C(175, 66, 42)),
            new VisualTheme("Toxic Bloom", Palette.C(14, 30, 23), Palette.C(28, 52, 38), Palette.C(46, 108, 82, 48), Palette.C(82, 170, 122, 140), Palette.C(90, 62, 128, 42), Palette.C(130, 95, 190, 128), Palette.C(40, 72, 60, 245), Palette.C(74, 130, 108, 255), Palette.C(122, 255, 196), Palette.C(224, 110, 185), Palette.C(244, 132, 208), Palette.C(160, 88, 172)),
            new VisualTheme("Frostline", Palette.C(11, 24, 34), Palette.C(20, 44, 62), Palette.C(48, 96, 130, 48), Palette.C(80, 144, 192, 132), Palette.C(62, 82, 118, 48), Palette.C(102, 132, 176, 130), Palette.C(48, 66, 86, 245), Palette.C(92, 126, 160, 255), Palette.C(176, 236, 255), Palette.C(235, 124, 124), Palette.C(244, 150, 150), Palette.C(170, 88, 88)),
            new VisualTheme("Synthwave", Palette.C(24, 8, 34), Palette.C(54, 24, 74), Palette.C(108, 42, 156, 46), Palette.C(166, 84, 222, 140), Palette.C(52, 108, 170, 44), Palette.C(92, 166, 232, 132), Palette.C(54, 46, 88, 245), Palette.C(112, 92, 164, 255), Palette.C(255, 152, 246), Palette.C(255, 124, 164), Palette.C(255, 154, 188), Palette.C(196, 90, 162))
        ];
    }

    private static float RectDistance(Rectangle a, Rectangle b)
    {
        var dx = MathF.Max(0f, MathF.Max(b.X - (a.X + a.Width), a.X - (b.X + b.Width)));
        var dy = MathF.Max(0f, MathF.Max(b.Y - (a.Y + a.Height), a.Y - (b.Y + b.Height)));
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}
