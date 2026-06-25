using System.Numerics;
using System.Runtime.CompilerServices;
using Raylib_cs;

namespace Bungus.Game;

public sealed class TerminalNote(Vector2 position, int index, string text)
{
    public Vector2 Position { get; } = position;
    public int Index { get; } = index;
    public string Text { get; } = text;
}
