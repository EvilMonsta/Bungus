using System.Numerics;
using System.Runtime.CompilerServices;
using Raylib_cs;

namespace Bungus.Game;

public sealed class BeamEffect(Vector2 start, Vector2 end, Color color, float life, float thickness, bool flowing)
{
    public Vector2 Start { get; } = start;
    public Vector2 End { get; } = end;
    public Color Color { get; } = color;
    public float MaxLife { get; } = life;
    public float Life { get; set; } = life;
    public float Thickness { get; } = thickness;
    public bool Flowing { get; } = flowing;

    public void Draw()
    {
        var ratio = MaxLife <= 0f ? 0f : Math.Clamp(Life / MaxLife, 0f, 1f);
        if (ratio <= 0f) return;

        var main = new Color(Color.R, Color.G, Color.B, (byte)(210 * ratio));
        Raylib.DrawLineEx(Start, End, Thickness, main);

        if (!Flowing) return;

        var dir = End - Start;
        if (dir.LengthSquared() <= 0.001f) return;

        var normal = Vector2.Normalize(new Vector2(-dir.Y, dir.X));
        var pulse = (float)Raylib.GetTime() * 18f;
        for (var i = 0; i < 3; i++)
        {
            var offset = normal * MathF.Sin(pulse + i * 1.7f) * (2f + i);
            var c = new Color((byte)Math.Min(255, Color.R + 35), (byte)Math.Min(255, Color.G + 35), (byte)Math.Min(255, Color.B + 35), (byte)(90 * ratio));
            Raylib.DrawLineEx(Start + offset, End + offset, MathF.Max(1f, Thickness * 0.35f), c);
        }
    }
}
