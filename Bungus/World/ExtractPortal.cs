using System.Numerics;
using System.Runtime.CompilerServices;
using Raylib_cs;

namespace Bungus.Game;

public sealed class ExtractPortal(Vector2 position, float seed)
{
    public Vector2 Position { get; } = position;
    public float Seed { get; } = seed;
    public float InteractionRadius { get; } = 34f;

    public void Draw(float time, bool active = true, bool emergency = false)
    {
        var fill = active
            ? emergency ? Palette.C(255, 90, 90, 110) : Palette.C(60, 150, 255, 110)
            : Palette.C(80, 40, 40, 90);
        var line = active
            ? emergency ? Palette.C(255, 160, 160) : Palette.C(120, 220, 255)
            : Palette.C(180, 90, 90);

        Raylib.DrawEllipse((int)Position.X, (int)Position.Y, 28f, 42f, fill);
        Raylib.DrawEllipseLines((int)Position.X, (int)Position.Y, 30f, 44f, line);

        for (var i = 0; i < 4; i++)
        {
            var speed = 0.6f + i * 0.32f;
            var angle = Seed + time * speed + i * MathF.PI * 0.5f;
            var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (8f + i * 3f);
            var size = 8f - i;
            var poly = active
                ? emergency ? Palette.C(255, 170 - i * 10, 170 - i * 10) : Palette.C(150 - i * 12, 220 - i * 10, 255)
                : Palette.C(165 - i * 10, 90 - i * 8, 90 - i * 8);
            Raylib.DrawPoly(Position + offset, 4, size, time * 100f * speed, poly);
        }
    }
}
