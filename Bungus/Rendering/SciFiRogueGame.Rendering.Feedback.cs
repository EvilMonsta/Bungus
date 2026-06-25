using Raylib_cs;

namespace Bungus.Game;

public sealed partial class SciFiRogueGame
{
    private void DrawCinematicPostProcess()
    {
        if (_state != GameState.Playing) return;
    }

    private void DrawFloatingCombatTexts()
    {
        if (_state != GameState.Playing) return;
        if (_floatingCombatTexts.Count == 0) return;

        var camera = GetRenderCamera();
        foreach (var text in _floatingCombatTexts)
        {
            text.Draw(camera);
        }
    }
}
