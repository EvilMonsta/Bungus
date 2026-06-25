using Raylib_cs;

namespace Bungus.Game;

public sealed class DragPayload(SlotKind kind, int index, ItemStack item)
{
    public SlotKind Kind { get; } = kind;
    public int Index { get; } = index;
    public ItemStack Item { get; } = item;
}
