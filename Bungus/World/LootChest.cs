using System.Numerics;
using System.Runtime.CompilerServices;
using Raylib_cs;

namespace Bungus.Game;

public sealed class LootChest(Vector2 position, List<ItemStack> items, int? zoneId = null, LootContainerKind kind = LootContainerKind.Chest)
{
    public Vector2 Position { get; } = position;
    public List<ItemStack> Items { get; } = items;
    public int? ZoneId { get; } = zoneId;
    public LootContainerKind Kind { get; } = kind;
    public bool Opened { get; set; }
    public bool RequiresClear => Kind == LootContainerKind.Chest && ZoneId is not null;
}
