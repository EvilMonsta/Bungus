using System.Numerics;
using Raylib_cs;

namespace Bungus.Game;

public sealed class TyrantGrenadeWarning(Vector2 position)
{
    public Vector2 Position { get; } = position;
    public float Timer { get; set; } = 0.5f;
}
