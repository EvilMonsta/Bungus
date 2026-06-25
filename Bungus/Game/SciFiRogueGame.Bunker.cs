using System.Numerics;
using System.Text.Json;
using Raylib_cs;

namespace Bungus.Game;

public sealed partial class SciFiRogueGame : IDisposable
{
    private void InitializeBunkerLayout()
    {
        _bunkerTyrant = new BunkerTyrant(new Vector2(2100f, 3750f));
        _bunkerScribs.Clear();
        _bunkerParasites.Clear();
        _bunkerToxicClouds.Clear();
        _bunkerSiegeEnemies.Clear();
        _bunkerAssaultEnemies.Clear();
        _bunkerInfectedEnemies.Clear();
        _bunkerInfectedClouds.Clear();
        _bunkerChests.Clear();
        Array.Fill(_bunkerTyrantSwitches, false);
        _bunkerTyrantFightStarted = false;
        _bunkerTyrantRewardDropped = false;
        _bunkerTyrantDoorSealTimer = -1f;
        _bunkerTyrantArenaObstaclesDestroyed = false;
        _bunkerTyrantDrop = null;
        _bunkerRooms =
        [
            new(1, new Rectangle(0, 200, 600, 200)),
            new(2, new Rectangle(600, 0, 600, 600)),
            new(3, new Rectangle(800, 600, 200, 1600)),
            new(4, new Rectangle(0, 800, 800, 600)),
            new(5, new Rectangle(0, 1400, 800, 600)),
            new(6, new Rectangle(0, 2000, 400, 400)),
            new(7, new Rectangle(1200, 200, 400, 200)),
            new(8, new Rectangle(1600, 200, 200, 600)),
            new(9, new Rectangle(1400, 800, 400, 600)),
            new(10, new Rectangle(1800, 200, 1400, 200)),
            new(11, new Rectangle(1800, 400, 600, 800)),
            new(12, new Rectangle(2400, 400, 600, 400)),
            new(13, new Rectangle(2400, 800, 600, 800)),
            new(14, new Rectangle(3200, 200, 200, 1400)),
            new(15, new Rectangle(3200, 1600, 800, 600)),
            new(16, new Rectangle(2400, 1800, 800, 400)),
            new(17, new Rectangle(1800, 1800, 600, 600)),
            new(18, new Rectangle(1800, 2400, 600, 200)),
            new(19, new Rectangle(1000, 2600, 2200, 1400)),
            new(20, new Rectangle(3200, 3600, 200, 200)),
            new(21, new Rectangle(3400, 3400, 600, 600))
        ];

        var connections = new (int A, int B)[]
        {
            (1, 2), (2, 3), (3, 4), (3, 5), (4, 5), (5, 6), (2, 7),
            (7, 8), (8, 9), (8, 10), (10, 11), (10, 12), (12, 13),
            (10, 14), (14, 15), (15, 16), (16, 17), (17, 18), (18, 19),
            (19, 20), (20, 21)
        };

        _bunkerDoors = connections
            .Select(connection => CreateBunkerDoor(connection.A, connection.B))
            .ToList();
        _revealedBunkerRooms.Clear();
        _revealedBunkerRooms.Add(1);
        SpawnBunkerRoomEnemies();
        SpawnBunkerChests();
        RebuildBunkerObstacles();
        GenerateBunkerDecals();
    }

    private void SpawnBunkerChests()
    {
        foreach (var roomId in new[] { 4, 6, 9, 13, 17 })
        {
            var room = _bunkerRooms.First(candidate => candidate.Id == roomId).Rect;
            var position = new Vector2(room.X + 58f, room.Y + 58f);
            var chest = new LootChest(position, RollBunkerChestLoot(), null, LootContainerKind.Crate);
            _chests.Add(chest);
            _bunkerChests.Add(chest);
        }
    }

