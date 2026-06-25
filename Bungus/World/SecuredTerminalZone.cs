using System.Numerics;
using System.Runtime.CompilerServices;
using Raylib_cs;

namespace Bungus.Game;

public sealed class SecuredTerminalZone(Vector2 position, string password)
{
    public Vector2 Position { get; } = position;
    public string Password { get; } = password;
    public bool Unlocked { get; set; }
    public Rectangle Rect => new(Position.X - 150f, Position.Y - 150f, 300f, 300f);
    public Vector2 HatchPosition => Position;
    public Vector2 TerminalPosition => Position + new Vector2(-46f, 38f);
    public float InteractionRadius { get; } = 34f;
}
