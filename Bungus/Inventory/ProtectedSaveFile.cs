using Raylib_cs;

namespace Bungus.Game;

public sealed class ProtectedSaveFile
{
    public int Version { get; set; } = 1;
    public string Iv { get; set; } = string.Empty;
    public string ProtectedPayload { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
}
