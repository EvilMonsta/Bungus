using System.Numerics;
using Raylib_cs;

namespace Bungus.Game;

public sealed partial class SciFiRogueGame
{
    private void Draw()
    {
        Raylib.BeginDrawing();
        Raylib.ClearBackground(Theme.Background);

        switch (_state)
        {
            case GameState.MainMenu:
                DrawMainMenu();
                break;
            case GameState.MapSelect:
                DrawMapSelect();
                break;
            case GameState.Storage:
                DrawStorage();
                break;
            case GameState.Armory:
                DrawArmory();
                break;
            case GameState.Character:
                DrawCharacter();
                break;
            case GameState.Settings:
                DrawSettings();
                break;
            case GameState.Playing:
                DrawWorld();
                DrawHud();
                DrawCombatCursor();
                if (_mapOpen) DrawMapWindow();
                else DrawInventory();
                break;
            case GameState.Paused:
                DrawWorld();
                DrawHud();
                DrawPause();
                break;
            case GameState.Death:
                DrawWorld();
                DrawDeath();
                break;
        }

        DrawNotice();
        DrawLowHealthOverlay();

        Raylib.EndDrawing();
    }

    private void DrawWorld()
    {
        Raylib.BeginMode2D(_camera);
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

        foreach (var chest in _chests)
        {
            var rect = new Rectangle(chest.Position.X - 14, chest.Position.Y - 10, 28, 20);
            var locked = chest.RequiresClear && chest.ZoneId is int zoneId && !IsZoneCleared(zoneId);
            var empty = chest.Items.Count == 0;
            var fill = empty
                ? Palette.C(65, 65, 65, 180)
                : chest.Kind == LootContainerKind.Crate
                    ? Palette.C(98, 62, 34, 240)
                    : Palette.C(122, 82, 38, 240);
            var line = empty
                ? Color.Gray
                : chest.Kind == LootContainerKind.Crate
                    ? Palette.C(140, 90, 52)
                    : locked ? Color.Red : Color.Gold;

            if (chest.Kind == LootContainerKind.Crate)
            {
                rect = new Rectangle(chest.Position.X - 14, chest.Position.Y - 14, 28, 28);
            }

            Raylib.DrawRectangleRec(rect, fill);
            Raylib.DrawRectangleLinesEx(rect, 1.5f, line);
            Raylib.DrawLine((int)rect.X, (int)(rect.Y + rect.Height / 2), (int)(rect.X + rect.Width), (int)(rect.Y + rect.Height / 2), Color.Black);

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
            var active = !_lastChanceActive || IsLastChancePortalOpen();
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
        foreach (var e in _enemies) e.Draw(Theme);
        foreach (var h in _hexEnemies) h.Draw();
        foreach (var t in _turrets) t.Draw();
        foreach (var b in _miniBosses) b.Draw(Theme);
        foreach (var g in _generatorGuards) g.Draw();
        foreach (var toxic in _toxicEnemies) toxic.Draw();
        _destroyerBoss?.Draw();
        _stationBoss?.Draw();
        foreach (var boss in _pitStationBosses) boss.Draw();
        foreach (var t in _turrets) t.DrawAimLine();
        DrawPlayerSniperAimLine();
        foreach (var beam in _beamEffects) beam.Draw();

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
            Raylib.DrawCircleLines((int)ex.Position.X, (int)ex.Position.Y, r, ex.Color);
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

        var mouseWorld = Raylib.GetScreenToWorld2D(Raylib.GetMousePosition(), _camera);
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
            var timer = $"{MathF.Ceiling(MathF.Max(0f, _pitWaveTimer)):0}";
            Raylib.DrawText(timer, Raylib.GetScreenWidth() / 2 - Raylib.MeasureText(timer, 56) / 2, 12, 56, Palette.C(130, 230, 255));
            var waveText = $"Wave {Math.Max(1, _pitNextWave - 1)}";
            Raylib.DrawText(waveText, Raylib.GetScreenWidth() / 2 - Raylib.MeasureText(waveText, 22) / 2, 72, 22, Color.White);
        }

        var activeWeapon = _player.ActiveWeapon;
        Raylib.DrawText($"Current: {activeWeapon?.Name ?? "None"} {BuildWeaponDamageText(_player, activeWeapon, _player.ActiveWeaponClass)}", 20, 48, 22, activeWeapon?.Color ?? Color.LightGray);
        Raylib.DrawText($"Consumables: Q [{(_player.Inventory.QuickSlotQ?.Name ?? "-")}]  R [{(_player.Inventory.QuickSlotR?.Name ?? "-")}]", 20, 78, 20, Color.White);
        if (!_challengeMode) Raylib.DrawText($"Run score {_runScore}", 20, 108, 20, Color.Gold);
        DrawExtractionHud();
        DrawVitalBars();
        DrawLevelUpIndicator();
        DrawStatusEffects();
        if (_pitRewardOpen) DrawPitRewardSelection();
        Raylib.DrawText("WASD move | LMB attack | 1 melee | 2 primary | 3 heavy | TAB inventory | ESC menu", 20, Raylib.GetScreenHeight() - 28, 18, Color.Gray);
        DrawZoneArrows();
    }

