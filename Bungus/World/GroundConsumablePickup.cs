using System.Numerics;
using System.Runtime.CompilerServices;
using Raylib_cs;

namespace Bungus.Game;

public sealed class GroundConsumablePickup(Vector2 position, ItemStack item)
{
    public Vector2 Position { get; } = position;
    public ItemStack Item { get; } = item;
}
