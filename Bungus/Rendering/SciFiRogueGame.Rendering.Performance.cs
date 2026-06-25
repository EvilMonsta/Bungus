using Raylib_cs;

namespace Bungus.Game;

public sealed partial class SciFiRogueGame
{
    private void DrawPerformanceOverlay()
    {
        if (!_showPerformanceOverlay) return;

        const int x = 12;
        const int y = 12;
        const int lineHeight = 18;
        const int width = 310;
        const int height = 214;

        Raylib.DrawRectangle(x - 6, y - 6, width, height, Palette.C(0, 0, 0, 210));
        Raylib.DrawRectangleLines(x - 6, y - 6, width, height, Palette.C(110, 190, 255, 190));

        var line = 0;
        DrawPerformanceLine("F3 performance", x, y + lineHeight * line++, Color.SkyBlue);
        DrawPerformanceLine($"fps: {Raylib.GetFPS()}  frame: {_smoothedFrameMs:0.00} ms", x, y + lineHeight * line++, Color.White);
        DrawPerformanceLine($"update: {_smoothedUpdateMs:0.00} ms", x, y + lineHeight * line++, Color.LightGray);
        DrawPerformanceLine($"draw:   {_smoothedDrawMs:0.00} ms", x, y + lineHeight * line++, Color.LightGray);
        DrawPerformanceLine($"fixed steps: {_fixedUpdateStepsLastFrame}", x, y + lineHeight * line++, Color.Gray);
        DrawPerformanceLine($"state: {_state}  bunker: {_inBunker}", x, y + lineHeight * line++, Color.Gray);
        DrawPerformanceLine($"projectiles: {_projectiles.Count}", x, y + lineHeight * line++, Color.White);
        DrawPerformanceLine($"enemies: {GetActiveEnemyCount()}", x, y + lineHeight * line++, Color.White);
        DrawPerformanceLine($"effects: {_explosions.Count + _beamEffects.Count + _lightningEffects.Count + _swings.Count}", x, y + lineHeight * line++, Color.White);
        DrawPerformanceLine($"trails: {_dashAfterImages.Count + _motionAfterImages.Count}", x, y + lineHeight * line++, Color.White);
        DrawPerformanceLine($"obstacles: {(_inBunker ? _bunkerObstacles.Count : _obstacles.Count)}", x, y + lineHeight * line, Color.White);
    }

    private int GetActiveEnemyCount()
    {
        var count = _enemies.Count
            + _hexEnemies.Count
            + _turrets.Count
            + _miniBosses.Count
            + _generatorGuards.Count
            + _toxicEnemies.Count
            + _pitStationBosses.Count
            + (_destroyerBoss is null ? 0 : 1)
            + (_stationBoss is null ? 0 : 1);

        if (!_inBunker) return count;

        return count
            + _bunkerScribs.Count
            + _bunkerParasites.Count
            + _bunkerSiegeEnemies.Count
            + _bunkerAssaultEnemies.Count
            + _bunkerInfectedEnemies.Count
            + (_bunkerTyrant is null ? 0 : 1);
    }

    private static void DrawPerformanceLine(string text, int x, int y, Color color)
        => Raylib.DrawText(text, x, y, 16, color);
}
