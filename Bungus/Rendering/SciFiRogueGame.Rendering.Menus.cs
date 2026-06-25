using System.Numerics;
using Raylib_cs;

namespace Bungus.Game;

public sealed partial class SciFiRogueGame
{
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
}
