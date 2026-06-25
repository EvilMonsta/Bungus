using System.Numerics;
using System.Runtime.CompilerServices;
using Raylib_cs;

namespace Bungus.Game;

public sealed class LightningEffect(Vector2 start, Vector2 end, float life = 0.18f)
{
    private readonly Vector2[] _points = BuildPoints(start, end);
    public float MaxLife { get; } = life;
    public float Life { get; set; } = life;
    public bool Alive => Life > 0f;

    public void Draw()
    {
        var ratio = MaxLife <= 0f ? 0f : Math.Clamp(Life / MaxLife, 0f, 1f);
        if (ratio <= 0f) return;

        for (var line = 0; line < 5; line++)
        {
            var alpha = (0.22f + line * 0.11f) * ratio;
            var color = new Color((byte)145, (byte)235, (byte)255, (byte)(255 * alpha));
            var thickness = line == 4 ? 2.6f : 1.2f;
            for (var i = 1; i < _points.Length; i++)
            {
                var wobble = new Vector2(MathF.Sin(i * 1.7f + line) * line, MathF.Cos(i * 1.3f + line) * line);
                Raylib.DrawLineEx(_points[i - 1] + wobble, _points[i] + wobble, thickness, color);
            }
        }
    }

    private static Vector2[] BuildPoints(Vector2 start, Vector2 end)
    {
        const int segments = 7;
        var points = new Vector2[segments + 1];
        var delta = end - start;
        var normal = delta.LengthSquared() <= 0.001f ? new Vector2(0f, 1f) : Vector2.Normalize(new Vector2(-delta.Y, delta.X));
        for (var i = 0; i <= segments; i++)
        {
            var t = i / (float)segments;
            var zig = i is 0 or segments ? 0f : (i % 2 == 0 ? -1f : 1f) * (8f + Random.Shared.NextSingle() * 10f);
            points[i] = Vector2.Lerp(start, end, t) + normal * zig;
        }

        return points;
    }
}
