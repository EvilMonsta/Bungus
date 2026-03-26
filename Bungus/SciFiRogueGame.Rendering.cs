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
            case GameState.Character:
                DrawCharacter();
                break;
            case GameState.Settings:
                DrawSettings();
                break;
            case GameState.Playing:
                DrawWorld();
                DrawHud();
                DrawInventory();
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

        foreach (var obstacle in _obstacles)
        {
            Raylib.DrawRectangleRec(obstacle.Rect, Theme.ObstacleFill);
            Raylib.DrawRectangleLinesEx(obstacle.Rect, 1.5f, Theme.ObstacleLine);
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

        foreach (var portal in _extractPortals) portal.Draw((float)Raylib.GetTime());

        foreach (var ghost in _dashAfterImages) ghost.Draw();

        foreach (var e in _enemies) e.DrawSight();
        foreach (var h in _hexEnemies) h.DrawSight();
        foreach (var t in _turrets) t.DrawSight();
        foreach (var b in _miniBosses) b.DrawSight();
        _destroyerBoss?.DrawSight();
        foreach (var e in _enemies) e.Draw(Theme);
        foreach (var h in _hexEnemies) h.Draw();
        foreach (var t in _turrets) t.Draw();
        foreach (var b in _miniBosses) b.Draw(Theme);
        _destroyerBoss?.Draw();
        foreach (var t in _turrets) t.DrawAimLine();

        foreach (var p in _projectiles)
        {
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

        Raylib.DrawRectangleLinesEx(new Rectangle(0, 0, World, World), 6f, Palette.C(120, 160, 220));
        Raylib.DrawCircleV(_player.Position, 16f, Theme.Player);
        Raylib.EndMode2D();
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
        Raylib.DrawText($"HP {_player.Health:0}/{_player.MaxHealth:0} | Level {_player.Level} ({_player.Kills}/{_player.KillsTarget})", 20, 14, 24, Color.White);

        var activeWeapon = _player.ActiveWeaponClass == WeaponClass.Ranged ? _player.RangedWeapon : _player.MeleeWeapon;
        Raylib.DrawText($"Current: {activeWeapon?.Name ?? "None"} {BuildWeaponDamageText(_player, activeWeapon, _player.ActiveWeaponClass)}", 20, 48, 22, activeWeapon?.Color ?? Color.LightGray);
        Raylib.DrawText($"Consumables: Q [{(_player.Inventory.QuickSlotQ?.Name ?? "-")}]  R [{(_player.Inventory.QuickSlotR?.Name ?? "-")}]", 20, 78, 20, Color.White);
        Raylib.DrawText($"Run score {_runScore}", 20, 108, 20, Color.Gold);
        DrawExtractionHud();
        Raylib.DrawText("WASD move | LMB attack | E switch active weapon | TAB inventory | ESC menu", 20, Raylib.GetScreenHeight() - 28, 18, Color.Gray);
        DrawZoneArrows();
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
            DrawButton(TakeAllButtonRect, "Take all");
        }

        foreach (var slot in slots)
        {
            Raylib.DrawRectangleRec(slot.Rect, Palette.C(22, 28, 42, 255));
            Raylib.DrawRectangleLinesEx(slot.Rect, 1f, Color.SkyBlue);
            if (slot.Kind == SlotKind.Trash) Raylib.DrawText("TR", (int)slot.Rect.X + 16, (int)slot.Rect.Y + 18, 20, Color.Orange);
            if (slot.Kind == SlotKind.QuickSlotQ) Raylib.DrawText("Q", (int)slot.Rect.X + 20, (int)slot.Rect.Y - 18, 16, Color.Green);
            if (slot.Kind == SlotKind.QuickSlotR) Raylib.DrawText("R", (int)slot.Rect.X + 20, (int)slot.Rect.Y - 18, 16, Color.Yellow);
            if (slot.Item is not null) DrawItemIcon(slot.Item, new Rectangle(slot.Rect.X + 8, slot.Rect.Y + 8, 42, 42));
        }

        if (_drag is not null)
        {
            var m = Raylib.GetMousePosition();
            DrawItemIcon(_drag.Item, new Rectangle(m.X + 8, m.Y + 8, 34, 34));
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

    private static void DrawItemIcon(ItemStack item, Rectangle rect)
    {
        if (item.Type == ItemType.Armor)
        {
            Raylib.DrawRectangleRec(rect, item.Color);
            Raylib.DrawText("AR", (int)rect.X + 8, (int)rect.Y + 10, 18, Color.Black);
        }
        else if (item.Type == ItemType.Weapon)
        {
            Raylib.DrawRectangleRec(rect, item.Color);
            Raylib.DrawText(item.WeaponKind == WeaponClass.Ranged ? "RW" : "MW", (int)rect.X + 4, (int)rect.Y + 10, 18, Color.Black);
        }
        else
        {
            Raylib.DrawRectangleRec(rect, Palette.C(130, 210, 120));
            Raylib.DrawText("CS", (int)rect.X + 8, (int)rect.Y + 10, 18, Color.Black);
        }

        if (item.Rarity == ArmorRarity.Damaged)
        {
            Raylib.DrawLineEx(new Vector2(rect.X + 4, rect.Y + 4), new Vector2(rect.X + rect.Width - 4, rect.Y + rect.Height - 4), 2.2f, Color.Red);
            Raylib.DrawLineEx(new Vector2(rect.X + rect.Width - 4, rect.Y + 4), new Vector2(rect.X + 4, rect.Y + rect.Height - 4), 2.2f, Color.Red);
        }
    }

    private static void DrawTooltip(ItemStack item, Vector2 mouse)
    {
        var x = (int)mouse.X + 20;
        var y = (int)mouse.Y + 14;
        Raylib.DrawRectangle(x, y, 300, 96, Palette.C(0, 0, 0, 220));
        Raylib.DrawRectangleLines(x, y, 300, 96, Color.SkyBlue);
        Raylib.DrawText(item.Name, x + 8, y + 8, 18, Color.White);
        Raylib.DrawText(item.Description, x + 8, y + 32, 16, Color.LightGray);
        if (item.Type == ItemType.Armor) Raylib.DrawText($"Defense: {item.Defense:0} | {item.Rarity}", x + 8, y + 58, 16, item.Color);
        if (item.Type == ItemType.Weapon) Raylib.DrawText($"Base damage: {item.BaseDamage:0} | {item.WeaponKind}", x + 8, y + 58, 16, item.Color);
        if (item.Type == ItemType.Consumable) Raylib.DrawText("Use by Q/R", x + 8, y + 58, 16, Color.Green);
    }

    private static void DrawPlus(Rectangle r)
    {
        Raylib.DrawRectangleRec(r, Palette.C(42, 95, 180));
        Raylib.DrawText("+", (int)r.X + 5, (int)r.Y - 1, 24, Color.White);
    }

    private void DrawGrid()
    {
        for (var x = 0; x < World; x += 80) Raylib.DrawLine(x, 0, x, World, Theme.Grid);
        for (var y = 0; y < World; y += 80) Raylib.DrawLine(0, y, World, y, Theme.Grid);
    }

    private void DrawMainMenu()
    {
        Raylib.DrawText("0.1.3", 86, 150, 24, Palette.C(150, 185, 220));
        DrawMetaProgressHeader();

        DrawButton(MainMenuButtonRect(0), "Play");
        DrawButton(MainMenuButtonRect(1), "Storage");
        DrawButton(MainMenuButtonRect(2), "Character");
        DrawButton(MainMenuButtonRect(3), "Settings");
        DrawButton(MainMenuButtonRect(4), "Exit");
    }

    private void DrawMapSelect()
    {
        DrawTitle("Select Map", 64, 66);
        Raylib.DrawText("Choose your landing zone", 72, 118, 26, Color.LightGray);

        var card = new Rectangle(340, 170, 600, 320);
        var hover = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), card);
        Raylib.DrawRectangleRec(card, hover ? Palette.C(22, 40, 62) : Palette.C(14, 24, 40));
        Raylib.DrawRectangleLinesEx(card, 2f, Palette.C(116, 180, 235));

        var sky = new Rectangle(card.X + 24, card.Y + 24, card.Width - 48, 120);
        Raylib.DrawRectangleRec(sky, Palette.C(34, 58, 86));
        Raylib.DrawCircleGradient((int)(sky.X + sky.Width - 90), (int)(sky.Y + 48), 42, Palette.C(250, 214, 120), Palette.C(250, 214, 120, 30));
        Raylib.DrawRectangle((int)card.X + 40, (int)card.Y + 220, 160, 54, Palette.C(60, 96, 126));
        Raylib.DrawRectangle((int)card.X + 240, (int)card.Y + 196, 122, 78, Palette.C(112, 74, 58));
        Raylib.DrawRectangle((int)card.X + 408, (int)card.Y + 182, 180, 92, Palette.C(138, 84, 64));
        Raylib.DrawCircle((int)card.X + 178, (int)card.Y + 248, 16, Palette.C(80, 170, 255));
        Raylib.DrawCircle((int)card.X + 498, (int)card.Y + 238, 24, Palette.C(220, 92, 82));
        Raylib.DrawLine((int)card.X + 54, (int)card.Y + 246, (int)card.X + 560, (int)card.Y + 246, Palette.C(210, 190, 160));

        Raylib.DrawText("Baselands", 378, 184, 34, Color.White);

        DrawButton(new Rectangle(340, 520, 280, 58), "Deploy");
        DrawButton(new Rectangle(70, 620, 220, 52), "Back");
    }

    private void DrawStorage()
    {
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), Palette.C(8, 12, 20));
        DrawTitle("Storage", 48, 56);
        Raylib.DrawText("Equip items here before deployment. Extracted loot returns to this stash.", 70, 106, 24, Color.LightGray);
        Raylib.DrawText($"Capacity {GetStoredItemCount()}/{MetaProfile.StorageCapacity}", 70, 138, 22, Color.White);

        Raylib.DrawRectangle(52, 170, 300, 380, Palette.C(10, 18, 30, 220));
        Raylib.DrawRectangleLinesEx(new Rectangle(52, 170, 300, 380), 2f, Palette.C(108, 170, 228));
        Raylib.DrawText("Loadout", 72, 184, 24, Color.White);
        Raylib.DrawText("Armor", 72, 236, 18, Color.LightGray);
        Raylib.DrawText("Ranged", 72, 304, 18, Color.LightGray);
        Raylib.DrawText("Melee", 72, 372, 18, Color.LightGray);
        Raylib.DrawText("Consumables", 72, 440, 18, Color.LightGray);

        Raylib.DrawRectangle(392, 116, 510, 530, Palette.C(10, 18, 30, 220));
        Raylib.DrawRectangleLinesEx(new Rectangle(392, 116, 510, 530), 2f, Palette.C(108, 170, 228));
        Raylib.DrawText("Stash", 414, 132, 24, Color.White);
        DrawStorageGrid(new Vector2(414, 174), 10, 10);

        var trashPanel = new Rectangle(1028, 170, 180, 220);
        Raylib.DrawRectangleRec(trashPanel, Palette.C(10, 18, 30, 220));
        Raylib.DrawRectangleLinesEx(trashPanel, 2f, Palette.C(108, 170, 228));
        Raylib.DrawText("Trash", 1080, 194, 24, Color.White);
        Raylib.DrawText("Right click sends", 1052, 338, 20, Color.LightGray);
        Raylib.DrawText("items here", 1080, 364, 20, Color.LightGray);

        DrawButton(new Rectangle(70, 620, 220, 52), "Back");

        var slots = BuildStorageSlots();
        foreach (var slot in slots)
        {
            Raylib.DrawRectangleRec(slot.Rect, Palette.C(22, 28, 42, 255));
            Raylib.DrawRectangleLinesEx(slot.Rect, 1f, slot.Kind == SlotKind.Trash ? Color.Orange : Color.SkyBlue);
            if (slot.Kind == SlotKind.Trash) Raylib.DrawText("TR", (int)slot.Rect.X + 14, (int)slot.Rect.Y + 14, 18, Color.Orange);
            if (slot.Kind == SlotKind.QuickSlotQ) Raylib.DrawText("Q", (int)slot.Rect.X + 15, (int)slot.Rect.Y - 18, 16, Color.Green);
            if (slot.Kind == SlotKind.QuickSlotR) Raylib.DrawText("R", (int)slot.Rect.X + 15, (int)slot.Rect.Y - 18, 16, Color.Yellow);
            if (slot.Item is not null) DrawItemIcon(slot.Item, new Rectangle(slot.Rect.X + 6, slot.Rect.Y + 6, slot.Rect.Width - 12, slot.Rect.Height - 12));
        }

        if (_drag is not null)
        {
            var m = Raylib.GetMousePosition();
            DrawItemIcon(_drag.Item, new Rectangle(m.X + 8, m.Y + 8, 32, 32));
        }

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
        Raylib.DrawText("Video", (Raylib.GetScreenWidth() - Raylib.MeasureText("Video", 28)) / 2, 118, 28, Color.LightGray);
        DrawButton(CenterRect(0, 156, 360, 56), _displayMode == DisplayMode.Windowed ? "> Windowed <" : "Windowed");
        DrawButton(CenterRect(0, 220, 360, 56), _displayMode == DisplayMode.Fullscreen ? "> Fullscreen <" : "Fullscreen");

        Raylib.DrawText("Choose theme", (Raylib.GetScreenWidth() - Raylib.MeasureText("Choose theme", 28)) / 2, 290, 28, Color.LightGray);
        for (var i = 0; i < _themes.Count; i++)
        {
            var name = i == _themeIndex ? $"> {_themes[i].Name} <" : _themes[i].Name;
            DrawButton(CenterRect(0, 330 + i * 56, 360, 48), name);
        }

        DrawButton(CenterRect(0, 620, 280, 56), "Back");
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
                var rect = new Rectangle(origin.X + c * 48, origin.Y + r * 46, 42, 42);
                Raylib.DrawRectangleLinesEx(rect, 1f, Palette.C(70, 90, 130, 170));
            }
        }
    }

    private void DrawExtractionHud()
    {
        var timerText = _extractPortals.Count == 0
            ? $"Portals in {FormatTime(_portalUnlockTimer)}"
            : $"Portals active {FormatTime(_portalActiveTimer)}";

        var color = _extractPortals.Count == 0 ? Color.LightGray : Palette.C(110, 215, 255);
        Raylib.DrawText(timerText, 20, 138, 22, color);
        Raylib.DrawText($"Map {_selectedMapName}", 20, 168, 20, Palette.C(165, 195, 220));
    }

    private Rectangle MainMenuButtonRect(int index)
        => new(70, Raylib.GetScreenHeight() - 344 + index * 60, 220, 48);

    private static Rectangle CenterRect(int offsetX, int y, int w, int h) => new((Raylib.GetScreenWidth() - w) / 2f + offsetX, y, w, h);
    private static bool Clicked(Rectangle rect) => Raylib.IsMouseButtonPressed(MouseButton.Left) && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), rect);
}
