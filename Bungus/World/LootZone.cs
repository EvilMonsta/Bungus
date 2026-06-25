using System.Numerics;
using System.Runtime.CompilerServices;
using Raylib_cs;

namespace Bungus.Game;

public sealed class LootZone
{
    public LootZone(int id, Rectangle rect, bool isOutpost)
        : this(id, rect, isOutpost ? LootZoneKind.Outpost : LootZoneKind.City)
    {
    }

    public LootZone(int id, Rectangle rect, LootZoneKind kind)
    {
        Id = id;
        Rect = rect;
        Kind = kind;
    }

    public int Id { get; }
    public Rectangle Rect { get; }
    public LootZoneKind Kind { get; }
    public bool IsOutpost => Kind == LootZoneKind.Outpost;
    public Vector2 Center => new(Rect.X + Rect.Width / 2f, Rect.Y + Rect.Height / 2f);
}
