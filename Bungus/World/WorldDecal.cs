using System.Numerics;
using Raylib_cs;

namespace Bungus.Game;

public enum WorldDecalKind
{
    Crack,
    Plate,
    Cable,
    LightStrip,
    Scorch,
    Vent
}

public sealed class WorldDecal(Vector2 position, Vector2 size, float rotation, Color color, WorldDecalKind kind)
{
    public Vector2 Position { get; } = position;
    public Vector2 Size { get; } = size;
    public float Rotation { get; } = rotation;
    public Color Color { get; } = color;
    public WorldDecalKind Kind { get; } = kind;

    public void Draw()
    {
        switch (Kind)
        {
            case WorldDecalKind.Crack:
                DrawCrack();
                break;
            case WorldDecalKind.Cable:
                DrawCable();
                break;
            case WorldDecalKind.LightStrip:
                DrawLightStrip();
                break;
            case WorldDecalKind.Scorch:
                Raylib.DrawCircleGradient((int)Position.X, (int)Position.Y, Size.X, Color, Palette.C(Color.R, Color.G, Color.B, 0));
                break;
            case WorldDecalKind.Vent:
                DrawVent();
                break;
            default:
                Raylib.DrawRectanglePro(new Rectangle(Position.X, Position.Y, Size.X, Size.Y), Size * 0.5f, Rotation, Color);
                break;
        }
    }

    private void DrawCrack()
    {
        var dir = FromAngle(Rotation);
        var normal = new Vector2(-dir.Y, dir.X);
        var start = Position - dir * Size.X * 0.5f;
        var segments = 4;
        for (var i = 0; i < segments; i++)
        {
            var a = start + dir * (Size.X * i / segments) + normal * MathF.Sin(i * 1.91f + Rotation) * Size.Y;
            var b = start + dir * (Size.X * (i + 1) / segments) + normal * MathF.Sin((i + 1) * 1.91f + Rotation) * Size.Y;
            Raylib.DrawLineEx(a, b, MathF.Max(1f, Size.Y * 0.28f), Color);
        }
    }

    private void DrawCable()
    {
        var dir = FromAngle(Rotation);
        var normal = new Vector2(-dir.Y, dir.X);
        var start = Position - dir * Size.X * 0.5f;
        var end = Position + dir * Size.X * 0.5f;
        Raylib.DrawLineEx(start, end, Size.Y, Color);
        Raylib.DrawLineEx(start + normal * Size.Y * 1.5f, end + normal * Size.Y * 1.5f, MathF.Max(1f, Size.Y * 0.55f), Palette.C(Color.R, Color.G, Color.B, Math.Min(180, (int)Color.A)));
    }

    private void DrawLightStrip()
    {
        var rect = new Rectangle(Position.X, Position.Y, Size.X, Size.Y);
        Raylib.DrawRectanglePro(rect, Size * 0.5f, Rotation, Palette.C(Color.R, Color.G, Color.B, Math.Min(90, (int)Color.A)));
        Raylib.BeginBlendMode(BlendMode.Additive);
        Raylib.DrawRectanglePro(rect, Size * 0.5f, Rotation, Color);
        Raylib.EndBlendMode();
    }

    private void DrawVent()
    {
        Raylib.DrawRectanglePro(new Rectangle(Position.X, Position.Y, Size.X, Size.Y), Size * 0.5f, Rotation, Color);
        var dir = FromAngle(Rotation);
        var normal = new Vector2(-dir.Y, dir.X);
        for (var i = -2; i <= 2; i++)
        {
            var center = Position + normal * i * Size.Y * 0.16f;
            Raylib.DrawLineEx(center - dir * Size.X * 0.35f, center + dir * Size.X * 0.35f, 1.2f, Palette.C(8, 10, 12, 150));
        }
    }

    private static Vector2 FromAngle(float degrees)
    {
        var radians = degrees * MathF.PI / 180f;
        return new Vector2(MathF.Cos(radians), MathF.Sin(radians));
    }
}
