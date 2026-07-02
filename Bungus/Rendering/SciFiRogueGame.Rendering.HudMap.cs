using System.Numerics;
using Raylib_cs;

namespace Bungus.Game;

public sealed partial class SciFiRogueGame
{
    private void DrawRunIntroOverlay(bool forceOpaque = false)
    {
        var alpha = forceOpaque ? 1f : GetRunIntroAlpha();
        if (alpha <= 0f) return;

        var width = GetUiScreenWidth();
        var height = GetUiScreenHeight();
        var fill = WithAlpha(Mix(Opaque(Theme.Background), Color.Black, 0.42f), alpha);
        Raylib.DrawRectangle(0, 0, width, height, fill);

        var text = T("common.loading");
        const int fontSize = 42;
        var textColor = WithAlpha(Color.White, alpha);
        DrawUiText(text, width / 2 - MeasureUiText(text, fontSize) / 2, height / 2 - fontSize / 2, fontSize, textColor);
    }

    private void DrawHud()
    {
        if (!_challengeMode)
        {
            DrawExperienceBar();
            DrawUiText($"{T("hud.level")} {_player.Level} ({_player.Kills}/{_player.KillsTarget})", 20, 14, 24, Color.White);
        }
        else
        {
            DrawUiText($"{T("hud.pit_level")} {_player.Level}", 20, 14, 24, Color.White);
            var timer = float.IsPositiveInfinity(_pitWaveTimer) ? "?" : $"{MathF.Ceiling(MathF.Max(0f, _pitWaveTimer)):0}";
            DrawUiText(timer, GetUiScreenWidth() / 2 - MeasureUiText(timer, 56) / 2, 12, 56, Palette.C(130, 230, 255));
            var waveText = $"{T("hud.wave")} {Math.Max(1, _pitNextWave - 1)}";
            DrawUiText(waveText, GetUiScreenWidth() / 2 - MeasureUiText(waveText, 22) / 2, 72, 22, Color.White);
            if (_challengeKind == ChallengeKind.PitNightmare) DrawPitNightmareModifiers();
        }

        var activeWeapon = _player.ActiveWeapon;
        var activeWeaponName = activeWeapon is null ? T("common.none") : LocalizedItemName(activeWeapon);
        var quickQ = GetQuickConsumablePreview(0);
        var quickE = GetQuickConsumablePreview(1);
        DrawUiText($"{T("hud.current")}: {activeWeaponName} {BuildWeaponDamageText(_player, activeWeapon, _player.ActiveWeaponClass)}", 20, 48, 22, activeWeapon?.Color ?? Color.LightGray);
        DrawUiText($"{T("hud.consumables")}: Q [{(quickQ is null ? "-" : LocalizedItemName(quickQ))}]  E [{(quickE is null ? "-" : LocalizedItemName(quickE))}]", 20, 78, 20, Color.White);
        if (!_challengeMode) DrawUiText($"{T("hud.run_score")} {_runScore}", 20, 108, 20, Color.Gold);
        if (!_inBunker) DrawExtractionHud();
        DrawVitalBars();
        DrawLevelUpIndicator();
        DrawStatusEffects();
        if (_pitRewardOpen) DrawPitRewardSelection();
        if (_pitDifficultyOpen) DrawPitDifficultySelection();
        DrawUiText(T("hud.controls"), 20, GetUiScreenHeight() - 28, 18, Color.Gray);
        if (!_inBunker) DrawZoneArrows();
    }

