using System.Numerics;
using Raylib_cs;

namespace Bungus.Game;

public sealed partial class SciFiRogueGame
{
    private void DrawStorage()
    {
        var previewPlayer = CreateLandingPreviewPlayer();
        DrawTitle(T("storage.title"), 48, 56);
        DrawUiText(T("storage.description"), 70, 106, 24, Color.LightGray);
        DrawUiText($"{T("storage.capacity")} {GetStoredItemCount()}/{MetaProfile.StorageCapacity}", 70, 138, 22, Color.White);
        DrawSynthCoinsCounter(24, 138, 22);
        DrawUiText(T("storage.selection_help"), 850, 800, 20, Color.LightGray);

        Raylib.DrawRectangle(40, 190, 300, 600, MenuPanelFill());
        Raylib.DrawRectangleLinesEx(new Rectangle(40, 190, 300, 600), 2f, MenuPanelLine());
        DrawUiText(T("storage.loadout"), 72, 164, 24, Color.White);
        DrawUiText(T("storage.armor"), 72, 240, 18, Color.LightGray);
        DrawUiText(T("storage.primary"), 72, 340, 18, Color.LightGray);
        DrawUiText(T("storage.heavy"), 72, 440, 18, Color.LightGray);
        DrawUiText(T("storage.melee"), 72, 540, 18, Color.LightGray);
        DrawUiText(T("storage.consumables"), 72, 640, 18, Color.LightGray);

        var runBackpackPanel = new Rectangle(400, 190, 460, 550);
        Raylib.DrawRectangleRec(runBackpackPanel, MenuPanelFill());
        Raylib.DrawRectangleLinesEx(runBackpackPanel, 2f, MenuPanelLine());
        DrawUiText(T("storage.run_backpack"), 410, 164, 24, Color.White);
        DrawStorageGrid(new Vector2(410, 200), 5, 6);

        var stashPanel = StashPanelRect();
        Raylib.DrawRectangleRec(stashPanel, MenuPanelFill());
        Raylib.DrawRectangleLinesEx(stashPanel, 2f, MenuPanelLine());
        DrawUiText(T("storage.stash"), 900, 164, 24, Color.White);
        var firstVisible = _storageScrollRow * StashGridColumns + 1;
        var lastVisible = Math.Min(_meta.StorageSlots.Count, (_storageScrollRow + StashVisibleRows) * StashGridColumns);
        Raylib.DrawText($"{firstVisible}-{lastVisible}", (int)stashPanel.X + 178, 112, 18, Color.LightGray);
        DrawStorageGrid(new Vector2(910, 200), StashGridColumns, StashVisibleRows);
        DrawStashScrollBar(stashPanel);
        DrawStorageSortButtons();

        DrawButton(StorageBackButtonRect(), T("common.back"));

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
        DrawTitle(T("armory.title"), 48, 56);
        DrawUiText(T("armory.description"), 70, 106, 24, Color.LightGray);
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

        DrawButton(ArmoryBackButtonRect(), T("common.back"));

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
        DrawUiText($"{T("pit.enemy_damage")}: +{_pitNightmareDamageBonusPercent:0}%", x, y, 20, Palette.C(255, 145, 145));
        DrawUiText($"{T("pit.enemy_health")}: +{_pitNightmareHealthBonusPercent:0}%", x, y + 26, 20, Palette.C(160, 255, 170));
        DrawUiText($"{T("pit.enemy_speed")}: +{_pitNightmareSpeedBonusPercent:0}%", x, y + 52, 20, Palette.C(150, 210, 255));
    }

