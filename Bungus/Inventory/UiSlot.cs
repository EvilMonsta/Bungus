using Raylib_cs;

namespace Bungus.Game;

public sealed class UiSlot(Rectangle rect, SlotKind kind, int? index, ItemStack? item, int slotId)
{
    public Rectangle Rect { get; } = rect;
    public SlotKind Kind { get; } = kind;
    public int Index { get; } = index ?? -1;
    public ItemStack? Item { get; } = item;
    public int SlotId { get; } = slotId;
}
