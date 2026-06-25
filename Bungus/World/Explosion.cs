using System.Numerics;
using System.Runtime.CompilerServices;
using Raylib_cs;

namespace Bungus.Game;

public sealed class Explosion(Vector2 pos, float radius, Color color, bool filled = false, bool outlined = true, float fillAlpha = 0.22f)
{
    public Vector2 Position { get; } = pos;
    public float Radius { get; } = radius;
    public float MaxLife { get; } = 0.24f;
    public float Life { get; set; } = 0.24f;
    public Color Color { get; } = color;
    public bool Filled { get; } = filled;
    public bool Outlined { get; } = outlined;
    public float FillAlpha { get; } = fillAlpha;
}
