using System.Numerics;
using Raylib_cs;

namespace Bungus.Game;

public sealed class BunkerToxicCloud(Vector2 position, float lifetime)
{
    public Vector2 Position { get; } = position;
    public float Radius { get; } = 112.5f;
    public float Life { get; private set; } = lifetime;
    public bool Alive => Life > 0f;

    public void Update(float dt) => Life = MathF.Max(0f, Life - dt);

    public void Draw()
    {
        var alpha = Math.Clamp(Life / 1f, 0f, 1f) * 0.25f;
        Raylib.DrawCircleV(Position, Radius, new Color((byte)92, (byte)150, (byte)62, (byte)(255f * alpha)));
        Raylib.DrawCircleLines((int)Position.X, (int)Position.Y, Radius, Palette.C(126, 190, 82, 150));
    }
}