    private void DrawCombatCursor()
    {
        if (_player.InventoryOpen || _mapOpen || _pitRewardOpen) return;

        var mouse = Raylib.GetMousePosition();
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
            if (_player.IsLinearRifleEquipped)
            {
                DrawCircularProgressFrame(mouse, 22f, _player.LinearRifleChargeProgress, Palette.C(130, 230, 255));
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
        var screenWidth = Raylib.GetScreenWidth();
        var screenHeight = Raylib.GetScreenHeight();
        Raylib.DrawRectangle(0, 0, screenWidth, screenHeight, Palette.C(0, 0, 0, 150));

        var mapRect = GetMapRect();
        var panel = new Rectangle(mapRect.X - 22f, mapRect.Y - 58f, mapRect.Width + 44f, mapRect.Height + 86f);
        Raylib.DrawRectangleRec(panel, Palette.C(6, 10, 20, 235));
        Raylib.DrawRectangleLinesEx(panel, 2f, Palette.C(100, 190, 255));
        Raylib.DrawText("Map", (int)panel.X + 18, (int)panel.Y + 16, 28, Color.White);
        Raylib.DrawText("LMB: place/move marker | RMB near marker: remove | M/Esc: close", (int)panel.X + 92, (int)panel.Y + 23, 18, Color.LightGray);

        Raylib.DrawRectangleRec(mapRect, Palette.C(12, 18, 28, 255));
        Raylib.DrawRectangleLinesEx(mapRect, 2f, Color.SkyBlue);

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
        var width = Raylib.GetScreenWidth();
        var ratio = Math.Clamp(_player.Kills / (float)Math.Max(1, _player.KillsTarget), 0f, 1f);
        Raylib.DrawRectangle(0, 0, width, 10, Palette.C(12, 20, 34, 230));
        Raylib.DrawRectangle(0, 0, (int)(width * ratio), 10, Palette.C(70, 190, 255));
        Raylib.DrawRectangleLinesEx(new Rectangle(0, 0, width, 10), 1f, Color.Black);
    }

    private void DrawLevelUpIndicator()
    {
        if (_player.StatPoints <= 0) return;

        var x = Raylib.GetScreenWidth() - 156;
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
        var x = Raylib.GetScreenWidth() - 42f;
        var y = _player.StatPoints > 0 ? 74f : 28f;

        if (_player.Poisoned)
        {
            DrawStatusEffectIcon(new Vector2(x, y), Palette.C(120, 20, 24), "P", _player.PoisonEffectProgress);
            y += 46f;
        }

        if (_player.StickyBulletsActive)
        {
            DrawStatusEffectIcon(new Vector2(x, y), Palette.C(120, 120, 120), "B", _player.StickyBulletsEffectProgress);
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
        var screenWidth = Raylib.GetScreenWidth();
        var screenHeight = Raylib.GetScreenHeight();
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
        Raylib.DrawText(ammoText, (int)(hpRect.X + hpRect.Width - Raylib.MeasureText(ammoText, ammoFont)), (int)hpRect.Y - 78, ammoFont, Palette.C(120, 210, 255));
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
        }

        if (_drag is not null)
        {
            var m = Raylib.GetMousePosition();
            DrawItemIcon(_drag.Item, new Rectangle(m.X + 8, m.Y + 8, UiSlotSize, UiSlotSize), comparison, _drag.Kind);
        }

        if (_hovered is not null) DrawTooltip(_hovered, Raylib.GetMousePosition(), comparison);
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
        var x = Raylib.GetScreenWidth() - rightMargin - Raylib.MeasureText(text, fontSize);
        Raylib.DrawText(text, x, y, fontSize, Palette.C(120, 230, 255));
    }

    private void DrawCryptoTokensCounter(int rightMargin, int y, int fontSize)
    {
        var text = $"CryptoTokens: {_meta.CryptoTokens}";
        var x = Raylib.GetScreenWidth() - rightMargin - Raylib.MeasureText(text, fontSize);
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

        DrawComparisonMarker(item, rect, comparison, sourceKind);
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

        _iconTextures[relativePath] = texture;
        return true;
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
        if (item.Type == ItemType.Armor) return Path.Combine("Assets", "Icons", "Armor", "armor.png");
        if (item.IsStationKey) return Path.Combine("Assets", "Icons", "KeyItems", "station_key.png");
        if (item.IsHeavyAmmo) return Path.Combine("Assets", "Icons", "Consumables", "heavy_ammo.png");

        if (item.Type == ItemType.Consumable)
        {
            var name = item.ConsumableKind switch
            {
                ConsumableType.Medkit => "medkit.png",
                ConsumableType.Stim => "stim.png",
                ConsumableType.ProtectiveDome => "protective_dome.png",
                ConsumableType.StickyBullets => "sticky_bullets.png",
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

        Raylib.DrawRectangle((int)(rect.X + rect.Width - 14f), (int)(rect.Y + 7f), 12, 3, Palette.C(255, 220, 90));
    }

    private ComparisonMarker GetComparisonMarker(ItemStack item, ComparisonContext? comparison, SlotKind? sourceKind)
    {
        if (item.Type is ItemType.Consumable or ItemType.KeyItem or ItemType.Ammo) return ComparisonMarker.None;
        if (comparison is null) return ComparisonMarker.None;
        if (sourceKind is SlotKind.Armor or SlotKind.RangedWeapon or SlotKind.HeavyWeapon or SlotKind.MeleeWeapon) return ComparisonMarker.None;

        if (item.Type == ItemType.Weapon)
        {
            if (item.WeaponKind is null) return ComparisonMarker.None;
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
            lines.Add(($"Base damage: {item.BaseDamage:0.0} | {item.WeaponKind}", item.Color));
            lines.Add(($"Fire rate: {GetWeaponFireRatePerMinute(item):0}/min", Palette.C(170, 220, 255)));
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

        if (item.IsStationKey) lines.Add(("Key item | opens station entrance", item.Color));
        else lines.Add(("Use by Q/R", Color.Green));
        return lines;
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

    private static float GetWeaponFireRatePerMinute(ItemStack item)
    {
        if (item.Type != ItemType.Weapon) return 0f;

        if (item.Pattern == WeaponPattern.GrenadeLauncher) return 90f;
        if (item.Pattern == WeaponPattern.RocketLauncher) return 40f;
        if (item.Pattern == WeaponPattern.TraceRifle) return 1000f;
        if (item.Pattern == WeaponPattern.LinearRifle) return (1f / 1.25f) * 60f;
        if (item.Pattern == WeaponPattern.Pulsar) return 3f * 60f;
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

        var damage = item.BaseDamage;
        if (item.Pattern == WeaponPattern.GrenadeLauncher) return (damage + 135f) / GetWeaponCooldown(item);
        if (item.Pattern == WeaponPattern.RocketLauncher) return (damage + 200f) / GetWeaponCooldown(item);
        if (item.Pattern == WeaponPattern.TraceRifle) return damage / GetWeaponCooldown(item);
        if (item.Pattern == WeaponPattern.LinearRifle) return damage / GetWeaponCooldown(item);
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
        if (item.Pattern == WeaponPattern.RocketLauncher) return 1.5f;
        if (item.Pattern == WeaponPattern.TraceRifle) return 60f / 1000f;
        if (item.Pattern == WeaponPattern.LinearRifle) return 1.25f;
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
        Raylib.DrawText("a0.3.2", 86, 150, 24, Palette.C(150, 185, 220));
        DrawMetaProgressHeader();

        DrawButton(MainMenuButtonRect(0), "Play");
        DrawButton(MainMenuButtonRect(1), "Storage");
        DrawButton(MainMenuButtonRect(2), "Store");
        DrawButton(MainMenuButtonRect(3), "Character");
        DrawButton(MainMenuButtonRect(4), "Settings");
        DrawButton(MainMenuButtonRect(5), "Exit");
        DrawButton(MainMenuCodesButtonRect(), "Codes");

        if (_codesPopupOpen) DrawCodesPopup();
    }

    private void DrawCodesPopup()
    {
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), Palette.C(0, 0, 0, 165));

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

    private void DrawMapSelect()
    {
        DrawTitle(_deploymentListMode == DeploymentListMode.Expeditions ? "Expeditions" : "Challenges", 64, 66);
        Raylib.DrawText(_deploymentListMode == DeploymentListMode.Expeditions ? "Choose your landing zone" : "Choose a trial", 72, 118, 26, Color.LightGray);
        DrawButton(DeploymentToggleRect(), _deploymentListMode == DeploymentListMode.Expeditions ? "Challenges" : "Expeditions");

        if (_deploymentListMode == DeploymentListMode.Challenges)
        {
            DrawChallengeCard(MapCardRect(0));
        }
        else
        {
            for (var i = 0; i < MapDefinition.All.Length; i++)
            {
                DrawMapCard(MapDefinition.All[i], MapCardRect(i));
            }
        }

        DrawButton(MapSelectBackButtonRect(), "Back");
    }

    private void DrawChallengeCard(Rectangle card)
    {
        var hover = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), card);
        Raylib.DrawRectangleRec(card, hover ? Palette.C(36, 30, 56) : Palette.C(18, 16, 34));
        Raylib.DrawRectangleLinesEx(card, 2f, Palette.C(191, 120, 255));

        var arena = new Rectangle(card.X + 28, card.Y + 28, card.Width - 56, 128);
        Raylib.DrawRectangleRec(arena, Palette.C(22, 24, 34));
        Raylib.DrawCircleGradient((int)(arena.X + arena.Width * 0.5f), (int)(arena.Y + arena.Height * 0.5f), 64, Palette.C(150, 90, 255, 120), Palette.C(40, 20, 80, 20));
        Raylib.DrawRectangleLinesEx(new Rectangle(arena.X + 36, arena.Y + 22, arena.Width - 72, arena.Height - 44), 3f, Palette.C(120, 80, 200));

        Raylib.DrawText("Pit", (int)card.X + 42, (int)card.Y + 174, 36, Color.White);
        Raylib.DrawText("Wave survival trial", (int)card.X + 42, (int)card.Y + 220, 22, Color.LightGray);
    }

    private void DrawMapCard(MapDefinition map, Rectangle card)
    {
        var hover = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), card);
        Raylib.DrawRectangleRec(card, hover ? Palette.C(22, 40, 62) : Palette.C(14, 24, 40));
        Raylib.DrawRectangleLinesEx(card, 2f, Palette.C(116, 180, 235));

        var sky = new Rectangle(card.X + 28, card.Y + 28, card.Width - 56, 128);
        Raylib.DrawRectangleRec(sky, map.IsDeadZone ? Palette.C(28, 40, 44) : Palette.C(34, 58, 86));
        Raylib.DrawCircleGradient((int)(sky.X + sky.Width - 90), (int)(sky.Y + 48), 42, map.IsDeadZone ? Palette.C(120, 255, 150, 90) : Palette.C(250, 214, 120), Palette.C(250, 214, 120, 30));
        Raylib.DrawRectangle((int)card.X + 46, (int)card.Y + 228, 128, 48, Palette.C(60, 96, 126));
        Raylib.DrawRectangle((int)card.X + 220, (int)card.Y + 204, 104, 72, map.IsDeadZone ? Palette.C(80, 90, 90) : Palette.C(112, 74, 58));
        Raylib.DrawRectangle((int)card.X + 372, (int)card.Y + 188, 116, 88, map.IsDeadZone ? Palette.C(40, 110, 70) : Palette.C(138, 84, 64));
        Raylib.DrawCircle((int)card.X + 138, (int)card.Y + 252, 15, Palette.C(80, 170, 255));
        Raylib.DrawCircle((int)card.X + 432, (int)card.Y + 244, 22, map.IsDeadZone ? Palette.C(90, 230, 110) : Palette.C(220, 92, 82));

        Raylib.DrawText(map.Name, (int)card.X + 42, (int)card.Y + 174, 32, Color.White);
        DrawDifficultySkulls(map.Difficulty, new Vector2(card.X + 42, card.Y + 308));
        Raylib.DrawText("Click to deploy", (int)(card.X + card.Width - 172), (int)card.Y + 310, 20, Palette.C(170, 220, 255));
    }

