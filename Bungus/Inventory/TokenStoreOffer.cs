using Raylib_cs;

namespace Bungus.Game;

public sealed class TokenStoreOffer
{
    public ItemStack Item { get; set; } = ItemStack.Pulsar(new Random());
    public int DiscountPercent { get; set; }
    public bool Purchased { get; set; }
}
