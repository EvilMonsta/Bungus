using System.Numerics;
using Raylib_cs;

namespace Bungus.Game;

public sealed partial class SciFiRogueGame
{
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