    private static void DrawDifficultySkulls(int count, Vector2 origin)
    {
        Raylib.DrawText("Difficulty", (int)origin.X, (int)origin.Y - 22, 18, Color.LightGray);
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
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), Palette.C(8, 12, 20));
        DrawTitle("Storage", 48, 56);
        Raylib.DrawText("Equip items here before deployment. Extracted loot returns to this stash.", 70, 106, 24, Color.LightGray);
        Raylib.DrawText($"Capacity {GetStoredItemCount()}/{MetaProfile.StorageCapacity}", 70, 138, 22, Color.White);
        DrawSynthCoinsCounter(24, 138, 22);
        Raylib.DrawText("Hold X over an item to sell it. Mouse wheel scrolls stash.", 1000, 800, 20, Color.LightGray);

        Raylib.DrawRectangle(40, 190, 300, 600, Palette.C(10, 18, 30, 220));
        Raylib.DrawRectangleLinesEx(new Rectangle(40, 190, 300, 600), 2f, Palette.C(108, 170, 228));
        Raylib.DrawText("Loadout", 72, 164, 24, Color.White);
        Raylib.DrawText("Armor", 72, 240, 18, Color.LightGray);
        Raylib.DrawText("Primary", 72, 340, 18, Color.LightGray);
        Raylib.DrawText("Heavy", 72, 440, 18, Color.LightGray);
        Raylib.DrawText("Melee", 72, 540, 18, Color.LightGray);
        Raylib.DrawText("Consumables", 72, 640, 18, Color.LightGray);

        var runBackpackPanel = new Rectangle(400, 190, 460, 550);
        Raylib.DrawRectangleRec(runBackpackPanel, Palette.C(10, 18, 30, 220));
        Raylib.DrawRectangleLinesEx(runBackpackPanel, 2f, Palette.C(108, 170, 228));
        Raylib.DrawText("Run Backpack", 410, 164, 24, Color.White);
        DrawStorageGrid(new Vector2(410, 200), 5, 6);

        var stashPanel = StashPanelRect();
        Raylib.DrawRectangleRec(stashPanel, Palette.C(10, 18, 30, 220));
        Raylib.DrawRectangleLinesEx(stashPanel, 2f, Palette.C(108, 170, 228));
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
        }

        if (_drag is not null)
        {
            var m = Raylib.GetMousePosition();
            DrawItemIcon(_drag.Item, new Rectangle(m.X + 8, m.Y + 8, UiSlotSize, UiSlotSize), comparison, _drag.Kind);
        }

        if (_hovered is not null) DrawTooltip(_hovered, Raylib.GetMousePosition(), comparison);
    }

    private void DrawArmory()
    {
        var previewPlayer = CreateLandingPreviewPlayer();
        var comparison = new ComparisonContext(previewPlayer, _meta.Armor, _meta.RangedWeapon, _meta.HeavyWeapon, _meta.MeleeWeapon);
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), Palette.C(8, 12, 20));
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

        if (_hovered is not null) DrawTooltip(_hovered, Raylib.GetMousePosition(), comparison);
    }

    private void DrawArmoryOffer(ArmoryOffer offer, Rectangle rect, ComparisonContext comparison)
    {
        var disabled = offer.Purchased && !offer.Item.IsHeavyAmmo;
        var border = offer.Item.IsHeavyAmmo ? Palette.C(120, 210, 255) : offer.Item.Rarity == ArmorRarity.Epic ? Palette.C(191, 120, 255) : Color.SkyBlue;
        DrawStoreCardBackground(rect, disabled, border, Palette.C(10, 18, 30, 230));

        var iconRect = new Rectangle(rect.X + 10, rect.Y + 10, rect.Width - 20, rect.Height - 20);
        DrawItemIcon(offer.Item, iconRect, comparison);

        DrawStorePrice(rect, $"{GetArmoryPrice(offer.Item)} SC", Palette.C(120, 230, 255));
        if (disabled) DrawStoreDisabledOverlay(rect);
    }

    private void DrawTokenStoreOffer(TokenStoreOffer offer, Rectangle rect, ComparisonContext comparison)
    {
        var disabled = offer.Purchased;
        var border = Palette.C(210, 150, 255);
        DrawStoreCardBackground(rect, disabled, border, Palette.C(24, 14, 36, 235));

        var iconRect = new Rectangle(rect.X + 10, rect.Y + 10, rect.Width - 20, rect.Height - 20);
        DrawItemIcon(offer.Item, iconRect, comparison);

        var price = GetTokenStorePrice(offer);
        DrawStorePrice(rect, $"{price} CT", Palette.C(210, 150, 255));
        if (disabled) DrawStoreDisabledOverlay(rect);
    }

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
        var mouse = Raylib.GetMousePosition();
        ItemStack? hoveredReward = null;
        var comparison = new ComparisonContext(_player, _player.Armor, _player.RangedWeapon, _player.HeavyWeapon, _player.MeleeWeapon);
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), Palette.C(0, 0, 0, 170));
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
                if (Raylib.CheckCollisionPointRec(mouse, PitRewardWinningIconRect(i))) hoveredReward = item;
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
            if (stopped && slot == 0)
            {
                Raylib.DrawRectangleLinesEx(new Rectangle(x - 4, y - 4, iconSize + 8, iconSize + 8), 3f, Color.White);
            }
        }
        Raylib.EndScissorMode();
    }

    private void DrawCharacter()
    {
        var previewPlayer = CreateLandingPreviewPlayer();
        var rangedDamage = BuildWeaponDamageText(previewPlayer, previewPlayer.RangedWeapon, WeaponClass.Ranged);
        var heavyDamage = BuildWeaponDamageText(previewPlayer, previewPlayer.HeavyWeapon, WeaponClass.Ranged);
        var meleeDamage = BuildWeaponDamageText(previewPlayer, previewPlayer.MeleeWeapon, WeaponClass.Melee);

        DrawTitle("Character", 56, 60);
        Raylib.DrawText("Common landing stats", 74, 126, 28, Color.LightGray);

        var panel = new Rectangle(70, 170, 640, 320);
        Raylib.DrawRectangleRec(panel, Palette.C(10, 18, 30, 220));
        Raylib.DrawRectangleLinesEx(panel, 2f, Palette.C(108, 170, 228));
        Raylib.DrawText($"General level: {_meta.Level}", 96, 208, 28, Color.Gold);
        Raylib.DrawText($"Next level: {_meta.Score}/{GetMetaScoreRequired(_meta.Level)}", 96, 250, 24, Color.White);
        Raylib.DrawText($"Character stats: STR {_meta.BaseStrength} | DEX {_meta.BaseDexterity} | SPD {_meta.BaseSpeed} | GUN {_meta.BaseGuns}", 96, 292, 24, Color.White);
        Raylib.DrawText($"Landing HP: {previewPlayer.MaxHealth:0}", 96, 334, 24, Palette.C(140, 220, 160));
        Raylib.DrawText($"Move speed: x{previewPlayer.SpeedMultiplier:0.00}", 96, 368, 24, Palette.C(170, 220, 255));
        Raylib.DrawText($"Primary damage: {rangedDamage}", 96, 402, 24, Palette.C(255, 210, 120));
        Raylib.DrawText($"Heavy damage: {heavyDamage}", 96, 436, 24, Palette.C(255, 210, 120));
        Raylib.DrawText($"Melee damage: {meleeDamage}", 96, 470, 24, Palette.C(255, 180, 120));

        DrawButton(new Rectangle(70, 620, 220, 52), "Back");
    }

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
        var bar = new Rectangle(square.X + square.Width, square.Y, Raylib.GetScreenWidth() - (square.X + square.Width) - 24, square.Height);
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
        Raylib.DrawText("Video", (Raylib.GetScreenWidth() - Raylib.MeasureText("Video", 28)) / 2, 180, 28, Color.LightGray);
        DrawButton(CenterRect(0, 226, 360, 56), _displayMode == DisplayMode.Windowed ? "> Windowed <" : "Windowed");
        DrawButton(CenterRect(0, 290, 360, 56), _displayMode == DisplayMode.Fullscreen ? "> Fullscreen <" : "Fullscreen");

        Raylib.DrawText("Choose theme", (Raylib.GetScreenWidth() - Raylib.MeasureText("Choose theme", 28)) / 2, 360, 28, Color.LightGray);
        for (var i = 0; i < _themes.Count; i++)
        {
            var name = i == _themeIndex ? $"> {_themes[i].Name} <" : _themes[i].Name;
            DrawButton(CenterRect(0, 400 + i * 56, 390, 48), name);
        }

        DrawButton(CenterRect(0, 720, 280, 56), "Back");
    }

    private void DrawPause()
    {
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), Palette.C(0, 0, 0, 175));
        DrawTitle("Paused", 170, 64);
        DrawButton(CenterRect(0, 320, 320, 62), "Resume");
        DrawButton(CenterRect(0, 400, 320, 62), "Abandon run");
    }

    private void DrawDeath()
    {
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), Palette.C(0, 0, 0, 180));
        DrawTitle(_deathHeader, 150, 68);
        var lines = _deathBody.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            Raylib.DrawText(line, (Raylib.GetScreenWidth() - Raylib.MeasureText(line, 24)) / 2, 250 + i * 34, 24, Color.LightGray);
        }
        DrawButton(CenterRect(0, 320, 320, 62), "Deploy again");
        DrawButton(CenterRect(0, 400, 320, 62), "Main menu");
    }

    private void DrawNotice()
    {
        if (string.IsNullOrWhiteSpace(_noticeText)) return;

        var width = Math.Max(360, Raylib.MeasureText(_noticeText, 20) + 36);
        var rect = new Rectangle(Raylib.GetScreenWidth() - width - 30, 26, width, 46);
        Raylib.DrawRectangleRec(rect, Palette.C(12, 22, 36, 220));
        Raylib.DrawRectangleLinesEx(rect, 2f, Palette.C(110, 185, 240));
        Raylib.DrawText(_noticeText, (int)rect.X + 18, (int)rect.Y + 12, 20, Color.White);
    }

    private static void DrawTitle(string text, int y, int size)
    {
        var x = (Raylib.GetScreenWidth() - Raylib.MeasureText(text, size)) / 2;
        Raylib.DrawText(text, x, y, size, Color.White);
    }

    private static void DrawButton(Rectangle rect, string text, bool enabled = true)
    {
        var hover = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), rect);
        Raylib.DrawRectangleRec(rect, !enabled ? Palette.C(34, 38, 48) : hover ? Palette.C(68, 112, 186) : Palette.C(36, 56, 90));
        Raylib.DrawRectangleLinesEx(rect, 2f, enabled ? Color.White : Color.DarkGray);
        const int fs = 24;
        Raylib.DrawText(text, (int)(rect.X + rect.Width / 2 - Raylib.MeasureText(text, fs) / 2f), (int)(rect.Y + rect.Height / 2 - fs / 2f), fs, enabled ? Color.White : Color.Gray);
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
        var hover = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), rect);
        var fill = active
            ? Palette.C(92, 150, 235)
            : hover ? Palette.C(68, 112, 186) : Palette.C(36, 56, 90);
        var border = active ? Palette.C(180, 230, 255) : Color.White;

        Raylib.DrawRectangleRec(rect, fill);
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
            1 => "P",
            2 => "H",
            3 => "M",
            4 => "C",
            5 => "K",
            6 => "A",
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
        var w = Raylib.GetScreenWidth();
        var h = Raylib.GetScreenHeight();
        var thick = 12;

        Raylib.DrawRectangle(0, 0, w, thick, color);
        Raylib.DrawRectangle(0, h - thick, w, thick + 4, color);
        Raylib.DrawRectangle(0, 0, thick, h, color);
        Raylib.DrawRectangle(w - thick, 0, thick, h, color);
    }

    private Rectangle MainMenuButtonRect(int index)
        => new(70, Raylib.GetScreenHeight() - 404 + index * 60, 220, 48);

    private static Rectangle MapCardRect(int index)
        => new(70 + index * 585, 160, 555, 380);

    private static Rectangle DeploymentToggleRect()
        => new(Raylib.GetScreenWidth() - 292, 74, 220, 46);

    private static Rectangle PitRewardPanelRect()
        => new((Raylib.GetScreenWidth() - 1140f) / 2f, (Raylib.GetScreenHeight() - 680f) / 2f, 1140, 680);

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

    private static Rectangle MainMenuCodesButtonRect()
        => new(Raylib.GetScreenWidth() - 290, Raylib.GetScreenHeight() - 110, 220, 48);

    private static Rectangle CodesPopupRect()
        => new((Raylib.GetScreenWidth() - 470) / 2f, (Raylib.GetScreenHeight() - 240) / 2f, 470, 240);

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

    private static Rectangle CenterRect(int offsetX, int y, int w, int h) => new((Raylib.GetScreenWidth() - w) / 2f + offsetX, y, w, h);
    private static bool Clicked(Rectangle rect) => Raylib.IsMouseButtonPressed(MouseButton.Left) && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), rect);
}
