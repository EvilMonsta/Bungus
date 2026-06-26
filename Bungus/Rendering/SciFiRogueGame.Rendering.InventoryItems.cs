using System.Numerics;
using Raylib_cs;

namespace Bungus.Game;

public sealed partial class SciFiRogueGame
{
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
            if (slot.Item is not null && IsInventorySlotSelected(slot))
            {
                Raylib.DrawRectangleLinesEx(slot.Rect, 4f, Palette.C(255, 220, 90));
            }
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

        if (relativePath.Contains(Path.Combine("Assets", "Icons", "Consumables"), StringComparison.OrdinalIgnoreCase))
        {
            Raylib.ImageColorReplace(ref image, Palette.C(138, 255, 166), Color.Blank);
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
        else lines.Add(("Use by Q/E", Color.Green));
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
}
