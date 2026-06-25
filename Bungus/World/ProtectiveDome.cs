using System.Numerics;
using System.Runtime.CompilerServices;
using Raylib_cs;

namespace Bungus.Game;

public sealed class ProtectiveDome(Vector2 position)
{
    public const float Radius = 80f;
    public const float MaxHealth = 300f;
    private const float DecayTickInterval = 1f;
    private const float DecayPercentPerTick = 0.0333f;

    private readonly Dictionary<int, float> _contactCooldowns = [];
    private float _decayTimer;

    public Vector2 Position { get; } = position;
    public float Health { get; private set; } = MaxHealth;
    public bool Alive => Health > 0f;

    public void Update(float dt)
    {
        _decayTimer += dt;
        while (_decayTimer >= DecayTickInterval && Alive)
        {
            _decayTimer -= DecayTickInterval;
            Damage(MaxHealth * DecayPercentPerTick);
        }

        if (_contactCooldowns.Count == 0) return;

        var keys = _contactCooldowns.Keys.ToArray();
        foreach (var key in keys)
        {
            var value = _contactCooldowns[key] - dt;
            if (value <= 0f) _contactCooldowns.Remove(key);
            else _contactCooldowns[key] = value;
        }
    }

    public void Damage(float amount)
    {
        if (amount <= 0f || !Alive) return;
        Health = MathF.Max(0f, Health - amount);
    }

    public bool TryApplyContactDamage(object source, float amount, float cooldown)
    {
        if (!Alive) return false;

        var key = RuntimeHelpers.GetHashCode(source);
        if (_contactCooldowns.TryGetValue(key, out var timeLeft) && timeLeft > 0f) return false;

        Damage(amount);
        _contactCooldowns[key] = cooldown;
        return true;
    }
}