    private List<ItemStack> RollBunkerChestLoot()
    {
        var loot = new List<ItemStack>
        {
            ItemStack.Consumable(RollConsumableType()),
            ItemStack.HeavyAmmo(_rng.Next(20, 26))
        };
        if (_rng.NextSingle() < 0.10f)
            loot.Add(_rng.Next(2) == 0 ? ItemStack.VexEye() : ItemStack.InfectedExemplar());
        var equipmentRoll = _rng.NextSingle();
        if (equipmentRoll < 0.05f) loot.Add(RollEquipmentOfRarity(ArmorRarity.Epic));
        else if (equipmentRoll < 0.25f) loot.Add(RollEquipmentOfRarity(ArmorRarity.Rare));
        return loot;
    }

    private void SpawnBunkerRoomEnemies()
    {
        SpawnBunkerRoomComposition(2, 3, 1, 0, 0);
        SpawnBunkerRoomComposition(3, 4, 0, 2, 0);
        SpawnBunkerRoomComposition(4, 0, 0, 5, 0);
        SpawnBunkerRoomComposition(5, 0, 0, 2, 2);
        SpawnBunkerRoomComposition(6, 0, 1, 0, 0);
        SpawnBunkerRoomComposition(8, 0, 1, 0, 0);
        SpawnBunkerRoomComposition(9, 0, 0, 2, 0);
        SpawnBunkerRoomComposition(10, 0, 4, 0, 0);
        SpawnBunkerRoomComposition(11, 0, 0, 0, 3);
        SpawnBunkerRoomComposition(12, 0, 0, 2, 0);
        SpawnBunkerRoomComposition(13, 0, 0, 3, 1);
        SpawnBunkerRoomComposition(15, 0, 0, 2, 3);
        SpawnBunkerRoomComposition(16, 0, 0, 2, 0);
        SpawnBunkerRoomComposition(17, 0, 0, 0, 3);
    }

    private void SpawnBunkerRoomComposition(int roomId, int siegeCount, int assaultCount, int scribCount, int infectedCount)
    {
        var total = siegeCount + assaultCount + scribCount + infectedCount;
        var room = _bunkerRooms.First(candidate => candidate.Id == roomId).Rect;
        var positions = GenerateBunkerRoomSpawnPoints(room, total);
        var index = 0;
        for (var i = 0; i < siegeCount; i++) _bunkerSiegeEnemies.Add(new BunkerSiegeEnemy(roomId, room, positions[index++]));
        for (var i = 0; i < assaultCount; i++) _bunkerAssaultEnemies.Add(new BunkerAssaultEnemy(roomId, room, positions[index++]));
        for (var i = 0; i < scribCount; i++) _bunkerScribs.Add(new BunkerScrib(positions[index++], roomId, room, startAggroed: false));
        for (var i = 0; i < infectedCount; i++) _bunkerInfectedEnemies.Add(new BunkerInfectedEnemy(roomId, room, positions[index++]));
    }

    private static List<Vector2> GenerateBunkerRoomSpawnPoints(Rectangle room, int count)
    {
        var result = new List<Vector2>(count);
        if (count <= 0) return result;
        var aspect = MathF.Max(0.2f, room.Width / MathF.Max(1f, room.Height));
        var columns = Math.Min(count, Math.Max(1, (int)MathF.Ceiling(MathF.Sqrt(count * aspect))));
        var rows = Math.Max(1, (int)MathF.Ceiling(count / (float)columns));
        const float margin = 42f;
        for (var i = 0; i < count; i++)
        {
            var column = i % columns;
            var row = i / columns;
            var x = room.X + margin + (column + 0.5f) * MathF.Max(1f, room.Width - margin * 2f) / columns;
            var y = room.Y + margin + (row + 0.5f) * MathF.Max(1f, room.Height - margin * 2f) / rows;
            result.Add(new Vector2(x, y));
        }
        return result;
    }

