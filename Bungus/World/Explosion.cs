using System.Numerics;
using System.Runtime.CompilerServices;
using Raylib_cs;

namespace Bungus.Game;

public sealed class Explosion(Vector2 pos, float radius, Color color, bool filled = false, bool outlined = true, float fillAlpha = 0.22f)
{
    public Vector2 Position { get; private set; } = pos;
    public float Radius { get; private set; } = radius;
    public float MaxLife { get; } = 0.24f;
    public float Life { get; set; } = 0.24f;
    public Color Color { get; private set; } = color;
    public bool Filled { get; private set; } = filled;
    public bool Outlined { get; private set; } = outlined;
    public float FillAlpha { get; private set; } = fillAlpha;

    public void Reset(Vector2 position, float radius, Color color, bool filled = false, bool outlined = true, float fillAlpha = 0.22f)
    {
        Position = position;
        Radius = radius;
        Color = color;
        Filled = filled;
        Outlined = outlined;
        FillAlpha = fillAlpha;
        Life = MaxLife;
    }
}
