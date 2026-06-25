using System.Numerics;
using Raylib_cs;

namespace Bungus.Game;

public enum VisualParticleShape
{
    Spark,
    Smoke,
    Glow,
    Shard
}

public sealed class VisualParticle
{
    public Vector2 Position { get; private set; }
    public Vector2 Velocity { get; private set; }
    public Color Color { get; private set; }
    public float Size { get; private set; }
    public float StartSize { get; private set; }
    public float Life { get; private set; }
    public float MaxLife { get; private set; }
    public float Rotation { get; private set; }
    public float Spin { get; private set; }
    public VisualParticleShape Shape { get; private set; }

    public bool Alive => Life > 0f;

    public void Reset(Vector2 position, Vector2 velocity, Color color, float size, float life, VisualParticleShape shape, float rotation = 0f, float spin = 0f)
    {
        Position = position;
        Velocity = velocity;
        Color = color;
        Size = size;
        StartSize = size;
        Life = life;
        MaxLife = life;
        Shape = shape;
        Rotation = rotation;
        Spin = spin;
    }

    public void Update(float dt)
    {
        Position += Velocity * dt;
        Velocity *= MathF.Pow(0.05f, dt);
        Rotation += Spin * dt;
        Life -= dt;

        var ratio = MaxLife <= 0f ? 0f : Math.Clamp(Life / MaxLife, 0f, 1f);
        Size = StartSize * (0.35f + ratio * 0.65f);
    }

    public void Draw()
    {
        if (Life <= 0f || MaxLife <= 0f) return;

        var ratio = Math.Clamp(Life / MaxLife, 0f, 1f);
        var alpha = (byte)Math.Clamp(Color.A * ratio, 0f, 255f);
        var color = new Color(Color.R, Color.G, Color.B, alpha);

        switch (Shape)
        {
            case VisualParticleShape.Spark:
                var direction = Velocity.LengthSquared() <= 0.001f ? new Vector2(1f, 0f) : Vector2.Normalize(Velocity);
                Raylib.DrawLineEx(Position - direction * Size * 1.2f, Position + direction * Size * 1.8f, MathF.Max(1f, Size * 0.28f), color);
                break;
            case VisualParticleShape.Shard:
                Raylib.DrawPoly(Position, 3, Size, Rotation, color);
                break;
            case VisualParticleShape.Smoke:
                Raylib.DrawCircleV(Position, Size * (1.4f - ratio * 0.35f), color);
                break;
            default:
                Raylib.DrawCircleGradient((int)Position.X, (int)Position.Y, Size * 1.61f, color, Palette.C(Color.R, Color.G, Color.B, 0));
                break;
        }
    }
}
