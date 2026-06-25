using System.Numerics;
using System.Runtime.CompilerServices;
using Raylib_cs;

namespace Bungus.Game;

public sealed class MotionAfterImage(Vector2 position, Color color, float alpha, float radius, MotionTrailShape shape, float rotationDegrees, float minRadius = -1f)
{
    public Vector2 Position { get; } = position;
    public Color Color { get; } = color;
    public float InitialAlpha { get; } = alpha;
    public float Radius { get; } = radius;
    public float MinRadius { get; } = minRadius < 0f ? radius : minRadius;
    public MotionTrailShape Shape { get; } = shape;
    public float RotationDegrees { get; } = rotationDegrees;
    public float Life { get; set; } = 1f;

    public void Draw()
    {
        var current = MathF.Max(0f, InitialAlpha * Life);
        var currentRadius = MinRadius + (Radius - MinRadius) * Life;
        var c = new Color(Color.R, Color.G, Color.B, (byte)(255 * current));
        switch (Shape)
        {
            case MotionTrailShape.Triangle:
                Raylib.DrawPoly(Position, 3, currentRadius, RotationDegrees + 90f, c);
                break;
            case MotionTrailShape.Square:
                Raylib.DrawRectanglePro(
                    new Rectangle(Position.X, Position.Y, currentRadius * 2f, currentRadius * 2f),
                    new Vector2(currentRadius, currentRadius),
                    RotationDegrees,
                    c);
                break;
            case MotionTrailShape.Hex:
                Raylib.DrawPoly(Position, 6, currentRadius, RotationDegrees, c);
                break;
            default:
                Raylib.DrawCircleV(Position, currentRadius, c);
                break;
        }
    }
}
