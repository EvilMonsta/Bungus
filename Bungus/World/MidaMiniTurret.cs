using System.Numerics;
using System.Runtime.CompilerServices;
using Raylib_cs;

namespace Bungus.Game;

public sealed class MidaMiniTurret(Vector2 position)
{
    public const float Range = 500f;
    public const float Lifetime = 15f;
    public const float Damage = 10f;
    public const float FireRate = 4f;
    private float _shotTimer;
    public Vector2 Position { get; } = position;
    public float Life { get; private set; } = Lifetime;
    public bool Alive => Life > 0f;
    public float LifeRatio => Math.Clamp(Life / Lifetime, 0f, 1f);

    public void Update(float dt)
    {
        Life -= dt;
        _shotTimer -= dt;
    }

    public bool ReadyToShoot => _shotTimer <= 0f;
    public void MarkShot() => _shotTimer = 1f / FireRate;
}
