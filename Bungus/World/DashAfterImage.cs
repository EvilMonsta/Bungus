using System.Numerics;
using System.Runtime.CompilerServices;
using Raylib_cs;

namespace Bungus.Game;

public sealed class DashAfterImage(Vector2 position, Color color, float alpha, bool square)
{
    public Vector2 Position { get; } = position;
    public Color Color { get; } = color;
    public float InitialAlpha { get; } = alpha;
    public float Life { get; set; } = 1f;
    public bool Square { get; } = square;

    public void Draw()
    {
        var current = MathF.Max(0f, InitialAlpha * (Life / 1f));
        var c = new Color(Color.R, Color.G, Color.B, (byte)(255 * current));
        if (Square)
            Raylib.DrawRectangle((int)Position.X - 21, (int)Position.Y - 21, 42, 42, c);
        else
            Raylib.DrawCircleV(Position, 16f, c);
    }

    public static void Spawn(List<DashAfterImage> target, Vector2 endPosition, Vector2 dashDir, float distance, Color color, bool square)
    {
        var dir = dashDir == Vector2.Zero ? new Vector2(1f, 0f) : Vector2.Normalize(dashDir);
        var steps = new[]
        {
            (10.0f, 0.66f),
            (9.97f, 0.62f),
            (9.92f, 0.58f),
            (9.85f, 0.54f),
            (9.6f, 0.48f),
            (9.25f, 0.42f),
            (8.8f, 0.34f),
            (8.1f, 0.26f),
            (7.2f, 0.18f),
            (6.1f, 0.10f),
            (5.0f, 0.06f)
        };

        foreach (var (ratio, alpha) in steps)
        {
            target.Add(new DashAfterImage(endPosition - dir * (distance * (10f - ratio) / 10f), color, alpha, square));
        }
    }
}
