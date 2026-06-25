using System.Numerics;
using System.Text.Json;
using Raylib_cs;

namespace Bungus.Game;

public sealed partial class SciFiRogueGame : IDisposable
{
    private void UpdateChests()
    {
        for (var i = 0; i < _chests.Count; i++)
        {
            var chest = _chests[i];
            if (_inBunker != _bunkerChests.Contains(chest)) continue;
            if (chest.Items.Count == 0)
            {
                if (_openedChestIndex == i)
                {
                    _openedChestIndex = null;
                    _player.InventoryOpen = false;
                }

                continue;
            }

            if (Vector2.Distance(chest.Position, _player.Position) > 28f) continue;
            if (!Raylib.IsKeyPressed(KeyboardKey.F)) continue;

            if (chest.RequiresClear && chest.ZoneId is int zoneId && !IsZoneCleared(zoneId))
            {
                ShowNotice("Clear all enemies in this zone first.");
                continue;
            }

            if (_openedChestIndex == i)
            {
                _openedChestIndex = null;
                _player.InventoryOpen = false;
                break;
            }

            chest.Opened = true;
            _openedChestIndex = i;
            _player.InventoryOpen = true;
            ResetInventoryUseHold();
            break;
        }

        if (_openedChestIndex is null) return;

        var openedChest = _chests[_openedChestIndex.Value];
        if (Vector2.Distance(openedChest.Position, _player.Position) > 120f)
        {
            _openedChestIndex = null;
            _player.InventoryOpen = false;
            ResetInventoryUseHold();
            return;
        }
    }

    private void UpdateInventoryUi()
    {
        _hovered = null;
        if (!_player.InventoryOpen) return;

        var slots = BuildSlots();
        var m = GetUiMousePosition();

        foreach (var s in slots)
        {
            if (Raylib.CheckCollisionPointRec(m, s.Rect)) _hovered = s.Item;
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Right))
        {
            var from = slots.FirstOrDefault(s => Raylib.CheckCollisionPointRec(m, s.Rect));
            if (from is not null && TryMoveInventorySlotToTrash(from))
            {
                _drag = null;
                ResetInventoryUseHold();
                return;
            }
        }

