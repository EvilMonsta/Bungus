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

        foreach (var e in _enemies) e.DrawSight();
        foreach (var h in _hexEnemies) h.DrawSight();
        foreach (var t in _turrets) t.DrawSight();
        foreach (var b in _miniBosses) b.DrawSight();
        foreach (var g in _generatorGuards) g.DrawSight();
        foreach (var toxic in _toxicEnemies) toxic.DrawSight();
        _destroyerBoss?.DrawSight();
        _stationBoss?.DrawSight();
        foreach (var e in _enemies) e.Draw(Theme);
        foreach (var h in _hexEnemies) h.Draw();
        foreach (var t in _turrets) t.Draw();
        foreach (var b in _miniBosses) b.Draw(Theme);
        foreach (var g in _generatorGuards) g.Draw();
        foreach (var toxic in _toxicEnemies) toxic.Draw();
        _destroyerBoss?.Draw();
        _stationBoss?.Draw();
        foreach (var t in _turrets) t.DrawAimLine();
        DrawPlayerSniperAimLine();

        foreach (var p in _projectiles)
        {
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
                VisibilityUtils.DrawDashedLine(swing.Origin, point, 14, color);
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
        DrawExperienceBar();
        Raylib.DrawText($"Level {_player.Level} ({_player.Kills}/{_player.KillsTarget})", 20, 14, 24, Color.White);

        var activeWeapon = _player.ActiveWeaponClass == WeaponClass.Ranged ? _player.RangedWeapon : _player.MeleeWeapon;
        Raylib.DrawText($"Current: {activeWeapon?.Name ?? "None"} {BuildWeaponDamageText(_player, activeWeapon, _player.ActiveWeaponClass)}", 20, 48, 22, activeWeapon?.Color ?? Color.LightGray);
        Raylib.DrawText($"Consumables: Q [{(_player.Inventory.QuickSlotQ?.Name ?? "-")}]  R [{(_player.Inventory.QuickSlotR?.Name ?? "-")}]", 20, 78, 20, Color.White);
        Raylib.DrawText($"Run score {_runScore}", 20, 108, 20, Color.Gold);
        DrawExtractionHud();
        DrawVitalBars();
        DrawLevelUpIndicator();
        DrawStatusEffects();
        Raylib.DrawText("WASD move | LMB attack | E switch active weapon | TAB inventory | ESC menu", 20, Raylib.GetScreenHeight() - 28, 18, Color.Gray);
        DrawZoneArrows();
    }

    private void DrawCombatCursor()
    {
        if (_player.InventoryOpen || _mapOpen) return;

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
            Raylib.DrawRectangle(32, 104, 1216, 460, Palette.C(6, 10, 20, 220));
            Raylib.DrawRectangleLines(32, 104, 1216, 460, Color.SkyBlue);
            Raylib.DrawText("Inventory", 42, 116, 24, Color.White);

            DrawBackpackGrid(new Vector2(700, 118), 6, 5);
            Raylib.DrawText("Backpack", 700, 86, 20, Color.LightGray);
            Raylib.DrawText("Equipment", 560, 86, 20, Color.LightGray);
            Raylib.DrawText("Stats", 54, 146, 20, Color.LightGray);

            Raylib.DrawText(BuildStatRow("STR", _player.Str, _pendingStrengthPoints), 54, 176, 20, Color.LightGray);
            Raylib.DrawText(BuildStatRow("DEX", _player.Dex, _pendingDexterityPoints), 54, 206, 20, Color.LightGray);
            Raylib.DrawText(BuildStatRow("SPD", _player.Spd, _pendingSpeedPoints), 54, 236, 20, Color.LightGray);
            Raylib.DrawText(BuildStatRow("GUN", _player.Guns, _pendingGunsmithPoints), 54, 266, 20, Color.LightGray);
            Raylib.DrawText($"Points {_player.StatPoints - GetPendingLevelUpPointCount()} free / {_player.StatPoints} total", 54, 296, 20, Color.Yellow);

            if (_player.StatPoints > 0)
            {
                DrawPlus(new Rectangle(252, 174, 22, 22));
                DrawPlus(new Rectangle(252, 204, 22, 22));
                DrawPlus(new Rectangle(252, 234, 22, 22));
                DrawPlus(new Rectangle(252, 264, 22, 22));
                if (GetPendingLevelUpPointCount() > 0)
                {
                    DrawButton(new Rectangle(54, 326, 120, 30), "Confirm");
                    DrawButton(new Rectangle(184, 326, 120, 30), "Reset");
                }
            }

            DrawStatTooltip();
        }
        else
        {
            Raylib.DrawRectangle(40, 138, 430, 370, Palette.C(6, 10, 20, 220));
            Raylib.DrawRectangleLines(40, 138, 430, 370, Color.SkyBlue);
            Raylib.DrawText("Backpack", 50, 150, 24, Color.White);
            DrawBackpackGrid(new Vector2(70, 190), 6, 5);

            Raylib.DrawRectangle(730, 138, 350, 170, Palette.C(6, 10, 20, 220));
            Raylib.DrawRectangleLines(730, 138, 350, 170, Color.SkyBlue);
            Raylib.DrawText("Chest", 740, 150, 24, Color.White);
            DrawBackpackGrid(new Vector2(760, 190), 5, 1);
            DrawButton(TakeAllButtonRect, "Take all [X]");
        }

        var comparison = new ComparisonContext(_player, _player.Armor, _player.RangedWeapon, _player.MeleeWeapon);
        foreach (var slot in slots)
        {
            Raylib.DrawRectangleRec(slot.Rect, Palette.C(22, 28, 42, 255));
            Raylib.DrawRectangleLinesEx(slot.Rect, 1f, Color.SkyBlue);
            if (slot.Kind == SlotKind.Trash) Raylib.DrawText("TR", (int)slot.Rect.X + 16, (int)slot.Rect.Y + 18, 20, Color.Orange);
            if (slot.Kind == SlotKind.QuickSlotQ) Raylib.DrawText("Q", (int)slot.Rect.X + 20, (int)slot.Rect.Y - 18, 16, Color.Green);
            if (slot.Kind == SlotKind.QuickSlotR) Raylib.DrawText("R", (int)slot.Rect.X + 20, (int)slot.Rect.Y - 18, 16, Color.Yellow);
            if (slot.Item is not null)
            {
                var iconRect = new Rectangle(slot.Rect.X + 8, slot.Rect.Y + 8, 42, 42);
                DrawItemIcon(slot.Item, iconRect, comparison, slot.Kind);
                DrawInventoryUseHoldFrame(slot, iconRect);
            }
        }

        if (_drag is not null)
        {
            var m = Raylib.GetMousePosition();
            DrawItemIcon(_drag.Item, new Rectangle(m.X + 8, m.Y + 8, 34, 34), comparison, _drag.Kind);
        }

        if (_hovered is not null) DrawTooltip(_hovered, Raylib.GetMousePosition());
    }

    private static void DrawBackpackGrid(Vector2 origin, int cols, int rows)
    {
        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                var rect = new Rectangle(origin.X + c * 62, origin.Y + r * 62, 58, 58);
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

    private void DrawItemIcon(ItemStack item, Rectangle rect, ComparisonContext? comparison = null, SlotKind? sourceKind = null)
    {
        var background = item.Type == ItemType.Consumable ? Palette.C(130, 210, 120) : item.Color;
        Raylib.DrawRectangleRec(rect, background);

        if (item.Type == ItemType.Armor)
        {
            DrawArmorIcon(rect);
            DrawItemTypeLabel(rect, "ar");
        }
        else if (item.Type == ItemType.Weapon)
        {
            if (item.WeaponKind == WeaponClass.Melee && item.Pattern is WeaponPattern.EnergySpear or WeaponPattern.Lancelot) DrawSpearIcon(rect);
            else if (item.WeaponKind == WeaponClass.Melee) DrawBladeIcon(rect);
            else if (item.Pattern == WeaponPattern.GrenadeLauncher) DrawGrenadeLauncherIcon(rect);
            else if (item.Pattern == WeaponPattern.SniperRifle) DrawSniperIcon(rect);
            else if (item.Pattern is WeaponPattern.PulseRifle or WeaponPattern.Toxikus) DrawPulseRifleIcon(rect);
            else DrawPistolIcon(rect);

            DrawItemTypeLabel(rect, item.WeaponKind == WeaponClass.Ranged ? "rw" : "mw");
        }
        else if (item.IsStationKey)
        {
            DrawStationKeyIcon(rect);
        }
        else
        {
            if (item.ConsumableKind == ConsumableType.Medkit) DrawMedKitIcon(rect);
            else if (item.ConsumableKind == ConsumableType.Stim) DrawStimIcon(rect);
            else if (item.ConsumableKind == ConsumableType.ProtectiveDome) DrawProtectiveDomeIcon(rect);
            else DrawStickyBulletsIcon(rect);
        }

        if (item.Rarity == ArmorRarity.Damaged)
        {
            Raylib.DrawLineEx(new Vector2(rect.X + 4, rect.Y + 4), new Vector2(rect.X + rect.Width - 4, rect.Y + rect.Height - 4), 2.2f, Color.Red);
            Raylib.DrawLineEx(new Vector2(rect.X + rect.Width - 4, rect.Y + 4), new Vector2(rect.X + 4, rect.Y + rect.Height - 4), 2.2f, Color.Red);
        }

        DrawComparisonMarker(item, rect, comparison, sourceKind);
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
        if (item.Type is ItemType.Consumable or ItemType.KeyItem) return ComparisonMarker.None;
        if (comparison is null) return ComparisonMarker.None;
        if (sourceKind is SlotKind.Armor or SlotKind.RangedWeapon or SlotKind.MeleeWeapon) return ComparisonMarker.None;

        if (item.Type == ItemType.Weapon)
        {
            if (item.WeaponKind is null) return ComparisonMarker.None;
            var equipped = item.WeaponKind == WeaponClass.Ranged ? comparison.RangedWeapon : comparison.MeleeWeapon;
            if (equipped is null || equipped.Type != ItemType.Weapon || equipped.WeaponKind != item.WeaponKind) return ComparisonMarker.Better;

            var candidateDamage = GetComparableWeaponDamage(comparison.StatsPlayer, item);
            var equippedDamage = GetComparableWeaponDamage(comparison.StatsPlayer, equipped);
            return CompareSingleValue(candidateDamage, equippedDamage);
        }

        var equippedArmor = comparison.Armor;
        if (equippedArmor is null || equippedArmor.Type != ItemType.Armor) return ComparisonMarker.Better;

        return CompareArmor(item, equippedArmor);
    }

    private static float GetComparableWeaponDamage(Player player, ItemStack weapon)
    {
        if (weapon.Pattern == WeaponPattern.SniperRifle) return player.GetSniperShotDamage(weapon);
        if (weapon.Pattern is WeaponPattern.PulseRifle or WeaponPattern.Toxikus) return player.GetPulseShotDamage(weapon) * player.GetPulseBurstShotCount(weapon);
        if (weapon.Pattern == WeaponPattern.GrenadeLauncher) return player.GetWeaponDamage(weapon) + 150f;
        if (weapon.WeaponKind == WeaponClass.Melee) return player.GetMeleeHitDamage(weapon);
        return player.GetWeaponDamage(weapon);
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

    private sealed record ComparisonContext(Player StatsPlayer, ItemStack? Armor, ItemStack? RangedWeapon, ItemStack? MeleeWeapon);

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

    private static void DrawTooltip(ItemStack item, Vector2 mouse)
    {
        var detailLines = BuildTooltipDetails(item);
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

    private static List<(string Text, Color Color)> BuildTooltipDetails(ItemStack item)
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
            return lines;
        }

        if (item.IsStationKey) lines.Add(("Key item | opens station entrance", item.Color));
        else lines.Add(("Use by Q/R", Color.Green));
        return lines;
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
        Raylib.DrawText("a0.2.2", 86, 150, 24, Palette.C(150, 185, 220));
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
        DrawTitle("Select Map", 64, 66);
        Raylib.DrawText("Choose your landing zone", 72, 118, 26, Color.LightGray);

        for (var i = 0; i < MapDefinition.All.Length; i++)
        {
            DrawMapCard(MapDefinition.All[i], MapCardRect(i));
        }

        DrawButton(new Rectangle(70, 620, 220, 52), "Back");
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
        Raylib.DrawText("Hold X over an item to sell it.", 742, 668, 20, Color.LightGray);

        Raylib.DrawRectangle(52, 170, 300, 380, Palette.C(10, 18, 30, 220));
        Raylib.DrawRectangleLinesEx(new Rectangle(52, 170, 300, 380), 2f, Palette.C(108, 170, 228));
        Raylib.DrawText("Loadout", 72, 184, 24, Color.White);
        Raylib.DrawText("Armor", 72, 236, 18, Color.LightGray);
        Raylib.DrawText("Ranged", 72, 304, 18, Color.LightGray);
        Raylib.DrawText("Melee", 72, 372, 18, Color.LightGray);
        Raylib.DrawText("Consumables", 72, 440, 18, Color.LightGray);

        var runBackpackPanel = new Rectangle(392, 154, 318, 304);
        Raylib.DrawRectangleRec(runBackpackPanel, Palette.C(10, 18, 30, 220));
        Raylib.DrawRectangleLinesEx(runBackpackPanel, 2f, Palette.C(108, 170, 228));
        Raylib.DrawText("Run Backpack", 416, 170, 24, Color.White);
        DrawStorageGrid(new Vector2(418, 228), 6, 5);

        Raylib.DrawRectangle(720, 154, 510, 496, Palette.C(10, 18, 30, 220));
        Raylib.DrawRectangleLinesEx(new Rectangle(720, 154, 510, 496), 2f, Palette.C(108, 170, 228));
        Raylib.DrawText("Stash", 742, 170, 24, Color.White);
        DrawStorageGrid(new Vector2(742, 206), 10, 10);

        DrawButton(new Rectangle(70, 620, 220, 52), "Back");

        var slots = BuildStorageSlots();
        var comparison = new ComparisonContext(previewPlayer, _meta.Armor, _meta.RangedWeapon, _meta.MeleeWeapon);
        foreach (var slot in slots)
        {
            Raylib.DrawRectangleRec(slot.Rect, Palette.C(22, 28, 42, 255));
            Raylib.DrawRectangleLinesEx(slot.Rect, 1f, Color.SkyBlue);
            if (slot.Kind == SlotKind.QuickSlotQ) Raylib.DrawText("Q", (int)slot.Rect.X + 15, (int)slot.Rect.Y - 18, 16, Color.Green);
            if (slot.Kind == SlotKind.QuickSlotR) Raylib.DrawText("R", (int)slot.Rect.X + 15, (int)slot.Rect.Y - 18, 16, Color.Yellow);
            if (slot.Item is not null)
            {
                var iconRect = new Rectangle(slot.Rect.X + 6, slot.Rect.Y + 6, slot.Rect.Width - 12, slot.Rect.Height - 12);
                DrawItemIcon(slot.Item, iconRect, comparison, slot.Kind);
                DrawInventoryUseHoldFrame(slot, iconRect);
            }
        }

        if (_drag is not null)
        {
            var m = Raylib.GetMousePosition();
            DrawItemIcon(_drag.Item, new Rectangle(m.X + 8, m.Y + 8, 32, 32), comparison, _drag.Kind);
        }

        if (_hovered is not null) DrawTooltip(_hovered, Raylib.GetMousePosition());
    }

    private void DrawArmory()
    {
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), Palette.C(8, 12, 20));
        DrawTitle("Armory", 48, 56);
        Raylib.DrawText("Buy equipment for SynthCoins. Stock refreshes after each run.", 70, 106, 24, Color.LightGray);
        DrawSynthCoinsCounter(70, 138, 24);

        for (var i = 0; i < _meta.ArmoryOffers.Count; i++)
        {
            var offer = _meta.ArmoryOffers[i];
            var rect = ArmoryOfferRect(i);
            var disabled = offer.Purchased;
            var border = offer.Item.Rarity == ArmorRarity.Epic ? Palette.C(191, 120, 255) : Color.SkyBlue;

            Raylib.DrawRectangleRec(rect, disabled ? Palette.C(18, 20, 26, 190) : Palette.C(10, 18, 30, 230));
            Raylib.DrawRectangleLinesEx(rect, 2f, disabled ? Color.DarkGray : border);

            var iconRect = new Rectangle(rect.X + 14, rect.Y + 18, 58, 58);
            DrawItemIcon(offer.Item, iconRect);

            Raylib.DrawText(offer.Item.Name, (int)rect.X + 84, (int)rect.Y + 20, 18, disabled ? Color.Gray : Color.White);
            Raylib.DrawText(offer.Item.Type == ItemType.Armor ? "Armor" : offer.Item.WeaponKind == WeaponClass.Ranged ? "Ranged" : "Melee", (int)rect.X + 84, (int)rect.Y + 46, 16, Color.LightGray);
            Raylib.DrawText($"{GetArmoryPrice(offer.Item)} SC", (int)rect.X + 84, (int)rect.Y + 70, 18, Palette.C(120, 230, 255));

            if (offer.Item.Type == ItemType.Armor && GetArmorModifierCount(offer.Item) > 0)
            {
                Raylib.DrawText($"+{GetArmorModifierCount(offer.Item) * 20}% mods", (int)rect.X + 14, (int)rect.Y + 92, 16, Color.Gold);
            }
            else if (offer.Item.Type == ItemType.Weapon)
            {
                Raylib.DrawText($"Damage {offer.Item.BaseDamage:0.0}", (int)rect.X + 14, (int)rect.Y + 92, 16, Color.Gold);
            }

            Raylib.DrawText(disabled ? "Purchased" : "Click to buy", (int)rect.X + 14, (int)rect.Y + 140, 18, disabled ? Color.Gray : Color.Green);
        }

        DrawButton(new Rectangle(70, 620, 220, 52), "Back");

        if (_hovered is not null) DrawTooltip(_hovered, Raylib.GetMousePosition());
    }

    private void DrawCharacter()
    {
        var previewPlayer = CreateLandingPreviewPlayer();
        var rangedDamage = BuildWeaponDamageText(previewPlayer, previewPlayer.RangedWeapon, WeaponClass.Ranged);
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
        Raylib.DrawText($"Ranged damage: {rangedDamage}", 96, 402, 24, Palette.C(255, 210, 120));
        Raylib.DrawText($"Melee damage: {meleeDamage}", 96, 436, 24, Palette.C(255, 180, 120));

        DrawButton(new Rectangle(70, 620, 220, 52), "Back");
    }

    private static string BuildStatRow(string label, int value, int pending)
        => pending > 0 ? $"{label} {value} (+{pending})" : $"{label} {value}";

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
        Raylib.DrawText(_deathBody, (Raylib.GetScreenWidth() - Raylib.MeasureText(_deathBody, 24)) / 2, 250, 24, Color.LightGray);
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

    private static void DrawButton(Rectangle rect, string text)
    {
        var hover = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), rect);
        Raylib.DrawRectangleRec(rect, hover ? Palette.C(68, 112, 186) : Palette.C(36, 56, 90));
        Raylib.DrawRectangleLinesEx(rect, 2f, Color.White);
        const int fs = 24;
        Raylib.DrawText(text, (int)(rect.X + rect.Width / 2 - Raylib.MeasureText(text, fs) / 2f), (int)(rect.Y + rect.Height / 2 - fs / 2f), fs, Color.White);
    }

    private static void DrawStorageGrid(Vector2 origin, int cols, int rows)
    {
        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                var rect = new Rectangle(origin.X + c * 48, origin.Y + r * 44, 42, 42);
                Raylib.DrawRectangleLinesEx(rect, 1f, Palette.C(70, 90, 130, 170));
            }
        }
    }

    private void DrawExtractionHud()
    {
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

    private static Rectangle ArmoryOfferRect(int index)
    {
        var col = index % 5;
        var row = index / 5;
        return new Rectangle(70 + col * 236, 178 + row * 210, 214, 180);
    }

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
