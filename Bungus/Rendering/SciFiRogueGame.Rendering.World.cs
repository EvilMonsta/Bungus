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

        DrawFrameOverlays();
        Raylib.EndDrawing();
    }

    private void DrawFrameOverlays()
    {
        DrawCinematicPostProcess();
        DrawFloatingCombatTexts();
        DrawUiTransitionOverlay();
        DrawPerformanceOverlay();
    }

    private void DrawUiTransitionOverlay()
    {
        if (_uiTransitionTimer <= 0f) return;

        var t = Math.Clamp(_uiTransitionTimer / UiTransitionDuration, 0f, 1f);
        var alpha = (int)(155f * t * t);
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), Palette.C(2, 4, 10, alpha));
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
        foreach (var decal in _worldDecals) decal.Draw();
        DrawWorldAmbientGlows();

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
            DrawProtectiveDome(dome);
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

        DrawProjectileGlowPass();
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
        DrawVisualParticles();

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

        foreach (var decal in _bunkerDecals)
        {
            if (!_bunkerRooms.Any(room => _revealedBunkerRooms.Contains(room.Id) && Raylib.CheckCollisionPointRec(decal.Position, room.Rect))) continue;
            decal.Draw();
        }
        DrawBunkerAmbientGlows();

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
            DrawProtectiveDome(dome);
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
        DrawProjectileGlowPass();
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
        DrawVisualParticles();

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

    private void DrawWorldAmbientGlows()
    {
        Raylib.BeginBlendMode(BlendMode.Additive);
        foreach (var zone in _generatorZones)
        {
            DrawAreaGlow(zone.Center, MathF.Max(zone.Rect.Width, zone.Rect.Height) * 0.38f, Palette.C(70, 190, 255, 26));
        }

        foreach (var hangar in _hangars)
        {
            DrawAreaGlow(hangar.Center, MathF.Max(hangar.Rect.Width, hangar.Rect.Height) * 0.3f, Palette.C(80, 220, 130, 18));
        }

        if (_stationZone is not null)
        {
            DrawAreaGlow(_stationZone.Center, MathF.Max(_stationZone.Rect.Width, _stationZone.Rect.Height) * 0.34f, Palette.C(180, 190, 220, 18));
        }

        foreach (var portal in _extractPortals)
        {
            DrawAreaGlow(portal.Position, 170f, _lastChanceActive ? Palette.C(255, 88, 92, 50) : Palette.C(110, 190, 255, 38));
        }

        foreach (var generator in _generators)
        {
            if (!generator.Destroyed) DrawAreaGlow(generator.Position, 112f, generator.Vulnerable ? Palette.C(255, 220, 92, 48) : Palette.C(80, 190, 255, 28));
        }

        Raylib.EndBlendMode();
    }

    private void DrawBunkerAmbientGlows()
    {
        Raylib.BeginBlendMode(BlendMode.Additive);
        foreach (var door in _bunkerDoors)
        {
            if (door.Open || (!_revealedBunkerRooms.Contains(door.RoomA) && !_revealedBunkerRooms.Contains(door.RoomB))) continue;
            DrawAreaGlow(door.Center, 84f, Palette.C(255, 146, 82, 28));
        }

        if (_revealedBunkerRooms.Contains(19))
        {
            foreach (var position in BunkerTyrantSwitchPositions)
            {
                DrawAreaGlow(position, 94f, Palette.C(255, 62, 82, 38));
            }
        }

        if (_bunkerTyrant?.Alive == true)
        {
            var tyrantGlow = _bunkerTyrant.Invulnerable ? Palette.C(255, 64, 92, 32) : Palette.C(255, 156, 82, 40);
            DrawAreaGlow(_bunkerTyrant.Position, _bunkerTyrant.Invulnerable ? 230f : 190f, tyrantGlow);
        }

        Raylib.EndBlendMode();
    }

    private static void DrawAreaGlow(Vector2 position, float radius, Color color)
    {
        Raylib.DrawCircleGradient((int)position.X, (int)position.Y, radius, color, Palette.C(color.R, color.G, color.B, 0));
    }

    private static void DrawProtectiveDome(ProtectiveDome dome)
    {
        var ratio = Math.Clamp(dome.Health / ProtectiveDome.MaxHealth, 0f, 1f);
        var time = (float)Raylib.GetTime();
        var pulse = 0.5f + 0.5f * MathF.Sin(time * 4.2f);
        var radius = ProtectiveDome.Radius;
        var outerRadius = radius + pulse * 3f;

        Raylib.BeginBlendMode(BlendMode.Additive);
        Raylib.DrawCircleGradient((int)dome.Position.X, (int)dome.Position.Y, radius * 1.12f, Palette.C(88, 190, 255, 30), Palette.C(88, 190, 255, 0));
        Raylib.EndBlendMode();

        Raylib.DrawCircleV(dome.Position, radius, Palette.C(120, 190, 255, 28));
        Raylib.DrawCircleLinesV(dome.Position, outerRadius, Palette.C(188, 235, 255, 205));

        var bar = new Rectangle(dome.Position.X - 40f, dome.Position.Y - radius - 18f, 80f, 5f);
        Raylib.DrawRectangleRec(bar, Palette.C(20, 20, 20, 220));
        Raylib.DrawRectangle((int)bar.X, (int)bar.Y, (int)(bar.Width * ratio), (int)bar.Height, Palette.C(120, 205, 255));
    }

    private void DrawProjectileGlowPass()
    {
        Raylib.BeginBlendMode(BlendMode.Additive);
        foreach (var projectile in _projectiles)
        {
            if (projectile.Kind == ProjectileKind.TraceBeam) continue;
            var glowRadius = projectile.Kind switch
            {
                ProjectileKind.PulsarBolt => projectile.DrawRadius * 5.4f,
                ProjectileKind.MicroCharge => projectile.DrawRadius * 4.8f,
                ProjectileKind.LinearShot => projectile.DrawRadius * 3.8f,
                ProjectileKind.Grenade or ProjectileKind.FreezeGrenade or ProjectileKind.HeGrenade => projectile.DrawRadius * 3.2f,
                _ => projectile.Highlighted ? projectile.DrawRadius * 3.4f : projectile.DrawRadius * 2.1f
            };

            glowRadius *= GetVisualEffectsSizeMultiplier();
            var alpha = projectile.Highlighted || projectile.Kind is ProjectileKind.PulsarBolt or ProjectileKind.MicroCharge ? 0.34f : 0.16f;
            alpha *= GetVisualEffectsMultiplier();
            Raylib.DrawCircleGradient(
                (int)projectile.Position.X,
                (int)projectile.Position.Y,
                glowRadius,
                WithAlpha(projectile.Color, alpha),
                WithAlpha(projectile.Color, 0f));
        }
        Raylib.EndBlendMode();
    }

    private void DrawVisualParticles()
    {
        Raylib.BeginBlendMode(BlendMode.Additive);
        foreach (var particle in _visualParticles)
        {
            if (particle.Shape == VisualParticleShape.Smoke) continue;
            particle.Draw();
        }
        Raylib.EndBlendMode();

        foreach (var particle in _visualParticles)
        {
            if (particle.Shape == VisualParticleShape.Smoke) particle.Draw();
        }
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
        var shake = Vector2.Zero;
        if (_state == GameState.Playing && _screenShakeTimer > 0f && _screenShakeDuration > 0f)
        {
            var ratio = Math.Clamp(_screenShakeTimer / _screenShakeDuration, 0f, 1f);
            var time = (float)Raylib.GetTime();
            shake = new Vector2(
                MathF.Sin(time * 92.7f) + MathF.Sin(time * 41.3f + 1.7f),
                MathF.Cos(time * 83.1f) + MathF.Sin(time * 37.9f + 0.4f))
                * (_screenShakeStrength * ratio * 0.5f);
        }

        return new Camera2D
        {
            Target = _camera.Target,
            Offset = _camera.Offset * scale + offset + shake,
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

}
