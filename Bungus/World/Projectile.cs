using System.Numerics;
using System.Runtime.CompilerServices;
using Raylib_cs;

namespace Bungus.Game;

public sealed class Projectile(Vector2 pos, Vector2 dir, float speed, float life, Color color, bool ownerEnemy, float damage, ProjectileKind kind = ProjectileKind.Bullet, float explosionRadius = 0f, float explosionDamage = 0f, float drawRadius = 4f, bool highlighted = false, Vector2? sourcePosition = null, float poisonDamagePerSecond = 0f, float poisonDuration = 0f, float playerPoisonDuration = 0f, int ricochetRemaining = 0, object? ignoreTarget = null, float playerDecompositionDuration = 0f, float playerArmorPenetration = 0f, float enemyDecompositionDuration = 0f)
{
    public Vector2 Position { get; private set; } = pos;
    public Vector2 PreviousPosition { get; private set; } = pos;
    public Vector2 SourcePosition { get; } = sourcePosition ?? pos;
    public Vector2 Direction { get; } = dir;
    public Color Color { get; } = color;
    public bool OwnerEnemy { get; } = ownerEnemy;
    public float Damage { get; } = damage;
    public ProjectileKind Kind { get; } = kind;
    public float ExplosionRadius { get; } = explosionRadius;
    public float ExplosionDamage { get; } = explosionDamage;
    public float DrawRadius { get; } = drawRadius;
    public bool Highlighted { get; } = highlighted;
    public float PoisonDamagePerSecond { get; } = poisonDamagePerSecond;
    public float PoisonDuration { get; } = poisonDuration;
    public float PlayerPoisonDuration { get; } = playerPoisonDuration;
    public float PlayerDecompositionDuration { get; } = playerDecompositionDuration;
    public float PlayerArmorPenetration { get; } = playerArmorPenetration;
    public float EnemyDecompositionDuration { get; } = enemyDecompositionDuration;
    public int RicochetRemaining { get; } = ricochetRemaining;
    public object? IgnoreTarget { get; } = ignoreTarget;
    private float _life = life;
    public bool Alive => _life > 0f;

    public void Update(float dt)
    {
        PreviousPosition = Position;
        Position += Direction * speed * dt;
        _life -= dt;
    }
}