    private BunkerDoor CreateBunkerDoor(int roomAId, int roomBId)
    {
        var roomA = _bunkerRooms.First(room => room.Id == roomAId).Rect;
        var roomB = _bunkerRooms.First(room => room.Id == roomBId).Rect;
        var halfWall = BunkerWallThickness * 0.5f;

        if (MathF.Abs(roomA.X + roomA.Width - roomB.X) < 0.1f || MathF.Abs(roomB.X + roomB.Width - roomA.X) < 0.1f)
        {
            var boundaryX = MathF.Abs(roomA.X + roomA.Width - roomB.X) < 0.1f ? roomB.X : roomA.X;
            var overlapStart = MathF.Max(roomA.Y, roomB.Y);
            var overlapEnd = MathF.Min(roomA.Y + roomA.Height, roomB.Y + roomB.Height);
            var centerY = (overlapStart + overlapEnd) * 0.5f;
            return new BunkerDoor(roomAId, roomBId, new Rectangle(boundaryX - halfWall, centerY - BunkerDoorLength * 0.5f, BunkerWallThickness, BunkerDoorLength));
        }

        var boundaryY = MathF.Abs(roomA.Y + roomA.Height - roomB.Y) < 0.1f ? roomB.Y : roomA.Y;
        var horizontalOverlapStart = MathF.Max(roomA.X, roomB.X);
        var horizontalOverlapEnd = MathF.Min(roomA.X + roomA.Width, roomB.X + roomB.Width);
        var centerX = (horizontalOverlapStart + horizontalOverlapEnd) * 0.5f;
        return new BunkerDoor(roomAId, roomBId, new Rectangle(centerX - BunkerDoorLength * 0.5f, boundaryY - halfWall, BunkerDoorLength, BunkerWallThickness));
    }

    private void RebuildBunkerObstacles()
    {
        var obstacles = new List<Obstacle>();
        foreach (var room in _bunkerRooms)
        {
            AddBunkerRoomWalls(room.Rect, obstacles);
        }

        foreach (var door in _bunkerDoors)
        {
            if (!door.Open) obstacles.Add(new Obstacle(door.Rect));
        }

        if (!_bunkerTyrantArenaObstaclesDestroyed)
        {
            obstacles.Add(new Obstacle(new Rectangle(1600f, 2996f, 200f, BunkerWallThickness)));
            obstacles.Add(new Obstacle(new Rectangle(2400f, 2996f, 200f, BunkerWallThickness)));
            obstacles.Add(new Obstacle(new Rectangle(1396f, 3400f, BunkerWallThickness, 200f)));
            obstacles.Add(new Obstacle(new Rectangle(1400f, 3596f, 200f, BunkerWallThickness)));
            obstacles.Add(new Obstacle(new Rectangle(2796f, 3400f, BunkerWallThickness, 200f)));
            obstacles.Add(new Obstacle(new Rectangle(2600f, 3596f, 200f, BunkerWallThickness)));
        }

        _bunkerObstacles = obstacles;
        MovementUtils.WarmObstacleIndex(_bunkerObstacles);
    }

    private void AddBunkerRoomWalls(Rectangle room, List<Obstacle> obstacles)
    {
        AddBunkerHorizontalWall(room.X, room.X + room.Width, room.Y, obstacles);
        AddBunkerHorizontalWall(room.X, room.X + room.Width, room.Y + room.Height, obstacles);
        AddBunkerVerticalWall(room.Y, room.Y + room.Height, room.X, obstacles);
        AddBunkerVerticalWall(room.Y, room.Y + room.Height, room.X + room.Width, obstacles);
    }

    private void AddBunkerHorizontalWall(float start, float end, float y, List<Obstacle> obstacles)
    {
        var gaps = _bunkerDoors
            .Where(door => door.Rect.Width > door.Rect.Height && MathF.Abs(door.Rect.Y + door.Rect.Height * 0.5f - y) < 0.1f)
            .Select(door => (Start: MathF.Max(start, door.Rect.X), End: MathF.Min(end, door.Rect.X + door.Rect.Width)))
            .Where(gap => gap.End > gap.Start)
            .OrderBy(gap => gap.Start)
            .ToList();

        var cursor = start;
        foreach (var gap in gaps)
        {
            if (gap.Start > cursor) obstacles.Add(new Obstacle(new Rectangle(cursor, y - BunkerWallThickness * 0.5f, gap.Start - cursor, BunkerWallThickness)));
            cursor = MathF.Max(cursor, gap.End);
        }

        if (cursor < end) obstacles.Add(new Obstacle(new Rectangle(cursor, y - BunkerWallThickness * 0.5f, end - cursor, BunkerWallThickness)));
    }