        UpdateInventoryConsumableUseHold(slots, m);

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            var from = slots.FirstOrDefault(s => Raylib.CheckCollisionPointRec(m, s.Rect));
            if (from is not null)
            {
                var now = Raylib.GetTime();
                var isDoubleClick = from.Item is not null &&
                                    from.Kind == _lastClickKind &&
                                    from.Index == _lastClickIndex &&
                                    now - _lastClickTime <= 0.3;

                _lastClickKind = from.Kind;
                _lastClickIndex = from.Index;
                _lastClickTime = now;

                if (isDoubleClick && from.Item is not null && HandleDoubleClick(from))
                {
                    _drag = null;
                    ResetInventoryUseHold();
                    return;
                }

                if (from.Item is null) return;
                _drag = new DragPayload(from.Kind, from.Index, from.Item!);
            }
        }

        if (_openedChestIndex is not null && (Clicked(TakeAllButtonRect) || (Raylib.IsKeyPressed(KeyboardKey.X) && _inventoryUseHoldIndex < 0)))
        {
            MoveAllFromChestToBackpack();
        }

        if (Raylib.IsMouseButtonReleased(MouseButton.Left) && _drag is not null)
        {
            var to = slots.FirstOrDefault(s => Raylib.CheckCollisionPointRec(m, s.Rect));
            if (to is not null) ApplyDrop(_drag, to);
            _drag = null;
        }
    }

    private bool TryMoveInventorySlotToTrash(UiSlot slot)
    {
        if (slot.Item is null) return false;
        if (slot.Kind is SlotKind.Trash or SlotKind.Chest) return false;

        _player.Inventory.Trash = slot.Item;
        RemoveFromSource(new DragPayload(slot.Kind, slot.Index, slot.Item));
        return true;
    }

    private bool HandleDoubleClick(UiSlot slot)
    {
        if (slot.Kind == SlotKind.Chest && _openedChestIndex is not null)
        {
            return MoveChestItemToBackpack(slot.Index);
        }

        if (slot.Item?.Type == ItemType.Consumable && !slot.Item.IsStationKey)
        {
            return MoveConsumableToQuickSlotQ(slot);
        }

        if (slot.Kind == SlotKind.Backpack)
        {
            return EquipFromBackpack(slot.Index);
        }

        return false;
    }

    private void UpdateInventoryConsumableUseHold(List<UiSlot> slots, Vector2 mouse)
    {
        if (_drag is not null || !Raylib.IsKeyDown(KeyboardKey.X))
        {
            ResetInventoryUseHold();
            return;
        }

        var slot = slots.FirstOrDefault(s => Raylib.CheckCollisionPointRec(mouse, s.Rect));
        if (slot?.Item?.Type != ItemType.Consumable || slot.Item.IsStationKey || slot.Kind == SlotKind.Trash || slot.Kind == SlotKind.Chest)
        {
            ResetInventoryUseHold();
            return;
        }

        if (_inventoryUseHoldKind != slot.Kind || _inventoryUseHoldIndex != slot.Index)
        {
            _inventoryUseHoldKind = slot.Kind;
            _inventoryUseHoldIndex = slot.Index;
            _inventoryUseHoldTimer = 0f;
        }

        _inventoryUseHoldTimer += Raylib.GetFrameTime();
        if (_inventoryUseHoldTimer < InventoryConsumableUseHoldDuration) return;

        var consumed = _player.UseConsumableItem(slot.Item);
        if (consumed is null)
        {
            ResetInventoryUseHold();
            return;
        }

        RemoveFromSource(new DragPayload(slot.Kind, slot.Index, slot.Item));
        HandleConsumedQuickSlot(consumed);
        _player.Inventory.AutoFillConsumableSlots();
        ResetInventoryUseHold();
    }

    private bool MoveConsumableToQuickSlotQ(UiSlot slot)
    {
        if (slot.Item?.Type != ItemType.Consumable) return false;
        if (slot.Item.IsStationKey) return false;
        if (slot.Kind == SlotKind.QuickSlotQ) return false;
        if (slot.Kind is not (SlotKind.Backpack or SlotKind.QuickSlotR)) return false;

        var target = _player.Inventory.QuickSlotQ;
        _player.Inventory.QuickSlotQ = slot.Item;

        if (slot.Kind == SlotKind.Backpack)
        {
            _player.Inventory.BackpackSlots[slot.Index] = target;
            return true;
        }

        _player.Inventory.QuickSlotR = target;
        return true;
    }

    private void ResetInventoryUseHold()
    {
        _inventoryUseHoldKind = default;
        _inventoryUseHoldIndex = -1;
        _inventoryUseHoldTimer = 0f;
    }

    private void UpdateStorageUi()
    {
        _hovered = null;
        UpdateStorageScroll();
        var mouse = GetUiMousePosition();

        if (_drag is null && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            for (var i = 0; i < StorageSortButtonCount; i++)
            {
                if (!Raylib.CheckCollisionPointRec(mouse, StorageSortButtonRect(i))) continue;
                SortStorage(i);
                return;
            }
        }

        var slots = BuildStorageSlots();
        PruneStorageSelection();

        foreach (var slot in slots)
        {
            if (Raylib.CheckCollisionPointRec(mouse, slot.Rect)) _hovered = slot.Item;
        }

        UpdateStorageSellHold(slots, mouse);

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            var from = slots.FirstOrDefault(s => Raylib.CheckCollisionPointRec(mouse, s.Rect));
            if (from is not null)
            {
                if (IsShiftDown() && TryToggleStorageSelection(from)) return;

                var now = Raylib.GetTime();
                var isDoubleClick = from.Item is not null &&
                                    from.Kind == _lastClickKind &&
                                    from.Index == _lastClickIndex &&
                                    now - _lastClickTime <= 0.3;

                _lastClickKind = from.Kind;
                _lastClickIndex = from.Index;
                _lastClickTime = now;

                if (isDoubleClick && from.Item is not null && HandleStorageDoubleClick(from))
                {
                    _drag = null;
                    return;
                }

                if (from.Item is null) return;
                _drag = new DragPayload(from.Kind, from.Index, from.Item);
            }
        }

        if (Raylib.IsMouseButtonReleased(MouseButton.Left) && _drag is not null)
        {
            var to = slots.FirstOrDefault(s => Raylib.CheckCollisionPointRec(mouse, s.Rect));
            if (to is not null)
            {
                ApplyStorageDrop(_drag, to);
                ClearStorageSelection();
            }
            _drag = null;
        }
    }

    private static void UpdateEnemyVisualEffectTimers(Dictionary<object, float> timers, float dt)
    {
        foreach (var target in timers.Keys.ToArray())
        {
            var remaining = timers[target] - dt;
            if (remaining <= 0f || !IsTargetAlive(target)) timers.Remove(target);
            else timers[target] = remaining;
        }
    }

    private float GetDamageAgainstTarget(object target, float damage)
        => damage * _radioactiveDecompositionDamageMultipliers.GetValueOrDefault(target, 1f);

    private void ApplyEnemyDecomposition(object target, float duration)
    {
        if (duration <= 0f) return;
        _radioactiveDecompositionTargets[target] = MathF.Max(
            _radioactiveDecompositionTargets.GetValueOrDefault(target),
            duration);
        _radioactiveDecompositionDamageMultipliers[target] = MathF.Max(
            _radioactiveDecompositionDamageMultipliers.GetValueOrDefault(target, 1f),
            1f + 0.25f * _player.GetArcaneEffectMultiplier());
    }

    private void UpdateBunkerProtectiveDomes(float dt)
    {
        for (var i = _bunkerProtectiveDomes.Count - 1; i >= 0; i--)
        {
            var dome = _bunkerProtectiveDomes[i];
            dome.Update(dt);
            foreach (var target in EnumerateEnemyTargets())
            {
                if (Vector2.Distance(target.Position, dome.Position) > ProtectiveDome.Radius + target.Radius) continue;
                var damage = GetBunkerDomeContactDamage(target.Target);
                if (dome.TryApplyContactDamage(target.Target, damage, 0.9f)) AddDamageText(dome, damage, Palette.C(120, 205, 255));
            }
            if (!dome.Alive) _bunkerProtectiveDomes.RemoveAt(i);
        }
    }

    private List<Obstacle> BuildBunkerEnemyCollisionObstacles()
    {
        if (!_bunkerProtectiveDomes.Any(dome => dome.Alive)) return _bunkerObstacles;

        var result = new List<Obstacle>(_bunkerObstacles);
        foreach (var dome in _bunkerProtectiveDomes.Where(dome => dome.Alive))
        {
            result.Add(new Obstacle(new Rectangle(
                dome.Position.X - ProtectiveDome.Radius,
                dome.Position.Y - ProtectiveDome.Radius,
                ProtectiveDome.Radius * 2f,
                ProtectiveDome.Radius * 2f)));
        }
        return result;
    }

    private static float GetBunkerDomeContactDamage(object target)
        => target switch
        {
            BunkerTyrant _ => 22f,
            BunkerSiegeEnemy or BunkerAssaultEnemy => 18f,
            _ => 10f
        };

    private void UpdateBunkerFreezeZones(float dt)
    {
        var activeTargets = EnumerateEnemyTargets()
            .Select(target => target.Target)
            .ToHashSet(ReferenceEqualityComparer.Instance);

        foreach (var target in _frozenTargets.Keys.Where(activeTargets.Contains).ToArray())
        {
            if (!IsTargetAlive(target))
            {
                _frozenTargets.Remove(target);
                continue;
            }

            var left = _frozenTargets[target] - dt;
            if (left <= 0f)
            {
                _frozenTargets.Remove(target);
                _chilledTargets[target] = GetPlayerChillDuration();
            }
            else _frozenTargets[target] = left;
        }

        for (var i = _bunkerFreezeZones.Count - 1; i >= 0; i--)
        {
            var zone = _bunkerFreezeZones[i];
            zone.Update(dt);
            if (zone.Alive) SpawnFreezeAmbientParticles(zone.Position, FreezeZone.Radius, dt);
            foreach (var target in QueryCombatTargets(zone.Position, FreezeZone.Radius + 56f))
            {
                if (_frozenTargets.ContainsKey(target.Target)) continue;
                if (zone.Freezing && zone.Contains(target.Position, target.Radius))
                    _chilledTargets[target.Target] = zone.ChillTime;
            }

            if (!zone.Alive) _bunkerFreezeZones.RemoveAt(i);
        }

        foreach (var target in _chilledTargets.Keys.Where(activeTargets.Contains).ToArray())
        {
            if (!IsTargetAlive(target))
            {
                _chilledTargets.Remove(target);
                continue;
            }

            ApplyFreezeChillToTarget(target, 0.12f, _player.GetArcaneEffectMultiplier());
            var left = _chilledTargets[target] - dt;
            if (left <= 0f) _chilledTargets.Remove(target);
            else _chilledTargets[target] = left;
        }
    }

    private void UpdateBunkerMidaMiniTurrets(float dt)
    {
        for (var i = _bunkerMidaMiniTurrets.Count - 1; i >= 0; i--)
        {
            var turret = _bunkerMidaMiniTurrets[i];
            turret.Update(dt);
            if (!turret.Alive)
            {
                _bunkerMidaMiniTurrets.RemoveAt(i);
                continue;
            }

            EnemyTarget? target = null;
            var bestDistance = float.MaxValue;
            foreach (var candidate in QueryCombatTargets(turret.Position, MidaMiniTurret.Range + 56f))
            {
                var maxDistance = MidaMiniTurret.Range + candidate.Radius;
                var distanceSquared = Vector2.DistanceSquared(candidate.Position, turret.Position);
                if (distanceSquared > maxDistance * maxDistance || distanceSquared >= bestDistance) continue;
                bestDistance = distanceSquared;
                target = candidate;
            }
            if (target is null) continue;

            var targetValue = target.Value;
            _beamEffects.Add(new BeamEffect(turret.Position, targetValue.Position, Palette.C(255, 60, 60), 0.045f, 1.4f, false));
            if (!turret.ReadyToShoot) continue;

            var direction = targetValue.Position - turret.Position;
            if (direction.LengthSquared() <= 0.001f) direction = new Vector2(1f, 0f);
            direction = Vector2.Normalize(direction);
            _projectiles.Add(new Projectile(
                turret.Position + direction * 14f,
                direction,
                1500f,
                0.5f,
                Palette.C(255, 225, 125),
                false,
                MidaMiniTurret.Damage,
                drawRadius: 3f,
                highlighted: true,
                sourcePosition: turret.Position));
            turret.MarkShot();
        }
    }

    private static bool IsShiftDown()
        => Raylib.IsKeyDown(KeyboardKey.LeftShift) || Raylib.IsKeyDown(KeyboardKey.RightShift);

    private bool TryToggleStorageSelection(UiSlot slot)
    {
        if (!CanSelectStorageSlot(slot)) return false;

        var key = GetStorageSelectionKey(slot);
        if (!_selectedStorageSlots.Add(key)) _selectedStorageSlots.Remove(key);
        ResetInventoryUseHold();
        return true;
    }

    private static (SlotKind Kind, int Index) GetStorageSelectionKey(UiSlot slot) => (slot.Kind, slot.Index);

    private static bool CanSelectStorageSlot(UiSlot slot)
        => slot.Item is not null && (slot.Kind == SlotKind.Storage || slot.Kind == SlotKind.RunBackpack || IsMetaLoadoutSlot(slot.Kind));

    private void ClearStorageSelection()
    {
        _selectedStorageSlots.Clear();
        ResetInventoryUseHold();
    }

    private void PruneStorageSelection()
    {
        _selectedStorageSlots.RemoveWhere(key => GetSelectedStorageItem(key) is null);
    }

    private ItemStack? GetSelectedStorageItem((SlotKind Kind, int Index) key)
    {
        if (key.Kind == SlotKind.Storage) return key.Index >= 0 && key.Index < _meta.StorageSlots.Count ? _meta.StorageSlots[key.Index] : null;
        if (key.Kind == SlotKind.RunBackpack) return key.Index >= 0 && key.Index < _meta.RunBackpackSlots.Count ? _meta.RunBackpackSlots[key.Index] : null;
        return IsMetaLoadoutSlot(key.Kind) ? GetMetaLoadoutItem(key.Kind) : null;
    }

    private void ClearSelectedStorageItem((SlotKind Kind, int Index) key)
    {
        if (key.Kind == SlotKind.Storage && key.Index >= 0 && key.Index < _meta.StorageSlots.Count) _meta.StorageSlots[key.Index] = null;
        else if (key.Kind == SlotKind.RunBackpack && key.Index >= 0 && key.Index < _meta.RunBackpackSlots.Count) _meta.RunBackpackSlots[key.Index] = null;
        else if (IsMetaLoadoutSlot(key.Kind)) SetMetaLoadoutItem(key.Kind, null);
    }

    private void SortStorage(int mode)
    {
        ClearStorageSelection();
        _storageSortMode = mode;
        var items = _meta.StorageSlots.Where(item => item is not null).Select(item => item!).ToList();
        var sorted = mode switch
        {
            0 => items.OrderBy(GetStorageGeneralGroup).ThenBy(GetStorageRarityRank).ThenBy(GetStorageEquipmentRank).ThenBy(item => item.Name).ToList(),
            1 or 2 or 3 or 4 or 5 or 6 or 7 => SortStorageCategoryFirst(items, item => IsStorageSortModeMatch(item, mode)),
            _ => items
        };

        for (var i = 0; i < _meta.StorageSlots.Count; i++)
        {
            _meta.StorageSlots[i] = i < sorted.Count ? sorted[i] : null;
        }

        _storageScrollRow = 0;
        SavePersistentState();
    }

    private static List<ItemStack> SortStorageCategoryFirst(List<ItemStack> items, Func<ItemStack, bool> predicate)
        => items
            .Where(predicate)
            .OrderBy(GetStorageRarityRank)
            .ThenBy(GetStorageEquipmentRank)
            .ThenByDescending(item => item.AmmoPercent)
            .ThenBy(item => item.Name)
            .Concat(items.Where(item => !predicate(item)))
            .ToList();

    private static int GetStorageGeneralGroup(ItemStack item)
        => item.Type switch
        {
            ItemType.Weapon or ItemType.Armor => 0,
            ItemType.Consumable => 1,
            ItemType.Ammo => 2,
            ItemType.KeyItem => 3,
            _ => 4
        };

    private static int GetStorageEquipmentRank(ItemStack item)
    {
        if (item.IsPrimaryWeapon) return 0;
        if (item.IsHeavyWeapon) return 1;
        if (item.Type == ItemType.Weapon && item.WeaponKind == WeaponClass.Melee) return 2;
        if (item.Type == ItemType.Armor) return 3;
        return 4;
    }

    private static bool IsStorageSortModeMatch(ItemStack item, int mode)
        => mode switch
        {
            1 => item.Type == ItemType.Armor,
            2 => item.IsPrimaryWeapon,
            3 => item.IsHeavyWeapon,
            4 => item.Type == ItemType.Weapon && item.WeaponKind == WeaponClass.Melee,
            5 => item.Type == ItemType.Consumable && !item.IsStationKey,
            6 => item.Type == ItemType.KeyItem || item.IsStationKey,
            7 => item.IsHeavyAmmo,
            _ => true
        };

    private static int GetStorageRarityRank(ItemStack item)
        => item.Rarity switch
        {
            ArmorRarity.Red => 0,
            ArmorRarity.Legendary => 1,
            ArmorRarity.Epic => 2,
            ArmorRarity.Rare => 3,
            ArmorRarity.Common => 4,
            ArmorRarity.Damaged => 5,
            _ => 6
        };

    private void UpdateStorageScroll()
    {
        var mouse = GetUiMousePosition();
        if (!Raylib.CheckCollisionPointRec(mouse, StashPanelRect())) return;

        var wheel = Raylib.GetMouseWheelMove();
        if (MathF.Abs(wheel) <= 0.01f) return;

        var maxRow = GetMaxStashScrollRow();
        _storageScrollRow = Math.Clamp(_storageScrollRow - Math.Sign(wheel), 0, maxRow);
    }

    private bool TryMoveStorageSlotToTrash(UiSlot slot)
    {
        if (slot.Item is null) return false;
        if (slot.Kind == SlotKind.Trash) return false;
        if (slot.Kind != SlotKind.Storage && slot.Kind != SlotKind.RunBackpack && !IsMetaLoadoutSlot(slot.Kind)) return false;

        _meta.Trash = slot.Item;
        ReplaceStorageSourceWith(new DragPayload(slot.Kind, slot.Index, slot.Item), null);
        SavePersistentState();
        return true;
    }

    private bool HandleStorageDoubleClick(UiSlot slot)
    {
        if (slot.Kind == SlotKind.Storage)
        {
            return EquipFromStorage(slot.Index);
        }

        if (slot.Kind == SlotKind.RunBackpack)
        {
            return EquipFromMetaRunBackpack(slot.Index);
        }

        if (IsMetaLoadoutSlot(slot.Kind))
        {
            return MoveLoadoutItemToStorage(slot.Kind);
        }

        return false;
    }

    private bool EquipFromStorage(int storageIndex)
    {
        if (storageIndex < 0 || storageIndex >= _meta.StorageSlots.Count) return false;

        var item = _meta.StorageSlots[storageIndex];
        if (item is null) return false;

        var target = GetPreferredLoadoutSlot(item);
        if (target is null) return false;

        var old = GetMetaLoadoutItem(target.Value);
        SetMetaLoadoutItem(target.Value, item);
        _meta.StorageSlots[storageIndex] = old;
        SavePersistentState();
        return true;
    }

    private bool MoveLoadoutItemToStorage(SlotKind kind)
    {
        var item = GetMetaLoadoutItem(kind);
        if (item is null) return false;
        if (!_meta.AddToStorage(item)) return false;
        SetMetaLoadoutItem(kind, null);
        SavePersistentState();
        return true;
    }

    private bool EquipFromMetaRunBackpack(int backpackIndex)
    {
        if (backpackIndex < 0 || backpackIndex >= _meta.RunBackpackSlots.Count) return false;

        var item = _meta.RunBackpackSlots[backpackIndex];
        if (item is null) return false;

        var target = GetPreferredLoadoutSlot(item);
        if (target is null) return false;

        var old = GetMetaLoadoutItem(target.Value);
        SetMetaLoadoutItem(target.Value, item);
        _meta.RunBackpackSlots[backpackIndex] = old;
        SavePersistentState();
        return true;
    }

    private List<UiSlot> BuildStorageSlots()
    {
        var list = new List<UiSlot>();

        var firstStashIndex = _storageScrollRow * StashGridColumns;
        var visibleStashSlots = StashGridColumns * StashVisibleRows;
        for (var visible = 0; visible < visibleStashSlots; visible++)
        {
            var i = firstStashIndex + visible;
            if (i >= _meta.StorageSlots.Count) break;

            var c = visible % StashGridColumns;
            var r = visible / StashGridColumns;
            list.Add(new UiSlot(new Rectangle(910 + c * UiSlotStep, 200 + r * UiSlotStep, UiSlotSize, UiSlotSize), SlotKind.Storage, i, _meta.StorageSlots[i], i));
        }

        for (var i = 0; i < _meta.RunBackpackSlots.Count; i++)
        {
            var c = i % 5;
            var r = i / 5;
            list.Add(new UiSlot(new Rectangle(410 + c * UiSlotStep, 200 + r * UiSlotStep, UiSlotSize, UiSlotSize), SlotKind.RunBackpack, i, _meta.RunBackpackSlots[i], i));
        }

        list.AddRange(
        [
            new UiSlot(new Rectangle(230, 206, UiSlotSize, UiSlotSize), SlotKind.Armor, -1, _meta.Armor, -1),
            new UiSlot(new Rectangle(230, 306, UiSlotSize, UiSlotSize), SlotKind.RangedWeapon, -1, _meta.RangedWeapon, -1),
            new UiSlot(new Rectangle(230, 406, UiSlotSize, UiSlotSize), SlotKind.HeavyWeapon, -1, _meta.HeavyWeapon, -1),
            new UiSlot(new Rectangle(230, 506, UiSlotSize, UiSlotSize), SlotKind.MeleeWeapon, -1, _meta.MeleeWeapon, -1),
            new UiSlot(new Rectangle(80, 688, UiSlotSize, UiSlotSize), SlotKind.QuickSlotQ, -1, _meta.QuickSlotQ, -1),
            new UiSlot(new Rectangle(180, 688, UiSlotSize, UiSlotSize), SlotKind.QuickSlotR, -1, _meta.QuickSlotR, -1)
        ]);

        return list;
    }

    private void ApplyStorageDrop(DragPayload drag, UiSlot target)
    {
        if (drag.Kind == target.Kind && drag.Index == target.Index) return;

        if (target.Kind == SlotKind.Trash)
        {
            _meta.Trash = drag.Item;
            ReplaceStorageSourceWith(drag, null);
            SavePersistentState();
            return;
        }

        if (target.Kind == SlotKind.Storage)
        {
            var old = _meta.StorageSlots[target.Index];
            if (!CanReplaceStorageSource(drag, old)) return;

            _meta.StorageSlots[target.Index] = drag.Item;
            ReplaceStorageSourceWith(drag, old);
            SavePersistentState();
            return;
        }

        if (target.Kind == SlotKind.RunBackpack)
        {
            var old = _meta.RunBackpackSlots[target.Index];
            if (!CanReplaceStorageSource(drag, old)) return;

            _meta.RunBackpackSlots[target.Index] = drag.Item;
            ReplaceStorageSourceWith(drag, old);
            SavePersistentState();
            return;
        }

        if (!IsMetaLoadoutSlot(target.Kind) || !CanPlaceIntoSlot(target.Kind, drag.Item)) return;

        var existing = GetMetaLoadoutItem(target.Kind);
        if (!CanReplaceStorageSource(drag, existing)) return;

        SetMetaLoadoutItem(target.Kind, drag.Item);
        ReplaceStorageSourceWith(drag, existing);
        SavePersistentState();
    }

    private bool CanReplaceStorageSource(DragPayload drag, ItemStack? replacement)
    {
        if (replacement is null) return true;
        if (drag.Kind is SlotKind.Storage or SlotKind.RunBackpack or SlotKind.Trash) return true;
        if (IsMetaLoadoutSlot(drag.Kind) && CanPlaceIntoSlot(drag.Kind, replacement)) return true;
        return _meta.HasFreeStorageSlot();
    }

    private void ReplaceStorageSourceWith(DragPayload drag, ItemStack? replacement)
    {
        if (drag.Kind == SlotKind.Storage)
        {
            _meta.StorageSlots[drag.Index] = replacement;
            return;
        }

        if (drag.Kind == SlotKind.RunBackpack)
        {
            _meta.RunBackpackSlots[drag.Index] = replacement;
            return;
        }

        if (drag.Kind == SlotKind.Trash)
        {
            _meta.Trash = replacement;
            return;
        }

        if (!IsMetaLoadoutSlot(drag.Kind)) return;

        if (replacement is null || CanPlaceIntoSlot(drag.Kind, replacement))
        {
            SetMetaLoadoutItem(drag.Kind, replacement);
            return;
        }

        SetMetaLoadoutItem(drag.Kind, null);
        _meta.AddToStorage(replacement);
    }

    private bool MoveChestItemToBackpack(int chestIndex)
    {
        if (_openedChestIndex is null || chestIndex < 0) return false;

        var chest = _chests[_openedChestIndex.Value];
        if (chestIndex >= chest.Items.Count) return false;

        var item = chest.Items[chestIndex];
        if (item.IsHeavyAmmo)
        {
            if (!_player.Inventory.TryAddHeavyAmmo(item.AmmoPercent, out var remainingPercent) && remainingPercent >= item.AmmoPercent - 0.001f) return false;

            if (remainingPercent > 0f) chest.Items[chestIndex] = ItemStack.HeavyAmmo(remainingPercent);
            else chest.Items.RemoveAt(chestIndex);
            return true;
        }

        if (!_player.Inventory.AddToBackpack(item)) return false;

        chest.Items.RemoveAt(chestIndex);
        return true;
    }

    private void MoveAllFromChestToBackpack()
    {
        if (_openedChestIndex is null) return;

        var chest = _chests[_openedChestIndex.Value];
        for (var i = chest.Items.Count - 1; i >= 0; i--)
        {
            var item = chest.Items[i];
            if (item.IsHeavyAmmo)
            {
                _player.Inventory.TryAddHeavyAmmo(item.AmmoPercent, out var remainingPercent);
                if (remainingPercent <= 0f) chest.Items.RemoveAt(i);
                else if (remainingPercent < item.AmmoPercent - 0.001f) chest.Items[i] = ItemStack.HeavyAmmo(remainingPercent);
                continue;
            }

            if (_player.Inventory.AddToBackpack(item)) chest.Items.RemoveAt(i);
        }
    }

    private bool EquipFromBackpack(int backpackIndex)
    {
        if (backpackIndex < 0 || backpackIndex >= _player.Inventory.BackpackSlots.Count) return false;
        if (_openedChestIndex is not null) return false;

        var item = _player.Inventory.BackpackSlots[backpackIndex];
        if (item is null) return false;

        if (item.Type == ItemType.Armor)
        {
            (_player.Armor, _player.Inventory.BackpackSlots[backpackIndex]) = (item, _player.Armor);
            return true;
        }

        if (item.Type != ItemType.Weapon || item.WeaponKind is null) return false;

        if (item.WeaponKind == WeaponClass.Ranged)
        {
            if (item.IsHeavyWeapon)
            {
                (_player.HeavyWeapon, _player.Inventory.BackpackSlots[backpackIndex]) = (item, _player.HeavyWeapon);
                return true;
            }

            if (!item.IsPrimaryWeapon) return false;
            (_player.RangedWeapon, _player.Inventory.BackpackSlots[backpackIndex]) = (item, _player.RangedWeapon);
            return true;
        }

        (_player.MeleeWeapon, _player.Inventory.BackpackSlots[backpackIndex]) = (item, _player.MeleeWeapon);
        return true;
    }

    private void UpdateLevelUi()
    {
        if (!_player.InventoryOpen) return;

        if (Clicked(new Rectangle(252, 174, 22, 22))) QueuePendingLevelUpPoint(StatType.Strength);
        if (Clicked(new Rectangle(252, 204, 22, 22))) QueuePendingLevelUpPoint(StatType.Dexterity);
        if (Clicked(new Rectangle(252, 234, 22, 22))) QueuePendingLevelUpPoint(StatType.Speed);
        if (Clicked(new Rectangle(252, 264, 22, 22))) QueuePendingLevelUpPoint(StatType.Gunsmith);

        if (GetPendingLevelUpPointCount() > 0 && Clicked(new Rectangle(54, 350, 120, 30)))
        {
            ApplyPendingLevelUpPoints();
        }

        if (GetPendingLevelUpPointCount() > 0 && Clicked(new Rectangle(184, 350, 120, 30)))
        {
            ClearPendingLevelUpPoints();
        }
    }

    private void QueuePendingLevelUpPoint(StatType stat)
    {
        if (_player.StatPoints - GetPendingLevelUpPointCount() <= 0) return;

        if (stat == StatType.Strength) _pendingStrengthPoints++;
        if (stat == StatType.Dexterity) _pendingDexterityPoints++;
        if (stat == StatType.Speed) _pendingSpeedPoints++;
        if (stat == StatType.Gunsmith) _pendingGunsmithPoints++;
    }

    private int GetPendingLevelUpPointCount()
        => _pendingStrengthPoints + _pendingDexterityPoints + _pendingSpeedPoints + _pendingGunsmithPoints;

    private void ApplyPendingLevelUpPoints()
    {
        ApplyPendingStat(StatType.Strength, _pendingStrengthPoints);
        ApplyPendingStat(StatType.Dexterity, _pendingDexterityPoints);
        ApplyPendingStat(StatType.Speed, _pendingSpeedPoints);
        ApplyPendingStat(StatType.Gunsmith, _pendingGunsmithPoints);
        ClearPendingLevelUpPoints();
    }

    private void ApplyPendingStat(StatType stat, int count)
    {
        for (var i = 0; i < count; i++) _player.ApplyPoint(stat);
    }

    private void ClearPendingLevelUpPoints()
    {
        _pendingStrengthPoints = 0;
        _pendingDexterityPoints = 0;
        _pendingSpeedPoints = 0;
        _pendingGunsmithPoints = 0;
    }

    private List<UiSlot> BuildSlots()
    {
        var list = new List<UiSlot>();

        var backpackOrigin = _openedChestIndex is null ? new Vector2(690, 118) : new Vector2(56, 166);
        for (var i = 0; i < _player.Inventory.BackpackSlots.Count; i++)
        {
            var c = i % 6;
            var r = i / 6;
            list.Add(new UiSlot(new Rectangle(backpackOrigin.X + c * UiSlotStep, backpackOrigin.Y + r * UiSlotStep, UiSlotSize, UiSlotSize), SlotKind.Backpack, i, _player.Inventory.BackpackSlots[i], i));
        }

        if (_openedChestIndex is null)
        {
            list.AddRange(
            [
                new UiSlot(new Rectangle(570, 118, UiSlotSize, UiSlotSize), SlotKind.Armor, null, _player.Armor, -1),
                new UiSlot(new Rectangle(570, 216, UiSlotSize, UiSlotSize), SlotKind.RangedWeapon, null, _player.RangedWeapon, -1),
                new UiSlot(new Rectangle(570, 314, UiSlotSize, UiSlotSize), SlotKind.HeavyWeapon, null, _player.HeavyWeapon, -1),
                new UiSlot(new Rectangle(570, 412, UiSlotSize, UiSlotSize), SlotKind.MeleeWeapon, null, _player.MeleeWeapon, -1),
                new UiSlot(new Rectangle(470, 520, UiSlotSize, UiSlotSize), SlotKind.QuickSlotQ, null, _player.Inventory.QuickSlotQ, -1),
                new UiSlot(new Rectangle(568, 520, UiSlotSize, UiSlotSize), SlotKind.QuickSlotR, null, _player.Inventory.QuickSlotR, -1),
                new UiSlot(new Rectangle(1138, 594, UiSlotSize, UiSlotSize), SlotKind.Trash, null, _player.Inventory.Trash, -1)
            ]);
        }

        if (_openedChestIndex is not null)
        {
            var chest = _chests[_openedChestIndex.Value];
            for (var i = 0; i < 5; i++)
            {
                var item = i < chest.Items.Count ? chest.Items[i] : null;
                list.Add(new UiSlot(new Rectangle(740 + i * UiSlotStep, 190, UiSlotSize, UiSlotSize), SlotKind.Chest, i, item, i));
            }
        }

        return list;
    }

    private void ApplyDrop(DragPayload drag, UiSlot target)
    {
        if (target.Kind == SlotKind.Trash)
        {
            _player.Inventory.Trash = null;
            _player.Inventory.Trash = drag.Item;
            RemoveFromSource(drag);
            return;
        }

        if (target.Kind == SlotKind.Armor && drag.Item.Type == ItemType.Armor)
        {
            var old = _player.Armor;
            _player.Armor = drag.Item;
            RemoveFromSource(drag);
            if (old is not null) _player.Inventory.AddToBackpack(old);
            return;
        }

        if (target.Kind == SlotKind.RangedWeapon && drag.Item.IsPrimaryWeapon)
        {
            var old = _player.RangedWeapon;
            _player.RangedWeapon = drag.Item;
            RemoveFromSource(drag);
            if (old is not null) _player.Inventory.AddToBackpack(old);
            return;
        }

        if (target.Kind == SlotKind.HeavyWeapon && drag.Item.IsHeavyWeapon)
        {
            var old = _player.HeavyWeapon;
            _player.HeavyWeapon = drag.Item;
            RemoveFromSource(drag);
            if (old is not null) _player.Inventory.AddToBackpack(old);
            return;
        }

        if (target.Kind == SlotKind.MeleeWeapon && drag.Item.Type == ItemType.Weapon && drag.Item.WeaponKind == WeaponClass.Melee)
        {
            var old = _player.MeleeWeapon;
            _player.MeleeWeapon = drag.Item;
            RemoveFromSource(drag);
            if (old is not null) _player.Inventory.AddToBackpack(old);
            return;
        }

        if (target.Kind == SlotKind.QuickSlotQ && drag.Item.Type == ItemType.Consumable && !drag.Item.IsStationKey)
        {
            var old = _player.Inventory.QuickSlotQ;
            _player.Inventory.QuickSlotQ = drag.Item;
            RemoveFromSource(drag);
            if (old is not null) _player.Inventory.AddToBackpack(old);
            return;
        }

        if (target.Kind == SlotKind.QuickSlotR && drag.Item.Type == ItemType.Consumable && !drag.Item.IsStationKey)
        {
            var old = _player.Inventory.QuickSlotR;
            _player.Inventory.QuickSlotR = drag.Item;
            RemoveFromSource(drag);
            if (old is not null) _player.Inventory.AddToBackpack(old);
            return;
        }

        if (target.Kind == SlotKind.Backpack && drag.Kind == SlotKind.Backpack && drag.Index >= 0 && target.Index >= 0)
        {
            (_player.Inventory.BackpackSlots[drag.Index], _player.Inventory.BackpackSlots[target.Index]) =
                (_player.Inventory.BackpackSlots[target.Index], _player.Inventory.BackpackSlots[drag.Index]);
            return;
        }

        if (target.Kind == SlotKind.Backpack && target.Index >= 0)
        {
            if (_player.Inventory.BackpackSlots[target.Index] is null)
            {
                _player.Inventory.BackpackSlots[target.Index] = drag.Item;
                RemoveFromSource(drag);
            }
            return;
        }

        if (target.Kind == SlotKind.Chest && _openedChestIndex is not null)
        {
            var chest = _chests[_openedChestIndex.Value];
            if (drag.Kind == SlotKind.Chest && drag.Index >= 0 && target.Index >= 0 && drag.Index < chest.Items.Count && target.Index < chest.Items.Count)
            {
                (chest.Items[drag.Index], chest.Items[target.Index]) = (chest.Items[target.Index], chest.Items[drag.Index]);
                return;
            }

            if (drag.Kind != SlotKind.Chest && chest.Items.Count < 5)
            {
                var insertAt = Math.Clamp(target.Index, 0, chest.Items.Count);
                chest.Items.Insert(insertAt, drag.Item);
                RemoveFromSource(drag);
            }
        }
    }

    private void RemoveFromSource(DragPayload drag)
    {
        if (drag.Kind == SlotKind.Backpack && drag.Index >= 0 && drag.Index < _player.Inventory.BackpackSlots.Count)
        {
            _player.Inventory.BackpackSlots[drag.Index] = null;
        }
        else if (drag.Kind == SlotKind.Armor)
        {
            _player.Armor = null;
        }
        else if (drag.Kind == SlotKind.RangedWeapon)
        {
            _player.RangedWeapon = null;
        }
        else if (drag.Kind == SlotKind.HeavyWeapon)
        {
            _player.HeavyWeapon = null;
        }
        else if (drag.Kind == SlotKind.MeleeWeapon)
        {
            _player.MeleeWeapon = null;
        }
        else if (drag.Kind == SlotKind.QuickSlotQ)
        {
            _player.Inventory.QuickSlotQ = null;
        }
        else if (drag.Kind == SlotKind.QuickSlotR)
        {
            _player.Inventory.QuickSlotR = null;
        }
        else if (drag.Kind == SlotKind.Trash)
        {
            _player.Inventory.Trash = null;
        }
        else if (drag.Kind == SlotKind.Chest && _openedChestIndex is not null && drag.Index >= 0)
        {
            _chests[_openedChestIndex.Value].Items.RemoveAt(drag.Index);
        }
    }

}
