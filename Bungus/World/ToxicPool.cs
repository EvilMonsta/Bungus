using System.Numerics;
using System.Runtime.CompilerServices;
using Raylib_cs;

namespace Bungus.Game;

public sealed class ToxicPool(Vector2 position, float radiusX, float radiusY)
{
    public Vector2 Position { get; } = position;
    public float RadiusX { get; } = radiusX;
    public float RadiusY { get; } = radiusY;

    public bool Contains(Vector2 point)
    {
        var dx = (point.X - Position.X) / MathF.Max(RadiusX, 0.001f);
        var dy = (point.Y - Position.Y) / MathF.Max(RadiusY, 0.001f);
        return dx * dx + dy * dy <= 1f;
    }
}
