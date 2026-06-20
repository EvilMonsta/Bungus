using System.Numerics;
using Raylib_cs;

namespace Bungus.Game;

public sealed partial class SciFiRogueGame
{
    private void Draw()
    {
        Raylib.BeginDrawing();
        Raylib.ClearBackground(_inBunker ? Color.Black : Theme.Background);

        switch (_state)
        {
            case GameState.MainMenu:
                DrawMainMenuBackground();
                BeginUiScale();
                DrawMainMenu();
                DrawNotice();
                DrawLowHealthOverlay();
                EndUiScale();
                break;
            case GameState.MapSelect:
                BeginUiScale();
                DrawMapSelect();
                DrawNotice();
                DrawLowHealthOverlay();
                EndUiScale();
                break;
            case GameState.Storage:
                BeginUiScale();
                DrawStorage();
                DrawNotice();
                DrawLowHealthOverlay();
                EndUiScale();
                break;
            case GameState.Armory:
                BeginUiScale();
                DrawArmory();
                DrawNotice();
                DrawLowHealthOverlay();
                EndUiScale();
                break;
            case GameState.Cradle:
                BeginUiScale();
                DrawCradle();
                DrawNotice();
                DrawLowHealthOverlay();
                EndUiScale();
                break;
            case GameState.Settings:
                BeginUiScale();
                DrawSettings();
                DrawNotice();
                DrawLowHealthOverlay();
                EndUiScale();
                break;
            case GameState.Playing:
                if (!ShouldHideWorldForRunIntro())
                {
                    DrawWorld();
                    BeginUiScale();
                    DrawHud();
                    DrawCombatCursor();
                    if (_mapOpen) DrawMapWindow();
                    else DrawInventory();
                    DrawTerminalPanel();
                    DrawTerminalNotePopup();
                    DrawNotice();
                    DrawLowHealthOverlay();
                    DrawRunIntroOverlay();
                    EndUiScale();
                }
                else
                {
                    BeginUiScale();
                    DrawRunIntroOverlay(forceOpaque: true);
                    EndUiScale();
                }
                break;
            case GameState.Paused:
                DrawWorld();
                BeginUiScale();
                DrawHud();
                DrawPause();
                DrawNotice();
                DrawLowHealthOverlay();
                EndUiScale();
                break;
            case GameState.Death:
                DrawWorld();
                BeginUiScale();
                DrawDeath();
                DrawNotice();
                DrawLowHealthOverlay();
                EndUiScale();
                break;
        }

        Raylib.EndDrawing();
    }

    private static void BeginUiScale()
    {
        var scale = GetUiScale();
        var offset = GetUiOffset();
        Rlgl.PushMatrix();
        Rlgl.Translatef(offset.X, offset.Y, 0f);
        Rlgl.Scalef(scale, scale, 1f);
    }

    private static void EndUiScale()
    {
        Rlgl.PopMatrix();
    }

    private void DrawWorld()
    {
        if (_inBunker)
        {
            DrawBunkerWorld();
            return;
        }

        Raylib.BeginMode2D(GetRenderCamera());
        DrawGrid();

        foreach (var b in _buildings)
        {
            Raylib.DrawRectangleRec(b.Rect, Theme.BuildingFill);
            Raylib.DrawRectangleLinesEx(b.Rect, 2f, Theme.BuildingLine);
        }

        foreach (var o in _outposts)
        {
            Raylib.DrawRectangleRec(o.Rect, Theme.OutpostFill);
            Raylib.DrawRectangleLinesEx(o.Rect, 2f, Theme.OutpostLine);
        }

        foreach (var g in _generatorZones)
        {
            Raylib.DrawRectangleRec(g.Rect, Palette.C(50, 90, 120, 42));
            Raylib.DrawRectangleLinesEx(g.Rect, 2f, Palette.C(120, 220, 255));
        }

        foreach (var h in _hangars)
        {
            Raylib.DrawRectangleRec(h.Rect, Palette.C(40, 70, 50, 38));
            Raylib.DrawRectangleLinesEx(h.Rect, 2f, Palette.C(80, 210, 110));
        }

        if (_stationZone is not null)
        {
            Raylib.DrawRectangleRec(_stationZone.Rect, Palette.C(80, 80, 90, 45));
        }

        foreach (var pool in _toxicPools)
        {
            Raylib.DrawEllipse((int)pool.Position.X, (int)pool.Position.Y, pool.RadiusX, pool.RadiusY, Palette.C(14, 72, 22, 230));
        }

        foreach (var obstacle in _obstacles)
        {
            Raylib.DrawRectangleRec(obstacle.Rect, Theme.ObstacleFill);
            Raylib.DrawRectangleLinesEx(obstacle.Rect, 1.5f, Theme.ObstacleLine);
        }

        DrawSecuredTerminalWorldObjects();

        foreach (var dome in _protectiveDomes)
        {
            if (!dome.Alive) continue;
            var ratio = Math.Clamp(dome.Health / ProtectiveDome.MaxHealth, 0f, 1f);
            Raylib.DrawCircleV(dome.Position, ProtectiveDome.Radius, Palette.C(120, 190, 255, 46));
            Raylib.DrawCircleLines((int)dome.Position.X, (int)dome.Position.Y, ProtectiveDome.Radius, Palette.C(170, 225, 255));
            Raylib.DrawCircleLines((int)dome.Position.X, (int)dome.Position.Y, ProtectiveDome.Radius - 6f, Palette.C(120, 180, 255, 110));
            var bar = new Rectangle(dome.Position.X - 40f, dome.Position.Y - ProtectiveDome.Radius - 18f, 80f, 5f);
            Raylib.DrawRectangleRec(bar, Palette.C(20, 20, 20, 220));
            Raylib.DrawRectangle((int)bar.X, (int)bar.Y, (int)(bar.Width * ratio), (int)bar.Height, Palette.C(120, 205, 255));
        }

        foreach (var zone in _freezeZones)
        {
            if (!zone.Alive) continue;
            var alpha = zone.Freezing ? 0.26f : 0.26f * zone.Alpha;
            Raylib.DrawCircleV(zone.Position, FreezeZone.Radius, WithAlpha(Palette.C(120, 225, 255), alpha));
            Raylib.DrawCircleLines((int)zone.Position.X, (int)zone.Position.Y, FreezeZone.Radius, WithAlpha(Palette.C(170, 240, 255), zone.Freezing ? 0.78f : 0.78f * zone.Alpha));
        }

        foreach (var turret in _midaMiniTurrets)
        {
            if (!turret.Alive) continue;
            Raylib.DrawCircleV(turret.Position, 13f, Palette.C(32, 34, 38));
            Raylib.DrawCircleV(turret.Position, 7f, Palette.C(255, 220, 120));
            Raylib.DrawCircleLines((int)turret.Position.X, (int)turret.Position.Y, 15f, Palette.C(255, 80, 70));
            var bar = new Rectangle(turret.Position.X - 34f, turret.Position.Y - 28f, 68f, 5f);
            Raylib.DrawRectangleRec(bar, Palette.C(20, 20, 20, 220));
            Raylib.DrawRectangle((int)bar.X, (int)bar.Y, (int)(bar.Width * turret.LifeRatio), (int)bar.Height, Palette.C(255, 210, 100));
        }

        foreach (var chest in _chests)
        {
            if (_bunkerChests.Contains(chest)) continue;
            var rect = new Rectangle(chest.Position.X - 14, chest.Position.Y - 10, 28, 20);
            var locked = chest.RequiresClear && chest.ZoneId is int zoneId && !IsZoneCleared(zoneId);
            var empty = chest.Items.Count == 0;
            var fill = empty
                ? Palette.C(65, 65, 65, 180)
                : chest.Kind == LootContainerKind.EnemyCache
                    ? Palette.C(96, 82, 68, 240)
                    : chest.Kind == LootContainerKind.Crate
                    ? Palette.C(98, 62, 34, 240)
                    : Palette.C(122, 82, 38, 240);
            var line = empty
                ? Color.Gray
                : chest.Kind == LootContainerKind.EnemyCache
                    ? Palette.C(120, 104, 88)
                    : chest.Kind == LootContainerKind.Crate
                    ? Palette.C(140, 90, 52)
                    : locked ? Color.Red : Color.Gold;

            if (chest.Kind == LootContainerKind.Crate)
            {
                rect = new Rectangle(chest.Position.X - 14, chest.Position.Y - 14, 28, 28);
            }
            else if (chest.Kind == LootContainerKind.EnemyCache)
            {
                rect = new Rectangle(chest.Position.X - 10, chest.Position.Y - 7, 20, 14);
            }

            Raylib.DrawRectangleRec(rect, fill);
            Raylib.DrawRectangleLinesEx(rect, 1.5f, line);
            var stripColor = chest.Kind == LootContainerKind.EnemyCache ? Palette.C(240, 190, 65) : Color.Black;
            Raylib.DrawLine((int)rect.X, (int)(rect.Y + rect.Height / 2), (int)(rect.X + rect.Width), (int)(rect.Y + rect.Height / 2), stripColor);

            if (!empty && Vector2.Distance(chest.Position, _player.Position) < 30f)
            {
                Raylib.DrawText("F", (int)rect.X + (chest.Kind == LootContainerKind.Crate ? 10 : 10), (int)rect.Y - 18, 18, line);
            }
        }

        foreach (var pickup in _groundConsumables)
        {
            var rect = new Rectangle(pickup.Position.X - 8, pickup.Position.Y - 5, 16, 10);
            Raylib.DrawRectangleRec(rect, Palette.C(82, 190, 96));
            Raylib.DrawRectangleLinesEx(rect, 1.5f, Palette.C(180, 255, 180));

            if (Vector2.Distance(pickup.Position, _player.Position) < 28f)
            {
                Raylib.DrawText("F", (int)rect.X + 4, (int)rect.Y - 18, 18, Palette.C(180, 255, 180));
            }
        }

        foreach (var portal in _extractPortals)
        {
            var active = _challengeKind == ChallengeKind.PitNightmare
                ? _pitNightmarePortalActive
                : !_lastChanceActive || IsLastChancePortalOpen();
            portal.Draw((float)Raylib.GetTime(), active, _lastChanceActive);
        }

        foreach (var generator in _generators)
        {
            var fill = generator.Destroyed
                ? Palette.C(60, 60, 70)
                : generator.Vulnerable ? Palette.C(120, 210, 255) : Palette.C(90, 130, 170);
            Raylib.DrawCircleV(generator.Position, 28f, fill);
            Raylib.DrawCircleV(generator.Position, 14f, Color.White);
            Raylib.DrawCircleLines((int)generator.Position.X, (int)generator.Position.Y, 30f, generator.Vulnerable ? Color.Gold : Color.SkyBlue);
            if (!generator.Destroyed)
            {
                var ratio = generator.Health / generator.MaxHealth;
                Raylib.DrawRectangle((int)generator.Position.X - 34, (int)generator.Position.Y - 44, 68, 5, Palette.C(20, 20, 20, 220));
                Raylib.DrawRectangle((int)generator.Position.X - 34, (int)generator.Position.Y - 44, (int)(68 * ratio), 5, Color.Green);
            }
        }

        foreach (var ghost in _dashAfterImages) ghost.Draw();
        foreach (var ghost in _motionAfterImages) ghost.Draw();

        foreach (var e in _enemies) e.DrawSight();
        foreach (var h in _hexEnemies) h.DrawSight();
        foreach (var t in _turrets) t.DrawSight();
        foreach (var b in _miniBosses) b.DrawSight();
        foreach (var g in _generatorGuards) g.DrawSight();
        foreach (var toxic in _toxicEnemies) toxic.DrawSight();
        _destroyerBoss?.DrawSight();
        _stationBoss?.DrawSight();
        foreach (var boss in _pitStationBosses) boss.DrawSight();
        var hasBaseEnemyTexture = TryGetIconTexture(Path.Combine("Assets", "Icons", "Enemies", "base_enemy.png"), out var baseEnemyTexture);
        var hasEnhancedBaseEnemyTexture = TryGetIconTexture(Path.Combine("Assets", "Icons", "Enemies", "base_enemy_enhanced.png"), out var enhancedBaseEnemyTexture);
        var hasTriangleEnemyTexture = TryGetIconTexture(Path.Combine("Assets", "Icons", "Enemies", "triangle.png"), out var triangleEnemyTexture);
        var hasEnhancedTriangleEnemyTexture = TryGetIconTexture(Path.Combine("Assets", "Icons", "Enemies", "triangle_enhanced.png"), out var enhancedTriangleEnemyTexture);
        foreach (var e in _enemies)
        {
            e.Draw(
                Theme,
                hasBaseEnemyTexture ? baseEnemyTexture : null,
                hasEnhancedBaseEnemyTexture ? enhancedBaseEnemyTexture : null,
                hasTriangleEnemyTexture ? triangleEnemyTexture : null,
                hasEnhancedTriangleEnemyTexture ? enhancedTriangleEnemyTexture : null);
        }
        foreach (var h in _hexEnemies) h.Draw();
        foreach (var t in _turrets) t.Draw();
        foreach (var b in _miniBosses) b.Draw(Theme);
        foreach (var g in _generatorGuards) g.Draw();
        foreach (var toxic in _toxicEnemies) toxic.Draw();
        _destroyerBoss?.Draw();
        _stationBoss?.Draw();
        foreach (var boss in _pitStationBosses) boss.Draw();
        DrawFrozenTargetCrystals();
        DrawEnemyDebuffIcons();
        foreach (var t in _turrets) t.DrawAimLine();
        DrawPlayerSniperAimLine();
        foreach (var beam in _beamEffects) beam.Draw();
        foreach (var lightning in _lightningEffects) lightning.Draw();

        foreach (var p in _projectiles)
        {
            if (p.Kind == ProjectileKind.TraceBeam) continue;
            if (p.Kind == ProjectileKind.LinearShot)
            {
                Raylib.DrawLineEx(p.SourcePosition, p.Position, 2f, WithAlpha(p.Color, 0.35f));
            }

            if (p.Kind is ProjectileKind.PulsarBolt or ProjectileKind.MicroCharge)
            {
                var pulse = 0.65f + MathF.Sin((float)Raylib.GetTime() * 18f) * 0.25f;
                Raylib.DrawCircleV(p.Position, p.DrawRadius + 2f, WithAlpha(Palette.C(180, 245, 255), 0.25f + pulse * 0.25f));
            }

            if (p.Highlighted) Raylib.DrawCircleV(p.Position, p.DrawRadius + 1f, Color.White);
            Raylib.DrawCircleV(p.Position, p.DrawRadius, p.Color);
        }

        foreach (var ex in _explosions)
        {
            var t = ex.Life / ex.MaxLife;
            var r = ex.Radius * (1f - t);
            if (ex.Filled)
            {
                Raylib.DrawCircleV(ex.Position, r, WithAlpha(ex.Color, ex.FillAlpha * t));
            }
            if (ex.Outlined)
            {
                Raylib.DrawCircleLines((int)ex.Position.X, (int)ex.Position.Y, r, ex.Color);
            }
        }

        foreach (var s in _swings)
        {
            DrawSwing(s);
        }

        Raylib.DrawRectangleLinesEx(new Rectangle(0, 0, _worldSize, _worldSize), 6f, Palette.C(120, 160, 220));
        DrawPlayerShieldAura();
        Raylib.DrawCircleV(_player.Position, 16f, Theme.Player);
        Raylib.EndMode2D();
    }

    private void DrawBunkerWorld()
    {
        Raylib.BeginMode2D(GetRenderCamera());
        Raylib.DrawRectangle(0, 0, BunkerWorldSize, BunkerWorldSize, Color.Black);

        foreach (var room in _bunkerRooms)
        {
            if (!_revealedBunkerRooms.Contains(room.Id)) continue;
            Raylib.DrawRectangleRec(room.Rect, Palette.C(48, 50, 56));
        }

        DrawBunkerTyrantTelegraphs();

        foreach (var obstacle in _bunkerObstacles)
        {
            var visible = _bunkerRooms.Any(room =>
            {
                if (!_revealedBunkerRooms.Contains(room.Id)) return false;
                var bounds = new Rectangle(
                    room.Rect.X - BunkerWallThickness,
                    room.Rect.Y - BunkerWallThickness,
                    room.Rect.Width + BunkerWallThickness * 2f,
                    room.Rect.Height + BunkerWallThickness * 2f);
                return Raylib.CheckCollisionRecs(obstacle.Rect, bounds);
            });
            if (!visible) continue;
            Raylib.DrawRectangleRec(obstacle.Rect, Palette.C(105, 108, 118));
        }

        foreach (var door in _bunkerDoors)
        {
            if (door.Open || (!_revealedBunkerRooms.Contains(door.RoomA) && !_revealedBunkerRooms.Contains(door.RoomB))) continue;
            Raylib.DrawRectangleRec(door.Rect, Palette.C(126, 82, 62));
            Raylib.DrawRectangleLinesEx(door.Rect, 1.5f, Palette.C(220, 160, 110));
            if (Vector2.Distance(_player.Position, door.Center) <= 44f)
            {
                Raylib.DrawText("F", (int)door.Center.X - 5, (int)door.Center.Y - 24, 18, Palette.C(245, 205, 150));
            }
        }

        var entranceHatch = new Rectangle(BunkerEntranceHatchPosition.X - 36f, BunkerEntranceHatchPosition.Y - 36f, 72f, 72f);
        Raylib.DrawRectangleRec(entranceHatch, Palette.C(34, 35, 40, 255));
        Raylib.DrawRectangleLinesEx(entranceHatch, 2f, Palette.C(125, 128, 138));
        if (Vector2.Distance(_player.Position, BunkerEntranceHatchPosition) <= 34f)
        {
            Raylib.DrawText("F", (int)entranceHatch.X + 31, (int)entranceHatch.Y - 20, 18, Palette.C(220, 225, 235));
        }

        if (_revealedBunkerRooms.Contains(21))
        {
            var hatch = new Rectangle(BunkerExitHatchPosition.X - 36f, BunkerExitHatchPosition.Y - 36f, 72f, 72f);
            Raylib.DrawRectangleRec(hatch, Palette.C(34, 35, 40, 255));
            Raylib.DrawRectangleLinesEx(hatch, 2f, Palette.C(125, 128, 138));
            if (Vector2.Distance(_player.Position, BunkerExitHatchPosition) <= 34f)
            {
                Raylib.DrawText("F", (int)hatch.X + 31, (int)hatch.Y - 20, 18, Palette.C(220, 225, 235));
            }
        }

        if (_revealedBunkerRooms.Contains(19))
        {
            Raylib.DrawRectangle(1000, 3200, 200, 200, Palette.C(80, 16, 42, 150));
            Raylib.DrawCircleV(BunkerTyrantLeftSpawn, 62f, Palette.C(116, 24, 62, 190));
            Raylib.DrawRectangle(3000, 3200, 200, 200, Palette.C(80, 16, 42, 150));
            Raylib.DrawCircleV(BunkerTyrantRightSpawn, 62f, Palette.C(116, 24, 62, 190));

            for (var i = 0; i < BunkerTyrantSwitchPositions.Length; i++)
            {
                var position = BunkerTyrantSwitchPositions[i];
                var active = _bunkerTyrantSwitches[i];
                var rect = new Rectangle(position.X - 16f, position.Y - 16f, 32f, 32f);
                Raylib.DrawRectangleRec(rect, active ? Palette.C(55, 180, 80) : Palette.C(180, 42, 48));
                Raylib.DrawRectangleLinesEx(rect, 2f, active ? Palette.C(120, 255, 145) : Palette.C(245, 100, 105));
                if (!active && _bunkerTyrant?.Invulnerable == true && Vector2.Distance(_player.Position, position) <= 38f)
                    Raylib.DrawText("F", (int)position.X - 5, (int)position.Y - 38, 18, Color.White);
            }
        }

        foreach (var cloud in _bunkerToxicClouds) cloud.Draw();
        foreach (var cloud in _bunkerInfectedClouds) cloud.Draw();
        var hasSiegeTexture = TryGetIconTexture(Path.Combine("Assets", "Icons", "Enemies", "siege.png"), out var siegeTexture);
        var hasAssaultTexture = TryGetIconTexture(Path.Combine("Assets", "Icons", "Enemies", "assault.png"), out var assaultTexture);
        foreach (var enemy in _bunkerSiegeEnemies.Where(enemy => enemy.Alive && _revealedBunkerRooms.Contains(enemy.RoomId)))
            enemy.Draw(hasSiegeTexture ? siegeTexture : null);
        foreach (var enemy in _bunkerAssaultEnemies.Where(enemy => enemy.Alive && _revealedBunkerRooms.Contains(enemy.RoomId)))
            enemy.Draw(hasAssaultTexture ? assaultTexture : null);
        foreach (var enemy in _bunkerInfectedEnemies.Where(enemy => enemy.Alive && _revealedBunkerRooms.Contains(enemy.RoomId)))
        {
            Raylib.DrawCircleV(enemy.Position, 75f, Palette.C(46, 78, 42));
            enemy.Draw();
        }
        foreach (var chest in _bunkerChests)
        {
            var visibleRoom = _bunkerRooms.FirstOrDefault(room =>
                _revealedBunkerRooms.Contains(room.Id) && Raylib.CheckCollisionPointRec(chest.Position, room.Rect));
            if (visibleRoom.Id == 0) continue;
            var rect = new Rectangle(chest.Position.X - 14f, chest.Position.Y - 10f, 28f, 20f);
            Raylib.DrawRectangleRec(rect, chest.Items.Count == 0 ? Palette.C(65, 65, 65) : Palette.C(92, 76, 58));
            Raylib.DrawRectangleLinesEx(rect, 2f, Palette.C(230, 190, 80));
            if (Vector2.Distance(_player.Position, chest.Position) <= 34f)
                Raylib.DrawText("F", (int)rect.X + 9, (int)rect.Y - 18, 18, Color.White);
        }
        foreach (var scrib in _bunkerScribs.Where(scrib => scrib.Alive && _revealedBunkerRooms.Contains(scrib.RoomId))) scrib.Draw();
        foreach (var parasite in _bunkerParasites.Where(parasite => parasite.Alive)) parasite.Draw();
        if (_bunkerTyrant?.Alive == true)
        {
            var hasTyrantTexture = TryGetIconTexture(Path.Combine("Assets", "Icons", "Enemies", "tyrant.png"), out var tyrantTexture);
            _bunkerTyrant.Draw(hasTyrantTexture ? tyrantTexture : null);
        }
        DrawEnemyDebuffIcons();

        if (_bunkerTyrantDrop is not null && _bunkerTyrant is not null)
        {
            var drop = new Rectangle(_bunkerTyrant.Position.X - 14f, _bunkerTyrant.Position.Y - 14f, 28f, 28f);
            Raylib.DrawRectangleRec(drop, Palette.C(245, 190, 45));
            Raylib.DrawRectangleLinesEx(drop, 2f, Palette.C(255, 235, 130));
            if (Vector2.Distance(_player.Position, _bunkerTyrant.Position) <= 34f)
                Raylib.DrawText("F", (int)drop.X + 9, (int)drop.Y - 20, 18, Color.White);
        }

        foreach (var dome in _bunkerProtectiveDomes)
        {
            if (!dome.Alive) continue;
            var ratio = Math.Clamp(dome.Health / ProtectiveDome.MaxHealth, 0f, 1f);
            Raylib.DrawCircleV(dome.Position, ProtectiveDome.Radius, Palette.C(120, 190, 255, 46));
            Raylib.DrawCircleLines((int)dome.Position.X, (int)dome.Position.Y, ProtectiveDome.Radius, Palette.C(170, 225, 255));
            Raylib.DrawCircleLines((int)dome.Position.X, (int)dome.Position.Y, ProtectiveDome.Radius - 6f, Palette.C(120, 180, 255, 110));
            var bar = new Rectangle(dome.Position.X - 40f, dome.Position.Y - ProtectiveDome.Radius - 18f, 80f, 5f);
            Raylib.DrawRectangleRec(bar, Palette.C(20, 20, 20, 220));
            Raylib.DrawRectangle((int)bar.X, (int)bar.Y, (int)(bar.Width * ratio), (int)bar.Height, Palette.C(120, 205, 255));
        }

        foreach (var zone in _bunkerFreezeZones)
        {
            if (!zone.Alive) continue;
            var alpha = zone.Freezing ? 0.26f : 0.26f * zone.Alpha;
            Raylib.DrawCircleV(zone.Position, FreezeZone.Radius, WithAlpha(Palette.C(120, 225, 255), alpha));
            Raylib.DrawCircleLines((int)zone.Position.X, (int)zone.Position.Y, FreezeZone.Radius, WithAlpha(Palette.C(170, 240, 255), zone.Freezing ? 0.78f : 0.78f * zone.Alpha));
        }

        foreach (var turret in _bunkerMidaMiniTurrets)
        {
            if (!turret.Alive) continue;
            Raylib.DrawCircleV(turret.Position, 13f, Palette.C(32, 34, 38));
            Raylib.DrawCircleV(turret.Position, 7f, Palette.C(255, 220, 120));
            Raylib.DrawCircleLines((int)turret.Position.X, (int)turret.Position.Y, 15f, Palette.C(255, 80, 70));
            var bar = new Rectangle(turret.Position.X - 34f, turret.Position.Y - 28f, 68f, 5f);
            Raylib.DrawRectangleRec(bar, Palette.C(20, 20, 20, 220));
            Raylib.DrawRectangle((int)bar.X, (int)bar.Y, (int)(bar.Width * turret.LifeRatio), (int)bar.Height, Palette.C(255, 210, 100));
        }

        foreach (var ghost in _dashAfterImages) ghost.Draw();
        foreach (var ghost in _motionAfterImages) ghost.Draw();
        foreach (var beam in _beamEffects) beam.Draw();
        foreach (var lightning in _lightningEffects) lightning.Draw();
        foreach (var p in _projectiles)
        {
            if (p.Kind == ProjectileKind.TraceBeam) continue;
            if (p.Kind == ProjectileKind.LinearShot)
            {
                Raylib.DrawLineEx(p.SourcePosition, p.Position, 2f, WithAlpha(p.Color, 0.35f));
            }

            if (p.Kind is ProjectileKind.PulsarBolt or ProjectileKind.MicroCharge)
            {
                var pulse = 0.65f + MathF.Sin((float)Raylib.GetTime() * 18f) * 0.25f;
                Raylib.DrawCircleV(p.Position, p.DrawRadius + 2f, WithAlpha(Palette.C(180, 245, 255), 0.25f + pulse * 0.25f));
            }

            if (p.Highlighted) Raylib.DrawCircleV(p.Position, p.DrawRadius + 1f, Color.White);
            Raylib.DrawCircleV(p.Position, p.DrawRadius, p.Color);
        }

        foreach (var ex in _explosions)
        {
            var t = ex.Life / ex.MaxLife;
            var r = ex.Radius * (1f - t);
            if (ex.Filled)
            {
                Raylib.DrawCircleV(ex.Position, r, WithAlpha(ex.Color, ex.FillAlpha * t));
            }
            if (ex.Outlined)
            {
                Raylib.DrawCircleLines((int)ex.Position.X, (int)ex.Position.Y, r, ex.Color);
            }
        }

        foreach (var s in _swings) DrawSwing(s);
        DrawPlayerSniperAimLine();
        DrawPlayerShieldAura();
        Raylib.DrawCircleV(_player.Position, 16f, Theme.Player);
        Raylib.DrawRectangleLinesEx(new Rectangle(0, 0, BunkerWorldSize, BunkerWorldSize), 6f, Palette.C(120, 124, 136));
        DrawBunkerLighting();
        Raylib.EndMode2D();
    }

