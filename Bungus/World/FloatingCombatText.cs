using System.Numerics;
using Raylib_cs;

namespace Bungus.Game;

public sealed class FloatingCombatText
{
    public Vector2 Position { get; private set; }
    public Vector2 Velocity { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public Color Color { get; private set; }
    public float Life { get; private set; }
    public float MaxLife { get; private set; }
    public float Size { get; private set; }

    public bool Alive => Life > 0f;

    public void Reset(Vector2 position, Vector2 velocity, string text, Color color, float life, float size)
    {
        Position = position;
        Velocity = velocity;
        Text = text;
        Color = color;
        Life = life;
        MaxLife = life;
        Size = size;
    }

    public void Update(float dt)
    {
        Position += Velocity * dt;
        Velocity *= MathF.Pow(0.18f, dt);
        Life -= dt;
    }

    public void Draw(Camera2D camera)
    {
        if (Life <= 0f || MaxLife <= 0f) return;

        var ratio = Math.Clamp(Life / MaxLife, 0f, 1f);
        var screen = Raylib.GetWorldToScreen2D(Position, camera);
        var fontSize = (int)MathF.Round(Size * (0.86f + (1f - ratio) * 0.18f));
        var alpha = (int)(255f * MathF.Min(1f, ratio * 1.35f));
        var color = Palette.C(Color.R, Color.G, Color.B, alpha);
        var shadow = Palette.C(0, 0, 0, (int)(180f * ratio));
        var width = Raylib.MeasureText(Text, fontSize);
        var x = (int)(screen.X - width * 0.5f);
        var y = (int)(screen.Y - fontSize * 0.5f);

        Raylib.DrawText(Text, x + 2, y + 2, fontSize, shadow);
        Raylib.DrawText(Text, x, y, fontSize, color);
    }
}
