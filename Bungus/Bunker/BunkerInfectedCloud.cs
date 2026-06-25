using System.Numerics;
using Raylib_cs;

namespace Bungus.Game;

public sealed class BunkerInfectedCloud(Vector2 position, float radius, float lifetime, float fadeDuration = 0f)
{
    public Vector2 Position { get; } = position;
    public float Radius { get; } = radius;
    public float Life { get; private set; } = lifetime;
    public bool Alive => Life > 0f;
    public void Update(float dt) => Life = MathF.Max(0f, Life - dt);
    public void Draw()
    {
        var alpha = fadeDuration <= 0f ? 1f : Math.Clamp(Life / fadeDuration, 0f, 1f);
        Raylib.DrawCircleV(Position, Radius, Palette.C(46, 78, 42, (int)(255f * alpha)));
    }
}