    private void DrawBunkerLighting()
    {
        Raylib.DrawRectangle(0, 0, BunkerWorldSize, BunkerWorldSize, Palette.C(0, 0, 0, 145));

        Raylib.BeginBlendMode(BlendMode.Additive);
        foreach (var room in _bunkerRooms)
        {
            if (!_revealedBunkerRooms.Contains(room.Id)) continue;
            if (room.Id == 19)
            {
                DrawBunkerLight(new Vector2(1600f, 3000f), 520f);
                DrawBunkerLight(new Vector2(2600f, 3000f), 520f);
                DrawBunkerLight(new Vector2(1600f, 3600f), 520f);
                DrawBunkerLight(new Vector2(2600f, 3600f), 520f);
                continue;
            }

            var horizontal = room.Rect.Width >= room.Rect.Height;
            var longSide = horizontal ? room.Rect.Width : room.Rect.Height;
            var shortSide = horizontal ? room.Rect.Height : room.Rect.Width;
            var lightCount = longSide >= shortSide * 2.5f ? 3 : 1;
            var radius = MathF.Max(150f, shortSide * 0.9f);

            for (var i = 0; i < lightCount; i++)
            {
                var position = (i + 1f) / (lightCount + 1f);
                var center = horizontal
                    ? new Vector2(room.Rect.X + room.Rect.Width * position, room.Rect.Y + room.Rect.Height * 0.5f)
                    : new Vector2(room.Rect.X + room.Rect.Width * 0.5f, room.Rect.Y + room.Rect.Height * position);
                DrawBunkerLight(center, radius);
            }
        }
        Raylib.EndBlendMode();
    }

    private static void DrawBunkerLight(Vector2 position, float radius)
    {
        var inner = Palette.C(220, 34, 38, 105);
        var outer = Palette.C(95, 0, 8, 0);
        Raylib.DrawCircleGradient((int)position.X, (int)position.Y, radius, inner, outer);
    }

    private void DrawBunkerTyrantTelegraphs()
    {
        if (_bunkerTyrant is null || !_bunkerTyrant.Alive || !_bunkerTyrant.Active) return;

        if (!_bunkerTyrant.Resting && _bunkerTyrant.WakeTimer <= 0f && _bunkerTyrant.Mode == TyrantMode.MachineGun)
        {
            var direction = _player.Position - _bunkerTyrant.Position;
            var centerAngle = MathF.Atan2(direction.Y, direction.X);
            const int segments = 20;
            const float halfAngle = 10f * MathF.PI / 180f;
            var color = Palette.C(235, 38, 48, 82);
            for (var i = 0; i < segments; i++)
            {
                var angleA = centerAngle - halfAngle + i / (float)segments * halfAngle * 2f;
                var angleB = centerAngle - halfAngle + (i + 1) / (float)segments * halfAngle * 2f;
                var pointA = ClipBunkerTyrantRay(_bunkerTyrant.Position, angleA, 2000f);
                var pointB = ClipBunkerTyrantRay(_bunkerTyrant.Position, angleB, 2000f);
                Raylib.DrawTriangle(_bunkerTyrant.Position, pointB, pointA, color);
            }
        }

        foreach (var warning in _bunkerTyrant.GrenadeWarnings)
        {
            var pulse = 0.55f + MathF.Sin((float)Raylib.GetTime() * 18f) * 0.18f;
            Raylib.DrawCircleV(warning.Position, 150f, WithAlpha(Palette.C(230, 45, 55), pulse * 0.25f));
            Raylib.DrawCircleLines((int)warning.Position.X, (int)warning.Position.Y, 150f, Palette.C(245, 75, 75, 190));
        }
    }

    private Vector2 ClipBunkerTyrantRay(Vector2 start, float angle, float maxDistance)
    {
        var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        for (var distance = 8f; distance <= maxDistance; distance += 8f)
        {
            var point = start + direction * distance;
            if (MovementUtils.CircleHitsObstacle(point, 2f, _bunkerObstacles))
                return start + direction * MathF.Max(0f, distance - 8f);
        }

        return start + direction * maxDistance;
    }

    private Camera2D GetRenderCamera()
    {
        var scale = GetUiScale();
        var offset = GetUiOffset();
        return new Camera2D
        {
            Target = _camera.Target,
            Offset = _camera.Offset * scale + offset,
            Rotation = _camera.Rotation,
            Zoom = _camera.Zoom * scale
        };
    }

    private void DrawPlayerShieldAura()
    {
        if (_player.ShieldCapacity <= 0f || _player.Shield <= 0f) return;

        var ratio = Math.Clamp(_player.Shield / _player.ShieldCapacity, 0f, 1f);
        var auraColor = WithAlpha(Palette.C(110, 190, 255), 0.08f + ratio * 0.08f);
        var lineColor = WithAlpha(Palette.C(155, 220, 255), 0.20f + ratio * 0.15f);
        Raylib.DrawCircleV(_player.Position, 22f, auraColor);
        Raylib.DrawCircleLinesV(_player.Position, 22f, lineColor);
    }

    private void DrawPlayerSniperAimLine()
    {
        if (!_player.IsSniperEquipped || _player.InventoryOpen) return;

        var mouseWorld = Raylib.GetScreenToWorld2D(GetUiMousePosition(), _camera);
        var toCursor = mouseWorld - _player.Position;
        if (toCursor.LengthSquared() <= 0.001f) return;

        var dir = Vector2.Normalize(toCursor);
        var lineColor = _player.SniperChargeReady ? Palette.C(176, 92, 255) : Palette.C(255, 48, 48);
        Raylib.DrawLineEx(_player.Position, mouseWorld, 1.5f, lineColor);

        if (!_player.SniperChargeVisible) return;

        var whiskerLength = 114f;
        var spreadAngle = 25f * (1f - _player.SniperChargeProgress) * MathF.PI / 180f;
        var whiskerColor = WithAlpha(lineColor, 0.42f);
        var leftDir = VisibilityUtils.Rotate(dir, -spreadAngle);
        var rightDir = VisibilityUtils.Rotate(dir, spreadAngle);
        Raylib.DrawLineEx(_player.Position, _player.Position + leftDir * whiskerLength, 1.0f, whiskerColor);
        Raylib.DrawLineEx(_player.Position, _player.Position + rightDir * whiskerLength, 1.0f, whiskerColor);
    }

    private static void DrawSwing(SwingArc swing)
    {
        if (swing.VisualStyle == SwingVisualStyle.SpearThrust)
        {
            DrawSpearSwing(swing);
            return;
        }

        DrawSlashSwing(swing);
    }

    private static void DrawSlashSwing(SwingArc swing)
    {
        const int trailCount = 8;
        const float trailStep = 0.06f;
        var progress = swing.ReverseSweep ? 1f - swing.Progress : swing.Progress;
        var lifeAlpha = Math.Clamp(swing.Life / MathF.Max(swing.MaxLife, 0.001f), 0f, 1f);
        var baseAlpha = 0.55f + lifeAlpha * 0.45f;

        for (var i = trailCount - 1; i >= 0; i--)
        {
            var trailProgress = swing.ReverseSweep
                ? Math.Clamp(progress + i * trailStep, 0f, 1f)
                : Math.Clamp(progress - i * trailStep, 0f, 1f);
            var angle = swing.AngleStart + (swing.AngleEnd - swing.AngleStart) * trailProgress;
            var point = swing.Origin + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * swing.Radius;
            var alpha = i == 0
                ? 1f
                : Math.Clamp(baseAlpha * (0.55f + (trailCount - 1 - i) * 0.07f), 0f, 1f);
            var color = WithAlpha(swing.Color, alpha);

            if (i == 0)
            {
                Raylib.DrawLineEx(swing.Origin, point, 5f, color);
            }
            else
            {
                Raylib.DrawLineEx(swing.Origin, point, MathF.Max(1.5f, 5f - i * 0.35f), color);
            }
        }
    }

    private static void DrawSpearSwing(SwingArc swing)
    {
        const int trailCount = 8;
        const float trailStep = 0.07f;
        var delta = swing.LineEnd - swing.LineStart;
        var length = delta.Length();
        if (length <= 0.001f) return;

        var dir = Vector2.Normalize(delta);
        var angle = MathF.Atan2(dir.Y, dir.X) * 180f / MathF.PI;
        var dashLength = length * Math.Clamp(swing.DashLengthRatio, 0.2f, 0.8f);
        var travelLength = MathF.Max(0f, length - dashLength);
        var lifeAlpha = Math.Clamp(swing.Life / MathF.Max(swing.MaxLife, 0.001f), 0f, 1f);
        var baseAlpha = 0.55f + lifeAlpha * 0.45f;

        VisibilityUtils.DrawDashedLine(swing.LineStart, swing.LineEnd, 16, WithAlpha(swing.Color, Math.Clamp(baseAlpha * 0.7f, 0f, 1f)));

        for (var i = trailCount - 1; i >= 0; i--)
        {
            var trailProgress = Math.Clamp(swing.Progress - i * trailStep, 0f, 1f);
            var center = swing.LineStart + dir * (dashLength * 0.5f + travelLength * trailProgress);
            var height = MathF.Max(2.5f, 12f - i * 1.2f);
            var alpha = i == 0
                ? 1f
                : Math.Clamp(baseAlpha * (0.55f + (trailCount - 1 - i) * 0.07f), 0f, 1f);
            var color = WithAlpha(swing.Color, alpha);
            Raylib.DrawRectanglePro(
                new Rectangle(center.X, center.Y, dashLength, height),
                new Vector2(dashLength * 0.5f, height * 0.5f),
                angle,
                color);

            var tipLength = dashLength * 0.22f;
            var tipHeight = MathF.Max(1.8f, height * 0.58f);
            var tipCenter = center + dir * (dashLength * 0.5f + tipLength * 0.22f);
            Raylib.DrawRectanglePro(
                new Rectangle(tipCenter.X, tipCenter.Y, tipLength, tipHeight),
                new Vector2(tipLength * 0.5f, tipHeight * 0.5f),
                angle,
                color);
        }
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        var clamped = Math.Clamp(alpha, 0f, 1f);
        return new Color(color.R, color.G, color.B, (byte)(255 * clamped));
    }

