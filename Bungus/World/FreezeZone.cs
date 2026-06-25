using System.Numerics;
using System.Runtime.CompilerServices;
using Raylib_cs;

namespace Bungus.Game;

public sealed class FreezeZone
{
    public const float Radius = 110f;
    public const float FreezeDuration = 5f;
    public const float FadeDuration = 1f;
    public const float ChillDuration = 10f;
    public Vector2 Position { get; }
    public float FreezeTime { get; }
    public float ChillTime { get; }
    public float SlowStrengthMultiplier { get; }
    public float Life { get; private set; }
    public bool Freezing => Life > FadeDuration;
    public bool Alive => Life > 0f;
    public float Alpha => Math.Clamp(Life / FadeDuration, 0f, 1f);

    public FreezeZone(Vector2 position, float effectMultiplier = 1f)
    {
        Position = position;
        FreezeTime = FreezeDuration * effectMultiplier;
        ChillTime = ChillDuration * effectMultiplier;
        SlowStrengthMultiplier = effectMultiplier;
        Life = FreezeTime + FadeDuration;
    }

    public void Update(float dt) => Life -= dt;
    public bool Contains(Vector2 point, float radius = 0f) => Vector2.Distance(point, Position) <= Radius + radius;
}