    private void DrawConsumableSelector()
    {
        if (!IsConsumableSelectorOpen) return;

        var slot = _activeConsumableSelectorSlot;
        var options = GetAvailableQuickConsumables(slot);
        if (options.Count == 0) return;

        var center = GetConsumableSelectorCenter();
        var t = Math.Clamp(_consumableSelectorOpenTimer / 0.18f, 0f, 1f);
        var ease = 1f - MathF.Pow(1f - t, 3f);
        var outerRadius = 158f * ease;
        var innerRadius = 72f * ease;
        var iconRadius = 116f * ease;
        var selected = GetHoveredSelectorConsumable(slot) ?? GetSelectedQuickConsumableType(slot);
        var slice = 360f / options.Count;

        Raylib.DrawRectangle(0, 0, GetUiScreenWidth(), GetUiScreenHeight(), Palette.C(0, 0, 0, (byte)(70 * ease)));
        Raylib.DrawCircleV(center, outerRadius + 10f * ease, Palette.C(80, 170, 255, (byte)(22 * ease)));

        for (var i = 0; i < options.Count; i++)
        {
            var type = options[i];
            var start = i * slice + 2f;
            var end = (i + 1) * slice - 2f;
            var isSelected = selected == type;
            var baseColor = GetConsumableColor(type);
            var color = isSelected
                ? Palette.C(baseColor.R, baseColor.G, baseColor.B, (byte)(205 * ease))
                : Palette.C(16, 24, 38, (byte)(188 * ease));

            Raylib.DrawCircleSector(center, outerRadius, start, end, 48, color);
            Raylib.DrawCircleSectorLines(center, outerRadius, start, end, 48, isSelected
                ? Palette.C(235, 245, 255, (byte)(220 * ease))
                : Palette.C(92, 142, 190, (byte)(130 * ease)));

            var mid = (start + end) * 0.5f * MathF.PI / 180f;
            var iconPos = center + new Vector2(MathF.Cos(mid), MathF.Sin(mid)) * iconRadius;
            DrawConsumableSelectorIcon(type, iconPos, (isSelected ? 62f : 54f) * ease);
        }

        Raylib.DrawCircleV(center, innerRadius, Palette.C(7, 10, 18, (byte)(235 * ease)));
        Raylib.DrawCircleLines((int)center.X, (int)center.Y, innerRadius, Palette.C(120, 200, 255, (byte)(190 * ease)));
        Raylib.DrawCircleLines((int)center.X, (int)center.Y, outerRadius, Palette.C(235, 245, 255, (byte)(135 * ease)));

        var current = selected ?? GetSelectedQuickConsumableType(slot);
        if (current is not null) DrawConsumableSelectorIcon(current.Value, center, 72f * ease);
    }

    private void DrawConsumableSelectorIcon(ConsumableType type, Vector2 center, float size)
    {
        var rect = new Rectangle(center.X - size * 0.5f, center.Y - size * 0.5f, size, size);
        if (!TryDrawItemTexture(ItemStack.Consumable(type), rect))
        {
            Raylib.DrawCircleV(center, size * 0.32f, GetConsumableColor(type));
        }
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
        DrawUiText(T("hud.map"), (int)panel.X + 18, (int)panel.Y + 16, 28, Color.White);
        DrawUiText(T("hud.map_help"), (int)panel.X + 92, (int)panel.Y + 23, 18, Color.LightGray);

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
        DrawUiText(T("hud.level_up"), x + 34, y + 2, 22, Palette.C(120, 255, 140));
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
            var textWidth = MeasureUiText(label, fontSize);
            DrawUiText(label, (int)(center.X - textWidth * 0.5f), (int)(center.Y - fontSize * 0.5f), fontSize, Color.White);

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
                $"{T("hud.shield")} {_player.Shield:0}/{_player.ShieldCapacity:0}",
                18);
        }

        DrawStatusBar(hpRect, Math.Clamp(_player.Health / MathF.Max(_player.MaxHealth, 0.001f), 0f, 1f), Palette.C(196, 48, 48), Color.Black, $"HP {_player.Health:0}/{_player.MaxHealth:0}", 18);
        DrawStatusBar(dashRect, _player.DashCooldownProgress, Palette.C(72, 210, 96), Color.Black, string.Empty, 14);

        var heavyWeapon = _player.ActiveWeapon?.IsHeavyWeapon == true ? _player.ActiveWeapon : _player.HeavyWeapon;
        var ammoText = $"{T("hud.heavy_ammo")}: {_player.Inventory.GetHeavyAmmoShotCount(heavyWeapon)}";
        var ammoFont = 20;
        var ammoX = hpRect.X + hpRect.Width - MeasureUiText(ammoText, ammoFont);
        if (_player.IsLegendaryRocketPulseRifleEquipped)
        {
            ammoX -= 48f;
            DrawRocketPulseModeText(new Vector2(ammoX, hpRect.Y - 78), ammoFont);
            ammoX += 48f;
        }

        var knownCode = GetKnownTerminalCodeDisplay();
        if (!string.IsNullOrEmpty(knownCode))
        {
            var codeText = $"{T("hud.access_code")}: {knownCode}";
            var codeFont = 20;
            var codeX = hpRect.X + hpRect.Width - MeasureUiText(codeText, codeFont);
            DrawUiText(codeText, (int)codeX, (int)hpRect.Y - 106, codeFont, Palette.C(235, 205, 110));
        }

        DrawUiText(ammoText, (int)ammoX, (int)hpRect.Y - 78, ammoFont, Palette.C(120, 210, 255));
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
            var textWidth = MeasureUiText(label, fontSize);
            var textX = (int)(rect.X + rect.Width * 0.5f - textWidth * 0.5f);
            var textY = (int)(rect.Y + rect.Height * 0.5f - fontSize * 0.5f);
            DrawUiText(label, textX, textY, fontSize, Color.White);
        }
    }
}