    private static Color Mix(Color a, Color b, float amount)
    {
        var t = Math.Clamp(amount, 0f, 1f);
        return new Color(
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t),
            (int)(a.A + (b.A - a.A) * t));
    }

    private static Color Opaque(Color color) => new((int)color.R, color.G, color.B, 255);

    private void DrawSecuredTerminalWorldObjects()
    {
        if (_securedTerminalZone is not null)
        {
            Raylib.DrawRectangleRec(_securedTerminalZone.Rect, Palette.C(90, 90, 96, 58));
            Raylib.DrawRectangleLinesEx(_securedTerminalZone.Rect, 1.5f, Palette.C(135, 135, 145, 110));

            var hatch = new Rectangle(_securedTerminalZone.HatchPosition.X - 32f, _securedTerminalZone.HatchPosition.Y - 32f, 64f, 64f);
            Raylib.DrawRectangleRec(hatch, Palette.C(38, 38, 42, 245));
            Raylib.DrawRectangleLinesEx(hatch, 2f, Palette.C(92, 92, 98));
            if (_securedTerminalZone.Unlocked && Vector2.Distance(_securedTerminalZone.HatchPosition, _player.Position) < 34f)
            {
                Raylib.DrawText("F", (int)hatch.X + 27, (int)hatch.Y - 20, 18, Palette.C(120, 255, 150));
            }

            var terminal = new Rectangle(_securedTerminalZone.TerminalPosition.X - 14f, _securedTerminalZone.TerminalPosition.Y - 10f, 28f, 20f);
            Raylib.DrawRectangleRec(terminal, Palette.C(92, 92, 98, 245));
            Raylib.DrawRectangleLinesEx(terminal, 1.5f, Palette.C(150, 150, 156));
            var indicator = new Rectangle(terminal.X + 6f, terminal.Y + 5f, terminal.Width - 12f, terminal.Height - 10f);
            Raylib.DrawRectangleRec(indicator, _securedTerminalZone.Unlocked ? Palette.C(70, 220, 110) : Palette.C(210, 44, 48));

            if (Vector2.Distance(_securedTerminalZone.TerminalPosition, _player.Position) < 34f)
            {
                Raylib.DrawText("F", (int)terminal.X + 9, (int)terminal.Y - 18, 18, _securedTerminalZone.Unlocked ? Palette.C(120, 255, 150) : Palette.C(255, 120, 125));
            }
        }

        if (_secondaryBunkerHatchPosition != Vector2.Zero)
        {
            var hatch = new Rectangle(_secondaryBunkerHatchPosition.X - 32f, _secondaryBunkerHatchPosition.Y - 32f, 64f, 64f);
            Raylib.DrawRectangleRec(hatch, Palette.C(38, 38, 42, 245));
            Raylib.DrawRectangleLinesEx(
                hatch,
                2f,
                _secondaryBunkerHatchUnlocked ? Palette.C(90, 220, 120) : Palette.C(92, 92, 98));
            if (_secondaryBunkerHatchUnlocked && Vector2.Distance(_secondaryBunkerHatchPosition, _player.Position) < 34f)
            {
                Raylib.DrawText("F", (int)hatch.X + 27, (int)hatch.Y - 20, 18, Palette.C(120, 255, 150));
            }
        }

        foreach (var note in _terminalNotes)
        {
            var read = note.Index >= 0 && note.Index < _terminalNotesRead.Length && _terminalNotesRead[note.Index];
            var rect = new Rectangle(note.Position.X - 9f, note.Position.Y - 7f, 18f, 14f);
            Raylib.DrawRectangleRec(rect, read ? Palette.C(72, 68, 58, 210) : Palette.C(104, 88, 62, 235));
            Raylib.DrawRectangleLinesEx(rect, 1.5f, read ? Palette.C(130, 120, 90) : Palette.C(230, 190, 80));
            Raylib.DrawLine((int)rect.X + 3, (int)rect.Y + 5, (int)(rect.X + rect.Width - 3), (int)rect.Y + 5, Palette.C(235, 190, 70));

            if (Vector2.Distance(note.Position, _player.Position) < 28f)
            {
                Raylib.DrawText("F", (int)rect.X + 5, (int)rect.Y - 18, 18, Palette.C(235, 205, 110));
            }
        }
    }

    private void DrawRunIntroOverlay(bool forceOpaque = false)
    {
        var alpha = forceOpaque ? 1f : GetRunIntroAlpha();
        if (alpha <= 0f) return;

        var width = GetUiScreenWidth();
        var height = GetUiScreenHeight();
        var fill = WithAlpha(Mix(Opaque(Theme.Background), Color.Black, 0.42f), alpha);
        Raylib.DrawRectangle(0, 0, width, height, fill);

        const string text = "Loading...";
        const int fontSize = 42;
        var textColor = WithAlpha(Color.White, alpha);
        Raylib.DrawText(text, width / 2 - Raylib.MeasureText(text, fontSize) / 2, height / 2 - fontSize / 2, fontSize, textColor);
    }

    private void DrawHud()
    {
        if (!_challengeMode)
        {
            DrawExperienceBar();
            Raylib.DrawText($"Level {_player.Level} ({_player.Kills}/{_player.KillsTarget})", 20, 14, 24, Color.White);
        }
        else
        {
            Raylib.DrawText($"Pit level {_player.Level}", 20, 14, 24, Color.White);
            var timer = float.IsPositiveInfinity(_pitWaveTimer) ? "∞" : $"{MathF.Ceiling(MathF.Max(0f, _pitWaveTimer)):0}";
            Raylib.DrawText(timer, GetUiScreenWidth() / 2 - Raylib.MeasureText(timer, 56) / 2, 12, 56, Palette.C(130, 230, 255));
            var waveText = $"Wave {Math.Max(1, _pitNextWave - 1)}";
            Raylib.DrawText(waveText, GetUiScreenWidth() / 2 - Raylib.MeasureText(waveText, 22) / 2, 72, 22, Color.White);
            if (_challengeKind == ChallengeKind.PitNightmare) DrawPitNightmareModifiers();
        }

        var activeWeapon = _player.ActiveWeapon;
        Raylib.DrawText($"Current: {activeWeapon?.Name ?? "None"} {BuildWeaponDamageText(_player, activeWeapon, _player.ActiveWeaponClass)}", 20, 48, 22, activeWeapon?.Color ?? Color.LightGray);
        Raylib.DrawText($"Consumables: Q [{(_player.Inventory.QuickSlotQ?.Name ?? "-")}]  R [{(_player.Inventory.QuickSlotR?.Name ?? "-")}]", 20, 78, 20, Color.White);
        if (!_challengeMode) Raylib.DrawText($"Run score {_runScore}", 20, 108, 20, Color.Gold);
        if (!_inBunker) DrawExtractionHud();
        DrawVitalBars();
        DrawLevelUpIndicator();
        DrawStatusEffects();
        if (_pitRewardOpen) DrawPitRewardSelection();
        if (_pitDifficultyOpen) DrawPitDifficultySelection();
        Raylib.DrawText("WASD move | LMB attack | 1 melee | 2 primary | 3 heavy | TAB inventory | ESC menu", 20, GetUiScreenHeight() - 28, 18, Color.Gray);
        if (!_inBunker) DrawZoneArrows();
    }

    private void DrawCombatCursor()
    {
        if (_player.InventoryOpen || _mapOpen || _pitRewardOpen || _pitDifficultyOpen || _terminalOpen || _openTerminalNoteIndex is not null) return;

        var mouse = GetUiMousePosition();
        var color = Color.White;

        if (_player.ActiveWeaponClass == WeaponClass.Ranged)
        {
            const float gap = 5f;
            const float length = 14f;
            Raylib.DrawLineEx(new Vector2(mouse.X - length, mouse.Y), new Vector2(mouse.X - gap, mouse.Y), 2f, color);
            Raylib.DrawLineEx(new Vector2(mouse.X + gap, mouse.Y), new Vector2(mouse.X + length, mouse.Y), 2f, color);
            Raylib.DrawLineEx(new Vector2(mouse.X, mouse.Y - length), new Vector2(mouse.X, mouse.Y - gap), 2f, color);
            Raylib.DrawLineEx(new Vector2(mouse.X, mouse.Y + gap), new Vector2(mouse.X, mouse.Y + length), 2f, color);
            Raylib.DrawCircleLines((int)mouse.X, (int)mouse.Y, 3f, color);
            if (_player.IsLegendaryRocketPulseRifleEquipped)
            {
                var barColor = Palette.C(150, 150, 150);
                Raylib.DrawLineEx(new Vector2(mouse.X - 22f, mouse.Y - length), new Vector2(mouse.X - 22f, mouse.Y + length), 2f, barColor);
                Raylib.DrawLineEx(new Vector2(mouse.X + 22f, mouse.Y - length), new Vector2(mouse.X + 22f, mouse.Y + length), 2f, barColor);
            }

            if (_player.IsLinearRifleEquipped)
            {
                DrawCircularProgressFrame(mouse, 22f, _player.LinearRifleChargeProgress, Palette.C(130, 230, 255));
            }
            else if (_player.IsTerrorEquipped)
            {
                var progress = _player.TerrorSpinProgress;
                var spinColor = Mix(Palette.C(90, 175, 255), Palette.C(245, 55, 65), progress);
                DrawCircularProgressFrame(mouse, 22f, progress, spinColor);
            }

            return;
        }

        var top = mouse;
        var left = new Vector2(mouse.X - 12f, mouse.Y + 23f);
        var right = new Vector2(mouse.X + 12f, mouse.Y + 23f);
        Raylib.DrawLineEx(top, left, 2.5f, color);
        Raylib.DrawLineEx(top, right, 2.5f, color);
    }

    private void DrawMapWindow()
    {
        var screenWidth = GetUiScreenWidth();
        var screenHeight = GetUiScreenHeight();
        Raylib.DrawRectangle(0, 0, screenWidth, screenHeight, Palette.C(0, 0, 0, 150));

        var mapRect = GetMapRect();
        var panel = new Rectangle(mapRect.X - 22f, mapRect.Y - 58f, mapRect.Width + 44f, mapRect.Height + 86f);
        Raylib.DrawRectangleRec(panel, Palette.C(6, 10, 20, 235));
        Raylib.DrawRectangleLinesEx(panel, 2f, Palette.C(100, 190, 255));
        Raylib.DrawText("Map", (int)panel.X + 18, (int)panel.Y + 16, 28, Color.White);
        Raylib.DrawText("LMB: place/move marker | RMB near marker: remove | M/Esc: close", (int)panel.X + 92, (int)panel.Y + 23, 18, Color.LightGray);

        Raylib.DrawRectangleRec(mapRect, Palette.C(12, 18, 28, 255));
        Raylib.DrawRectangleLinesEx(mapRect, 2f, Color.SkyBlue);

        if (_inBunker)
        {
            DrawBunkerMapContents(mapRect);
            return;
        }

        foreach (var building in _buildings)
        {
            DrawMapRect(building.Rect, mapRect, Theme.BuildingFill, Theme.BuildingLine);
        }

        foreach (var outpost in _outposts)
        {
            DrawMapRect(outpost.Rect, mapRect, Theme.OutpostFill, Theme.OutpostLine);
        }

        foreach (var generatorZone in _generatorZones)
        {
            DrawMapRect(generatorZone.Rect, mapRect, Palette.C(50, 90, 120, 90), Palette.C(120, 220, 255));
        }

        foreach (var hangar in _hangars)
        {
            DrawMapRect(hangar.Rect, mapRect, Palette.C(40, 80, 52, 90), Palette.C(80, 220, 110));
        }

        if (_stationZone is not null)
        {
            DrawMapRect(_stationZone.Rect, mapRect, Palette.C(80, 80, 90, 80), Palette.C(240, 60, 70));
        }

        if (_securedTerminalZone is not null)
        {
            DrawMapRect(_securedTerminalZone.Rect, mapRect, Palette.C(90, 90, 96, 80), Palette.C(190, 190, 200));
            DrawMapCircle(_securedTerminalZone.TerminalPosition, mapRect, 5f, _securedTerminalZone.Unlocked ? Palette.C(90, 230, 120) : Palette.C(230, 80, 85));
        }

        if (_secondaryBunkerHatchPosition != Vector2.Zero)
        {
            DrawMapCircle(
                _secondaryBunkerHatchPosition,
                mapRect,
                5f,
                _secondaryBunkerHatchUnlocked ? Palette.C(90, 230, 120) : Palette.C(105, 105, 112));
        }

        foreach (var obstacle in _obstacles)
        {
            DrawMapRect(obstacle.Rect, mapRect, Theme.ObstacleFill, Theme.ObstacleLine);
        }

        foreach (var portal in _extractPortals)
        {
            DrawMapCircle(portal.Position, mapRect, 6f, Palette.C(90, 230, 255));
        }

        foreach (var generator in _generators)
        {
            DrawMapCircle(generator.Position, mapRect, 5f, generator.Destroyed ? Color.Gray : Palette.C(130, 225, 255));
        }

        if (_mapMarker is Vector2 marker)
        {
            var markerPos = WorldToMap(marker, mapRect);
            Raylib.DrawCircleV(markerPos, 7f, Palette.C(255, 220, 80));
            Raylib.DrawCircleLines((int)markerPos.X, (int)markerPos.Y, 11f, Color.White);
            Raylib.DrawText("M", (int)markerPos.X - 5, (int)markerPos.Y - 24, 18, Palette.C(255, 230, 120));
        }

        var playerPos = WorldToMap(_player.Position, mapRect);
        Raylib.DrawCircleV(playerPos, 6f, Theme.Player);
        Raylib.DrawCircleLines((int)playerPos.X, (int)playerPos.Y, 9f, Color.White);
        Raylib.DrawText("P", (int)playerPos.X - 5, (int)playerPos.Y - 24, 18, Color.White);
    }

    private void DrawBunkerMapContents(Rectangle mapRect)
    {
        Raylib.DrawRectangleRec(mapRect, Color.Black);
        foreach (var room in _bunkerRooms)
        {
            if (!_revealedBunkerRooms.Contains(room.Id)) continue;
            DrawMapRect(room.Rect, mapRect, Palette.C(48, 50, 56, 255), Palette.C(150, 154, 166));
        }

        DrawMapCircle(BunkerEntranceHatchPosition, mapRect, 6f, Palette.C(220, 225, 235));

        foreach (var door in _bunkerDoors)
        {
            if (door.Open || (!_revealedBunkerRooms.Contains(door.RoomA) && !_revealedBunkerRooms.Contains(door.RoomB))) continue;
            DrawMapRect(door.Rect, mapRect, Palette.C(126, 82, 62), Palette.C(220, 160, 110));
        }

        if (_revealedBunkerRooms.Contains(21))
        {
            DrawMapCircle(BunkerExitHatchPosition, mapRect, 6f, Palette.C(220, 225, 235));
        }

        if (_bunkerMapMarker is Vector2 marker)
        {
            var markerPos = WorldToMap(marker, mapRect);
            Raylib.DrawCircleV(markerPos, 7f, Palette.C(255, 220, 80));
            Raylib.DrawCircleLines((int)markerPos.X, (int)markerPos.Y, 11f, Color.White);
            Raylib.DrawText("M", (int)markerPos.X - 5, (int)markerPos.Y - 24, 18, Palette.C(255, 230, 120));
        }

        var playerPos = WorldToMap(_player.Position, mapRect);
        Raylib.DrawCircleV(playerPos, 6f, Theme.Player);
        Raylib.DrawCircleLines((int)playerPos.X, (int)playerPos.Y, 9f, Color.White);
        Raylib.DrawText("P", (int)playerPos.X - 5, (int)playerPos.Y - 24, 18, Color.White);
    }

    private void DrawMapRect(Rectangle worldRect, Rectangle mapRect, Color fill, Color line)
    {
        var topLeft = WorldToMap(new Vector2(worldRect.X, worldRect.Y), mapRect);
        var bottomRight = WorldToMap(new Vector2(worldRect.X + worldRect.Width, worldRect.Y + worldRect.Height), mapRect);
        var rect = new Rectangle(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
        Raylib.DrawRectangleRec(rect, fill);
        Raylib.DrawRectangleLinesEx(rect, 1f, line);
    }

    private void DrawMapCircle(Vector2 worldPoint, Rectangle mapRect, float radius, Color color)
    {
        Raylib.DrawCircleV(WorldToMap(worldPoint, mapRect), radius, color);
    }

    private void DrawExperienceBar()
    {
        var width = GetUiScreenWidth();
        var ratio = Math.Clamp(_player.Kills / (float)Math.Max(1, _player.KillsTarget), 0f, 1f);
        Raylib.DrawRectangle(0, 0, width, 10, Palette.C(12, 20, 34, 230));
        Raylib.DrawRectangle(0, 0, (int)(width * ratio), 10, Palette.C(70, 190, 255));
        Raylib.DrawRectangleLinesEx(new Rectangle(0, 0, width, 10), 1f, Color.Black);
    }

    private void DrawLevelUpIndicator()
    {
        if (_player.StatPoints <= 0) return;

        var x = GetUiScreenWidth() - 156;
        var y = 24;
        Raylib.DrawTriangle(
            new Vector2(x + 12, y),
            new Vector2(x, y + 20),
            new Vector2(x + 24, y + 20),
            Palette.C(80, 230, 110));
        Raylib.DrawText("Level Up", x + 34, y + 2, 22, Palette.C(120, 255, 140));
    }

    private void DrawStatusEffects()
    {
        var x = GetUiScreenWidth() - 42f;
        var y = _player.StatPoints > 0 ? 74f : 28f;

        if (_player.Poisoned)
        {
            DrawStatusEffectIcon(new Vector2(x, y), Palette.C(120, 20, 24), "P", _player.PoisonEffectProgress);
            y += 46f;
        }

        if (_player.RadioactiveDecompositionActive)
        {
            DrawStatusEffectIcon(new Vector2(x, y), Palette.C(145, 38, 82), "R", _player.RadioactiveDecompositionProgress);
            y += 46f;
        }

        if (_player.MovementSlowed)
        {
            DrawStatusEffectIcon(new Vector2(x, y), Palette.C(115, 120, 130), "M", _player.MovementSlowProgress);
            y += 46f;
        }

        if (_player.StickyBulletsActive)
        {
            DrawStatusEffectIcon(new Vector2(x, y), Palette.C(120, 120, 120), "B", _player.StickyBulletsEffectProgress);
            y += 46f;
        }

        if (_player.TeslaBulletsActive)
        {
            DrawStatusEffectIcon(new Vector2(x, y), Palette.C(120, 230, 255), "T", _player.TeslaBulletsEffectProgress);
            y += 46f;
        }

        if (_player.StimActive)
        {
            DrawStatusEffectIcon(new Vector2(x, y), Palette.C(80, 210, 100), "S", _player.StimEffectProgress);
        }
    }

    private static void DrawStatusEffectIcon(Vector2 center, Color color, string label, float progress)
    {
        Raylib.DrawCircleV(center, 18f, Palette.C(8, 10, 14, 210));
        Raylib.DrawCircleV(center, 12f, color);

        var fontSize = 18;
        var textWidth = Raylib.MeasureText(label, fontSize);
        Raylib.DrawText(label, (int)(center.X - textWidth * 0.5f), (int)(center.Y - fontSize * 0.5f), fontSize, Color.White);

        DrawCircularProgressFrame(center, 19f, progress, color);
    }

    private void DrawFrozenTargetCrystals()
    {
        foreach (var pair in _frozenTargets)
        {
            if (pair.Value <= 0f) continue;
            var center = GetTargetPosition(pair.Key);
            if (center is null) continue;

            var seed = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(pair.Key);
            for (var i = 0; i < 5; i++)
            {
                var angle = ((seed + i * 73) % 360) * MathF.PI / 180f;
                var distance = 5f + Math.Abs((seed >> (i % 8)) % 18);
                var position = center.Value + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
                var color = Palette.C(130, 230, 255, 76);
                if (i % 2 == 0)
                {
                    DrawDiamond(position, 5f, color);
                }
                else
                {
                    Raylib.DrawTriangle(
                        position + new Vector2(0f, -6f),
                        position + new Vector2(-5f, 5f),
                        position + new Vector2(6f, 4f),
                        color);
                }
            }
        }
    }

    private void DrawEnemyDebuffIcons()
    {
        foreach (var target in EnumerateEnemyTargets())
        {
            var icons = new List<(Color Fill, Color Border)>(4);
            if (_slowVisualTargets.ContainsKey(target.Target) || _chilledTargets.ContainsKey(target.Target))
                icons.Add((Palette.C(125, 125, 130), Palette.C(175, 175, 180)));
            if (_poisonVisualTargets.ContainsKey(target.Target))
                icons.Add((Palette.C(70, 185, 75), Palette.C(115, 235, 120)));
            if (_radioactiveDecompositionTargets.ContainsKey(target.Target))
                icons.Add((Palette.C(110, 110, 115), Palette.C(90, 235, 90)));
            if (_frozenTargets.ContainsKey(target.Target))
                icons.Add((Palette.C(105, 210, 245), Palette.C(175, 245, 255)));
            if (icons.Count == 0) continue;

            const float spacing = 13f;
            var y = target.Position.Y - target.Radius - 14f;
            var startX = target.Position.X - (icons.Count - 1) * spacing * 0.5f;
            for (var i = 0; i < icons.Count; i++)
            {
                var center = new Vector2(startX + i * spacing, y);
                Raylib.DrawCircleV(center, 5f, icons[i].Fill);
                Raylib.DrawCircleLinesV(center, 5f, icons[i].Border);
            }
        }
    }

    private static void DrawCircularProgressFrame(Vector2 center, float radius, float progress, Color color)
    {
        progress = Math.Clamp(progress, 0f, 1f);
        Raylib.DrawCircleLinesV(center, radius, Palette.C(0, 0, 0, 220));
        if (progress <= 0f) return;

        var segments = Math.Max(3, (int)MathF.Ceiling(48f * progress));
        var start = -MathF.PI * 0.5f;
        for (var i = 0; i < segments; i++)
        {
            var a1 = start + MathF.Tau * progress * i / segments;
            var a2 = start + MathF.Tau * progress * (i + 1) / segments;
            var p1 = center + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * radius;
            var p2 = center + new Vector2(MathF.Cos(a2), MathF.Sin(a2)) * radius;
            Raylib.DrawLineEx(p1, p2, 3f, color);
        }
    }

    private void DrawVitalBars()
    {
        var screenWidth = GetUiScreenWidth();
        var screenHeight = GetUiScreenHeight();
        var barWidth = 450f;
        var hpRect = new Rectangle(screenWidth - barWidth - 24f, screenHeight - 64f, barWidth, 22f);
        var dashRect = new Rectangle(screenWidth - barWidth - 24f, screenHeight - 36f, barWidth, 15f);
        var shieldRect = new Rectangle(screenWidth - barWidth - 24f, screenHeight - 92f, barWidth, 22f);

        if (_player.ShieldCapacity > 0f)
        {
            DrawStatusBar(
                shieldRect,
                Math.Clamp(_player.Shield / MathF.Max(_player.ShieldCapacity, 0.001f), 0f, 1f),
                Palette.C(92, 176, 255),
                Color.Black,
                $"SHIELD {_player.Shield:0}/{_player.ShieldCapacity:0}",
                18);
        }

        DrawStatusBar(hpRect, Math.Clamp(_player.Health / MathF.Max(_player.MaxHealth, 0.001f), 0f, 1f), Palette.C(196, 48, 48), Color.Black, $"HP {_player.Health:0}/{_player.MaxHealth:0}", 18);
        DrawStatusBar(dashRect, _player.DashCooldownProgress, Palette.C(72, 210, 96), Color.Black, string.Empty, 14);

        var heavyWeapon = _player.ActiveWeapon?.IsHeavyWeapon == true ? _player.ActiveWeapon : _player.HeavyWeapon;
        var ammoText = $"Heavy Ammo: {_player.Inventory.GetHeavyAmmoShotCount(heavyWeapon)}";
        var ammoFont = 20;
        var ammoX = hpRect.X + hpRect.Width - Raylib.MeasureText(ammoText, ammoFont);
        if (_player.IsLegendaryRocketPulseRifleEquipped)
        {
            ammoX -= 48f;
            DrawRocketPulseModeText(new Vector2(ammoX, hpRect.Y - 78), ammoFont);
            ammoX += 48f;
        }

        var knownCode = GetKnownTerminalCodeDisplay();
        if (!string.IsNullOrEmpty(knownCode))
        {
            var codeText = $"Access code: {knownCode}";
            var codeFont = 20;
            var codeX = hpRect.X + hpRect.Width - Raylib.MeasureText(codeText, codeFont);
            Raylib.DrawText(codeText, (int)codeX, (int)hpRect.Y - 106, codeFont, Palette.C(235, 205, 110));
        }

        Raylib.DrawText(ammoText, (int)ammoX, (int)hpRect.Y - 78, ammoFont, Palette.C(120, 210, 255));
    }

    private void DrawRocketPulseModeText(Vector2 position, int fontSize)
    {
        var normalColor = _player.RocketPulseBurstMode ? Color.Gray : Color.White;
        var burstColor = _player.RocketPulseBurstMode ? Color.White : Color.Gray;
        Raylib.DrawText("1", (int)position.X, (int)position.Y, fontSize, normalColor);
        Raylib.DrawText("/", (int)position.X + Raylib.MeasureText("1", fontSize) + 2, (int)position.Y, fontSize, Color.Gray);
        Raylib.DrawText("2", (int)position.X + Raylib.MeasureText("1/", fontSize) + 4, (int)position.Y, fontSize, burstColor);
    }

    private static void DrawStatusBar(Rectangle rect, float ratio, Color fillColor, Color lineColor, string label, int fontSize)
    {
        ratio = Math.Clamp(ratio, 0f, 1f);
        Raylib.DrawRectangleRec(rect, Palette.C(18, 18, 18, 220));

        var fillRect = new Rectangle(rect.X + 2f, rect.Y + 2f, MathF.Max(0f, (rect.Width - 4f) * ratio), rect.Height - 4f);
        Raylib.DrawRectangleRec(fillRect, fillColor);
        Raylib.DrawRectangleLinesEx(rect, 2f, lineColor);

        if (!string.IsNullOrEmpty(label))
        {
            var textWidth = Raylib.MeasureText(label, fontSize);
            var textX = (int)(rect.X + rect.Width * 0.5f - textWidth * 0.5f);
            var textY = (int)(rect.Y + rect.Height * 0.5f - fontSize * 0.5f);
            Raylib.DrawText(label, textX, textY, fontSize, Color.White);
        }
    }

    private void DrawInventory()
    {
        if (!_player.InventoryOpen) return;

        var slots = BuildSlots();
        if (_openedChestIndex is null)
        {
            Raylib.DrawRectangle(32, 92, 1216, 650, Palette.C(6, 10, 20, 220));
            Raylib.DrawRectangleLines(32, 92, 1216, 650, Color.SkyBlue);
            Raylib.DrawText("Inventory", 46, 116, 24, Color.White);

            DrawBackpackGrid(new Vector2(690, 118), 6, 5);
            Raylib.DrawText("Backpack", 690, 98, 20, Color.LightGray);
            Raylib.DrawText("Equipment", 570, 98, 20, Color.LightGray);
            Raylib.DrawText("Stats", 54, 146, 20, Color.LightGray);

            DrawInventoryStatRow("STR", _player.Str, _pendingStrengthPoints, 54, 176);
            DrawInventoryStatRow("DEX", _player.Dex, _pendingDexterityPoints, 54, 206);
            DrawInventoryStatRow("SPD", _player.Spd, _pendingSpeedPoints, 54, 236);
            DrawInventoryStatRow("GUN", _player.Guns, _pendingGunsmithPoints, 54, 266);
            Raylib.DrawText($"Free points: {_player.StatPoints - GetPendingLevelUpPointCount()}", 54, 296, 18, Color.Yellow);
            Raylib.DrawText($"Total points: {_player.StatPoints}", 54, 320, 18, Color.LightGray);

            if (_player.StatPoints > 0)
            {
                DrawPlus(new Rectangle(252, 174, 22, 22));
                DrawPlus(new Rectangle(252, 204, 22, 22));
                DrawPlus(new Rectangle(252, 234, 22, 22));
                DrawPlus(new Rectangle(252, 264, 22, 22));
                if (GetPendingLevelUpPointCount() > 0)
                {
                    DrawButton(new Rectangle(54, 350, 120, 30), "Confirm");
                    DrawButton(new Rectangle(184, 350, 120, 30), "Reset");
                }
            }

            DrawStatTooltip();
        }
        else
        {
            Raylib.DrawRectangle(40, 138, 610, 548, Palette.C(6, 10, 20, 220));
            Raylib.DrawRectangleLines(40, 138, 610, 548, Color.SkyBlue);
            Raylib.DrawText("Backpack", 56, 146, 20, Color.White);
            DrawBackpackGrid(new Vector2(56, 170), 6, 5);

            Raylib.DrawRectangle(720, 138, 500, 230, Palette.C(6, 10, 20, 220));
            Raylib.DrawRectangleLines(720, 138, 500, 230, Color.SkyBlue);
            Raylib.DrawText("Chest", 740, 150, 24, Color.White);
            DrawBackpackGrid(new Vector2(740, 190), 5, 1);
            DrawButton(TakeAllButtonRect, "Take all [X]");
        }

        var comparison = new ComparisonContext(_player, _player.Armor, _player.RangedWeapon, _player.HeavyWeapon, _player.MeleeWeapon);
        var mouse = GetUiMousePosition();
        foreach (var slot in slots)
        {
            Raylib.DrawRectangleRec(slot.Rect, Palette.C(22, 28, 42, 255));
            Raylib.DrawRectangleLinesEx(slot.Rect, 1f, Color.SkyBlue);
            if (slot.Kind == SlotKind.Trash) Raylib.DrawText("TR", (int)slot.Rect.X + 16, (int)slot.Rect.Y + 18, 20, Color.Orange);
            if (slot.Kind == SlotKind.QuickSlotQ) Raylib.DrawText("Q", (int)slot.Rect.X + 20, (int)slot.Rect.Y - 18, 16, Color.Green);
            if (slot.Kind == SlotKind.QuickSlotR) Raylib.DrawText("R", (int)slot.Rect.X + 20, (int)slot.Rect.Y - 18, 16, Color.Yellow);
            if (slot.Item is not null)
            {
                var iconRect = new Rectangle(slot.Rect.X + UiIconPadding, slot.Rect.Y + UiIconPadding, slot.Rect.Width - UiIconPadding * 2f, slot.Rect.Height - UiIconPadding * 2f);
                DrawItemIcon(slot.Item, iconRect, comparison, slot.Kind);
                DrawInventoryUseHoldFrame(slot, iconRect);
                if (slot.Kind == SlotKind.Storage && _storageSortMode > 0 && !IsStorageSortModeMatch(slot.Item, _storageSortMode))
                {
                    Raylib.DrawRectangleRec(slot.Rect, Palette.C(0, 0, 0, 165));
                }
            }
            if (slot.Item is not null && Raylib.CheckCollisionPointRec(mouse, slot.Rect)) DrawHoverOrbitFrame(slot.Rect, slot.Item.Color);
        }
        DrawArmorModifierDiamondsForSlots(slots);

        if (_drag is not null)
        {
            var m = GetUiMousePosition();
            var dragRect = new Rectangle(m.X + 8, m.Y + 8, UiSlotSize, UiSlotSize);
            DrawItemIcon(_drag.Item, dragRect, comparison, _drag.Kind);
            DrawArmorModifierDiamonds(_drag.Item, dragRect);
        }

        if (_hovered is not null) DrawTooltip(_hovered, GetUiMousePosition(), comparison);
    }

    private static void DrawBackpackGrid(Vector2 origin, int cols, int rows)
    {
        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                var rect = new Rectangle(origin.X + c * UiSlotStep, origin.Y + r * UiSlotStep, UiSlotSize, UiSlotSize);
                Raylib.DrawRectangleLinesEx(rect, 1f, Palette.C(70, 90, 130, 170));
            }
        }
    }

    private void DrawSynthCoinsCounter(int rightMargin, int y, int fontSize)
    {
        var text = $"SynthCoins: {_meta.SynthCoins}";
        var x = GetUiScreenWidth() - rightMargin - Raylib.MeasureText(text, fontSize);
        Raylib.DrawText(text, x, y, fontSize, Palette.C(120, 230, 255));
    }

    private void DrawCryptoTokensCounter(int rightMargin, int y, int fontSize)
    {
        var text = $"CryptoTokens: {_meta.CryptoTokens}";
        var x = GetUiScreenWidth() - rightMargin - Raylib.MeasureText(text, fontSize);
        Raylib.DrawText(text, x, y, fontSize, Palette.C(210, 150, 255));
    }

    private void DrawItemIcon(ItemStack item, Rectangle rect, ComparisonContext? comparison = null, SlotKind? sourceKind = null)
    {
        var background = item.Type == ItemType.Consumable ? Palette.C(130, 210, 120) : item.Color;
        Raylib.DrawRectangleRec(rect, background);
        var textureDrawn = TryDrawItemTexture(item, rect);

        if (item.Type == ItemType.Armor)
        {
            if (!textureDrawn)
            {
                DrawArmorIcon(rect);
                DrawItemTypeLabel(rect, "ar");
            }
        }
        else if (item.Type == ItemType.Weapon)
        {
            if (!textureDrawn)
            {
                if (item.WeaponKind == WeaponClass.Melee && item.Pattern is WeaponPattern.EnergySpear or WeaponPattern.Lancelot) DrawSpearIcon(rect);
                else if (item.WeaponKind == WeaponClass.Melee) DrawBladeIcon(rect);
                else if (item.Pattern == WeaponPattern.GrenadeLauncher) DrawGrenadeLauncherIcon(rect);
                else if (item.Pattern == WeaponPattern.RocketLauncher) DrawRocketLauncherIcon(rect);
                else if (item.Pattern == WeaponPattern.TraceRifle) DrawTraceRifleIcon(rect);
                else if (item.Pattern == WeaponPattern.LinearRifle) DrawLinearRifleIcon(rect);
                else if (item.Pattern == WeaponPattern.Pulsar) DrawPulsarIcon(rect);
                else if (item.Pattern == WeaponPattern.SniperRifle) DrawSniperIcon(rect);
                else if (item.Pattern is WeaponPattern.PulseRifle or WeaponPattern.Toxikus) DrawPulseRifleIcon(rect);
                else DrawPistolIcon(rect);

                DrawItemTypeLabel(rect, item.WeaponKind == WeaponClass.Ranged ? "rw" : "mw");
            }
        }
        else if (item.IsStationKey)
        {
            if (!textureDrawn) DrawStationKeyIcon(rect);
        }
        else if (item.IsDeviceDataFragment)
        {
            if (!textureDrawn) DrawDeviceDataFragmentIcon(rect);
        }
        else if (item.IsHeavyAmmo)
        {
            if (!textureDrawn) DrawHeavyAmmoIcon(rect);
            DrawAmmoPercentStrip(item, rect);
        }
        else
        {
            if (!textureDrawn)
            {
                if (item.ConsumableKind == ConsumableType.Medkit) DrawMedKitIcon(rect);
                else if (item.ConsumableKind == ConsumableType.Stim) DrawStimIcon(rect);
                else if (item.ConsumableKind == ConsumableType.ProtectiveDome) DrawProtectiveDomeIcon(rect);
                else DrawStickyBulletsIcon(rect);
            }
        }

        if (item.Rarity == ArmorRarity.Damaged)
        {
            Raylib.DrawLineEx(new Vector2(rect.X + 4, rect.Y + 4), new Vector2(rect.X + rect.Width - 4, rect.Y + rect.Height - 4), 2.2f, Color.Red);
            Raylib.DrawLineEx(new Vector2(rect.X + rect.Width - 4, rect.Y + 4), new Vector2(rect.X + 4, rect.Y + rect.Height - 4), 2.2f, Color.Red);
        }

        if (item.IsHeavyWeapon)
        {
            DrawHeavyWeaponMarker(rect);
        }

        if (item.Quantity > 1)
        {
            DrawStackQuantity(item, rect);
        }

        DrawComparisonMarker(item, rect, comparison, sourceKind);
    }

    private static void DrawStackQuantity(ItemStack item, Rectangle rect)
    {
        var text = item.Quantity.ToString();
        var fontSize = Math.Max(12, (int)(rect.Height * 0.22f));
        var width = Raylib.MeasureText(text, fontSize);
        var pad = 4;
        var bg = new Rectangle(rect.X + rect.Width - width - pad * 2 - 2, rect.Y + rect.Height - fontSize - pad - 2, width + pad * 2, fontSize + pad);
        Raylib.DrawRectangleRec(bg, Palette.C(0, 0, 0, 210));
        Raylib.DrawText(text, (int)(bg.X + pad), (int)(bg.Y + 1), fontSize, Color.White);
    }

    private static void DrawHeavyWeaponMarker(Rectangle rect)
    {
        var blue = Palette.C(70, 170, 255);
        Raylib.DrawRectangleLinesEx(rect, 4f, blue);
        Raylib.DrawRectangle((int)rect.X + 3, (int)rect.Y + 3, 18, 18, Palette.C(6, 12, 24, 210));
        Raylib.DrawText("H", (int)rect.X + 7, (int)rect.Y + 3, 18, blue);
    }

    private static void DrawAmmoPercentStrip(ItemStack item, Rectangle rect)
    {
        var stripHeight = MathF.Max(14f, rect.Height * 0.2f);
        var strip = new Rectangle(rect.X, rect.Y + rect.Height - stripHeight, rect.Width, stripHeight);
        Raylib.DrawRectangleRec(strip, Palette.C(0, 0, 0, 220));

        var text = $"{item.AmmoPercent:0.0}%";
        var fontSize = Math.Max(10, (int)(stripHeight - 3f));
        var textWidth = Raylib.MeasureText(text, fontSize);
        Raylib.DrawText(text, (int)(strip.X + strip.Width * 0.5f - textWidth * 0.5f), (int)(strip.Y + strip.Height * 0.5f - fontSize * 0.5f), fontSize, Color.White);
    }

    private void DrawInventoryUseHoldFrame(UiSlot slot, Rectangle rect)
    {
        if (_inventoryUseHoldIndex != slot.Index || _inventoryUseHoldKind != slot.Kind) return;

        var progress = Math.Clamp(_inventoryUseHoldTimer / InventoryConsumableUseHoldDuration, 0f, 1f);
        var perimeter = rect.Width * 2f + rect.Height * 2f;
        DrawProgressFrameSegment(rect, perimeter * progress, Color.White);
    }

    private static void DrawProgressFrameSegment(Rectangle rect, float length, Color color)
    {
        const float thickness = 3f;
        var remaining = length;
        DrawEdge(new Vector2(rect.X, rect.Y), new Vector2(rect.X + rect.Width, rect.Y), ref remaining, thickness, color);
        DrawEdge(new Vector2(rect.X + rect.Width, rect.Y), new Vector2(rect.X + rect.Width, rect.Y + rect.Height), ref remaining, thickness, color);
        DrawEdge(new Vector2(rect.X + rect.Width, rect.Y + rect.Height), new Vector2(rect.X, rect.Y + rect.Height), ref remaining, thickness, color);
        DrawEdge(new Vector2(rect.X, rect.Y + rect.Height), new Vector2(rect.X, rect.Y), ref remaining, thickness, color);
    }

    private static void DrawEdge(Vector2 from, Vector2 to, ref float remaining, float thickness, Color color)
    {
        if (remaining <= 0f) return;

        var edge = to - from;
        var edgeLength = edge.Length();
        var drawLength = MathF.Min(edgeLength, remaining);
        var end = from + Vector2.Normalize(edge) * drawLength;
        Raylib.DrawLineEx(from, end, thickness, color);
        remaining -= drawLength;
    }

    private static void DrawItemTypeLabel(Rectangle rect, string label)
    {
        Raylib.DrawText(label, (int)(rect.X + 4), (int)(rect.Y + 3), 10, Color.Black);
    }

    private bool TryDrawItemTexture(ItemStack item, Rectangle rect)
    {
        var relativePath = GetItemIconPath(item);
        if (relativePath is null) return false;
        if (!TryGetIconTexture(relativePath, out var texture)) return false;

        var padding = MathF.Max(2f, rect.Width * 0.05f);
        var availableWidth = MathF.Max(1f, rect.Width - padding * 2f);
        var availableHeight = MathF.Max(1f, rect.Height - padding * 2f);
        var scale = MathF.Min(availableWidth / texture.Width, availableHeight / texture.Height);
        var width = texture.Width * scale;
        var height = texture.Height * scale;
        var dest = new Rectangle(rect.X + (rect.Width - width) * 0.5f, rect.Y + (rect.Height - height) * 0.5f, width, height);
        var source = new Rectangle(0f, 0f, texture.Width, texture.Height);
        Raylib.DrawTexturePro(texture, source, dest, Vector2.Zero, 0f, Color.White);
        return true;
    }

    private bool TryGetIconTexture(string relativePath, out Texture2D texture)
    {
        if (_iconTextures.TryGetValue(relativePath, out texture)) return true;
        if (_missingIconTextures.Contains(relativePath)) return false;

        var fullPath = ResolveIconPath(relativePath);
        if (fullPath is null)
        {
            _missingIconTextures.Add(relativePath);
            return false;
        }

        var image = Raylib.LoadImage(fullPath);
        if (image.Width <= 0 || image.Height <= 0)
        {
            Raylib.UnloadImage(image);
            _missingIconTextures.Add(relativePath);
            return false;
        }

        texture = Raylib.LoadTextureFromImage(image);
        Raylib.UnloadImage(image);

        if (texture.Id == 0)
        {
            _missingIconTextures.Add(relativePath);
            return false;
        }

        Raylib.SetTextureFilter(texture, GetRaylibTextureFilter());
        _iconTextures[relativePath] = texture;
        return true;
    }

    private void PreloadGameplayTextures()
    {
        TryGetIconTexture(Path.Combine("Assets", "Icons", "Enemies", "base_enemy.png"), out _);
        TryGetIconTexture(Path.Combine("Assets", "Icons", "Enemies", "base_enemy_enhanced.png"), out _);
        TryGetIconTexture(Path.Combine("Assets", "Icons", "Enemies", "triangle.png"), out _);
        TryGetIconTexture(Path.Combine("Assets", "Icons", "Enemies", "triangle_enhanced.png"), out _);
        TryGetIconTexture(Path.Combine("Assets", "Icons", "Enemies", "tyrant.png"), out _);
        TryGetIconTexture(Path.Combine("Assets", "Icons", "Enemies", "siege.png"), out _);
        TryGetIconTexture(Path.Combine("Assets", "Icons", "Enemies", "assault.png"), out _);
    }

    private TextureFilter GetRaylibTextureFilter()
        => _textureFilteringMode == TextureFilteringMode.Bilinear ? TextureFilter.Bilinear : TextureFilter.Point;

    private void ApplyTextureFiltering()
    {
        var filter = GetRaylibTextureFilter();
        foreach (var texture in _iconTextures.Values)
        {
            Raylib.SetTextureFilter(texture, filter);
        }
    }

    private static string? ResolveIconPath(string relativePath)
    {
        var fullPath = Path.Combine(AppContext.BaseDirectory, relativePath);
        if (File.Exists(fullPath)) return fullPath;

        var directory = Path.GetDirectoryName(fullPath);
        var fileName = Path.GetFileName(fullPath);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return null;

        return Directory.EnumerateFiles(directory)
            .FirstOrDefault(path => Path.GetFileName(path).Equals(fileName, StringComparison.OrdinalIgnoreCase));
    }

    private static string? GetItemIconPath(ItemStack item)
    {
        if (item.Type == ItemType.Armor)
        {
            var armorIcon = item.ArmorKind switch
            {
                ArmorKind.Light => "light_armor.png",
                ArmorKind.Heavy => "heavy_armor.png",
                _ => "armor.png"
            };
            return Path.Combine("Assets", "Icons", "Armor", armorIcon);
        }
        if (item.IsStationKey) return Path.Combine("Assets", "Icons", "KeyItems", "station_key.png");
        if (item.IsDeviceDataFragment) return Path.Combine("Assets", "Icons", "KeyItems", "device's_data_fragment.png");
        if (item.IsVexEye) return Path.Combine("Assets", "Icons", "KeyItems", "vex's_eye.png");
        if (item.IsInfectedExemplar) return Path.Combine("Assets", "Icons", "KeyItems", "infected_exemplar.png");
        if (item.IsHeavyAmmo) return Path.Combine("Assets", "Icons", "Consumables", "heavy_ammo.png");

        if (item.Type == ItemType.Consumable)
        {
            var name = item.ConsumableKind switch
            {
                ConsumableType.Medkit => "medkit.png",
                ConsumableType.Stim => "stim.png",
                ConsumableType.ProtectiveDome => "protective_dome.png",
                ConsumableType.StickyBullets => "sticky_bullets.png",
                ConsumableType.TeslaBullets => "tesla_bullets.png",
                ConsumableType.FreezeGrenade => "freeze_grenade.png",
                ConsumableType.HeGrenade => "he_grenade.png",
                ConsumableType.MidaMiniTurret => "mida_mini_turret.png",
                _ => null
            };
            return name is null ? null : Path.Combine("Assets", "Icons", "Consumables", name);
        }

        if (item.Type != ItemType.Weapon) return null;

        var weaponIcon = item.Pattern switch
        {
            WeaponPattern.Lancelot => "lancelot.png",
            WeaponPattern.EnergySpear => "spear.png",
            WeaponPattern.GrenadeLauncher => "grenade_launcher.png",
            WeaponPattern.RocketLauncher => "rocket_launcher.png",
            WeaponPattern.TraceRifle => "trace_rifle.png",
            WeaponPattern.LinearRifle => "linear_rifle.png",
            WeaponPattern.Pulsar => "pulsar.png",
            WeaponPattern.RamBomber => "ram.png",
            WeaponPattern.AutoRifle => "auto_rifle.png",
            WeaponPattern.RocketPulseRifle => "rocket_pulse_rifle.png",
            WeaponPattern.Terror => "terror.png",
            WeaponPattern.SniperRifle => "sniper_rifle.png",
            WeaponPattern.Toxikus => "toxikus.png",
            WeaponPattern.PulseRifle => "pulse_rifle.png",
            _ => item.WeaponKind == WeaponClass.Melee ? "blade.png" : "pistol.png"
        };

        return Path.Combine("Assets", "Icons", "Weapons", weaponIcon);
    }

    private void UnloadIconTextures()
    {
        foreach (var texture in _iconTextures.Values)
        {
            Raylib.UnloadTexture(texture);
        }

        _iconTextures.Clear();
        _missingIconTextures.Clear();
    }

    private void DrawComparisonMarker(ItemStack item, Rectangle rect, ComparisonContext? comparison, SlotKind? sourceKind)
    {
        var marker = GetComparisonMarker(item, comparison, sourceKind);
        if (marker == ComparisonMarker.None) return;

        if (marker == ComparisonMarker.Better)
        {
            var tip = new Vector2(rect.X + rect.Width - 8f, rect.Y + 4f);
            var left = new Vector2(rect.X + rect.Width - 14f, rect.Y + 12f);
            var right = new Vector2(rect.X + rect.Width - 2f, rect.Y + 12f);
            Raylib.DrawTriangle(
                new Vector2(tip.X, tip.Y - 2f),
                new Vector2(left.X - 2f, left.Y + 2f),
                new Vector2(right.X + 2f, right.Y + 2f),
                Palette.C(28, 116, 54));
            Raylib.DrawTriangle(tip, left, right, Palette.C(80, 230, 110));
            return;
        }

        if (marker == ComparisonMarker.Worse)
        {
            var tip = new Vector2(rect.X + rect.Width - 8f, rect.Y + 12f);
            var left = new Vector2(rect.X + rect.Width - 14f, rect.Y + 4f);
            var right = new Vector2(rect.X + rect.Width - 2f, rect.Y + 4f);
            Raylib.DrawTriangle(tip, left, right, Palette.C(230, 70, 70));
            return;
        }

        Raylib.DrawRectangle((int)(rect.X + rect.Width - 16f), (int)(rect.Y + 5f), 16, 7, Palette.C(124, 92, 18));
        Raylib.DrawRectangle((int)(rect.X + rect.Width - 14f), (int)(rect.Y + 7f), 12, 3, Palette.C(255, 220, 90));
    }

    private static void DrawArmorModifierDiamonds(ItemStack item, Rectangle rect, float outerRadius = 7f, float innerRadius = 4.8f, float spacing = 12f)
    {
        var count = GetArmorModifierCount(item);
        if (count <= 0) return;

        var x = rect.X + outerRadius + 6f;
        var y = rect.Y + outerRadius + 5f;
        for (var i = 0; i < count; i++)
        {
            var center = new Vector2(x, y + i * spacing);
            DrawDiamond(center, outerRadius, Palette.C(30, 120, 58));
            DrawDiamond(center, innerRadius, Palette.C(255, 220, 90));
        }
    }

    private static void DrawArmorModifierDiamondsForSlots(IEnumerable<UiSlot> slots)
    {
        foreach (var slot in slots)
        {
            if (slot.Item is null) continue;
            DrawArmorModifierDiamonds(slot.Item, slot.Rect);
        }
    }

    private static void DrawDiamond(Vector2 center, float radius, Color color)
    {
        Raylib.DrawTriangle(
            new Vector2(center.X, center.Y - radius),
            new Vector2(center.X, center.Y + radius),
            new Vector2(center.X + radius, center.Y),
            color);
        Raylib.DrawTriangle(
            new Vector2(center.X, center.Y - radius),
            new Vector2(center.X - radius, center.Y),
            new Vector2(center.X, center.Y + radius),
            color);
    }

    private ComparisonMarker GetComparisonMarker(ItemStack item, ComparisonContext? comparison, SlotKind? sourceKind)
    {
        if (item.Type is ItemType.Consumable or ItemType.KeyItem or ItemType.Ammo) return ComparisonMarker.None;
        if (comparison is null) return ComparisonMarker.None;
        if (sourceKind is SlotKind.Armor or SlotKind.RangedWeapon or SlotKind.HeavyWeapon or SlotKind.MeleeWeapon) return ComparisonMarker.None;

        if (item.Type == ItemType.Weapon)
        {
            if (item.WeaponKind is null) return ComparisonMarker.None;
            if (item.Pattern == WeaponPattern.RamBomber) return ComparisonMarker.None;
            var equipped = GetComparedWeapon(item, comparison);
            if (equipped is null || equipped.Type != ItemType.Weapon || equipped.WeaponKind != item.WeaponKind) return ComparisonMarker.Better;

            var candidateDamage = item.BaseDamage;
            var equippedDamage = equipped.BaseDamage;
            return CompareSingleValue(candidateDamage, equippedDamage);
        }

        var equippedArmor = comparison.Armor;
        if (equippedArmor is null || equippedArmor.Type != ItemType.Armor) return ComparisonMarker.Better;

        return CompareArmor(item, equippedArmor);
    }

    private static ComparisonMarker CompareSingleValue(float candidate, float equipped)
    {
        const float epsilon = 0.001f;
        if (candidate > equipped + epsilon) return ComparisonMarker.Better;
        if (candidate < equipped - epsilon) return ComparisonMarker.Worse;
        return ComparisonMarker.Neutral;
    }

    private static ComparisonMarker CompareArmor(ItemStack candidate, ItemStack equipped)
    {
        const float epsilon = 0.001f;
        var armorDiff = candidate.Defense - equipped.Defense;
        var resilienceDiff = candidate.ResiliencePercent - equipped.ResiliencePercent;

        var armorBetter = armorDiff > epsilon;
        var armorWorse = armorDiff < -epsilon;
        var resilienceBetter = resilienceDiff > epsilon;
        var resilienceWorse = resilienceDiff < -epsilon;

        if ((armorBetter || MathF.Abs(armorDiff) <= epsilon) && (resilienceBetter || MathF.Abs(resilienceDiff) <= epsilon) && (armorBetter || resilienceBetter))
            return ComparisonMarker.Better;

        if ((armorWorse || MathF.Abs(armorDiff) <= epsilon) && (resilienceWorse || MathF.Abs(resilienceDiff) <= epsilon) && (armorWorse || resilienceWorse))
            return ComparisonMarker.Worse;

        return ComparisonMarker.Neutral;
    }

    private enum ComparisonMarker
    {
        None,
        Better,
        Worse,
        Neutral
    }

    private sealed record ComparisonContext(Player StatsPlayer, ItemStack? Armor, ItemStack? RangedWeapon, ItemStack? HeavyWeapon, ItemStack? MeleeWeapon);

    private static void DrawBladeIcon(Rectangle rect)
    {
        var rotation = -42f;
        var center = new Vector2(rect.X + rect.Width * 0.50f, rect.Y + rect.Height * 0.56f);
        var blade = new Rectangle(center.X, center.Y, rect.Width * 0.56f, rect.Height * 0.08f);
        var guard = new Rectangle(center.X - rect.Width * 0.11f, center.Y + rect.Height * 0.10f, rect.Width * 0.10f, rect.Height * 0.30f);
        Raylib.DrawRectanglePro(blade, new Vector2(blade.Width * 0.5f, blade.Height * 0.5f), rotation, Color.Black);
        Raylib.DrawRectanglePro(guard, new Vector2(guard.Width * 0.5f, guard.Height * 0.5f), rotation, Color.Black);
    }

    private static void DrawSpearIcon(Rectangle rect)
    {
        var rotation = -42f;
        var center = new Vector2(rect.X + rect.Width * 0.5f, rect.Y + rect.Height * 0.5f);
        var shaft = new Rectangle(center.X, center.Y, rect.Width * 0.72f, rect.Height * 0.07f);
        Raylib.DrawRectanglePro(shaft, new Vector2(shaft.Width * 0.7f, shaft.Height * 0.7f), rotation, Color.Black);
    }

    private static void DrawPistolIcon(Rectangle rect)
    {
        var body = new Rectangle(rect.X + rect.Width * 0.20f, rect.Y + rect.Height * 0.42f, rect.Width * 0.44f, rect.Height * 0.14f);
        var grip = new Rectangle(rect.X + rect.Width * 0.40f, rect.Y + rect.Height * 0.50f, rect.Width * 0.14f, rect.Height * 0.24f);
        Raylib.DrawRectangleRec(body, Color.Black);
        Raylib.DrawRectangleRec(grip, Color.Black);
    }

    private static void DrawPulseRifleIcon(Rectangle rect)
    {
        var barrel = new Rectangle(rect.X + rect.Width * 0.16f, rect.Y + rect.Height * 0.42f, rect.Width * 0.56f, rect.Height * 0.10f);
        var stock = new Rectangle(rect.X + rect.Width * 0.12f, rect.Y + rect.Height * 0.38f, rect.Width * 0.12f, rect.Height * 0.18f);
        var grip = new Rectangle(rect.X + rect.Width * 0.42f, rect.Y + rect.Height * 0.50f, rect.Width * 0.10f, rect.Height * 0.18f);
        var scope = new Rectangle(rect.X + rect.Width * 0.34f, rect.Y + rect.Height * 0.32f, rect.Width * 0.18f, rect.Height * 0.08f);
        Raylib.DrawRectangleRec(barrel, Color.Black);
        Raylib.DrawRectangleRec(stock, Color.Black);
        Raylib.DrawRectangleRec(grip, Color.Black);
        Raylib.DrawRectangleRec(scope, Color.Black);
    }

    private static void DrawGrenadeLauncherIcon(Rectangle rect)
    {
        var rear = new Rectangle(rect.X + rect.Width * 0.12f, rect.Y + rect.Height * 0.44f, rect.Width * 0.34f, rect.Height * 0.18f);
        var body = new Rectangle(rect.X + rect.Width * 0.30f, rect.Y + rect.Height * 0.38f, rect.Width * 0.42f, rect.Height * 0.26f);
        var muzzle = new Rectangle(rect.X + rect.Width * 0.66f, rect.Y + rect.Height * 0.40f, rect.Width * 0.22f, rect.Height * 0.16f);
        var frontRise = new Rectangle(rect.X + rect.Width * 0.80f, rect.Y + rect.Height * 0.30f, rect.Width * 0.08f, rect.Height * 0.26f);
        var rearGrip = new Rectangle(rect.X + rect.Width * 0.30f, rect.Y + rect.Height * 0.60f, rect.Width * 0.16f, rect.Height * 0.18f);
        var frontGrip = new Rectangle(rect.X + rect.Width * 0.66f, rect.Y + rect.Height * 0.55f, rect.Width * 0.09f, rect.Height * 0.22f);

        Raylib.DrawRectangleRec(rear, Color.Black);
        Raylib.DrawRectangleRec(body, Color.Black);
        Raylib.DrawRectangleRec(muzzle, Color.Black);
        Raylib.DrawRectangleRec(frontRise, Color.Black);
        Raylib.DrawRectangleRec(rearGrip, Color.Black);
        Raylib.DrawRectangleRec(frontGrip, Color.Black);
    }

    private static void DrawSniperIcon(Rectangle rect)
    {
        var barrel = new Rectangle(rect.X + rect.Width * 0.12f, rect.Y + rect.Height * 0.44f, rect.Width * 0.8f, rect.Height * 0.07f);
        var stock = new Rectangle(rect.X + rect.Width * 0.08f, rect.Y + rect.Height * 0.40f, rect.Width * 0.24f, rect.Height * 0.16f);
        var scope = new Rectangle(rect.X + rect.Width * 0.45f, rect.Y + rect.Height * 0.34f, rect.Width * 0.24f, rect.Height * 0.08f);
        var grip = new Rectangle(rect.X + rect.Width * 0.46f, rect.Y + rect.Height * 0.46f, rect.Width * 0.14f, rect.Height * 0.12f);
        Raylib.DrawRectangleRec(barrel, Color.Black);
        Raylib.DrawRectangleRec(stock, Color.Black);
        Raylib.DrawRectangleRec(scope, Color.Black);
        Raylib.DrawRectangleRec(grip, Color.Black);
    }

    private static void DrawTraceRifleIcon(Rectangle rect)
    {
        Raylib.DrawRectangleRec(new Rectangle(rect.X + rect.Width * 0.09f, rect.Y + rect.Height * 0.36f, rect.Width * 0.28f, rect.Height * 0.25f), Color.Black);
        Raylib.DrawRectangleRec(new Rectangle(rect.X + rect.Width * 0.35f, rect.Y + rect.Height * 0.39f, rect.Width * 0.33f, rect.Height * 0.19f), Color.Black);
        Raylib.DrawRectangleRec(new Rectangle(rect.X + rect.Width * 0.65f, rect.Y + rect.Height * 0.44f, rect.Width * 0.22f, rect.Height * 0.07f), Color.Black);
        Raylib.DrawRectangleRec(new Rectangle(rect.X + rect.Width * 0.72f, rect.Y + rect.Height * 0.50f, rect.Width * 0.07f, rect.Height * 0.16f), Color.Black);
        Raylib.DrawRectangleRec(new Rectangle(rect.X + rect.Width * 0.46f, rect.Y + rect.Height * 0.55f, rect.Width * 0.10f, rect.Height * 0.16f), Color.Black);
    }

    private static void DrawLinearRifleIcon(Rectangle rect)
    {
        Raylib.DrawRectangleRec(new Rectangle(rect.X + rect.Width * 0.12f, rect.Y + rect.Height * 0.42f, rect.Width * 0.64f, rect.Height * 0.17f), Color.Black);
        Raylib.DrawRectangleRec(new Rectangle(rect.X + rect.Width * 0.55f, rect.Y + rect.Height * 0.36f, rect.Width * 0.13f, rect.Height * 0.23f), Color.Black);
        Raylib.DrawRectangleRec(new Rectangle(rect.X + rect.Width * 0.75f, rect.Y + rect.Height * 0.41f, rect.Width * 0.18f, rect.Height * 0.19f), Color.Black);
        Raylib.DrawRectangleRec(new Rectangle(rect.X + rect.Width * 0.34f, rect.Y + rect.Height * 0.55f, rect.Width * 0.10f, rect.Height * 0.17f), Color.Black);
    }

    private static void DrawRocketLauncherIcon(Rectangle rect)
    {
        Raylib.DrawRectangleRec(new Rectangle(rect.X + rect.Width * 0.08f, rect.Y + rect.Height * 0.43f, rect.Width * 0.72f, rect.Height * 0.14f), Color.Black);
        Raylib.DrawRectangleRec(new Rectangle(rect.X + rect.Width * 0.12f, rect.Y + rect.Height * 0.39f, rect.Width * 0.24f, rect.Height * 0.22f), Color.Black);
        Raylib.DrawRectangleRec(new Rectangle(rect.X + rect.Width * 0.72f, rect.Y + rect.Height * 0.37f, rect.Width * 0.18f, rect.Height * 0.24f), Color.Black);
        Raylib.DrawRectangleRec(new Rectangle(rect.X + rect.Width * 0.34f, rect.Y + rect.Height * 0.55f, rect.Width * 0.10f, rect.Height * 0.16f), Color.Black);
    }

    private static void DrawPulsarIcon(Rectangle rect)
    {
        Raylib.DrawRectangleRec(new Rectangle(rect.X + rect.Width * 0.10f, rect.Y + rect.Height * 0.42f, rect.Width * 0.26f, rect.Height * 0.18f), Color.Black);
        Raylib.DrawRectangleRec(new Rectangle(rect.X + rect.Width * 0.32f, rect.Y + rect.Height * 0.38f, rect.Width * 0.34f, rect.Height * 0.24f), Color.Black);
        Raylib.DrawRectangleRec(new Rectangle(rect.X + rect.Width * 0.62f, rect.Y + rect.Height * 0.43f, rect.Width * 0.22f, rect.Height * 0.10f), Color.Black);
        Raylib.DrawRectangleRec(new Rectangle(rect.X + rect.Width * 0.84f, rect.Y + rect.Height * 0.39f, rect.Width * 0.05f, rect.Height * 0.18f), Color.Black);
        Raylib.DrawRectangleRec(new Rectangle(rect.X + rect.Width * 0.36f, rect.Y + rect.Height * 0.58f, rect.Width * 0.11f, rect.Height * 0.16f), Color.Black);
    }

    private static void DrawArmorIcon(Rectangle rect)
    {
        var leftShoulder = new Rectangle(rect.X + rect.Width * 0.20f, rect.Y + rect.Height * 0.22f, rect.Width * 0.18f, rect.Height * 0.18f);
        var rightShoulder = new Rectangle(rect.X + rect.Width * 0.62f, rect.Y + rect.Height * 0.22f, rect.Width * 0.18f, rect.Height * 0.18f);
        var chest = new Rectangle(rect.X + rect.Width * 0.28f, rect.Y + rect.Height * 0.28f, rect.Width * 0.44f, rect.Height * 0.42f);
        Raylib.DrawRectangleRec(leftShoulder, Color.Black);
        Raylib.DrawRectangleRec(rightShoulder, Color.Black);
        Raylib.DrawRectangleRec(chest, Color.Black);
    }

    private static void DrawMedKitIcon(Rectangle rect)
    {
        Raylib.DrawRectangle((int)(rect.X + rect.Width * 0.20f), (int)(rect.Y + rect.Height * 0.34f), (int)(rect.Width * 0.60f), (int)(rect.Height * 0.60f), Color.Black);
        Raylib.DrawRectangle((int)(rect.X + rect.Width * 0.46f), (int)(rect.Y + rect.Height * 0.445f), (int)(rect.Width * 0.10f), (int)(rect.Height * 0.40f), Color.White);
        Raylib.DrawRectangle((int)(rect.X + rect.Width * 0.305f), (int)(rect.Y + rect.Height * 0.6f), (int)(rect.Width * 0.40f), (int)(rect.Height * 0.10f), Color.White);        
        Raylib.DrawText("cs", (int)(rect.X + rect.Width - 18), (int)(rect.Y + 3), 10, Color.Black);
    }

    private static void DrawStimIcon(Rectangle rect)
    {
        Raylib.DrawRectangle((int)(rect.X + rect.Width * 0.48f), (int)(rect.Y + rect.Height * 0.34f), (int)(rect.Width * 0.10f), (int)(rect.Height * 0.50f), Color.Black);
        Raylib.DrawRectangle((int)(rect.X + rect.Width * 0.42f), (int)(rect.Y + rect.Height * 0.4f), (int)(rect.Width * 0.20f), (int)(rect.Height * 0.10f), Color.Black);
        Raylib.DrawText("cs", (int)(rect.X + rect.Width - 18), (int)(rect.Y + 3), 10, Color.Black);
    }

    private static void DrawProtectiveDomeIcon(Rectangle rect)
    {
        Raylib.DrawCircle((int)(rect.X + rect.Width * 0.5f), (int)(rect.Y + rect.Height * 0.55f), rect.Width * 0.22f, Color.Black);
        Raylib.DrawCircle((int)(rect.X + rect.Width * 0.5f), (int)(rect.Y + rect.Height * 0.55f), rect.Width * 0.12f, Color.White);
        Raylib.DrawRectangle((int)(rect.X + rect.Width * 0.36f), (int)(rect.Y + rect.Height * 0.18f), (int)(rect.Width * 0.28f), (int)(rect.Height * 0.08f), Color.Black);
        Raylib.DrawRectangle((int)(rect.X + rect.Width * 0.36f), (int)(rect.Y + rect.Height * 0.80f), (int)(rect.Width * 0.28f), (int)(rect.Height * 0.08f), Color.Black);
    }

    private static void DrawStickyBulletsIcon(Rectangle rect)
    {
        Raylib.DrawRectangle((int)(rect.X + rect.Width * 0.24f), (int)(rect.Y + rect.Height * 0.24f), (int)(rect.Width * 0.52f), (int)(rect.Height * 0.52f), Color.Black);
        var tip = new Vector2(rect.X + rect.Width * 0.5f, rect.Y + rect.Height * 0.32f);
        var left = new Vector2(rect.X + rect.Width * 0.34f, rect.Y + rect.Height * 0.66f);
        var right = new Vector2(rect.X + rect.Width * 0.66f, rect.Y + rect.Height * 0.66f);
        Raylib.DrawTriangle(tip, left, right, Color.White);
    }

    private static void DrawHeavyAmmoIcon(Rectangle rect)
    {
        Raylib.DrawRectangleRec(new Rectangle(rect.X + rect.Width * 0.22f, rect.Y + rect.Height * 0.30f, rect.Width * 0.56f, rect.Height * 0.40f), Color.Black);
        Raylib.DrawRectangleRec(new Rectangle(rect.X + rect.Width * 0.34f, rect.Y + rect.Height * 0.20f, rect.Width * 0.32f, rect.Height * 0.14f), Color.Black);
        Raylib.DrawRectangleRec(new Rectangle(rect.X + rect.Width * 0.43f, rect.Y + rect.Height * 0.42f, rect.Width * 0.08f, rect.Height * 0.16f), Color.White);
        Raylib.DrawRectangleRec(new Rectangle(rect.X + rect.Width * 0.56f, rect.Y + rect.Height * 0.42f, rect.Width * 0.08f, rect.Height * 0.16f), Color.White);
    }

    private static void DrawStationKeyIcon(Rectangle rect)
    {
        Raylib.DrawRectangleLinesEx(rect, MathF.Max(2f, rect.Width * 0.04f), Color.Black);

        var shaft = new Rectangle(rect.X + rect.Width * 0.18f, rect.Y + rect.Height * 0.46f, rect.Width * 0.27f, rect.Height * 0.13f);
        var head = new Rectangle(rect.X + rect.Width * 0.39f, rect.Y + rect.Height * 0.36f, rect.Width * 0.48f, rect.Height * 0.30f);
        Raylib.DrawRectangleRec(shaft, Color.Black);
        Raylib.DrawRectangleRec(head, Color.Black);

        const string label = "S";
        var fontSize = Math.Max(8, (int)(rect.Height * 0.20f));
        var textX = (int)(head.X + (head.Width - Raylib.MeasureText(label, fontSize)) * 0.5f);
        var textY = (int)(head.Y + (head.Height - fontSize) * 0.5f);
        Raylib.DrawText(label, textX, textY, fontSize, Color.White);
    }

    private static void DrawDeviceDataFragmentIcon(Rectangle rect)
    {
        Raylib.DrawRectangleLinesEx(rect, MathF.Max(2f, rect.Width * 0.04f), Palette.C(55, 42, 30));
        var chip = new Rectangle(rect.X + rect.Width * 0.24f, rect.Y + rect.Height * 0.24f, rect.Width * 0.52f, rect.Height * 0.52f);
        Raylib.DrawRectangleRec(chip, Palette.C(82, 68, 54));
        Raylib.DrawRectangleLinesEx(chip, MathF.Max(1f, rect.Width * 0.025f), Palette.C(245, 196, 72));
        Raylib.DrawLineEx(new Vector2(chip.X + chip.Width * 0.18f, chip.Y + chip.Height * 0.5f), new Vector2(chip.X + chip.Width * 0.82f, chip.Y + chip.Height * 0.5f), MathF.Max(2f, rect.Height * 0.04f), Palette.C(245, 196, 72));
    }

    private static void DrawTooltip(ItemStack item, Vector2 mouse, ComparisonContext? comparison = null)
    {
        var detailLines = BuildTooltipDetails(item, comparison);
        var x = (int)mouse.X + 20;
        var y = (int)mouse.Y + 14;
        const int width = 360;
        const int padding = 8;
        const int lineHeight = 20;
        var textWidth = width - padding * 2;
        var descriptionLines = WrapText(item.Description, 16, textWidth);
        var wrappedDetails = detailLines
            .SelectMany(line => WrapText(line.Text, 16, textWidth).Select(text => (Text: text, line.Color)))
            .ToList();
        var height = 44 + descriptionLines.Count * lineHeight + 8 + wrappedDetails.Count * lineHeight + padding;
        Raylib.DrawRectangle(x, y, width, height, Palette.C(0, 0, 0, 220));
        Raylib.DrawRectangleLines(x, y, width, height, Color.SkyBlue);
        Raylib.DrawText(item.Name, x + 8, y + 8, 18, Color.White);
        var lineY = y + 32;
        foreach (var line in descriptionLines)
        {
            Raylib.DrawText(line, x + padding, lineY, 16, Color.LightGray);
            lineY += lineHeight;
        }

        lineY += 8;
        foreach (var (text, color) in wrappedDetails)
        {
            Raylib.DrawText(text, x + padding, lineY, 16, color);
            lineY += lineHeight;
        }
    }

    private static List<string> WrapText(string text, int fontSize, int maxWidth)
    {
        var lines = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
        {
            lines.Add(string.Empty);
            return lines;
        }

        var current = string.Empty;
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = string.IsNullOrEmpty(current) ? word : $"{current} {word}";
            if (Raylib.MeasureText(candidate, fontSize) <= maxWidth)
            {
                current = candidate;
                continue;
            }

            if (!string.IsNullOrEmpty(current)) lines.Add(current);
            current = word;
        }

        if (!string.IsNullOrEmpty(current)) lines.Add(current);
        return lines;
    }

    private static List<(string Text, Color Color)> BuildTooltipDetails(ItemStack item, ComparisonContext? comparison)
    {
        var lines = new List<(string Text, Color Color)>();

        if (item.Type == ItemType.Armor)
        {
            lines.Add(($"Armor {item.Defense:0} | Resilience {item.ResiliencePercent * 100f:0}% | {item.Rarity}", item.Color));
            if (MathF.Abs(item.MovementSpreadPercent) > 0.001f)
            {
                var sign = item.MovementSpreadPercent > 0f ? "+" : "-";
                lines.Add(($"Moving spread {sign}{MathF.Abs(item.MovementSpreadPercent) * 100f:0}%", item.MovementSpreadPercent > 0f ? Palette.C(255, 150, 120) : Palette.C(150, 220, 255)));
            }
            if (MathF.Abs(item.DashDistancePercent) > 0.001f)
            {
                var sign = item.DashDistancePercent > 0f ? "+" : "-";
                lines.Add(($"Dash distance {sign}{MathF.Abs(item.DashDistancePercent) * 100f:0}%", item.DashDistancePercent > 0f ? Palette.C(150, 220, 255) : Palette.C(255, 150, 120)));
            }
            if (item.SpeedBonusPercent > 0f) lines.Add(($"Speed +{item.SpeedBonusPercent * 100f:0}%", Palette.C(170, 220, 255)));
            if (item.ExplosionResistancePercent > 0f) lines.Add(($"Explosion resist +{item.ExplosionResistancePercent * 100f:0}%", Palette.C(255, 170, 120)));
            if (item.HealingBonusPercent > 0f) lines.Add(($"Healing +{item.HealingBonusPercent * 100f:0}%", Palette.C(135, 230, 150)));
            if (item.DashRecoveryPercent > 0f) lines.Add(($"Dash recovery +{item.DashRecoveryPercent * 100f:0}%", Palette.C(120, 230, 180)));
            if (item.ShieldMax > 0f) lines.Add(($"Shield {item.ShieldMax:0}", Palette.C(120, 205, 255)));
            if (item.RegenPercentPerSecond > 0f) lines.Add(($"Regen {item.RegenPercentPerSecond * 100f:0.0}%/sec", Palette.C(160, 255, 170)));
            return lines;
        }

        if (item.Type == ItemType.Weapon)
        {
            if (item.Pattern == WeaponPattern.RamBomber)
            {
                lines.Add(("Base damage: ??? | Ranged", item.Color));
                lines.Add(("Fire rate: click only", Palette.C(170, 220, 255)));
                lines.Add(("Explosion radius: 1000", Palette.C(255, 170, 120)));
                lines.Add(("Effect damage: 1 / 1000 / 10000", Palette.C(180, 230, 255)));
                lines.Add(("DPS: ???", Color.LightGray));
                return lines;
            }

            AddWeaponStatLines(lines, item);
            var dps = GetWeaponDps(item);
            lines.Add(($"DPS: {dps:0.0}", GetWeaponDpsColor(item, dps, comparison)));
            return lines;
        }

        if (item.IsHeavyAmmo)
        {
            lines.Add(($"Heavy ammo: {item.AmmoPercent:0.0}%", Palette.C(120, 210, 255)));
            lines.Add(("Used by heavy weapons.", Color.LightGray));
            return lines;
        }

        if (item.IsDeviceDataFragment) return lines;
        if (item.IsStationKey) lines.Add(("Key item | opens station entrance", item.Color));
        else lines.Add(("Use by Q/R", Color.Green));
        return lines;
    }

    private static void AddWeaponStatLines(List<(string Text, Color Color)> lines, ItemStack item)
    {
        lines.Add(($"Base damage: {GetWeaponBaseHitDamage(item):0.0} | {item.WeaponKind}", item.Color));

        if (GetWeaponBurstCount(item) is int burstCount)
        {
            lines.Add(($"Burst: {burstCount} shots", Palette.C(170, 220, 255)));
            lines.Add(($"Burst rate: {GetWeaponBurstRatePerMinute(item):0}/min", Palette.C(170, 220, 255)));
        }
        else
        {
            lines.Add(($"Fire rate: {GetWeaponFireRatePerMinute(item):0}/min", Palette.C(170, 220, 255)));
        }

        if (item.Pattern == WeaponPattern.LinearRifle)
        {
            lines.Add(($"Charge time: {GetLinearRifleChargeTime(item):0.00}s", Palette.C(130, 230, 255)));
        }

        var range = GetWeaponRange(item);
        if (range > 0f) lines.Add(($"Range: {range:0}", Palette.C(190, 210, 240)));

        var explosionRadius = GetWeaponExplosionRadius(item);
        if (explosionRadius > 0f)
        {
            lines.Add(($"Explosion radius: {explosionRadius:0}", Palette.C(255, 170, 120)));
            lines.Add(($"Explosion damage: {GetWeaponExplosionDamage(item):0.0}", Palette.C(255, 190, 130)));
        }

        foreach (var effect in GetWeaponEffectLines(item))
        {
            lines.Add(effect);
        }
    }

    private static Color GetWeaponDpsColor(ItemStack item, float dps, ComparisonContext? comparison)
    {
        if (comparison is null || item.WeaponKind is null) return Color.LightGray;
        var equipped = GetComparedWeapon(item, comparison);
        if (equipped is null || equipped.Type != ItemType.Weapon || equipped.WeaponKind != item.WeaponKind) return Palette.C(80, 230, 110);

        var equippedDps = GetWeaponDps(equipped);
        const float epsilon = 0.001f;
        if (dps > equippedDps + epsilon) return Palette.C(80, 230, 110);
        if (dps < equippedDps - epsilon) return Palette.C(230, 70, 70);
        return Palette.C(255, 220, 90);
    }

    private static ItemStack? GetComparedWeapon(ItemStack item, ComparisonContext comparison)
    {
        if (item.WeaponKind == WeaponClass.Melee) return comparison.MeleeWeapon;
        if (item.IsHeavyWeapon) return comparison.HeavyWeapon;
        return comparison.RangedWeapon;
    }

    private static float GetWeaponBaseHitDamage(ItemStack item)
    {
        var damage = item.BaseDamage;
        return item.Pattern switch
        {
            WeaponPattern.AutoRifle => damage * 0.53f,
            WeaponPattern.RocketPulseRifle => damage * 0.9f,
            WeaponPattern.GrenadeLauncher => damage + 135f,
            WeaponPattern.RocketLauncher => damage + 200f,
            WeaponPattern.LinearRifle => damage * 9f,
            WeaponPattern.SniperRifle => damage * 8.325f,
            WeaponPattern.PulseRifle or WeaponPattern.Toxikus => damage * 0.525f,
            WeaponPattern.Pulsar => damage,
            WeaponPattern.TraceRifle => damage,
            WeaponPattern.EnergySpear or WeaponPattern.Lancelot => damage * 6.3f,
            _ => item.WeaponKind == WeaponClass.Melee ? damage * 6.3f : damage
        };
    }

    private static int? GetWeaponBurstCount(ItemStack item)
        => item.Pattern switch
        {
            WeaponPattern.PulseRifle => item.Rarity == ArmorRarity.Legendary ? 4 : 3,
            WeaponPattern.Toxikus => 2,
            WeaponPattern.RocketPulseRifle => 3,
            _ => null
        };

    private static float GetWeaponBurstRatePerMinute(ItemStack item)
        => item.Pattern switch
        {
            WeaponPattern.Toxikus => 2.2f * 60f,
            WeaponPattern.PulseRifle => (1f / 0.374f) * 60f,
            WeaponPattern.RocketPulseRifle => (1f / (3f / (400f / 60f))) * 60f,
            _ => 0f
        };

    private static float GetWeaponRange(ItemStack item)
        => item.Pattern switch
        {
            WeaponPattern.Standard when item.WeaponKind == WeaponClass.Ranged => 550f,
            WeaponPattern.PulseRifle => 650f,
            WeaponPattern.AutoRifle => 620f,
            WeaponPattern.SniperRifle => 2000f,
            WeaponPattern.LinearRifle => 1000f,
            WeaponPattern.RocketPulseRifle => 600f,
            WeaponPattern.GrenadeLauncher => 350f,
            WeaponPattern.RocketLauncher => 510f,
            WeaponPattern.TraceRifle => 820f,
            WeaponPattern.Pulsar => 600f,
            WeaponPattern.Toxikus => 625f,
            WeaponPattern.Terror => 800f,
            WeaponPattern.EnergySpear => item.Rarity == ArmorRarity.Legendary ? 150f : 125f,
            WeaponPattern.Lancelot => 150f,
            WeaponPattern.Standard when item.WeaponKind == WeaponClass.Melee => 75f,
            _ => 0f
        };

    private static float GetWeaponExplosionRadius(ItemStack item)
        => item.Pattern switch
        {
            WeaponPattern.GrenadeLauncher => 90f,
            WeaponPattern.RocketLauncher => 117f,
            WeaponPattern.RocketPulseRifle => 35f,
            WeaponPattern.Pulsar => 14.9625f,
            _ => 0f
        };

    private static float GetWeaponExplosionDamage(ItemStack item)
        => item.Pattern switch
        {
            WeaponPattern.GrenadeLauncher => item.BaseDamage,
            WeaponPattern.RocketLauncher => item.BaseDamage,
            WeaponPattern.RocketPulseRifle => item.BaseDamage * 0.45f,
            WeaponPattern.Pulsar => 15f,
            _ => 0f
        };

    private static float GetLinearRifleChargeTime(ItemStack item)
        => item.Rarity == ArmorRarity.Legendary ? 0.7f : 0.8f;

    private static List<(string Text, Color Color)> GetWeaponEffectLines(ItemStack item)
    {
        var lines = new List<(string Text, Color Color)>();
        if (item.Pattern == WeaponPattern.Toxikus)
        {
            lines.Add(($"Poison damage: {30f + item.BaseDamage * 0.4f:0.0}/sec for 3s", Palette.C(120, 230, 120)));
        }

        if (item.Pattern == WeaponPattern.Pulsar)
        {
            lines.Add(("Micro charges: 2-3", Palette.C(140, 230, 255)));
            lines.Add(("Micro delay: 0.25s", Palette.C(140, 230, 255)));
        }

        if (item.Pattern == WeaponPattern.AutoRifle && item.Rarity == ArmorRarity.Legendary)
        {
            lines.Add(("Effect: 20% ricochet, once", Palette.C(170, 220, 255)));
        }

        if (item.Pattern == WeaponPattern.RocketPulseRifle && item.Rarity == ArmorRarity.Legendary)
        {
            lines.Add(("Mode 2: faster burst, 5 deg spread", Palette.C(170, 220, 255)));
        }

        if (item.Pattern == WeaponPattern.SniperRifle && item.Rarity == ArmorRarity.Legendary)
        {
            lines.Add(("Charged damage: x20.8125 after standing", Palette.C(176, 92, 255)));
        }

        if (item.Pattern == WeaponPattern.Terror)
        {
            lines.Add(("Spin-up: 6.0s", Palette.C(110, 185, 255)));
            lines.Add(("Fire rate: 2-15/sec", Palette.C(245, 90, 95)));
            lines.Add(("Radioactive decomposition: +25% damage for 5s", Palette.C(190, 85, 135)));
        }

        return lines;
    }

    private static float GetWeaponFireRatePerMinute(ItemStack item)
    {
        if (item.Type != ItemType.Weapon) return 0f;

        if (item.Pattern == WeaponPattern.RamBomber) return 0f;
        if (item.Pattern == WeaponPattern.AutoRifle) return 500f;
        if (item.Pattern == WeaponPattern.RocketPulseRifle) return 3f * (1f / GetWeaponCooldown(item)) * 60f;
        if (item.Pattern == WeaponPattern.GrenadeLauncher) return 90f;
        if (item.Pattern == WeaponPattern.RocketLauncher) return 40f;
        if (item.Pattern == WeaponPattern.TraceRifle) return 1000f;
        if (item.Pattern == WeaponPattern.LinearRifle) return (1f / GetWeaponCooldown(item)) * 60f;
        if (item.Pattern == WeaponPattern.Pulsar) return 3f * 60f;
        if (item.Pattern == WeaponPattern.Terror) return 15f * 60f;
        if (item.Pattern == WeaponPattern.PulseRifle)
        {
            var shots = item.Rarity == ArmorRarity.Legendary ? 4 : 3;
            return shots * (1f / 0.374f) * 60f;
        }

        if (item.Pattern == WeaponPattern.Toxikus) return 2f * 2.2f * 60f;
        if (item.Pattern == WeaponPattern.SniperRifle) return (1f / 1.75f) * 60f;
        if (item.WeaponKind == WeaponClass.Melee) return (1f / GetWeaponCooldown(item)) * 60f;

        var expectedShotsPerAttack = item.Rarity == ArmorRarity.Legendary ? 1.33f : 1f;
        return expectedShotsPerAttack * (1f / 0.22f) * 60f;
    }

    private static float GetWeaponDps(ItemStack item)
    {
        if (item.Type != ItemType.Weapon) return 0f;

        if (item.Pattern == WeaponPattern.RamBomber) return 0f;
        var damage = item.BaseDamage;
        if (item.Pattern == WeaponPattern.AutoRifle) return damage * 0.53f / GetWeaponCooldown(item);
        if (item.Pattern == WeaponPattern.RocketPulseRifle) return damage * 1.35f / GetWeaponCooldown(item);
        if (item.Pattern == WeaponPattern.GrenadeLauncher) return (damage + 135f) / GetWeaponCooldown(item);
        if (item.Pattern == WeaponPattern.RocketLauncher) return (damage + 200f) / GetWeaponCooldown(item);
        if (item.Pattern == WeaponPattern.TraceRifle) return damage / GetWeaponCooldown(item);
        if (item.Pattern == WeaponPattern.LinearRifle) return damage * 9f / GetWeaponCooldown(item);
        if (item.Pattern == WeaponPattern.Pulsar) return (damage + 2.5f * 15f) / GetWeaponCooldown(item);
        if (item.Pattern == WeaponPattern.PulseRifle)
        {
            var shots = item.Rarity == ArmorRarity.Legendary ? 4 : 3;
            var perShot = damage * 0.525f;
            return perShot * shots / GetWeaponCooldown(item);
        }

        if (item.Pattern == WeaponPattern.Toxikus)
        {
            var perShot = damage * 0.525f;
            var poisonDps = 30f + damage * 0.4f;
            return perShot * 2f / GetWeaponCooldown(item) + poisonDps;
        }

        if (item.Pattern == WeaponPattern.SniperRifle)
        {
            var shotDamage = damage * 8.325f;
            return shotDamage / GetWeaponCooldown(item);
        }

        if (item.WeaponKind == WeaponClass.Melee)
        {
            var hitDamage = damage * 6.3f;
            return hitDamage / GetWeaponCooldown(item);
        }

        var expectedShotsPerAttack = item.Rarity == ArmorRarity.Legendary ? 1.33f : 1f;
        return damage * expectedShotsPerAttack / GetWeaponCooldown(item);
    }

    private static float GetWeaponCooldown(ItemStack item)
    {
        if (item.Pattern == WeaponPattern.GrenadeLauncher) return 1f / 1.5f;
        if (item.Pattern == WeaponPattern.RamBomber) return 0f;
        if (item.Pattern == WeaponPattern.AutoRifle) return 60f / 500f;
        if (item.Pattern == WeaponPattern.RocketPulseRifle) return 3f / (400f / 60f);
        if (item.Pattern == WeaponPattern.RocketLauncher) return 1.5f;
        if (item.Pattern == WeaponPattern.TraceRifle) return 60f / 1000f;
        if (item.Pattern == WeaponPattern.LinearRifle) return (item.Rarity == ArmorRarity.Legendary ? 0.7f : 0.8f) + 0.45f;
        if (item.Pattern == WeaponPattern.Pulsar) return 1f / 3f;
        if (item.Pattern == WeaponPattern.Toxikus) return 1f / 2.2f;
        if (item.Pattern == WeaponPattern.PulseRifle) return 0.374f;
        if (item.Pattern == WeaponPattern.SniperRifle) return 1.75f;
        if (item.Pattern is WeaponPattern.EnergySpear or WeaponPattern.Lancelot) return 0.70f;
        if (item.WeaponKind == WeaponClass.Melee) return 0.64f;
        return 0.22f;
    }

    private static void DrawPlus(Rectangle r)
    {
        Raylib.DrawRectangleRec(r, Palette.C(42, 95, 180));
        Raylib.DrawText("+", (int)r.X + 5, (int)r.Y - 1, 24, Color.White);
    }

    private void DrawGrid()
    {
        for (var x = 0; x < _worldSize; x += 80) Raylib.DrawLine(x, 0, x, _worldSize, Theme.Grid);
        for (var y = 0; y < _worldSize; y += 80) Raylib.DrawLine(0, y, _worldSize, y, Theme.Grid);
    }

    private void DrawMainMenu()
    {
        Raylib.DrawText("a0.3.8", 86, 150, 24, Palette.C(150, 185, 220));
        DrawMetaProgressHeader();

        DrawButton(MainMenuButtonRect(0), "Play");
        DrawButton(MainMenuButtonRect(1), "Storage");
        DrawButton(MainMenuButtonRect(2), "Store");
        DrawButton(MainMenuButtonRect(3), "Cradle");
        DrawButton(MainMenuButtonRect(4), "Settings");
        DrawButton(MainMenuButtonRect(5), "Exit");
        DrawButton(MainMenuCodesButtonRect(), "Codes");
        DrawButton(MainMenuChangelogButtonRect(), "Changelog");
        DrawButton(MainMenuAboutButtonRect(), "About");

        if (_codesPopupOpen) DrawCodesPopup();
        if (_changelogPopupOpen) DrawChangelogPopup();
        if (_aboutPopupOpen) DrawAboutPopup();
    }

    private Color MenuPanelFill(float alpha = 0.86f)
        => WithAlpha(Mix(Opaque(Theme.Background), Opaque(Theme.BuildingLine), 0.12f), alpha);

    private Color MenuPanelAltFill(float alpha = 0.88f)
        => WithAlpha(Mix(Opaque(Theme.Background), Opaque(Theme.Boss), 0.18f), alpha);

    private Color MenuPanelLine(float alpha = 0.78f)
        => WithAlpha(Mix(Opaque(Theme.BuildingLine), Color.White, 0.18f), alpha);

    private void DrawMainMenuBackground()
    {
        DrawMainMenuSky();

        var time = (float)Raylib.GetTime();
        var orbit = time * (0.26f / 3f);
        var camera = new Camera3D
        {
            Position = new Vector3(MathF.Cos(orbit) * 7.4f, 2.65f, MathF.Sin(orbit) * 7.4f),
            Target = new Vector3(0f, 1.85f, 0f),
            Up = Vector3.UnitY,
            FovY = 48f,
            Projection = CameraProjection.Perspective
        };

        Raylib.BeginMode3D(camera);
        DrawMainMenuGroundGrid();
        DrawMainMenuWireHills();
        DrawMainMenuMonolith();
        Raylib.EndMode3D();

        DrawMainMenuVignette();
    }

    private void DrawMainMenuSky()
    {
        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();
        var classicNeon = Theme.Name == "Neon Night";
        var top = classicNeon ? Palette.C(8, 4, 28) : Mix(Opaque(Theme.Background), Color.Black, 0.35f);
        var bottom = classicNeon ? Palette.C(38, 4, 50) : Mix(Opaque(Theme.Background), Opaque(Theme.Boss), 0.42f);
        Raylib.DrawRectangleGradientV(0, 0, width, height, top, bottom);

        for (var i = 0; i < 90; i++)
        {
            var x = (int)((MathF.Sin(i * 37.7f) * 0.5f + 0.5f) * width);
            var y = (int)((MathF.Sin(i * 19.3f + 3f) * 0.5f + 0.5f) * height * 0.52f);
            if (x > width * 0.38f && x < width * 0.62f && y < height * 0.42f) continue;
            var alpha = 80 + (int)((MathF.Sin(i * 11.1f) * 0.5f + 0.5f) * 130f);
            var star = classicNeon ? Palette.C(230, 245, 255, alpha) : WithAlpha(Mix(Color.White, Opaque(Theme.Player), 0.45f), alpha / 255f);
            Raylib.DrawPixel(x, y, star);
        }
    }

    private void DrawMainMenuGroundGrid()
    {
        var classicNeon = Theme.Name == "Neon Night";
        var cyan = classicNeon ? Palette.C(26, 230, 255, 190) : WithAlpha(Theme.BuildingLine, 0.82f);
        var magenta = classicNeon ? Palette.C(255, 40, 220, 150) : WithAlpha(Theme.Player, 0.72f);
        const float extent = 18f;
        const float step = 1f;

        for (var x = -extent; x <= extent; x += step)
        {
            var color = MathF.Abs(x % 4f) < 0.01f ? magenta : cyan;
            Raylib.DrawLine3D(new Vector3(x, 0f, -extent), new Vector3(x, 0f, extent), color);
        }

        for (var z = -extent; z <= extent; z += step)
        {
            var color = MathF.Abs(z % 4f) < 0.01f ? magenta : cyan;
            Raylib.DrawLine3D(new Vector3(-extent, 0f, z), new Vector3(extent, 0f, z), color);
        }
    }

    private void DrawMainMenuWireHills()
    {
        var classicNeon = Theme.Name == "Neon Night";
        var cyan = classicNeon ? Palette.C(28, 220, 255) : Opaque(Theme.BuildingLine);
        var violet = classicNeon ? Palette.C(255, 40, 220) : Opaque(Theme.Player);
        var fill = classicNeon ? Palette.C(25, 5, 37) : Opaque(Mix(Theme.Background, Theme.Boss, 0.18f));
        const int segments = 96;
        const float angleStep = MathF.Tau / segments;

        for (var ring = 0; ring < 4; ring++)
        {
            var radius = 9f + ring * 1.45f;
            var color = ring % 2 == 0 ? cyan : violet;
            for (var i = 0; i < segments; i++)
            {
                var a0 = i * angleStep;
                var a1 = (i + 1) * angleStep;
                var p0 = MainMenuHillPoint(a0, radius);
                var p1 = MainMenuHillPoint(a1, radius);
                DrawSolidQuad3D(
                    new Vector3(p0.X, 0f, p0.Z),
                    new Vector3(p1.X, 0f, p1.Z),
                    p1,
                    p0,
                    fill);
            }
        }

        for (var ring = 0; ring < 3; ring++)
        {
            var innerRadius = 9f + ring * 1.45f;
            var outerRadius = innerRadius + 1.45f;
            for (var i = 0; i < segments; i++)
            {
                var a0 = i * angleStep;
                var a1 = (i + 1) * angleStep;
                DrawSolidQuad3D(
                    MainMenuHillPoint(a0, innerRadius),
                    MainMenuHillPoint(a1, innerRadius),
                    MainMenuHillPoint(a1, outerRadius),
                    MainMenuHillPoint(a0, outerRadius),
                    fill);
            }
        }

        for (var ring = 0; ring < 4; ring++)
        {
            var radius = 9f + ring * 1.45f;
            var color = ring % 2 == 0 ? cyan : violet;
            for (var i = 0; i < segments; i++)
            {
                var a0 = i * angleStep;
                var a1 = (i + 1) * angleStep;
                var p0 = MainMenuHillPoint(a0, radius);
                var p1 = MainMenuHillPoint(a1, radius);
                Raylib.DrawLine3D(p0, p1, color);

                if (i % 8 == 0 && ring == 0)
                {
                    Raylib.DrawLine3D(new Vector3(p0.X, 0f, p0.Z), p0, i % 16 == 0 ? violet : cyan);
                }
            }
        }

        for (var i = 0; i < segments; i += 4)
        {
            var angle = i * angleStep;
            Raylib.DrawLine3D(MainMenuHillPoint(angle, 9f), MainMenuHillPoint(angle, 13.35f), i % 12 == 0 ? violet : cyan);
        }
    }

    private static Vector3 MainMenuHillPoint(float angle, float radius)
    {
        var height = 0.35f
            + (MathF.Sin(angle * 3.0f + radius) * 0.5f + 0.5f) * 0.75f
            + (MathF.Sin(angle * 7.0f - radius * 0.7f) * 0.5f + 0.5f) * 0.45f;
        return new Vector3(MathF.Cos(angle) * radius, height, MathF.Sin(angle) * radius);
    }

    private void DrawMainMenuMonolith()
    {
        const float halfWidth = 0.68f;
        const float halfDepth = 0.38f;
        const float lowTop = 3.3f;
        const float highTop = 4.0f;

        var bfl = new Vector3(-halfWidth, 0f, -halfDepth);
        var bfr = new Vector3(halfWidth, 0f, -halfDepth);
        var bbr = new Vector3(halfWidth, 0f, halfDepth);
        var bbl = new Vector3(-halfWidth, 0f, halfDepth);
        var tfl = new Vector3(-halfWidth, highTop, -halfDepth);
        var tfr = new Vector3(halfWidth, highTop, -halfDepth);
        var tbr = new Vector3(halfWidth, lowTop, halfDepth);
        var tbl = new Vector3(-halfWidth, lowTop, halfDepth);

        var classicNeon = Theme.Name == "Neon Night";
        var face = classicNeon ? Palette.C(8, 12, 24) : Opaque(Mix(Theme.Background, Color.Black, 0.28f));
        var side = classicNeon ? Palette.C(16, 12, 38) : Opaque(Mix(Theme.Background, Theme.Boss, 0.22f));
        DrawSolidQuad3D(bfl, bfr, tfr, tfl, face);
        DrawSolidQuad3D(bfr, bbr, tbr, tfr, side);
        DrawSolidQuad3D(bbr, bbl, tbl, tbr, face);
        DrawSolidQuad3D(bbl, bfl, tfl, tbl, side);
        DrawSolidQuad3D(tfl, tfr, tbr, tbl, classicNeon ? Palette.C(50, 18, 74) : Opaque(Mix(Theme.Boss, Theme.Player, 0.28f)));

        var edgeA = classicNeon ? Palette.C(255, 58, 238) : Opaque(Theme.Player);
        var edgeB = classicNeon ? Palette.C(25, 230, 255) : Opaque(Theme.BuildingLine);
        DrawMonolithEdge(bfl, bfr, edgeA);
        DrawMonolithEdge(bfr, bbr, edgeB);
        DrawMonolithEdge(bbr, bbl, edgeA);
        DrawMonolithEdge(bbl, bfl, edgeB);
        DrawMonolithEdge(tfl, tfr, edgeA);
        DrawMonolithEdge(tfr, tbr, edgeB);
        DrawMonolithEdge(tbr, tbl, edgeA);
        DrawMonolithEdge(tbl, tfl, edgeB);
        DrawMonolithEdge(bfl, tfl, edgeB);
        DrawMonolithEdge(bfr, tfr, edgeA);
        DrawMonolithEdge(bbr, tbr, edgeB);
        DrawMonolithEdge(bbl, tbl, edgeA);
        DrawMonolithLightStreams(classicNeon ? Palette.C(25, 230, 255) : Opaque(Theme.BuildingLine), bfl, bfr, tfr, tfl, bfr, bbr, tbr, tfr, bbr, bbl, tbl, tbr, bbl, bfl, tfl, tbl);
    }

    private static void DrawQuad3D(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
    {
        Raylib.DrawTriangle3D(a, b, c, color);
        Raylib.DrawTriangle3D(a, c, d, color);
    }

    private static void DrawSolidQuad3D(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
    {
        DrawQuad3D(a, b, c, d, color);
        DrawQuad3D(d, c, b, a, color);
    }

    private static void DrawMonolithEdge(Vector3 from, Vector3 to, Color color)
    {
        Raylib.DrawLine3D(from, to, color);
        Raylib.DrawLine3D(from + new Vector3(0.015f, 0f, 0.015f), to + new Vector3(0.015f, 0f, 0.015f), WithAlpha(color, 0.45f));
    }

    private static void DrawMonolithLightStreams(Color trailColor, params Vector3[] facePoints)
    {
        var time = (float)Raylib.GetTime();
        for (var face = 0; face + 3 < facePoints.Length; face += 4)
        {
            DrawMonolithFaceLightStreams(facePoints[face], facePoints[face + 1], facePoints[face + 2], facePoints[face + 3], time, face / 4, trailColor);
        }
    }

    private static void DrawMonolithFaceLightStreams(Vector3 bottomLeft, Vector3 bottomRight, Vector3 topRight, Vector3 topLeft, float time, int faceIndex, Color trailColor)
    {
        var right = Vector3.Normalize(bottomRight - bottomLeft);
        var faceCenter = (bottomLeft + bottomRight + topRight + topLeft) * 0.25f;
        var faceNormal = Vector3.Normalize(Vector3.Cross(right, Vector3.UnitY));
        if (Vector3.Dot(faceNormal, faceCenter) < 0f) faceNormal = -faceNormal;

        for (var lane = 0; lane < 7; lane++)
        {
            var seed = faceIndex * 17.37f + lane * 5.91f;
            var u = 0.12f + MainMenuHash(seed) * 0.76f;
            var bottom = Vector3.Lerp(bottomLeft, bottomRight, u);
            var top = Vector3.Lerp(topLeft, topRight, u);
            var up = Vector3.Normalize(top - bottom);
            var end = 0.30f + MainMenuHash(seed + 1.7f) * 0.68f;
            var speed = 0.22f + MainMenuHash(seed + 3.1f) * 0.34f;
            var progress = (time * speed + MainMenuHash(seed + 5.4f)) % 1f;
            var fade = progress < 0.70f ? 1f : 1f - (progress - 0.70f) / 0.30f;
            var size = 0.055f + MainMenuHash(seed + 8.2f) * 0.045f;
            var streamProgress = progress * end;
            var core = Vector3.Lerp(bottom, top, streamProgress) + faceNormal * 0.028f;

            var trailLength = MathF.Min(streamProgress, 0.16f + MainMenuHash(seed + 11.4f) * 0.12f);
            if (trailLength > 0.01f)
            {
                DrawSurfaceTrail(bottom, top, right, up, faceNormal, streamProgress, trailLength, size * 0.72f, fade, trailColor);
            }

            DrawSurfaceSquare(core + faceNormal * 0.004f, right, up, size, WithAlpha(Color.White, fade));
        }
    }

    private static float MainMenuHash(float value)
    {
        var hash = MathF.Sin(value * 12.9898f + 78.233f) * 43758.5453f;
        hash -= MathF.Floor(hash);
        return hash;
    }

    private static void DrawSurfaceSquare(Vector3 center, Vector3 right, Vector3 up, float size, Color color)
        => DrawSurfaceRect(center, right, up, size, size, color);

    private static void DrawSurfaceTrail(Vector3 bottom, Vector3 top, Vector3 right, Vector3 up, Vector3 faceNormal, float streamProgress, float trailLength, float width, float fade, Color trailColor)
    {
        const int segments = 70;
        var height = Vector3.Distance(bottom, top);
        for (var i = 0; i < segments; i++)
        {
            var near = streamProgress - trailLength * (i / (float)segments);
            var far = streamProgress - trailLength * ((i + 1) / (float)segments);
            if (near <= 0f) break;

            far = MathF.Max(0f, far);
            var centerProgress = (near + far) * 0.5f;
            var segmentLength = MathF.Max(0.001f, near - far);
            var alpha = fade * 0.74f * (1f - i / (float)segments);
            var center = Vector3.Lerp(bottom, top, centerProgress) + faceNormal * 0.03f;
            DrawSurfaceRect(center, right, up, width, segmentLength * height, WithAlpha(trailColor, alpha));
        }
    }

    private static void DrawSurfaceRect(Vector3 center, Vector3 right, Vector3 up, float width, float height, Color color)
    {
        var halfRight = right * (width * 0.5f);
        var halfUp = up * (height * 0.5f);
        DrawSolidQuad3D(center - halfRight - halfUp, center + halfRight - halfUp, center + halfRight + halfUp, center - halfRight + halfUp, color);
    }

    private static void DrawMainMenuVignette()
    {
        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();
        Raylib.DrawRectangleGradientV(0, 0, width, height / 3, Palette.C(0, 0, 0, 80), Palette.C(0, 0, 0, 0));
        Raylib.DrawRectangleGradientV(0, height - height / 3, width, height / 3, Palette.C(0, 0, 0, 0), Palette.C(0, 0, 0, 125));
        Raylib.DrawRectangle(0, 0, width, height, Palette.C(0, 0, 0, 35));
    }

    private void DrawTerminalPanel()
    {
        if (!_terminalOpen || _securedTerminalZone is null) return;

        Raylib.DrawRectangle(0, 0, GetUiScreenWidth(), GetUiScreenHeight(), Palette.C(0, 0, 0, 170));

        var panel = TerminalPanelRect();
        var screen = TerminalScreenRect();
        var input = TerminalInputRect();
        var unlocked = _securedTerminalZone.Unlocked;

        Raylib.DrawRectangleRec(panel, Palette.C(10, 12, 18, 242));
        Raylib.DrawRectangleLinesEx(panel, 2f, Palette.C(120, 130, 145));

        Raylib.DrawRectangleRec(screen, unlocked ? Palette.C(52, 104, 64, 235) : Palette.C(122, 46, 50, 235));
        Raylib.DrawRectangleLinesEx(screen, 2f, unlocked ? Palette.C(110, 245, 140) : Palette.C(245, 120, 125));
        var screenText = string.IsNullOrEmpty(_terminalScreenText) ? "ACCESS DENIED" : _terminalScreenText;
        var screenFont = screenText.Length > 18 ? 20 : 28;
        Raylib.DrawText(screenText, (int)(screen.X + screen.Width / 2f - Raylib.MeasureText(screenText, screenFont) / 2f), (int)(screen.Y + screen.Height / 2f - screenFont / 2f), screenFont, unlocked ? Palette.C(190, 255, 200) : Palette.C(255, 190, 195));

        Raylib.DrawRectangleRec(input, Palette.C(20, 24, 32, 255));
        Raylib.DrawRectangleLinesEx(input, 2f, Palette.C(82, 92, 110));
        var shownInput = unlocked ? "------" : _terminalInput.PadRight(6, '_');
        Raylib.DrawText(shownInput, (int)(input.X + input.Width / 2f - Raylib.MeasureText(shownInput, 28) / 2f), (int)input.Y + 6, 28, Color.White);

        for (var digit = 1; digit <= 9; digit++)
        {
            DrawButton(TerminalDigitButtonRect(digit), digit.ToString(), !unlocked);
        }

        DrawTerminalActionButton(TerminalDeleteButtonRect(), "DEL", Palette.C(132, 42, 46), Palette.C(245, 110, 115), !unlocked);
        DrawButton(TerminalDigitButtonRect(0), "0", !unlocked);
        DrawTerminalActionButton(TerminalEnterButtonRect(), "OK", Palette.C(44, 118, 62), Palette.C(115, 245, 140), !unlocked);
    }

    private static void DrawTerminalActionButton(Rectangle rect, string text, Color fill, Color line, bool enabled)
    {
        var hover = enabled && Raylib.CheckCollisionPointRec(GetUiMousePosition(), rect);
        Raylib.DrawRectangleRec(rect, enabled ? (hover ? Mix(fill, Color.White, 0.18f) : fill) : Palette.C(34, 38, 48));
        DrawButtonPulseFill(rect, line, hover);
        Raylib.DrawRectangleLinesEx(rect, 2f, enabled ? line : Color.DarkGray);
        const int fs = 22;
        Raylib.DrawText(text, (int)(rect.X + rect.Width / 2f - Raylib.MeasureText(text, fs) / 2f), (int)(rect.Y + rect.Height / 2f - fs / 2f), fs, enabled ? Color.White : Color.Gray);
    }

    private void DrawTerminalNotePopup()
    {
        if (_openTerminalNoteIndex is not int noteIndex) return;
        var note = _terminalNotes.FirstOrDefault(n => n.Index == noteIndex);
        if (note is null) return;

        Raylib.DrawRectangle(0, 0, GetUiScreenWidth(), GetUiScreenHeight(), Palette.C(0, 0, 0, 150));

        var panel = TerminalNotePanelRect();
        Raylib.DrawRectangleRec(panel, Palette.C(20, 18, 16, 245));
        Raylib.DrawRectangleLinesEx(panel, 2f, Palette.C(230, 190, 80));
        Raylib.DrawText("DATA NOTE", (int)panel.X + 26, (int)panel.Y + 20, 26, Color.White);

        var codeFont = 42;
        Raylib.DrawText(note.Text, (int)(panel.X + panel.Width / 2f - Raylib.MeasureText(note.Text, codeFont) / 2f), (int)panel.Y + 82, codeFont, Palette.C(235, 205, 110));
        DrawButton(TerminalNoteCloseButtonRect(), "Close");
    }

    private void DrawCodesPopup()
    {
        Raylib.DrawRectangle(0, 0, GetUiScreenWidth(), GetUiScreenHeight(), Palette.C(0, 0, 0, 165));

        var popup = CodesPopupRect();
        var input = CodesPopupInputRect();
        var apply = CodesPopupApplyRect();
        var close = CodesPopupCloseRect();

        Raylib.DrawRectangleRec(popup, Palette.C(10, 18, 30, 240));
        Raylib.DrawRectangleLinesEx(popup, 2f, Palette.C(108, 170, 228));
        Raylib.DrawText("Codes", (int)popup.X + 24, (int)popup.Y + 22, 28, Color.White);
        Raylib.DrawText("Enter a promo code", (int)popup.X + 30, (int)popup.Y + 58, 20, Color.LightGray);

        Raylib.DrawRectangleRec(input, Palette.C(18, 26, 40, 255));
        Raylib.DrawRectangleLinesEx(input, 2f, Color.Black);
        var shownInput = string.IsNullOrEmpty(_codeInput) ? "CODE" : _codeInput;
        var inputColor = string.IsNullOrEmpty(_codeInput) ? Palette.C(130, 145, 168) : Color.White;
        Raylib.DrawText(shownInput, (int)input.X + 14, (int)input.Y + 10, 24, inputColor);

        DrawButton(apply, "Apply");

        Raylib.DrawRectangleRec(close, Palette.C(36, 56, 90));
        Raylib.DrawRectangleLinesEx(close, 2f, Color.White);
        Raylib.DrawText("X", (int)close.X + 9, (int)close.Y + 4, 22, Color.White);

        if (!string.IsNullOrEmpty(_codeStatusText))
        {
            var statusColor = _codeStatusSuccess ? Palette.C(120, 220, 140) : Palette.C(255, 120, 120);
            Raylib.DrawText(_codeStatusText, (int)popup.X + 30, (int)popup.Y + 146, 20, statusColor);
        }
    }

    private void DrawAboutPopup()
    {
        Raylib.DrawRectangle(0, 0, GetUiScreenWidth(), GetUiScreenHeight(), Palette.C(0, 0, 0, 165));

        var popup = AboutPopupRect();
        var close = AboutPopupCloseRect();

        Raylib.DrawRectangleRec(popup, Palette.C(10, 18, 30, 242));
        Raylib.DrawRectangleLinesEx(popup, 2f, Palette.C(108, 170, 228));
        Raylib.DrawText("About", (int)popup.X + 24, (int)popup.Y + 22, 30, Color.White);

        Raylib.DrawRectangleRec(close, Palette.C(36, 56, 90));
        Raylib.DrawRectangleLinesEx(close, 2f, Color.White);
        Raylib.DrawText("X", (int)close.X + 9, (int)close.Y + 4, 22, Color.White);

        var lines = new[]
        {
            "Developer:",
            "Rambros",
            "",
            "Tester:",
            "Yukii(yukendoze)",
            "",
            "Contributors:",
            "Inferlas, Maks1mka, Jinko, BlackyCGS",
            "",
            "Special thanks to my nerve cells",
            "for letting this become more than just an idea.",
            "",
            "Bungus will keep getting better."
        };

        const int fontSize = 22;
        const int lineStep = 30;
        var totalHeight = lines.Length * lineStep;
        var y = popup.Y + popup.Height / 2f - totalHeight / 2f + 8f;
        foreach (var line in lines)
        {
            var width = Raylib.MeasureText(line, fontSize);
            Raylib.DrawText(line, (int)(popup.X + popup.Width / 2f - width / 2f), (int)y, fontSize, string.IsNullOrEmpty(line) ? Color.White : Color.LightGray);
            y += lineStep;
        }
    }

    private void DrawChangelogPopup()
    {
        Raylib.DrawRectangle(0, 0, GetUiScreenWidth(), GetUiScreenHeight(), Palette.C(0, 0, 0, 175));

        var popup = ChangelogPopupRect();
        var content = ChangelogContentRect();
        var close = ChangelogPopupCloseRect();

        Raylib.DrawRectangleRec(popup, Palette.C(10, 18, 30, 248));
        Raylib.DrawRectangleLinesEx(popup, 2f, Palette.C(108, 170, 228));
        Raylib.DrawText("Changelog", (int)popup.X + 28, (int)popup.Y + 22, 32, Color.White);

        Raylib.DrawRectangleRec(close, Palette.C(36, 56, 90));
        Raylib.DrawRectangleLinesEx(close, 2f, Color.White);
        Raylib.DrawText("X", (int)close.X + 9, (int)close.Y + 4, 22, Color.White);

        Raylib.DrawRectangleRec(content, Palette.C(6, 12, 22, 235));
        Raylib.DrawRectangleLinesEx(content, 1f, Palette.C(68, 104, 145));

        BeginUiScissor(content);
        const float lineStep = 27f;
        var y = content.Y + 10f - _changelogScroll;
        foreach (var line in _changelogLines)
        {
            if (y + lineStep >= content.Y && y <= content.Y + content.Height)
            {
                var color = line.Version ? Palette.C(110, 210, 255) : Color.LightGray;
                Raylib.DrawText(line.Text, (int)content.X + 14, (int)y, 20, color);
            }
            y += lineStep;
        }
        Raylib.EndScissorMode();

        var totalHeight = _changelogLines.Count * lineStep;
        if (totalHeight > content.Height)
        {
            var track = new Rectangle(content.X + content.Width + 9f, content.Y, 7f, content.Height);
            var thumbHeight = MathF.Max(44f, track.Height * content.Height / totalHeight);
            var maxScroll = totalHeight - content.Height + 12f;
            var thumbY = track.Y + (track.Height - thumbHeight) * (_changelogScroll / MathF.Max(1f, maxScroll));
            Raylib.DrawRectangleRec(track, Palette.C(18, 28, 44));
            Raylib.DrawRectangleRec(new Rectangle(track.X, thumbY, track.Width, thumbHeight), Palette.C(108, 170, 228));
        }
    }

    private static void BeginUiScissor(Rectangle rect)
    {
        var scale = GetUiScale();
        var offset = GetUiOffset();
        Raylib.BeginScissorMode(
            (int)(offset.X + rect.X * scale),
            (int)(offset.Y + rect.Y * scale),
            Math.Max(1, (int)(rect.Width * scale)),
            Math.Max(1, (int)(rect.Height * scale)));
    }

    private void DrawMapSelect()
    {
        DrawTitle(_deploymentListMode == DeploymentListMode.Expeditions ? "Expeditions" : "Challenges", 64, 66);
        Raylib.DrawText(_deploymentListMode == DeploymentListMode.Expeditions ? "Choose your landing zone" : "Choose a trial", 72, 118, 26, Color.LightGray);
        DrawButton(DeploymentToggleRect(), _deploymentListMode == DeploymentListMode.Expeditions ? "Challenges" : "Expeditions");

        if (_deploymentListMode == DeploymentListMode.Challenges)
        {
            DrawChallengeCard(MapCardRect(0), "Pit", "Wave survival trial", false, 0);
            DrawChallengeCard(MapCardRect(1), "Pit (Nightmare)", "Bring your own gear", true, 1);
        }
        else
        {
            for (var i = 0; i < MapDefinition.All.Length; i++)
            {
                DrawMapCard(MapDefinition.All[i], MapCardRect(i));
            }
        }

        DrawButton(MapSelectBackButtonRect(), "Back");
        if (_pitNightmareInfoOpen) DrawPitNightmareInfoPopup();
    }

    private void DrawChallengeCard(Rectangle card, string title, string subtitle, bool nightmare, int index)
    {
        var hover = Raylib.CheckCollisionPointRec(GetUiMousePosition(), card);
        Raylib.DrawRectangleRec(card, hover ? Palette.C(36, 30, 56) : Palette.C(18, 16, 34));
        Raylib.DrawRectangleLinesEx(card, 2f, nightmare ? Palette.C(210, 90, 120) : Palette.C(191, 120, 255));
        if (hover) DrawHoverOrbitFrame(card, nightmare ? Palette.C(255, 104, 140) : Palette.C(210, 150, 255));

        var arena = new Rectangle(card.X + 28, card.Y + 28, card.Width - 56, 128);
        Raylib.DrawRectangleRec(arena, Palette.C(22, 24, 34));
        Raylib.DrawCircleGradient((int)(arena.X + arena.Width * 0.5f), (int)(arena.Y + arena.Height * 0.5f), 64, nightmare ? Palette.C(255, 80, 110, 120) : Palette.C(150, 90, 255, 120), Palette.C(40, 20, 80, 20));
        Raylib.DrawRectangleLinesEx(new Rectangle(arena.X + 36, arena.Y + 22, arena.Width - 72, arena.Height - 44), 3f, nightmare ? Palette.C(180, 55, 80) : Palette.C(120, 80, 200));

        Raylib.DrawText(title, (int)card.X + 42, (int)card.Y + 174, 36, Color.White);
        Raylib.DrawText(subtitle, (int)card.X + 42, (int)card.Y + 220, 22, Color.LightGray);
        if (!nightmare) return;

        var info = ChallengeInfoButtonRect(index);
        Raylib.DrawCircle((int)(info.X + info.Width * 0.5f), (int)(info.Y + info.Height * 0.5f), info.Width * 0.5f, Palette.C(28, 34, 48));
        Raylib.DrawCircleLines((int)(info.X + info.Width * 0.5f), (int)(info.Y + info.Height * 0.5f), info.Width * 0.5f, Color.White);
        Raylib.DrawText("i", (int)info.X + 11, (int)info.Y + 4, 24, Color.White);
    }

    private void DrawPitNightmareInfoPopup()
    {
        var popup = new Rectangle((GetUiScreenWidth() - 620) / 2f, 230, 620, 360);
        Raylib.DrawRectangle(0, 0, GetUiScreenWidth(), GetUiScreenHeight(), Palette.C(0, 0, 0, 145));
        Raylib.DrawRectangleRec(popup, Palette.C(14, 18, 30, 245));
        Raylib.DrawRectangleLinesEx(popup, 2f, Palette.C(210, 90, 120));
        Raylib.DrawText("Pit (Nightmare)", (int)popup.X + 30, (int)popup.Y + 26, 34, Color.White);

        var lines = new[]
        {
            "Enter only with your own equipment",
            "Equipment roulettes are disabled",
            "Enemy speed +50%",
            "Enemy health +25%",
            "Every 3 waves: difficulty modifier",
            "CryptoTokens for every 10 completed waves"
        };

        for (var i = 0; i < lines.Length; i++)
        {
            Raylib.DrawText(lines[i], (int)popup.X + 38, (int)popup.Y + 92 + i * 34, 22, Color.LightGray);
        }

        DrawButton(PitNightmareInfoCloseRect(), "Ok");
    }

    private void DrawMapCard(MapDefinition map, Rectangle card)
    {
        var hover = Raylib.CheckCollisionPointRec(GetUiMousePosition(), card);
        Raylib.DrawRectangleRec(card, hover ? Palette.C(22, 40, 62) : Palette.C(14, 24, 40));
        Raylib.DrawRectangleLinesEx(card, 2f, Palette.C(116, 180, 235));
        if (hover) DrawHoverOrbitFrame(card, map.IsDeadZone ? Palette.C(120, 255, 150) : Palette.C(170, 220, 255));

        var inner = new Rectangle(card.X + 8, card.Y + 8, card.Width - 16, card.Height - 16);
        DrawMapCardScene(map, inner);

        Raylib.DrawText(map.Name, (int)card.X + 34, (int)card.Y + 40, 36, Color.White);
        DrawDifficultySkulls(map.Difficulty, new Vector2(card.X + 42, card.Y + 92));
        Raylib.DrawText("Click to deploy", (int)(card.X + card.Width - 206), (int)(card.Y + card.Height - 72), 22, Palette.C(170, 220, 255));
    }

    private static void DrawMapCardScene(MapDefinition map, Rectangle rect)
    {
        var top = map.IsDeadZone ? Palette.C(8, 20, 22) : Palette.C(16, 28, 44);
        var bottom = map.IsDeadZone ? Palette.C(22, 42, 36) : Palette.C(31, 34, 58);
        var glow = map.IsDeadZone ? Palette.C(82, 235, 130, 72) : Palette.C(255, 150, 98, 70);
        var ground = map.IsDeadZone ? Palette.C(10, 24, 18) : Palette.C(33, 10, 42);
        var skyline = map.IsDeadZone ? Palette.C(4, 14, 16) : Palette.C(8, 7, 31);

        Raylib.DrawRectangleGradientV((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height, top, bottom);
        Raylib.DrawCircleGradient((int)(rect.X + rect.Width * 0.52f), (int)(rect.Y + rect.Height * 0.42f), rect.Width * 0.42f, glow, Palette.C(glow.R, glow.G, glow.B, 0));

        var horizon = rect.Y + rect.Height * 0.58f;
        DrawMapCitySkyline(rect, horizon, skyline, map.IsDeadZone);
        Raylib.DrawRectangle((int)rect.X, (int)horizon, (int)rect.Width, (int)(rect.Y + rect.Height - horizon), ground);
    }

    private static void DrawMapCitySkyline(Rectangle rect, float groundY, Color color, bool deadZone)
    {
        var buildings = deadZone
            ? new (float X, float W, float H)[]
            {
                (0.06f, 0.08f, 0.10f), (0.18f, 0.05f, 0.18f), (0.26f, 0.11f, 0.13f),
                (0.39f, 0.07f, 0.22f), (0.50f, 0.16f, 0.15f), (0.70f, 0.18f, 0.12f)
            }
            : new (float X, float W, float H)[]
            {
                (0.08f, 0.07f, 0.09f), (0.18f, 0.05f, 0.17f), (0.26f, 0.10f, 0.13f),
                (0.36f, 0.07f, 0.24f), (0.44f, 0.20f, 0.17f), (0.67f, 0.25f, 0.12f)
            };

        foreach (var b in buildings)
        {
            var x = rect.X + rect.Width * b.X;
            var w = rect.Width * b.W;
            var h = rect.Height * b.H;
            Raylib.DrawRectangle((int)x, (int)(groundY - h), (int)w, (int)h, color);
        }
    }

    private static void DrawDifficultySkulls(int count, Vector2 origin)
    {
        for (var i = 0; i < count; i++)
        {
            var x = origin.X + i * 26f;
            Raylib.DrawCircle((int)x + 10, (int)origin.Y + 10, 10f, Palette.C(230, 230, 230));
            Raylib.DrawCircle((int)x + 6, (int)origin.Y + 8, 2.5f, Color.Black);
            Raylib.DrawCircle((int)x + 14, (int)origin.Y + 8, 2.5f, Color.Black);
            Raylib.DrawRectangle((int)x + 5, (int)origin.Y + 17, 10, 5, Palette.C(230, 230, 230));
        }
    }

    private void DrawStorage()
    {
        var previewPlayer = CreateLandingPreviewPlayer();
        DrawTitle("Storage", 48, 56);
        Raylib.DrawText("Equip items here before deployment. Extracted loot returns to this stash.", 70, 106, 24, Color.LightGray);
        Raylib.DrawText($"Capacity {GetStoredItemCount()}/{MetaProfile.StorageCapacity}", 70, 138, 22, Color.White);
        DrawSynthCoinsCounter(24, 138, 22);
        Raylib.DrawText("Shift+click selects items. Hold X on a selected item to sell selected.", 850, 800, 20, Color.LightGray);

        Raylib.DrawRectangle(40, 190, 300, 600, MenuPanelFill());
        Raylib.DrawRectangleLinesEx(new Rectangle(40, 190, 300, 600), 2f, MenuPanelLine());
        Raylib.DrawText("Loadout", 72, 164, 24, Color.White);
        Raylib.DrawText("Armor", 72, 240, 18, Color.LightGray);
        Raylib.DrawText("Primary", 72, 340, 18, Color.LightGray);
        Raylib.DrawText("Heavy", 72, 440, 18, Color.LightGray);
        Raylib.DrawText("Melee", 72, 540, 18, Color.LightGray);
        Raylib.DrawText("Consumables", 72, 640, 18, Color.LightGray);

        var runBackpackPanel = new Rectangle(400, 190, 460, 550);
        Raylib.DrawRectangleRec(runBackpackPanel, MenuPanelFill());
        Raylib.DrawRectangleLinesEx(runBackpackPanel, 2f, MenuPanelLine());
        Raylib.DrawText("Run Backpack", 410, 164, 24, Color.White);
        DrawStorageGrid(new Vector2(410, 200), 5, 6);

        var stashPanel = StashPanelRect();
        Raylib.DrawRectangleRec(stashPanel, MenuPanelFill());
        Raylib.DrawRectangleLinesEx(stashPanel, 2f, MenuPanelLine());
        Raylib.DrawText("Stash", 900, 164, 24, Color.White);
        var firstVisible = _storageScrollRow * StashGridColumns + 1;
        var lastVisible = Math.Min(_meta.StorageSlots.Count, (_storageScrollRow + StashVisibleRows) * StashGridColumns);
        Raylib.DrawText($"{firstVisible}-{lastVisible}", (int)stashPanel.X + 178, 112, 18, Color.LightGray);
        DrawStorageGrid(new Vector2(910, 200), StashGridColumns, StashVisibleRows);
        DrawStashScrollBar(stashPanel);
        DrawStorageSortButtons();

        DrawButton(StorageBackButtonRect(), "Back");

        var slots = BuildStorageSlots();
        var comparison = new ComparisonContext(previewPlayer, _meta.Armor, _meta.RangedWeapon, _meta.HeavyWeapon, _meta.MeleeWeapon);
        var mouse = GetUiMousePosition();
        foreach (var slot in slots)
        {
            Raylib.DrawRectangleRec(slot.Rect, Palette.C(22, 28, 42, 255));
            Raylib.DrawRectangleLinesEx(slot.Rect, 1f, Color.SkyBlue);
            if (slot.Kind == SlotKind.QuickSlotQ) Raylib.DrawText("Q", (int)slot.Rect.X + 15, (int)slot.Rect.Y - 18, 16, Color.Green);
            if (slot.Kind == SlotKind.QuickSlotR) Raylib.DrawText("R", (int)slot.Rect.X + 15, (int)slot.Rect.Y - 18, 16, Color.Yellow);
            if (slot.Item is not null)
            {
                var iconRect = new Rectangle(slot.Rect.X + UiIconPadding, slot.Rect.Y + UiIconPadding, slot.Rect.Width - UiIconPadding * 2f, slot.Rect.Height - UiIconPadding * 2f);
                DrawItemIcon(slot.Item, iconRect, comparison, slot.Kind);
                DrawInventoryUseHoldFrame(slot, iconRect);
                if (slot.Kind == SlotKind.Storage && _storageSortMode > 0 && !IsStorageSortModeMatch(slot.Item, _storageSortMode))
                {
                    Raylib.DrawRectangleRec(slot.Rect, Palette.C(0, 0, 0, 165));
                }
            }
            if (slot.Item is not null && _selectedStorageSlots.Contains((slot.Kind, slot.Index)))
            {
                DrawStorageSelectionFrame(slot.Rect, slot.Item.Color);
            }
            if (slot.Item is not null && Raylib.CheckCollisionPointRec(mouse, slot.Rect)) DrawHoverOrbitFrame(slot.Rect, slot.Item.Color);
        }
        DrawArmorModifierDiamondsForSlots(slots);

        if (_drag is not null)
        {
            var m = GetUiMousePosition();
            var dragRect = new Rectangle(m.X + 8, m.Y + 8, UiSlotSize, UiSlotSize);
            DrawItemIcon(_drag.Item, dragRect, comparison, _drag.Kind);
            DrawArmorModifierDiamonds(_drag.Item, dragRect);
        }

        if (_hovered is not null) DrawTooltip(_hovered, GetUiMousePosition(), comparison);
    }

    private void DrawArmory()
    {
        var previewPlayer = CreateLandingPreviewPlayer();
        var comparison = new ComparisonContext(previewPlayer, _meta.Armor, _meta.RangedWeapon, _meta.HeavyWeapon, _meta.MeleeWeapon);
        DrawTitle("Armory", 48, 56);
        Raylib.DrawText("Buy equipment. Stock refreshes after each run.", 70, 106, 24, Color.LightGray);
        DrawSynthCoinsCounter(70, 138, 24);
        DrawCryptoTokensCounter(70, 164, 24);

        for (var i = 0; i < _meta.TokenStoreOffers.Count; i++)
        {
            DrawTokenStoreOffer(_meta.TokenStoreOffers[i], TokenStoreOfferRect(i), comparison);
        }

        for (var i = 0; i < _meta.ArmoryOffers.Count; i++)
        {
            DrawArmoryOffer(_meta.ArmoryOffers[i], ArmoryOfferRect(i), comparison);
        }

        DrawButton(ArmoryBackButtonRect(), "Back");

        if (_hovered is not null) DrawTooltip(_hovered, GetUiMousePosition(), comparison);
    }

    private void DrawArmoryOffer(ArmoryOffer offer, Rectangle rect, ComparisonContext comparison)
    {
        var disabled = offer.Purchased && !offer.Item.IsHeavyAmmo;
        var border = offer.Item.IsHeavyAmmo ? Palette.C(120, 210, 255) : offer.Item.Rarity == ArmorRarity.Epic ? Palette.C(191, 120, 255) : Color.SkyBlue;
        DrawStoreCardBackground(rect, disabled, border, MenuPanelFill(0.90f));

        var iconRect = new Rectangle(rect.X + 10, rect.Y + 10, rect.Width - 20, rect.Height - 20);
        DrawItemIcon(offer.Item, iconRect, comparison);
        DrawArmorModifierDiamonds(offer.Item, iconRect);
        if (Raylib.CheckCollisionPointRec(GetUiMousePosition(), rect)) DrawHoverOrbitFrame(rect, border);

        DrawStorePrice(rect, $"{GetArmoryPrice(offer.Item)} SC", Palette.C(120, 230, 255));
        if (disabled) DrawStoreDisabledOverlay(rect);
    }

    private void DrawTokenStoreOffer(TokenStoreOffer offer, Rectangle rect, ComparisonContext comparison)
    {
        var disabled = offer.Purchased;
        var border = Palette.C(210, 150, 255);
        DrawStoreCardBackground(rect, disabled, border, MenuPanelAltFill(0.92f));

        var iconRect = new Rectangle(rect.X + 10, rect.Y + 10, rect.Width - 20, rect.Height - 20);
        DrawItemIcon(offer.Item, iconRect, comparison);
        DrawArmorModifierDiamonds(offer.Item, iconRect);
        if (Raylib.CheckCollisionPointRec(GetUiMousePosition(), rect)) DrawHoverOrbitFrame(rect, border);

        var price = GetTokenStorePrice(offer);
        DrawStorePrice(rect, $"{price} CT", Palette.C(210, 150, 255));
        if (disabled) DrawStoreDisabledOverlay(rect);
    }

    private void DrawPitNightmareModifiers()
    {
        var x = GetUiScreenWidth() - 220;
        var y = 80;
        Raylib.DrawText($"Enemy damage: +{_pitNightmareDamageBonusPercent:0}%", x, y, 20, Palette.C(255, 145, 145));
        Raylib.DrawText($"Enemy health: +{_pitNightmareHealthBonusPercent:0}%", x, y + 26, 20, Palette.C(160, 255, 170));
        Raylib.DrawText($"Enemy speed: +{_pitNightmareSpeedBonusPercent:0}%", x, y + 52, 20, Palette.C(150, 210, 255));
    }

    private void DrawPitDifficultySelection()
    {
        var ready = _pitDifficultySpinElapsed >= PitDifficultySpinDuration;
        var panel = PitDifficultyPanelRect();
        Raylib.DrawRectangle(0, 0, GetUiScreenWidth(), GetUiScreenHeight(), Palette.C(0, 0, 0, 170));
        Raylib.DrawRectangleRec(panel, Palette.C(10, 16, 28, 245));
        Raylib.DrawRectangleLinesEx(panel, 2f, Palette.C(210, 90, 120));
        Raylib.DrawText("Nightmare modifier", (int)panel.X + 32, (int)panel.Y + 28, 34, Color.White);
        if (ready) Raylib.DrawText("The next difficulty modifier is active.", (int)panel.X + 32, (int)panel.Y + 72, 22, Color.LightGray);

        var card = PitDifficultyCardRect();
        Raylib.DrawRectangleRec(card, Palette.C(18, 26, 42, 245));
        Raylib.DrawRectangleLinesEx(card, 2f, Color.LightGray);
        Raylib.DrawText("Modifier", (int)card.X + 18, (int)card.Y + 18, 24, Color.White);
        DrawPitDifficultyRoulette(card);

        if (ready)
        {
            var result = FormatPitDifficultyOffer(_pitDifficultyOffer);
            Raylib.DrawText(result, (int)card.X + 18, (int)card.Y + 56, 24, GetPitDifficultyColor(_pitDifficultyOffer.Kind));
        }

        DrawButton(PitDifficultyOkButtonRect(), "Ok", ready);
    }

    private void DrawPitDifficultyRoulette(Rectangle card)
    {
        var reel = new Rectangle(card.X + 150, card.Y + 21, 640, 62);
        Raylib.DrawRectangleRec(reel, Palette.C(6, 10, 18, 245));
        Raylib.DrawRectangleLinesEx(reel, 2f, Palette.C(80, 120, 190));
        Raylib.DrawRectangle((int)(reel.X + reel.Width / 2f - 30), (int)reel.Y, 60, (int)reel.Height, Palette.C(255, 255, 255, 24));
        Raylib.DrawLine((int)(reel.X + reel.Width / 2f), (int)reel.Y, (int)(reel.X + reel.Width / 2f), (int)(reel.Y + reel.Height), Palette.C(255, 255, 255, 70));

        if (_pitDifficultyRouletteItems.Count == 0) return;

        const float iconSize = 42f;
        const float step = 58f;
        var stopped = _pitDifficultySpinElapsed >= PitDifficultySpinDuration;
        var spinProgress = Math.Clamp(_pitDifficultySpinElapsed / PitDifficultySpinDuration, 0f, 1f);
        var easedProgress = 1f - MathF.Pow(1f - spinProgress, 3f);
        var totalSteps = 4 * _pitDifficultyRouletteItems.Count + _pitDifficultyRouletteItems.Count - 1;
        var reelPosition = totalSteps * easedProgress;
        var centerIndex = (int)MathF.Floor(reelPosition) % _pitDifficultyRouletteItems.Count;
        var offset = (reelPosition - MathF.Floor(reelPosition)) * step;

        Raylib.BeginScissorMode((int)reel.X, (int)reel.Y, (int)reel.Width, (int)reel.Height);
        for (var slot = -7; slot <= 7; slot++)
        {
            var itemIndex = (centerIndex + slot + _pitDifficultyRouletteItems.Count) % _pitDifficultyRouletteItems.Count;
            var offer = _pitDifficultyRouletteItems[itemIndex];
            var x = reel.X + reel.Width / 2f - iconSize / 2f + slot * step - offset;
            var y = reel.Y + reel.Height / 2f - iconSize / 2f;
            var distanceFromCenter = MathF.Abs(x + iconSize / 2f - (reel.X + reel.Width / 2f));
            var alpha = Math.Clamp(1f - distanceFromCenter / 330f, 0.25f, 1f);
            var rect = new Rectangle(x - 5, y - 5, iconSize + 10, iconSize + 10);
            var color = GetPitDifficultyColor(offer.Kind);

            Raylib.DrawRectangleRec(rect, Palette.C(14, 20, 34, (int)(220 * alpha)));
            Raylib.DrawRectangleLinesEx(rect, 2f, color);
            Raylib.DrawText(offer.Kind.ToString(), (int)x + 11, (int)y + 7, 28, color);
            if (stopped && slot == 0)
            {
                Raylib.DrawRectangleLinesEx(new Rectangle(x - 4, y - 4, iconSize + 8, iconSize + 8), 3f, Color.White);
            }
        }
        Raylib.EndScissorMode();
    }

    private static string FormatPitDifficultyOffer(PitDifficultyOffer offer)
        => offer.Kind switch
        {
            'D' => $"Damage +{offer.Percent:0}%",
            'H' => $"Health +{offer.Percent:0}%",
            _ => $"Speed +{offer.Percent:0}%"
        };

    private static Color GetPitDifficultyColor(char kind)
        => kind switch
        {
            'D' => Palette.C(255, 120, 120),
            'H' => Palette.C(140, 255, 160),
            _ => Palette.C(130, 210, 255)
        };

    private static void DrawStoreCardBackground(Rectangle rect, bool disabled, Color border, Color fill)
    {
        Raylib.DrawRectangleRec(rect, disabled ? Palette.C(18, 20, 26, 190) : fill);
        Raylib.DrawRectangleLinesEx(rect, 2f, disabled ? Color.DarkGray : border);
    }

    private static void DrawStorePrice(Rectangle rect, string text, Color color)
    {
        const int fontSize = 16;
        var textWidth = Raylib.MeasureText(text, fontSize);
        var x = (int)(rect.X + rect.Width - textWidth - 7);
        var y = (int)rect.Y + 6;
        Raylib.DrawRectangle(x - 4, y - 3, textWidth + 8, fontSize + 6, Palette.C(0, 0, 0, 205));
        Raylib.DrawText(text, x, y, fontSize, color);
    }

    private static void DrawStoreDisabledOverlay(Rectangle rect)
    {
        Raylib.DrawRectangleRec(rect, Palette.C(0, 0, 0, 120));
        Raylib.DrawRectangleLinesEx(rect, 2f, Color.DarkGray);
    }

    private void DrawPitRewardSelection()
    {
        var ready = PitRewardReady;
        var mouse = GetUiMousePosition();
        ItemStack? hoveredReward = null;
        var comparison = new ComparisonContext(_player, _player.Armor, _player.RangedWeapon, _player.HeavyWeapon, _player.MeleeWeapon);
        Raylib.DrawRectangle(0, 0, GetUiScreenWidth(), GetUiScreenHeight(), Palette.C(0, 0, 0, 170));
        var panel = PitRewardPanelRect();
        Raylib.DrawRectangleRec(panel, Palette.C(10, 16, 28, 245));
        Raylib.DrawRectangleLinesEx(panel, 2f, Palette.C(191, 120, 255));
        Raylib.DrawText("Wave reward", (int)panel.X + 32, (int)panel.Y + 28, 34, Color.White);
        if (ready) Raylib.DrawText("Choose up to four items, or skip the offer.", (int)panel.X + 32, (int)panel.Y + 72, 22, Color.LightGray);

        var labels = new[] { "Melee", "Primary", "Heavy", "Armor" };
        for (var i = 0; i < _pitRewardOffers.Count; i++)
        {
            var rect = PitRewardCardRect(i);
            var item = _pitRewardOffers[i];
            Raylib.DrawRectangleRec(rect, Palette.C(18, 26, 42, 245));
            Raylib.DrawRectangleLinesEx(rect, 2f, Color.LightGray);
            Raylib.DrawText(labels[i], (int)rect.X + 18, (int)rect.Y + 18, 24, Color.White);
            DrawPitRoulette(i, rect, comparison);

            if (_pitRewardSpinElapsed >= PitRewardSpinDurations[i])
            {
                Raylib.DrawText(item.Name, (int)rect.X + 18, (int)rect.Y + 52, 18, item.Color);
                var rarity = item.Rarity == ArmorRarity.Rare ? "Rare" : item.Rarity.ToString();
                Raylib.DrawText(rarity, (int)rect.X + 18, (int)rect.Y + 76, 16, Color.LightGray);
                var winningIconRect = PitRewardWinningIconRect(i);
                if (Raylib.CheckCollisionPointRec(mouse, winningIconRect))
                {
                    hoveredReward = item;
                    DrawHoverOrbitFrame(winningIconRect, item.Color);
                }
            }

            var claimEnabled = ready && !_pitRewardClaimed[i];
            DrawButton(PitRewardTakeButtonRect(i), _pitRewardClaimed[i] ? "Claimed" : "Claim", claimEnabled);
        }

        DrawButton(PitRewardSkipButtonRect(), "Skip", ready);
        if (hoveredReward is not null) DrawTooltip(hoveredReward, mouse, comparison);
    }

    private void DrawPitRoulette(int index, Rectangle card, ComparisonContext comparison)
    {
        var reel = new Rectangle(card.X + 150, card.Y + 21, 640, 62);
        Raylib.DrawRectangleRec(reel, Palette.C(6, 10, 18, 245));
        Raylib.DrawRectangleLinesEx(reel, 2f, Palette.C(80, 120, 190));
        Raylib.DrawRectangle((int)(reel.X + reel.Width / 2f - 30), (int)reel.Y, 60, (int)reel.Height, Palette.C(255, 255, 255, 24));
        Raylib.DrawLine((int)(reel.X + reel.Width / 2f), (int)reel.Y, (int)(reel.X + reel.Width / 2f), (int)(reel.Y + reel.Height), Palette.C(255, 255, 255, 70));

        if (index >= _pitRouletteItems.Count || _pitRouletteItems[index].Count == 0) return;

        var items = _pitRouletteItems[index];
        const float iconSize = 42f;
        const float step = 58f;
        var stopped = _pitRewardSpinElapsed >= PitRewardSpinDurations[index];
        var spinDuration = PitRewardSpinDurations[index];
        var spinProgress = Math.Clamp(_pitRewardSpinElapsed / spinDuration, 0f, 1f);
        var easedProgress = 1f - MathF.Pow(1f - spinProgress, 3f);
        var totalSteps = (3 + index) * items.Count + items.Count - 1;
        var reelPosition = totalSteps * easedProgress;
        var centerIndex = (int)MathF.Floor(reelPosition) % items.Count;
        var offset = (reelPosition - MathF.Floor(reelPosition)) * step;

        Raylib.BeginScissorMode((int)reel.X, (int)reel.Y, (int)reel.Width, (int)reel.Height);
        for (var slot = -7; slot <= 7; slot++)
        {
            var itemIndex = (centerIndex + slot + items.Count) % items.Count;
            var item = items[itemIndex];
            var x = reel.X + reel.Width / 2f - iconSize / 2f + slot * step - offset;
            var y = reel.Y + reel.Height / 2f - iconSize / 2f;
            var distanceFromCenter = MathF.Abs(x + iconSize / 2f - (reel.X + reel.Width / 2f));
            var alpha = Math.Clamp(1f - distanceFromCenter / 330f, 0.25f, 1f);

            Raylib.DrawRectangleRec(new Rectangle(x - 5, y - 5, iconSize + 10, iconSize + 10), Palette.C(14, 20, 34, (int)(220 * alpha)));
            var iconRect = new Rectangle(x, y, iconSize, iconSize);
            DrawItemIcon(item, iconRect, comparison);
            DrawArmorModifierDiamonds(item, iconRect, 5f, 3.3f, 8f);
            if (stopped && slot == 0)
            {
                Raylib.DrawRectangleLinesEx(new Rectangle(x - 4, y - 4, iconSize + 8, iconSize + 8), 3f, Color.White);
            }
        }
        Raylib.EndScissorMode();
    }

    private void DrawCradle()
    {
        var previewPlayer = CreateLandingPreviewPlayer();

        DrawTitle("Cradle", 56, 60);
        Raylib.DrawText("Account upgrades", 74, 126, 28, Color.LightGray);

        var statPanel = new Rectangle(70, 170, 430, 390);
        Raylib.DrawRectangleRec(statPanel, MenuPanelFill());
        Raylib.DrawRectangleLinesEx(statPanel, 2f, MenuPanelLine());
        Raylib.DrawText($"General level: {_meta.Level}", 96, 204, 26, Color.Gold);
        Raylib.DrawText($"Next level: {_meta.Score}/{GetMetaScoreRequired(_meta.Level)}", 96, 240, 22, Color.White);
        DrawCradleStatLine("HP", $"{previewPlayer.MaxHealth:0}", 96, 292, Palette.C(140, 220, 160));
        DrawCradleStatLine("Speed", $"x{previewPlayer.SpeedMultiplier:0.00}", 96, 324, Palette.C(170, 220, 255));
        DrawCradleStatLine("Ranged damage", $"+{_meta.CradleGunsmith * 0.4f:0.0}%", 96, 356, Palette.C(255, 180, 100));
        DrawCradleStatLine("Melee damage", $"+{_meta.CradleFighter * 0.4f:0.0}%", 96, 388, Palette.C(255, 180, 100));
        DrawCradleStatLine("Melee attack speed", $"+{_meta.CradleMeleeSpeed * 1.6f:0.0}%", 96, 420, Palette.C(150, 220, 255));
        DrawCradleStatLine("Dash recovery", $"+{_meta.CradleDashRecovery:0}%", 96, 452, Palette.C(150, 240, 170));
        DrawCradleStatLine("Stability", $"{_meta.CradleStability:0}%", 96, 484, Color.White);
        DrawCradleStatLine("Arcane", $"+{_meta.CradleArcane:0}%", 96, 516, Palette.C(235, 85, 85));

        var freeRect = new Rectangle(1322, 74, 170, 54);
        Raylib.DrawRectangleRec(freeRect, MenuPanelFill(0.90f));
        Raylib.DrawRectangleLinesEx(freeRect, 2f, MenuPanelLine());
        Raylib.DrawText("Points", (int)freeRect.X + 14, (int)freeRect.Y + 8, 18, Color.LightGray);
        var freeText = $"{GetAvailableCradleCells()}";
        Raylib.DrawText(freeText, (int)(freeRect.X + freeRect.Width - Raylib.MeasureText(freeText, 30) - 16), (int)freeRect.Y + 14, 30, Color.Gold);

        foreach (var track in CradleTracks) DrawCradleTrack(track);
        DrawCradleTrackTooltip();

        DrawButton(new Rectangle(70, 620, 220, 52), "Back");
    }

    private static void DrawCradleStatLine(string label, string value, int x, int y, Color color)
    {
        const int fontSize = 20;
        Raylib.DrawText($"{label}: {value}", x, y, fontSize, color);
    }

    private void DrawCradleTrack(CradleTrack track)
    {
        var row = GetCradleTrackIndex(track);
        var y = 176 + row * 54;
        var label = GetCradleTrackLabel(track);
        var active = _meta.GetCradleTrack(track);

        Raylib.DrawText(label, 540, y + 5, 20, Color.LightGray);

        const float cellWidth = 32f;
        const float cellHeight = 14f;
        const float gap = 5f;
        var startX = 750f;
        for (var i = 0; i < 15; i++)
        {
            var rect = new Rectangle(startX + i * (cellWidth + gap), y + 9, cellWidth, cellHeight);
            Raylib.DrawRectangleRec(rect, Palette.C(26, 34, 48, 255));
            Raylib.DrawRectangleLinesEx(rect, 1f, Palette.C(80, 110, 150));
        }
        DrawCradleTrackCurves(new Rectangle(startX, y + 9, 15 * cellWidth + 14 * gap, cellHeight), active, GetCradleTrackColor(track), row);

        DrawCradleButton(CradlePlusRect(track), "+", active < 15 && GetAvailableCradleCells() > 0);
        DrawCradleButton(CradleMinusRect(track), "-", active > 0);
        Raylib.DrawText(GetCradleTrackBonusText(track, active), 1404, y + 5, 20, GetCradleTrackColor(track));
    }

    private static void DrawCradleTrackCurves(Rectangle trackRect, int active, Color activeColor, int row)
    {
        const int curveCount = 5;
        const int samples = 140;
        const float cellWidth = 32f;
        const float gap = 5f;

        var time = (float)Raylib.GetTime();
        var activeEnd = active <= 0
            ? trackRect.X
            : trackRect.X + active * cellWidth + (active - 1) * gap;
        activeEnd = Math.Clamp(activeEnd, trackRect.X, trackRect.X + trackRect.Width);

        for (var curve = 0; curve < curveCount; curve++)
        {
            var phase = row * 0.71f + curve * 1.37f;
            var baseOffset = (curve - (curveCount - 1) * 0.5f) * 2.15f;
            var frequency = 4.0f + curve * 0.42f;
            var speed = 0.9f + curve * 0.16f + row * 0.035f;
            var morph = 0.18f + 0.82f * (0.5f + 0.5f * MathF.Sin(time * (0.75f + curve * 0.09f) + phase));
            var amplitude = (3.4f + curve * 0.28f) * morph;

            var previous = CradleCurvePoint(trackRect, curve, baseOffset, amplitude, frequency, phase, speed, time, 0f);
            for (var sample = 1; sample <= samples; sample++)
            {
                var t = sample / (float)samples;
                var current = CradleCurvePoint(trackRect, curve, baseOffset, amplitude, frequency, phase, speed, time, t);
                DrawCradleCurveSegment(previous, current, activeEnd, activeColor);
                previous = current;
            }
        }
    }

    private static Vector2 CradleCurvePoint(Rectangle trackRect, int curve, float baseOffset, float amplitude, float frequency, float phase, float speed, float time, float t)
    {
        var angle = t * MathF.Tau * frequency + phase + time * speed;
        var waveBlend = 0.5f + 0.5f * MathF.Sin(time * (0.55f + curve * 0.07f) + phase);
        var wave = MathF.Sin(angle) * (1f - waveBlend) + MathF.Cos(angle) * waveBlend;
        return new Vector2(trackRect.X + trackRect.Width * t, trackRect.Y + trackRect.Height * 0.5f + baseOffset + wave * amplitude);
    }

    private static void DrawCradleCurveSegment(Vector2 from, Vector2 to, float activeEnd, Color activeColor)
    {
        var inactiveColor = Palette.C(124, 134, 150, 165);
        const float thickness = 1.15f;

        if (from.X >= activeEnd)
        {
            Raylib.DrawLineEx(from, to, thickness, inactiveColor);
            return;
        }

        if (to.X <= activeEnd)
        {
            Raylib.DrawLineEx(from, to, thickness, activeColor);
            return;
        }

        var split = (activeEnd - from.X) / MathF.Max(0.001f, to.X - from.X);
        var middle = Vector2.Lerp(from, to, split);
        Raylib.DrawLineEx(from, middle, thickness, activeColor);
        Raylib.DrawLineEx(middle, to, thickness, inactiveColor);
    }

    private static void DrawCradleButton(Rectangle rect, string label, bool enabled)
    {
        var hover = enabled && Raylib.CheckCollisionPointRec(GetUiMousePosition(), rect);
        var fill = enabled ? Palette.C(42, 95, 180) : Palette.C(44, 50, 62);
        var line = enabled ? Color.White : Color.Gray;
        Raylib.DrawRectangleRec(rect, fill);
        DrawButtonPulseFill(rect, Palette.C(120, 210, 255), hover);
        Raylib.DrawRectangleLinesEx(rect, 2f, line);
        var fontSize = 24;
        Raylib.DrawText(
            label,
            (int)(rect.X + rect.Width / 2f - Raylib.MeasureText(label, fontSize) / 2f),
            (int)(rect.Y + rect.Height / 2f - fontSize / 2f),
            fontSize,
            line);
    }

    private static string GetCradleTrackLabel(CradleTrack track) => track switch
    {
        CradleTrack.Health => "Health",
        CradleTrack.Speed => "Speed",
        CradleTrack.MeleeSpeed => "Melee attack speed",
        CradleTrack.DashRecovery => "Dash recovery",
        CradleTrack.Stability => "Stability",
        CradleTrack.Gunsmith => "Gunsmith",
        CradleTrack.Fighter => "Fighter",
        CradleTrack.Arcane => "Arcane",
        _ => "Track"
    };

    private static string GetCradleTrackBonusText(CradleTrack track, int active) => track switch
    {
        CradleTrack.Health => $"+{active * 5} HP",
        CradleTrack.Speed => $"+{active * 2.8f:0.0}%",
        CradleTrack.MeleeSpeed => $"+{active * 1.6f:0.0}%",
        CradleTrack.DashRecovery => $"+{active}%",
        CradleTrack.Stability => $"+{active}%",
        CradleTrack.Gunsmith => $"+{active * 0.4f:0.0}%",
        CradleTrack.Fighter => $"+{active * 0.4f:0.0}%",
        CradleTrack.Arcane => $"+{active}%",
        _ => string.Empty
    };

    private void DrawCradleTrackTooltip()
    {
        var mouse = GetUiMousePosition();
        foreach (var track in CradleTracks)
        {
            var row = GetCradleTrackIndex(track);
            var hitbox = new Rectangle(532, 172 + row * 54, 700, 40);
            if (!Raylib.CheckCollisionPointRec(mouse, hitbox)) continue;

            DrawCradleTooltip(mouse, GetCradleTrackLabel(track), GetCradleTrackDescription(track));
            return;
        }
    }

    private static void DrawCradleTooltip(Vector2 mouse, string title, string body)
    {
        const int width = 390;
        const int padding = 14;
        var bodyLines = WrapText(body, 18, width - padding * 2);
        var height = 58 + bodyLines.Count * 24;
        var x = Math.Min(mouse.X + 18, GetUiScreenWidth() - width - 16);
        var y = Math.Min(mouse.Y + 18, GetUiScreenHeight() - height - 16);
        var rect = new Rectangle(x, y, width, height);

        Raylib.DrawRectangleRec(rect, Palette.C(8, 14, 24, 245));
        Raylib.DrawRectangleLinesEx(rect, 2f, Palette.C(108, 170, 228));
        Raylib.DrawText(title, (int)x + padding, (int)y + 12, 22, Color.White);
        for (var i = 0; i < bodyLines.Count; i++)
        {
            Raylib.DrawText(bodyLines[i], (int)x + padding, (int)y + 44 + i * 24, 18, Color.LightGray);
        }
    }

    private static string GetCradleTrackDescription(CradleTrack track) => track switch
    {
        CradleTrack.Health => "Increases maximum health by 5 for each active cell.",
        CradleTrack.Speed => "Increases movement speed by 2.8% for each active cell.",
        CradleTrack.MeleeSpeed => "Increases melee attack speed by 1.6% for each active cell, reducing time between melee attacks.",
        CradleTrack.DashRecovery => "Reduces dash cooldown by 1% for each active cell.",
        CradleTrack.Stability => "Reduces moving ranged spread by 1% for each active cell.",
        CradleTrack.Gunsmith => "Increases ranged weapon damage by 0.4% for each active cell.",
        CradleTrack.Fighter => "Increases melee weapon damage by 0.4% for each active cell.",
        CradleTrack.Arcane => "Increases effect strength by 1% for each active cell: poison, sticky bullets, stim, regeneration and shield recovery.",
        _ => string.Empty
    };

    private static Color GetCradleTrackColor(CradleTrack track) => track switch
    {
        CradleTrack.Health => Palette.C(210, 74, 74),
        CradleTrack.Speed => Palette.C(88, 190, 255),
        CradleTrack.MeleeSpeed => Palette.C(255, 170, 88),
        CradleTrack.DashRecovery => Palette.C(92, 220, 120),
        CradleTrack.Stability => Palette.C(210, 210, 235),
        CradleTrack.Gunsmith => Palette.C(255, 215, 86),
        CradleTrack.Fighter => Palette.C(255, 126, 92),
        CradleTrack.Arcane => Palette.C(180, 112, 255),
        _ => Color.White
    };

    private static string BuildStatRow(string label, int value, int pending)
        => pending > 0 ? $"{label} {value} (+{pending})" : $"{label} {value}";

    private static void DrawInventoryStatRow(string label, int value, int pending, int x, int y)
    {
        const int fontSize = 20;
        const int valueColumnX = 102;
        const int maxPendingWidth = 116;

        Raylib.DrawText(label, x, y, fontSize, Color.LightGray);
        Raylib.DrawText($"{value}", valueColumnX, y, fontSize, Color.LightGray);
        if (pending <= 0) return;

        var pendingText = $"(+{pending})";
        var pendingX = valueColumnX + 44;
        if (Raylib.MeasureText(pendingText, fontSize) <= maxPendingWidth)
        {
            Raylib.DrawText(pendingText, pendingX, y, fontSize, Color.LightGray);
            return;
        }

        Raylib.DrawText(pendingText, x, y + 18, 16, Color.LightGray);
    }

    private void DrawMetaProgressHeader()
    {
        var square = new Rectangle(24, 24, 78, 78);
        var bar = new Rectangle(square.X + square.Width, square.Y, GetUiScreenWidth() - (square.X + square.Width) - 24, square.Height);
        var progressInset = 6f;
        var required = GetMetaScoreRequired(_meta.Level);
        var progress = required <= 0 ? 0f : Math.Clamp(_meta.Score / (float)required, 0f, 1f);

        Raylib.DrawRectangleRec(square, Palette.C(18, 32, 52, 235));
        Raylib.DrawRectangleLinesEx(square, 2f, Palette.C(108, 170, 228));
        Raylib.DrawText($"{_meta.Level}", (int)(square.X + square.Width / 2f - Raylib.MeasureText($"{_meta.Level}", 36) / 2f), (int)(square.Y + 20), 36, Color.Gold);

        Raylib.DrawRectangleRec(bar, Palette.C(14, 24, 40, 235));
        var fill = new Rectangle(bar.X + progressInset, bar.Y + progressInset, Math.Max(0, (bar.Width - progressInset * 2f) * progress), bar.Height - progressInset * 2f);
        Raylib.DrawRectangleRec(fill, Palette.C(72, 126, 196));
        Raylib.DrawRectangleLinesEx(bar, 2f, Palette.C(108, 170, 228));

        var progressText = $"{_meta.Score}/{required}";
        Raylib.DrawText(progressText, (int)(bar.X + bar.Width / 2f - Raylib.MeasureText(progressText, 28) / 2f), (int)(bar.Y + bar.Height / 2f - 14), 28, Color.White);
    }

    private void DrawSettings()
    {
        DrawTitle("Settings", 100, 66);
        Raylib.DrawText("Video", (GetUiScreenWidth() - Raylib.MeasureText("Video", 28)) / 2, 160, 28, Color.LightGray);
        DrawButton(CenterRect(0, 204, 360, 50), _displayMode == DisplayMode.Windowed ? "> Windowed <" : "Windowed");
        DrawButton(CenterRect(0, 260, 360, 50), _displayMode == DisplayMode.Fullscreen ? "> Fullscreen <" : "Fullscreen");

        DrawSettingsLabel("Antialiasing", -220, 330);
        DrawButton(CenterRect(-320, 370, 180, 44), _antialiasingMode == AntialiasingMode.Off ? "> Off <" : "Off");
        DrawButton(CenterRect(-120, 370, 180, 44), _antialiasingMode == AntialiasingMode.Msaa4x ? "> MSAA x4 <" : "MSAA x4");

        DrawSettingsLabel("VSync", 220, 330);
        DrawButton(CenterRect(120, 370, 180, 44), !_vsyncEnabled ? "> Off <" : "Off");
        DrawButton(CenterRect(320, 370, 180, 44), _vsyncEnabled ? "> On <" : "On");
        Raylib.DrawText("Applied after restart", (GetUiScreenWidth() - Raylib.MeasureText("Applied after restart", 18)) / 2, 420, 18, Palette.C(150, 185, 220));

        DrawSettingsLabel("Texture filter", -220, 460);
        DrawButton(CenterRect(-320, 500, 180, 44), _textureFilteringMode == TextureFilteringMode.Point ? "> Point <" : "Point");
        DrawButton(CenterRect(-120, 500, 180, 44), _textureFilteringMode == TextureFilteringMode.Bilinear ? "> Bilinear <" : "Bilinear");

        DrawSettingsLabel("FPS", 220, 460);
        DrawButton(CenterRect(90, 500, 96, 44), _targetFps == 30 ? "> 30 <" : "30");
        DrawButton(CenterRect(202, 500, 96, 44), _targetFps == 60 ? "> 60 <" : "60");
        DrawButton(CenterRect(314, 500, 96, 44), _targetFps == 120 ? "> 120 <" : "120");

        Raylib.DrawText("Choose theme", (GetUiScreenWidth() - Raylib.MeasureText("Choose theme", 28)) / 2, 580, 28, Color.LightGray);
        for (var i = 0; i < _themes.Count; i++)
        {
            var name = i == _themeIndex ? $"> {_themes[i].Name} <" : _themes[i].Name;
            DrawButton(CenterRect(0, 620 + i * 50, 390, 44), name);
        }

        DrawButton(CenterRect(0, 900, 280, 52), "Back");
    }

    private static void DrawSettingsLabel(string text, int xOffset, int y)
    {
        var x = GetUiScreenWidth() / 2 + xOffset - Raylib.MeasureText(text, 28) / 2;
        Raylib.DrawText(text, x, y, 28, Color.LightGray);
    }

    private void DrawPause()
    {
        Raylib.DrawRectangle(0, 0, GetUiScreenWidth(), GetUiScreenHeight(), Palette.C(0, 0, 0, 175));
        DrawTitle("Paused", 170, 64);
        DrawButton(CenterRect(0, 320, 320, 62), "Resume");
        DrawButton(CenterRect(0, 400, 320, 62), "Abandon run");
    }

    private void DrawDeath()
    {
        Raylib.DrawRectangle(0, 0, GetUiScreenWidth(), GetUiScreenHeight(), Palette.C(0, 0, 0, 180));
        DrawTitle(_deathHeader, 150, 68);
        var lines = _deathBody.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            Raylib.DrawText(line, (GetUiScreenWidth() - Raylib.MeasureText(line, 24)) / 2, 250 + i * 34, 24, Color.LightGray);
        }
        DrawButton(CenterRect(0, 320, 320, 62), "Deploy again");
        DrawButton(CenterRect(0, 400, 320, 62), "Main menu");
    }

    private void DrawNotice()
    {
        if (string.IsNullOrWhiteSpace(_noticeText)) return;

        var width = Math.Max(360, Raylib.MeasureText(_noticeText, 20) + 36);
        var rect = new Rectangle(GetUiScreenWidth() - width - 30, 26, width, 46);
        Raylib.DrawRectangleRec(rect, Palette.C(12, 22, 36, 220));
        Raylib.DrawRectangleLinesEx(rect, 2f, Palette.C(110, 185, 240));
        Raylib.DrawText(_noticeText, (int)rect.X + 18, (int)rect.Y + 12, 20, Color.White);
    }

    private static void DrawTitle(string text, int y, int size)
    {
        var x = (GetUiScreenWidth() - Raylib.MeasureText(text, size)) / 2;
        Raylib.DrawText(text, x, y, size, Color.White);
    }

    private static void DrawButton(Rectangle rect, string text, bool enabled = true)
    {
        var hover = enabled && Raylib.CheckCollisionPointRec(GetUiMousePosition(), rect);
        Raylib.DrawRectangleRec(rect, !enabled ? Palette.C(34, 38, 48) : hover ? Palette.C(68, 112, 186) : Palette.C(36, 56, 90));
        DrawButtonPulseFill(rect, Palette.C(120, 210, 255), hover);
        Raylib.DrawRectangleLinesEx(rect, 2f, enabled ? Color.White : Color.DarkGray);
        const int fs = 24;
        Raylib.DrawText(text, (int)(rect.X + rect.Width / 2 - Raylib.MeasureText(text, fs) / 2f), (int)(rect.Y + rect.Height / 2 - fs / 2f), fs, enabled ? Color.White : Color.Gray);
    }

    private static void DrawButtonPulseFill(Rectangle rect, Color color, bool active)
    {
        if (!active) return;

        const float cycle = 1f;
        const float duration = 0.7f;
        var phase = (float)(Raylib.GetTime() % cycle);
        if (phase > duration) return;

        var progress = phase / duration;
        var inset = (MathF.Min(rect.Width, rect.Height) * 0.5f - 1f) * progress;
        if (inset <= 0f) return;

        var alpha = 0.24f * (1f - progress);
        var pulse = WithAlpha(color, alpha);
        var inner = new Rectangle(rect.X + inset, rect.Y + inset, rect.Width - inset * 2f, rect.Height - inset * 2f);

        Raylib.DrawRectangleRec(new Rectangle(rect.X, rect.Y, rect.Width, inset), pulse);
        Raylib.DrawRectangleRec(new Rectangle(rect.X, rect.Y + rect.Height - inset, rect.Width, inset), pulse);
        Raylib.DrawRectangleRec(new Rectangle(rect.X, inner.Y, inset, MathF.Max(0f, inner.Height)), pulse);
        Raylib.DrawRectangleRec(new Rectangle(rect.X + rect.Width - inset, inner.Y, inset, MathF.Max(0f, inner.Height)), pulse);
    }

    private static void DrawHoverOrbitFrame(Rectangle rect, Color color)
    {
        var outer = new Rectangle(rect.X - 3f, rect.Y - 3f, rect.Width + 6f, rect.Height + 6f);
        Raylib.DrawRectangleLinesEx(outer, 4f, WithAlpha(color, 0.18f));
        Raylib.DrawRectangleLinesEx(rect, 1.5f, WithAlpha(color, 0.72f));

        var perimeter = 2f * (rect.Width + rect.Height);
        var offset = (float)(Raylib.GetTime() * 150f % perimeter);
        var segmentLength = MathF.Min(perimeter * 0.18f, 150f);
        for (var i = 0; i < 3; i++)
        {
            var start = (offset + i * perimeter / 3f) % perimeter;
            DrawHoverOrbitSegment(rect, start, segmentLength, color);
        }
    }

    private static void DrawStorageSelectionFrame(Rectangle rect, Color color)
    {
        Raylib.DrawRectangleRec(rect, WithAlpha(color, 0.12f));
        Raylib.DrawRectangleLinesEx(new Rectangle(rect.X - 2f, rect.Y - 2f, rect.Width + 4f, rect.Height + 4f), 3f, WithAlpha(color, 0.76f));

        var pulse = 0.5f + 0.5f * MathF.Sin((float)Raylib.GetTime() * 7.5f);
        Raylib.DrawRectangleLinesEx(new Rectangle(rect.X + 4f, rect.Y + 4f, rect.Width - 8f, rect.Height - 8f), 1.5f, WithAlpha(Color.White, 0.28f + pulse * 0.28f));
    }

    private static void DrawHoverOrbitSegment(Rectangle rect, float start, float length, Color color)
    {
        const int steps = 18;
        var previous = HoverFramePoint(rect, start);
        for (var i = 1; i <= steps; i++)
        {
            var current = HoverFramePoint(rect, start + length * i / steps);
            var alpha = 1f - i / (float)(steps + 1);
            Raylib.DrawLineEx(previous, current, 3.0f, WithAlpha(color, 0.35f * alpha));
            Raylib.DrawLineEx(previous, current, 1.4f, WithAlpha(Color.White, 0.85f * alpha));
            previous = current;
        }
    }

    private static Vector2 HoverFramePoint(Rectangle rect, float distance)
    {
        var perimeter = 2f * (rect.Width + rect.Height);
        var d = distance % perimeter;
        if (d < 0f) d += perimeter;

        if (d <= rect.Width) return new Vector2(rect.X + d, rect.Y);
        d -= rect.Width;
        if (d <= rect.Height) return new Vector2(rect.X + rect.Width, rect.Y + d);
        d -= rect.Height;
        if (d <= rect.Width) return new Vector2(rect.X + rect.Width - d, rect.Y + rect.Height);
        d -= rect.Width;
        return new Vector2(rect.X, rect.Y + rect.Height - d);
    }

    private static void DrawStorageGrid(Vector2 origin, int cols, int rows)
    {
        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                var rect = new Rectangle(origin.X + c * UiSlotStep, origin.Y + r * UiSlotStep, UiSlotSize, UiSlotSize);
                Raylib.DrawRectangleLinesEx(rect, 1f, Palette.C(70, 90, 130, 170));
            }
        }
    }

    private void DrawStorageSortButtons()
    {
        for (var i = 0; i < StorageSortButtonCount; i++)
        {
            DrawStorageSortButton(i);
        }
    }

    private void DrawStorageSortButton(int index)
    {
        var rect = StorageSortButtonRect(index);
        var active = _storageSortMode == index;
        var hover = Raylib.CheckCollisionPointRec(GetUiMousePosition(), rect);
        var fill = active
            ? Palette.C(92, 150, 235)
            : hover ? Palette.C(68, 112, 186) : Palette.C(36, 56, 90);
        var border = active ? Palette.C(180, 230, 255) : Color.White;

        Raylib.DrawRectangleRec(rect, fill);
        DrawButtonPulseFill(rect, border, hover);
        Raylib.DrawRectangleLinesEx(rect, active ? 3f : 2f, border);

        const int fontSize = 20;
        var text = StorageSortButtonLabel(index);
        Raylib.DrawText(
            text,
            (int)(rect.X + rect.Width / 2f - Raylib.MeasureText(text, fontSize) / 2f),
            (int)(rect.Y + rect.Height / 2f - fontSize / 2f),
            fontSize,
            Color.White);
    }

    private static string StorageSortButtonLabel(int index)
        => index switch
        {
            0 => "-",
            1 => "A",
            2 => "P",
            3 => "H",
            4 => "M",
            5 => "C",
            6 => "K",
            7 => "G",
            _ => "?"
        };

    private void DrawStashScrollBar(Rectangle panel)
    {
        var maxRow = GetMaxStashScrollRow();
        if (maxRow <= 0) return;

        var track = new Rectangle(panel.X + panel.Width - 12f, panel.Y + 52f, 6f, panel.Height - 70f);
        Raylib.DrawRectangleRec(track, Palette.C(35, 48, 68, 220));

        var thumbHeight = MathF.Max(42f, track.Height * StashVisibleRows / (StashVisibleRows + maxRow));
        var travel = track.Height - thumbHeight;
        var thumbY = track.Y + travel * (_storageScrollRow / (float)maxRow);
        Raylib.DrawRectangleRec(new Rectangle(track.X - 1f, thumbY, track.Width + 2f, thumbHeight), Palette.C(120, 190, 255));
    }

    private void DrawExtractionHud()
    {
        if (_challengeMode)
        {
            Raylib.DrawText($"Challenge {_selectedMapName}", 20, 138, 22, Palette.C(191, 120, 255));
            Raylib.DrawText($"Completed waves {_pitCompletedWaves.Count}", 20, 168, 20, Palette.C(165, 195, 220));
            if (_challengeKind == ChallengeKind.PitNightmare)
            {
                var portalText = _pitNightmarePortalActive ? "Exit portal active" : "Exit portal inactive";
                Raylib.DrawText(portalText, 20, 198, 20, _pitNightmarePortalActive ? Palette.C(170, 220, 255) : Color.Gray);
            }
            return;
        }

        string timerText;
        Color color;

        if (_lastChanceActive)
        {
            timerText = IsLastChancePortalOpen()
                ? $"Last portal active {FormatTime(_lastChanceTimer)}"
                : $"Last chance {FormatTime(_lastChanceTimer)}";
            color = Palette.C(255, 95, 95);
        }
        else
        {
            timerText = _extractPortals.Count == 0
                ? $"Portals in {FormatTime(_portalUnlockTimer)}"
                : $"Portals active {FormatTime(_portalActiveTimer)}";
            color = _extractPortals.Count == 0 ? Color.LightGray : Palette.C(110, 215, 255);
        }

        Raylib.DrawText(timerText, 20, 138, 22, color);
        Raylib.DrawText($"Map {_selectedMapName}", 20, 168, 20, Palette.C(165, 195, 220));
    }

    private void DrawLowHealthOverlay()
    {
        if (_state is not GameState.Playing and not GameState.Paused) return;
        if (_player.MaxHealth <= 0f) return;

        var hpRatio = _player.Health / _player.MaxHealth;
        if (hpRatio >= 0.20f) return;

        var pulse = 0.25f + 0.20f * (0.5f + 0.5f * MathF.Sin((float)Raylib.GetTime() * 5f));
        var intensity = (0.20f - hpRatio) / 0.20f;
        var alpha = (byte)(255 * Math.Clamp(pulse * intensity, 0f, 0.45f));
        var color = Palette.C(220, 40, 40, alpha);
        var w = GetUiScreenWidth();
        var h = GetUiScreenHeight();
        var thick = 12;

        Raylib.DrawRectangle(0, 0, w, thick, color);
        Raylib.DrawRectangle(0, h - thick, w, thick + 4, color);
        Raylib.DrawRectangle(0, 0, thick, h, color);
        Raylib.DrawRectangle(w - thick, 0, thick, h, color);
    }

    private Rectangle MainMenuButtonRect(int index)
        => new(70, GetUiScreenHeight() - 404 + index * 60, 220, 48);

    private Rectangle MainMenuCodesButtonRect()
    {
        var cradle = MainMenuButtonRect(3);
        return new Rectangle(GetUiScreenWidth() - 290, cradle.Y, 220, 48);
    }

    private Rectangle MainMenuChangelogButtonRect()
    {
        var settings = MainMenuButtonRect(4);
        return new Rectangle(GetUiScreenWidth() - 290, settings.Y, 220, 48);
    }

    private Rectangle MainMenuAboutButtonRect()
    {
        var exit = MainMenuButtonRect(5);
        return new Rectangle(GetUiScreenWidth() - 290, exit.Y, 220, 48);
    }

    private static Rectangle MapCardRect(int index)
        => new(70 + index * 585, 160, 555, 380);

    private static Rectangle ChallengeInfoButtonRect(int index)
    {
        var card = MapCardRect(index);
        return new Rectangle(card.X + card.Width - 52, card.Y + 22, 30, 30);
    }

    private static Rectangle PitNightmareInfoCloseRect()
        => new((GetUiScreenWidth() - 180) / 2f, 520, 180, 46);

    private static Rectangle DeploymentToggleRect()
        => new(GetUiScreenWidth() - 292, 74, 220, 46);

    private static Rectangle PitRewardPanelRect()
        => new((GetUiScreenWidth() - 1140f) / 2f, (GetUiScreenHeight() - 680f) / 2f, 1140, 680);

    private static Rectangle PitRewardCardRect(int index)
    {
        var panel = PitRewardPanelRect();
        return new Rectangle(panel.X + 40, panel.Y + 88 + index * 112, 880, 96);
    }

    private static Rectangle PitRewardWinningIconRect(int index)
    {
        var card = PitRewardCardRect(index);
        const float iconSize = 42f;
        var reel = new Rectangle(card.X + 150, card.Y + 21, 640, 62);
        return new Rectangle(
            reel.X + reel.Width / 2f - iconSize / 2f,
            reel.Y + reel.Height / 2f - iconSize / 2f,
            iconSize,
            iconSize);
    }

    private static Rectangle PitRewardTakeButtonRect(int index)
    {
        var card = PitRewardCardRect(index);
        return new Rectangle(card.X + card.Width + 26, card.Y + 29, 132, 38);
    }

    private static Rectangle PitRewardSkipButtonRect()
    {
        var panel = PitRewardPanelRect();
        return new Rectangle(panel.X + panel.Width / 2f - 90f, panel.Y + panel.Height - 60f, 180, 42);
    }

    private static Rectangle PitDifficultyPanelRect()
        => PitRewardPanelRect();

    private static Rectangle PitDifficultyCardRect()
    {
        var panel = PitDifficultyPanelRect();
        return new Rectangle(panel.X + 130, panel.Y + 236, 880, 96);
    }

    private static Rectangle PitDifficultyOkButtonRect()
    {
        var panel = PitDifficultyPanelRect();
        return new Rectangle(panel.X + panel.Width / 2f - 90f, panel.Y + panel.Height - 60f, 180, 42);
    }

    private static Rectangle ArmoryOfferRect(int index)
    {
        var col = index % 6;
        var row = index / 6;
        return new Rectangle(70 + col * 154, 246 + row * 154, 138, 138);
    }

    private static Rectangle TokenStoreOfferRect(int index)
        => new(1116, 246 + index * 154, 138, 138);

    private static Rectangle MapSelectBackButtonRect()
        => new(70, 676, 220, 52);

    private static Rectangle StorageBackButtonRect()
        => new(70, 900, 220, 52);

    private static Rectangle StorageSortButtonRect(int index)
    {
        var size = UiSlotSize * 0.5f;
        var panel = StashPanelRect();
        return new Rectangle(panel.X + panel.Width, panel.Y + index * size, size, size);
    }

    private static Rectangle ArmoryBackButtonRect()
        => new(70, 900, 220, 52);

    private static Rectangle CodesPopupRect()
         => new((GetUiScreenWidth() - 470) / 2f, (GetUiScreenHeight() - 240) / 2f, 470, 240);

    private static Rectangle AboutPopupRect()
         => new((GetUiScreenWidth() - 640) / 2f, (GetUiScreenHeight() - 500) / 2f, 640, 500);

    private static Rectangle ChangelogPopupRect()
         => new((GetUiScreenWidth() - 1040) / 2f, (GetUiScreenHeight() - 760) / 2f, 1040, 760);

    private static Rectangle ChangelogContentRect()
    {
        var popup = ChangelogPopupRect();
        return new Rectangle(popup.X + 28, popup.Y + 78, popup.Width - 74, popup.Height - 108);
    }

    private static Rectangle CodesPopupInputRect()
    {
        var popup = CodesPopupRect();
        return new Rectangle(popup.X + 30, popup.Y + 84, popup.Width - 60, 44);
    }

    private static Rectangle CodesPopupApplyRect()
    {
        var popup = CodesPopupRect();
        return new Rectangle(popup.X + 30, popup.Y + popup.Height - 70, 160, 40);
    }

    private static Rectangle CodesPopupCloseRect()
    {
        var popup = CodesPopupRect();
        return new Rectangle(popup.X + popup.Width - 46, popup.Y + 14, 32, 32);
    }

    private static Rectangle AboutPopupCloseRect()
    {
        var popup = AboutPopupRect();
        return new Rectangle(popup.X + popup.Width - 46, popup.Y + 14, 32, 32);
    }

    private static Rectangle ChangelogPopupCloseRect()
    {
        var popup = ChangelogPopupRect();
        return new Rectangle(popup.X + popup.Width - 48, popup.Y + 16, 32, 32);
    }

    private static Rectangle TerminalPanelRect()
        => new((GetUiScreenWidth() - 520) / 2f, (GetUiScreenHeight() - 500) / 2f, 520, 500);

    private static Rectangle TerminalScreenRect()
    {
        var panel = TerminalPanelRect();
        return new Rectangle(panel.X + 36, panel.Y + 32, panel.Width - 72, 82);
    }

    private static Rectangle TerminalInputRect()
    {
        var panel = TerminalPanelRect();
        return new Rectangle(panel.X + 146, panel.Y + 132, 228, 40);
    }

    private static Rectangle TerminalDigitButtonRect(int digit)
    {
        var panel = TerminalPanelRect();
        const float size = 58f;
        const float gap = 12f;
        var startX = panel.X + panel.Width / 2f - size * 1.5f - gap;
        var startY = panel.Y + 192f;

        if (digit == 0) return new Rectangle(startX + size + gap, startY + (size + gap) * 3f, size, size);

        var index = digit - 1;
        var col = index % 3;
        var row = index / 3;
        return new Rectangle(startX + col * (size + gap), startY + row * (size + gap), size, size);
    }

    private static Rectangle TerminalDeleteButtonRect()
    {
        var zero = TerminalDigitButtonRect(0);
        return new Rectangle(zero.X - zero.Width - 12f, zero.Y, zero.Width, zero.Height);
    }

    private static Rectangle TerminalEnterButtonRect()
    {
        var zero = TerminalDigitButtonRect(0);
        return new Rectangle(zero.X + zero.Width + 12f, zero.Y, zero.Width, zero.Height);
    }

    private static Rectangle TerminalNotePanelRect()
        => new((GetUiScreenWidth() - 420) / 2f, (GetUiScreenHeight() - 220) / 2f, 420, 220);

    private static Rectangle TerminalNoteCloseButtonRect()
    {
        var panel = TerminalNotePanelRect();
        return new Rectangle(panel.X + panel.Width / 2f - 80f, panel.Y + panel.Height - 64f, 160, 40);
    }

    private static Rectangle CenterRect(int offsetX, int y, int w, int h) => new((GetUiScreenWidth() - w) / 2f + offsetX, y, w, h);
    private static bool Clicked(Rectangle rect) => Raylib.IsMouseButtonPressed(MouseButton.Left) && Raylib.CheckCollisionPointRec(GetUiMousePosition(), rect);
}



