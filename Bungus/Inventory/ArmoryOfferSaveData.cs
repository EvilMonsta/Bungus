using Raylib_cs;

namespace Bungus.Game;

public sealed class ArmoryOfferSaveData
{
    public ItemStackSaveData? Item { get; set; }
    public bool Purchased { get; set; }
}