    private void AddBunkerVerticalWall(float start, float end, float x, List<Obstacle> obstacles)
    {
        var gaps = _bunkerDoors
            .Where(door => door.Rect.Height > door.Rect.Width && MathF.Abs(door.Rect.X + door.Rect.Width * 0.5f - x) < 0.1f)
            .Select(door => (Start: MathF.Max(start, door.Rect.Y), End: MathF.Min(end, door.Rect.Y + door.Rect.Height)))
            .Where(gap => gap.End > gap.Start)
            .OrderBy(gap => gap.Start)
            .ToList();

        var cursor = start;
        foreach (var gap in gaps)
        {
            if (gap.Start > cursor) obstacles.Add(new Obstacle(new Rectangle(x - BunkerWallThickness * 0.5f, cursor, BunkerWallThickness, gap.Start - cursor)));
            cursor = MathF.Max(cursor, gap.End);
        }

        if (cursor < end) obstacles.Add(new Obstacle(new Rectangle(x - BunkerWallThickness * 0.5f, cursor, BunkerWallThickness, end - cursor)));
    }

    private void UpdateBunker(float dt)
    {
        if (_player.InventoryOpen)
        {
            UpdateInventoryUi();
            UpdateLevelUi();
            if (_drag is null) _player.Inventory.AutoFillConsumableSlots();
            return;
        }

        var previousPosition = _player.Position;
        _player.Update(dt, _bunkerObstacles, BunkerWorldSize, _dashAfterImages);
        _player.UpdateCombat(dt, _projectiles);
        AddMotionTrail(previousPosition, _player.Position, Theme.Player, 15f, MotionTrailShape.Circle, 0.18f, 13f);
        if (Raylib.IsKeyPressed(KeyboardKey.Q)) HandleConsumedQuickSlot(_player.UseQuickSlotQ());
        if (Raylib.IsKeyPressed(KeyboardKey.R)) HandleConsumedQuickSlot(_player.UseQuickSlotR());
        if (Raylib.IsKeyPressed((KeyboardKey)49)) _player.SelectWeaponSlot(WeaponSlot.Melee);
        if (Raylib.IsKeyPressed((KeyboardKey)50)) _player.SelectWeaponSlot(WeaponSlot.PrimaryRanged);
        if (Raylib.IsKeyPressed((KeyboardKey)51)) _player.SelectWeaponSlot(WeaponSlot.HeavyRanged);
        if (Raylib.IsMouseButtonPressed(MouseButton.Right)) _player.ToggleRocketPulseMode();

        var mouseWorld = Raylib.GetScreenToWorld2D(GetUiMousePosition(), _camera);
        var linearRelease = _player.IsLinearRifleEquipped && Raylib.IsMouseButtonReleased(MouseButton.Left);
        var activeWeapon = _player.ActiveWeapon;
        if (activeWeapon?.Pattern == WeaponPattern.RamBomber && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            _player.Attack(mouseWorld, _projectiles, _swings, _bunkerObstacles, BunkerWorldSize, _dashAfterImages);
        }
        else if (activeWeapon?.Pattern != WeaponPattern.RamBomber && (Raylib.IsMouseButtonDown(MouseButton.Left) || linearRelease))
        {
            _player.Attack(mouseWorld, _projectiles, _swings, _bunkerObstacles, BunkerWorldSize, _dashAfterImages);
        }

        if (Raylib.IsKeyPressed(KeyboardKey.F) && TryActivateBunkerTyrantSwitch())
        {
            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.F)
            && _bunkerTyrantDrop is not null
            && _bunkerTyrant is not null
            && Vector2.Distance(_player.Position, _bunkerTyrant.Position) <= 34f
            && TryPickGroundItem(_bunkerTyrantDrop))
        {
            _bunkerTyrantDrop = null;
            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.F) && TryOpenNearbyBunkerDoor())
        {
            return;
        }

        if (Vector2.Distance(_player.Position, BunkerEntranceHatchPosition) <= 34f && Raylib.IsKeyPressed(KeyboardKey.F))
        {
            ExitBunkerToSurface(_securedTerminalZone?.HatchPosition + new Vector2(0f, 72f) ?? _surfaceReturnPosition);
            return;
        }

        if (Vector2.Distance(_player.Position, BunkerExitHatchPosition) <= 34f && Raylib.IsKeyPressed(KeyboardKey.F))
        {
            _secondaryBunkerHatchUnlocked = true;
            ExitBunkerToSurface(_secondaryBunkerHatchPosition + new Vector2(0f, 72f));
            return;
        }

        RebuildCombatTargetCache();
        UpdateBunkerFreezeZones(dt);
        RebuildCombatTargetCache();
        UpdateBunkerMidaMiniTurrets(dt);
        UpdateBunkerRoomEnemies(dt);
        UpdateBunkerTyrantFight(dt);
        UpdateChests();
        RebuildCombatTargetCache();
        UpdateProjectiles(dt);
        UpdateEffects(dt);
        UpdateSwings(dt);
        RebuildCombatTargetCache();
        UpdateBunkerProtectiveDomes(dt);
        if (_drag is null) _player.Inventory.AutoFillConsumableSlots();
        _camera.Target = Vector2.Lerp(_camera.Target, _player.Position, 0.2f);
        if (_player.Health <= 0f) FailRun("You Died", "All carried items were lost.");
    }

    private void UpdateBunkerRoomEnemies(float dt)
    {
        var enemyObstacles = BuildBunkerEnemyCollisionObstacles();
        foreach (var enemy in _bunkerSiegeEnemies.Where(enemy => _revealedBunkerRooms.Contains(enemy.RoomId)))
        {
            if (!enemy.Alive)
            {
                AwardBunkerEnemyKill(enemy, 3);
                continue;
            }
            if (IsFrozenTarget(enemy)) continue;
            enemy.Update(dt, _player.Position, enemyObstacles, _projectiles);
        }

        foreach (var enemy in _bunkerAssaultEnemies.Where(enemy => _revealedBunkerRooms.Contains(enemy.RoomId)))
        {
            if (!enemy.Alive)
            {
                AwardBunkerEnemyKill(enemy, 3);
                continue;
            }
            if (IsFrozenTarget(enemy)) continue;
            enemy.Update(dt, _player, enemyObstacles, _projectiles);
        }

        foreach (var enemy in _bunkerInfectedEnemies.Where(enemy => _revealedBunkerRooms.Contains(enemy.RoomId)))
        {
            if (!enemy.Alive)
            {
                AwardBunkerEnemyKill(enemy, 2);
                continue;
            }
            if (IsFrozenTarget(enemy)) continue;
            enemy.Update(dt, _player, enemyObstacles, _bunkerInfectedClouds);
            if (Vector2.Distance(_player.Position, enemy.Position) <= 75f + 16f)
            {
                _player.ApplyPoison(2f);
                _player.ApplyRadioactiveDecomposition(10f);
            }
        }

        for (var i = _bunkerScribs.Count - 1; i >= 0; i--)
        {
            var scrib = _bunkerScribs[i];
            if (!_revealedBunkerRooms.Contains(scrib.RoomId)) continue;
            if (!scrib.Alive)
            {
                AwardBunkerEnemyKill(scrib, 1);
                _bunkerScribs.RemoveAt(i);
                continue;
            }
            if (IsFrozenTarget(scrib)) continue;
            if (scrib.Update(dt, _player.Position, enemyObstacles)) ExplodeBunkerScrib(scrib.Position);
            if (!scrib.Alive)
            {
                AwardBunkerEnemyKill(scrib, 1);
                _bunkerScribs.RemoveAt(i);
            }
        }

        for (var i = _bunkerInfectedClouds.Count - 1; i >= 0; i--)
        {
            var cloud = _bunkerInfectedClouds[i];
            cloud.Update(dt);
            SpawnToxicAmbientParticles(cloud.Position, cloud.Radius, cloud.Radius, dt);
            if (Vector2.Distance(_player.Position, cloud.Position) <= cloud.Radius + 16f)
            {
                _player.ApplyPoison(2f);
                _player.ApplyRadioactiveDecomposition(10f);
            }
            if (!cloud.Alive) _bunkerInfectedClouds.RemoveAt(i);
        }
    }

    private void UpdateBunkerTyrantFight(float dt)
    {
        if (_bunkerTyrant is null) return;
        var bossRoom = _bunkerRooms.First(room => room.Id == 19).Rect;
        if (!_bunkerTyrantFightStarted && Raylib.CheckCollisionPointRec(_player.Position, bossRoom))
        {
            _bunkerTyrantFightStarted = true;
            _bunkerTyrant.Activate();
            _bunkerTyrantDoorSealTimer = 0.5f;
        }

        if (_bunkerTyrantDoorSealTimer > 0f)
        {
            _bunkerTyrantDoorSealTimer -= dt;
            if (_bunkerTyrantDoorSealTimer <= 0f) SealBunkerTyrantArena();
        }

        if (!_bunkerTyrantFightStarted) return;

        if (!IsFrozenTarget(_bunkerTyrant))
        {
            _bunkerTyrant.Update(
                dt,
                _player.Position,
                _bunkerObstacles,
                _projectiles,
                position => _bunkerScribs.Add(new BunkerScrib(position)),
                position => _bunkerParasites.Add(new BunkerParasite(position)),
                BunkerTyrantLeftSpawn,
                BunkerTyrantRightSpawn,
                () => Array.Fill(_bunkerTyrantSwitches, false));
        }

        if (_bunkerTyrant.ShockwaveReady) TriggerBunkerTyrantShockwave();

        var enemyObstacles = BuildBunkerEnemyCollisionObstacles();
        for (var i = _bunkerParasites.Count - 1; i >= 0; i--)
        {
            var parasite = _bunkerParasites[i];
            if (IsFrozenTarget(parasite)) continue;
            if (parasite.Update(dt, _bunkerTyrant.Position, enemyObstacles))
            {
                _bunkerTyrant.HealFromParasite();
                _bunkerParasites.RemoveAt(i);
                continue;
            }
            if (!parasite.Alive) _bunkerParasites.RemoveAt(i);
        }

        for (var i = _bunkerToxicClouds.Count - 1; i >= 0; i--)
        {
            var cloud = _bunkerToxicClouds[i];
            cloud.Update(dt);
            SpawnToxicAmbientParticles(cloud.Position, cloud.Radius, cloud.Radius, dt);
            if (Vector2.Distance(_player.Position, cloud.Position) <= cloud.Radius + 16f)
            {
                _player.ApplyPoison(2f);
                _player.ApplyRadioactiveDecomposition(10f);
            }
            if (!cloud.Alive) _bunkerToxicClouds.RemoveAt(i);
        }

        if (_bunkerTyrant.Alive || _bunkerTyrantRewardDropped) return;
        _bunkerTyrantRewardDropped = true;
        if (!_bunkerTyrant.KillAwarded)
        {
            _bunkerTyrant.KillAwarded = true;
            _player.RegisterKill(20);
            AddRunScore(20);
        }
        _bunkerTyrantDoorSealTimer = -1f;
        SetBunkerDoorOpen(18, 19, true);
        SetBunkerDoorOpen(19, 20, true);
        _revealedBunkerRooms.Add(20);
        RebuildBunkerObstacles();
        _bunkerTyrantDrop = ItemStack.Terror();
    }

    private void TriggerBunkerTyrantShockwave()
    {
        if (_bunkerTyrant is null) return;
        _bunkerTyrant.MarkShockwaveTriggered();
        _bunkerTyrantArenaObstaclesDestroyed = true;
        RebuildBunkerObstacles();
        AddExplosion(_bunkerTyrant.Position, 520f, Palette.C(185, 40, 75), true, true, 0.2f);

        var direction = _player.Position - _bunkerTyrant.Position;
        if (direction.LengthSquared() <= 0.001f) direction = new Vector2(0f, -1f);
        direction = Vector2.Normalize(direction);
        var position = _player.Position;
        var hitWall = false;
        for (var distance = 0f; distance < 100f; distance += 4f)
        {
            var candidate = position + direction * 4f;
            if (MovementUtils.CircleHitsObstacle(candidate, 16f, _bunkerObstacles))
            {
                hitWall = true;
                break;
            }
            position = candidate;
        }
        _player.PlaceAt(position);
        if (hitWall) _player.TakeDamage(20f);
    }

    private void SealBunkerTyrantArena()
    {
        _bunkerTyrantDoorSealTimer = -1f;
        var entrance = FindBunkerDoor(18, 19);
        if (entrance is not null && CircleIntersectsRect(_player.Position, 16f, entrance.Rect))
        {
            var bossRoom = _bunkerRooms.First(room => room.Id == 19).Rect;
            _player.PlaceAt(new Vector2(
                Math.Clamp(_player.Position.X, bossRoom.X + 24f, bossRoom.X + bossRoom.Width - 24f),
                bossRoom.Y + 28f));
        }

        SetBunkerDoorOpen(18, 19, false);
        SetBunkerDoorOpen(19, 20, false);
        RebuildBunkerObstacles();
    }

    private void ExplodeBunkerScrib(Vector2 position)
    {
        const float explosionRadius = 112.5f;
        AddExplosion(position, explosionRadius, Palette.C(116, 185, 72), true);
        _bunkerInfectedClouds.Add(new BunkerInfectedCloud(position, explosionRadius, 5f, 1f));
        if (Vector2.Distance(_player.Position, position) <= explosionRadius + 16f)
        {
            _player.TakeDamage(50f, true);
            _player.ApplyPoison(2f);
            _player.ApplyRadioactiveDecomposition(10f);
        }
    }

    private void AwardBunkerEnemyKill(BunkerSiegeEnemy enemy, int experience)
    {
        if (enemy.KillAwarded) return;
        enemy.KillAwarded = true;
        _player.RegisterKill(experience);
        AddRunScore(experience);
    }

    private void AwardBunkerEnemyKill(BunkerAssaultEnemy enemy, int experience)
    {
        if (enemy.KillAwarded) return;
        enemy.KillAwarded = true;
        _player.RegisterKill(experience);
        AddRunScore(experience);
    }

    private void AwardBunkerEnemyKill(BunkerInfectedEnemy enemy, int experience)
    {
        if (enemy.KillAwarded) return;
        enemy.KillAwarded = true;
        _player.RegisterKill(experience);
        AddRunScore(experience);
    }

    private void AwardBunkerEnemyKill(BunkerScrib enemy, int experience)
    {
        if (enemy.KillAwarded) return;
        enemy.KillAwarded = true;
        _player.RegisterKill(experience);
        AddRunScore(experience);
    }

    private bool TryActivateBunkerTyrantSwitch()
    {
        if (_bunkerTyrant is null || !_bunkerTyrantFightStarted || !_bunkerTyrant.Alive
            || !_bunkerTyrant.Invulnerable || _bunkerTyrant.WakeTimer > 0f)
        {
            return false;
        }

        for (var i = 0; i < BunkerTyrantSwitchPositions.Length; i++)
        {
            if (_bunkerTyrantSwitches[i] || Vector2.Distance(_player.Position, BunkerTyrantSwitchPositions[i]) > 38f) continue;
            _bunkerTyrantSwitches[i] = true;
            if (_bunkerTyrantSwitches.All(active => active)) _bunkerTyrant.MakeVulnerable();
            return true;
        }

        return false;
    }

    private void SetBunkerDoorOpen(int roomA, int roomB, bool open)
    {
        var door = FindBunkerDoor(roomA, roomB);
        if (door is not null) door.Open = open;
    }

    private BunkerDoor? FindBunkerDoor(int roomA, int roomB)
        => _bunkerDoors.FirstOrDefault(candidate =>
            candidate.RoomA == roomA && candidate.RoomB == roomB
            || candidate.RoomA == roomB && candidate.RoomB == roomA);

    private bool TryOpenNearbyBunkerDoor()
    {
        var door = _bunkerDoors
            .Where(candidate => !candidate.Open)
            .Where(candidate => !_bunkerTyrantFightStarted
                || _bunkerTyrant?.Alive != true
                || !IsBunkerTyrantArenaDoor(candidate))
            .Where(candidate => _revealedBunkerRooms.Contains(candidate.RoomA) || _revealedBunkerRooms.Contains(candidate.RoomB))
            .OrderBy(candidate => Vector2.DistanceSquared(candidate.Center, _player.Position))
            .FirstOrDefault();
        if (door is null || Vector2.Distance(door.Center, _player.Position) > 44f) return false;

        door.Open = true;
        _revealedBunkerRooms.Add(door.RoomA);
        _revealedBunkerRooms.Add(door.RoomB);
        RebuildBunkerObstacles();
        return true;
    }

    private static bool IsBunkerTyrantArenaDoor(BunkerDoor door)
        => door.RoomA == 18 && door.RoomB == 19
        || door.RoomA == 19 && door.RoomB == 18
        || door.RoomA == 19 && door.RoomB == 20
        || door.RoomA == 20 && door.RoomB == 19;

    private void EnterBunker(bool fromSecondaryHatch = false)
    {
        if (_securedTerminalZone?.Unlocked != true) return;
        if (fromSecondaryHatch && !_secondaryBunkerHatchUnlocked) return;

        _surfaceReturnPosition = fromSecondaryHatch
            ? _secondaryBunkerHatchPosition + new Vector2(0f, 72f)
            : _securedTerminalZone.HatchPosition + new Vector2(0f, 72f);
        ResetBunkerEnemyAggro();
        _inBunker = true;
        _player.PlaceAt(fromSecondaryHatch ? BunkerSecondarySpawnPosition : BunkerSpawnPosition);
        _camera.Target = _player.Position;
        _mapOpen = false;
        _openedChestIndex = null;
        _terminalOpen = false;
        _openTerminalNoteIndex = null;
        _player.InventoryOpen = false;
        _drag = null;
        ResetInventoryUseHold();
        ClearTransitionEffects();
        StartRunIntro();
    }

    private void ResetBunkerEnemyAggro()
    {
        foreach (var enemy in _bunkerSiegeEnemies) enemy.ResetAggro();
        foreach (var enemy in _bunkerAssaultEnemies) enemy.ResetAggro();
        foreach (var enemy in _bunkerInfectedEnemies) enemy.ResetAggro();
        foreach (var scrib in _bunkerScribs) scrib.ResetAggro();
    }

    private void ExitBunkerToSurface(Vector2 surfacePosition)
    {
        _inBunker = false;
        _surfaceReturnPosition = surfacePosition == Vector2.Zero ? _surfaceReturnPosition : surfacePosition;
        _player.PlaceAt(_surfaceReturnPosition);
        _camera.Target = _player.Position;
        _mapOpen = false;
        _openedChestIndex = null;
        _player.InventoryOpen = false;
        _drag = null;
        ResetInventoryUseHold();
        ClearTransitionEffects();
        StartRunIntro();
    }

    private void ClearTransitionEffects()
    {
        _dashAfterImages.Clear();
        _motionAfterImages.Clear();
        _swings.Clear();
        _beamEffects.Clear();
        _lightningEffects.Clear();
        _explosions.Clear();
    }

}