    private void DrawPitDifficultySelection()
    {
        var ready = _pitDifficultySpinElapsed >= PitDifficultySpinDuration;
        var panel = PitDifficultyPanelRect();
        Raylib.DrawRectangle(0, 0, GetUiScreenWidth(), GetUiScreenHeight(), Palette.C(0, 0, 0, 170));
        Raylib.DrawRectangleRec(panel, Palette.C(10, 16, 28, 245));
        Raylib.DrawRectangleLinesEx(panel, 2f, Palette.C(210, 90, 120));
        DrawUiText(T("pit.nightmare_modifier"), (int)panel.X + 32, (int)panel.Y + 28, 34, Color.White);
        if (ready) DrawUiText(T("pit.next_modifier_active"), (int)panel.X + 32, (int)panel.Y + 72, 22, Color.LightGray);

        var card = PitDifficultyCardRect();
        Raylib.DrawRectangleRec(card, Palette.C(18, 26, 42, 245));
        Raylib.DrawRectangleLinesEx(card, 2f, Color.LightGray);
        DrawUiText(T("pit.modifier"), (int)card.X + 18, (int)card.Y + 18, 24, Color.White);
        DrawPitDifficultyRoulette(card);

        if (ready)
        {
            var result = FormatPitDifficultyOffer(_pitDifficultyOffer);
            DrawUiText(result, (int)card.X + 18, (int)card.Y + 56, 24, GetPitDifficultyColor(_pitDifficultyOffer.Kind));
        }

        DrawButton(PitDifficultyOkButtonRect(), T("common.ok"), ready);
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

    private string FormatPitDifficultyOffer(PitDifficultyOffer offer)
        => offer.Kind switch
        {
            'D' => $"{T("pit.damage")} +{offer.Percent:0}%",
            'H' => $"{T("pit.health")} +{offer.Percent:0}%",
            _ => $"{T("pit.speed")} +{offer.Percent:0}%"
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
        var textWidth = MeasureUiText(text, fontSize);
        var x = (int)(rect.X + rect.Width - textWidth - 7);
        var y = (int)rect.Y + 6;
        Raylib.DrawRectangle(x - 4, y - 3, textWidth + 8, fontSize + 6, Palette.C(0, 0, 0, 205));
        DrawUiText(text, x, y, fontSize, color);
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
        DrawUiText(T("pit.wave_reward"), (int)panel.X + 32, (int)panel.Y + 28, 34, Color.White);
        if (ready) DrawUiText(T("pit.reward_hint"), (int)panel.X + 32, (int)panel.Y + 72, 22, Color.LightGray);

        var labels = new[] { T("storage.melee"), T("storage.primary"), T("storage.heavy"), T("storage.armor") };
        for (var i = 0; i < _pitRewardOffers.Count; i++)
        {
            var rect = PitRewardCardRect(i);
            var item = _pitRewardOffers[i];
            Raylib.DrawRectangleRec(rect, Palette.C(18, 26, 42, 245));
            Raylib.DrawRectangleLinesEx(rect, 2f, Color.LightGray);
            DrawUiText(labels[i], (int)rect.X + 18, (int)rect.Y + 18, 24, Color.White);
            DrawPitRoulette(i, rect, comparison);

            if (_pitRewardSpinElapsed >= PitRewardSpinDurations[i])
            {
                DrawUiText(LocalizedItemName(item), (int)rect.X + 18, (int)rect.Y + 52, 18, item.Color);
                var rarity = LocalizedRarity(item.Rarity);
                DrawUiText(rarity, (int)rect.X + 18, (int)rect.Y + 76, 16, Color.LightGray);
                var winningIconRect = PitRewardWinningIconRect(i);
                if (Raylib.CheckCollisionPointRec(mouse, winningIconRect))
                {
                    hoveredReward = item;
                    DrawHoverOrbitFrame(winningIconRect, item.Color);
                }
            }

            var claimEnabled = ready && !_pitRewardClaimed[i];
            DrawButton(PitRewardTakeButtonRect(i), _pitRewardClaimed[i] ? T("pit.claimed") : T("pit.claim"), claimEnabled);
        }

        DrawButton(PitRewardSkipButtonRect(), T("pit.skip"), ready);
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

        DrawTitle(T("cradle.title"), 56, 60);
        DrawUiText(T("cradle.upgrades"), 74, 126, 28, Color.LightGray);

        var statPanel = new Rectangle(70, 170, 430, 390);
        Raylib.DrawRectangleRec(statPanel, MenuPanelFill());
        Raylib.DrawRectangleLinesEx(statPanel, 2f, MenuPanelLine());
        DrawUiText($"{T("cradle.general_level")}: {_meta.Level}", 96, 204, 26, Color.Gold);
        DrawUiText($"{T("cradle.next_level")}: {_meta.Score}/{GetMetaScoreRequired(_meta.Level)}", 96, 240, 22, Color.White);
        DrawCradleStatLine("HP", $"{previewPlayer.MaxHealth:0}", 96, 292, Palette.C(140, 220, 160));
        DrawCradleStatLine(T("cradle.speed"), $"x{previewPlayer.SpeedMultiplier:0.00}", 96, 324, Palette.C(170, 220, 255));
        DrawCradleStatLine(T("cradle.ranged_damage"), $"+{_meta.CradleGunsmith * 0.4f:0.0}%", 96, 356, Palette.C(255, 180, 100));
        DrawCradleStatLine(T("cradle.melee_damage"), $"+{_meta.CradleFighter * 0.4f:0.0}%", 96, 388, Palette.C(255, 180, 100));
        DrawCradleStatLine(T("cradle.melee_attack_speed"), $"+{_meta.CradleMeleeSpeed * 1.6f:0.0}%", 96, 420, Palette.C(150, 220, 255));
        DrawCradleStatLine(T("cradle.dash_recovery"), $"+{_meta.CradleDashRecovery:0}%", 96, 452, Palette.C(150, 240, 170));
        DrawCradleStatLine(T("cradle.stability"), $"{_meta.CradleStability:0}%", 96, 484, Color.White);
        DrawCradleStatLine(T("cradle.arcane"), $"+{_meta.CradleArcane:0}%", 96, 516, Palette.C(235, 85, 85));

        var freeRect = new Rectangle(1322, 74, 170, 54);
        Raylib.DrawRectangleRec(freeRect, MenuPanelFill(0.90f));
        Raylib.DrawRectangleLinesEx(freeRect, 2f, MenuPanelLine());
        DrawUiText(T("cradle.points"), (int)freeRect.X + 14, (int)freeRect.Y + 8, 18, Color.LightGray);
        var freeText = $"{GetAvailableCradleCells()}";
        DrawUiText(freeText, (int)(freeRect.X + freeRect.Width - MeasureUiText(freeText, 30) - 16), (int)freeRect.Y + 14, 30, Color.Gold);

        foreach (var track in CradleTracks) DrawCradleTrack(track);
        DrawCradleTrackTooltip();

        DrawButton(new Rectangle(70, 620, 220, 52), T("common.back"));
    }

    private static void DrawCradleStatLine(string label, string value, int x, int y, Color color)
    {
        const int fontSize = 20;
        DrawUiText($"{label}: {value}", x, y, fontSize, color);
    }

    private void DrawCradleTrack(CradleTrack track)
    {
        var row = GetCradleTrackIndex(track);
        var y = 176 + row * 54;
        var label = GetCradleTrackLabel(track);
        var active = _meta.GetCradleTrack(track);

        DrawUiText(label, 540, y + 5, 20, Color.LightGray);

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
        DrawUiText(GetCradleTrackBonusText(track, active), 1404, y + 5, 20, GetCradleTrackColor(track));
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
        DrawUiText(
            label,
            (int)(rect.X + rect.Width / 2f - MeasureUiText(label, fontSize) / 2f),
            (int)(rect.Y + rect.Height / 2f - fontSize / 2f),
            fontSize,
            line);
    }

    private string GetCradleTrackLabel(CradleTrack track) => track switch
    {
        CradleTrack.Health => T("cradle.health"),
        CradleTrack.Speed => T("cradle.speed"),
        CradleTrack.MeleeSpeed => T("cradle.melee_attack_speed"),
        CradleTrack.DashRecovery => T("cradle.dash_recovery"),
        CradleTrack.Stability => T("cradle.stability"),
        CradleTrack.Gunsmith => T("cradle.gunsmith"),
        CradleTrack.Fighter => T("cradle.fighter"),
        CradleTrack.Arcane => T("cradle.arcane"),
        _ => T("cradle.track")
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
        DrawUiText(title, (int)x + padding, (int)y + 12, 22, Color.White);
        for (var i = 0; i < bodyLines.Count; i++)
        {
            DrawUiText(bodyLines[i], (int)x + padding, (int)y + 44 + i * 24, 18, Color.LightGray);
        }
    }

    private string GetCradleTrackDescription(CradleTrack track) => track switch
    {
        CradleTrack.Health => T("cradle.health_desc"),
        CradleTrack.Speed => T("cradle.speed_desc"),
        CradleTrack.MeleeSpeed => T("cradle.melee_speed_desc"),
        CradleTrack.DashRecovery => T("cradle.dash_recovery_desc"),
        CradleTrack.Stability => T("cradle.stability_desc"),
        CradleTrack.Gunsmith => T("cradle.gunsmith_desc"),
        CradleTrack.Fighter => T("cradle.fighter_desc"),
        CradleTrack.Arcane => T("cradle.arcane_desc"),
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

        DrawUiText(label, x, y, fontSize, Color.LightGray);
        DrawUiText($"{value}", valueColumnX, y, fontSize, Color.LightGray);
        if (pending <= 0) return;

        var pendingText = $"(+{pending})";
        var pendingX = valueColumnX + 44;
        if (MeasureUiText(pendingText, fontSize) <= maxPendingWidth)
        {
            DrawUiText(pendingText, pendingX, y, fontSize, Color.LightGray);
            return;
        }

        DrawUiText(pendingText, x, y + 18, 16, Color.LightGray);
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
        DrawUiText($"{_meta.Level}", (int)(square.X + square.Width / 2f - MeasureUiText($"{_meta.Level}", 36) / 2f), (int)(square.Y + 20), 36, Color.Gold);

        Raylib.DrawRectangleRec(bar, Palette.C(14, 24, 40, 235));
        var fill = new Rectangle(bar.X + progressInset, bar.Y + progressInset, Math.Max(0, (bar.Width - progressInset * 2f) * progress), bar.Height - progressInset * 2f);
        Raylib.DrawRectangleRec(fill, Palette.C(72, 126, 196));
        Raylib.DrawRectangleLinesEx(bar, 2f, Palette.C(108, 170, 228));

        var progressText = $"{_meta.Score}/{required}";
        DrawUiText(progressText, (int)(bar.X + bar.Width / 2f - MeasureUiText(progressText, 28) / 2f), (int)(bar.Y + bar.Height / 2f - 14), 28, Color.White);
    }
}
