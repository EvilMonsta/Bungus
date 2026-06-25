using Raylib_cs;

namespace Bungus.Game;

public sealed class TokenStoreOfferSaveData
{
    public ItemStackSaveData? Item { get; set; }
    public int DiscountPercent { get; set; }
    public bool Purchased { get; set; }
}
