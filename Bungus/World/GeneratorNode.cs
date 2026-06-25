using System.Numerics;
using System.Runtime.CompilerServices;
using Raylib_cs;

namespace Bungus.Game;

public sealed class GeneratorNode(Vector2 position, int zoneId)
{
    public Vector2 Position { get; } = position;
    public int ZoneId { get; } = zoneId;
    public float MaxHealth { get; } = 500f;
    public float Health { get; private set; } = 500f;
    public bool GuardDefeated { get; set; }
    public bool Destroyed => Health <= 0f;
    public bool Vulnerable => GuardDefeated && !Destroyed;

    public void Damage(float amount)
    {
        if (!Vulnerable || amount <= 0f) return;
        Health = MathF.Max(0f, Health - amount);
    }
}
