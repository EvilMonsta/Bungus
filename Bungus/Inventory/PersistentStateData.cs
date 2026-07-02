using Raylib_cs;

namespace Bungus.Game;

public sealed class PersistentStateData
{
    public int ThemeIndex { get; set; }
    public DisplayMode DisplayMode { get; set; } = DisplayMode.Windowed;
    public AntialiasingMode AntialiasingMode { get; set; } = AntialiasingMode.Msaa4x;
    public TextureFilteringMode TextureFilteringMode { get; set; } = TextureFilteringMode.Bilinear;
    public bool VSyncEnabled { get; set; }
    public int TargetFps { get; set; } = 60;
    public VisualEffectsIntensity VisualEffectsIntensity { get; set; } = VisualEffectsIntensity.Normal;
    public GameLanguage Language { get; set; } = GameLanguage.English;
    public bool DamageNumbersEnabled { get; set; } = true;
    public bool ScreenShakeEnabled { get; set; } = true;
    public string SelectedMapName { get; set; } = "Baselands";
    public bool IsFunnyNextRun { get; set; }
    public bool ToBunkerNextRun { get; set; }
    public Dictionary<string, int> PromoCodeUses { get; set; } = [];
    public MetaProfileSaveData Meta { get; set; } = new();
}
