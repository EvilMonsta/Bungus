using System.Numerics;
using Raylib_cs;

namespace Bungus.Game;

public sealed class HexEnemy
{
    public Vector2 Position;
    public float MaxHealth = 200f;
    public float Health = 200f;
    public bool Alive => Health > 0f;
    public bool KillAwarded;
    public float ChallengeSpeedMultiplier { get; private set; } = 1f;
    public float ChallengeDamageMultiplier { get; private set; } = 1f;

    private Vector2 _facing = new(1f, 0f);
    private float _strafeSwitch;
    private float _fireCd;
    private float _burstCd;
    private int _burstLeft;
    private float _burstShotCd;
    private readonly bool _burstMode;
    private float _slowTimer;
    private float _chillTimer;
    private float _slowSpeedMultiplier = 0.7f;
    private float _chillSpeedMultiplier = 0.75f;
    private float _poisonTimer;
    private float _poisonDamagePerSecond;

    private const float DesiredDistance = 290f;

    private HexEnemy(Vector2 pos, bool burstMode)
    {
        Position = pos;
        _burstMode = burstMode;
    }

    public static HexEnemy Create(Vector2 pos, Random rng) => new(pos, rng.NextSingle() < 0.5f);

    public void Update(float dt, Vector2 playerPos, List<Projectile> projectiles, List<Obstacle> obstacles, int worldSize, bool infiniteAggro = false)
    {
        if (!Alive) return;
        _slowTimer = MathF.Max(0f, _slowTimer - dt);
        _chillTimer = MathF.Max(0f, _chillTimer - dt);
        TickPoison(dt);
        if (!Alive) return;

        var toPlayer = playerPos - Position;
        if (toPlayer == Vector2.Zero) toPlayer = new Vector2(1f, 0f);
        var dist = toPlayer.Length();
        var dir = Vector2.Normalize(toPlayer);
        _facing = dir;

        var radial = 0f;
        if (dist > DesiredDistance + 20f) radial = 175f;
        else if (dist < DesiredDistance - 20f) radial = -137.5f;

        _strafeSwitch -= dt;
        if (_strafeSwitch <= 0f) _strafeSwitch = 0.25f + Random.Shared.NextSingle() * 0.65f;
        var strafeSign = MathF.Sin(_strafeSwitch * 8f + Position.X * 0.01f) > 0f ? 1f : -1f;
        var strafeDir = new Vector2(-dir.Y, dir.X) * strafeSign;
        var move = dir * radial + strafeDir * 100f;
        Position = MovementUtils.MoveWithCollisions(Position, move * GetMovementSpeedMultiplier() * ChallengeSpeedMultiplier * dt, 16f, obstacles, worldSize);

        if (_burstMode)
        {
            _burstCd -= dt;
            if (_burstCd <= 0f && _burstLeft <= 0)
            {
                _burstLeft = 5;
                _burstShotCd = 0f;
                _burstCd = 1f;
            }

            _burstShotCd -= dt;
            while (_burstLeft > 0 && _burstShotCd <= 0f)
            {
                projectiles.Add(new Projectile(Position + dir * 18f, dir, 560f, 1.44f, Palette.C(255, 110, 180), true, 4f * ChallengeDamageMultiplier));
                _burstLeft--;
                _burstShotCd += 0.06f;
            }
        }
        else
        {
            _fireCd -= dt;
            if (_fireCd <= 0f)
            {
                projectiles.Add(new Projectile(Position + dir * 18f, dir, 560f, 1.44f, Palette.C(255, 110, 180), true, 10f * ChallengeDamageMultiplier));
                _fireCd = 0.5f;
            }
        }
    }

    public void ApplyChallengeModifiers(float healthMultiplier, float speedMultiplier, float damageMultiplier)
    {
        var healthRatio = MaxHealth <= 0f ? 1f : Health / MaxHealth;
        MaxHealth *= healthMultiplier;
        Health = MaxHealth * healthRatio;
        ChallengeSpeedMultiplier = speedMultiplier;
        ChallengeDamageMultiplier = damageMultiplier;
    }

    public void Damage(float amount)
    {
        if (!Alive) return;
        Health = MathF.Max(0f, Health - amount);
    }

    public void ApplyPoison(float damagePerSecond, float duration)
    {
        if (!Alive || damagePerSecond <= 0f || duration <= 0f) return;
        _poisonDamagePerSecond = MathF.Max(_poisonDamagePerSecond, damagePerSecond);
        _poisonTimer = MathF.Max(_poisonTimer, duration);
    }

    private void TickPoison(float dt)
    {
        if (_poisonTimer <= 0f || !Alive) return;
        _poisonTimer = MathF.Max(0f, _poisonTimer - dt);
        Health = MathF.Max(0f, Health - _poisonDamagePerSecond * dt);
    }

    public void ApplyStickySlow(float duration = 1f, float strengthMultiplier = 1f)
    {
        var multiplier = MathF.Max(0f, 1f - 0.3f * strengthMultiplier);
        _slowSpeedMultiplier = _slowTimer > 0f ? MathF.Min(_slowSpeedMultiplier, multiplier) : multiplier;
        _slowTimer = MathF.Max(_slowTimer, duration);
    }

    public void ApplyFreezeChill(float duration = 10f, float strengthMultiplier = 1f)
    {
        var multiplier = MathF.Max(0f, 1f - 0.25f * strengthMultiplier);
        _chillSpeedMultiplier = _chillTimer > 0f ? MathF.Min(_chillSpeedMultiplier, multiplier) : multiplier;
        _chillTimer = MathF.Max(_chillTimer, duration);
    }

    private float GetMovementSpeedMultiplier() => _slowTimer > 0f ? _slowSpeedMultiplier : _chillTimer > 0f ? _chillSpeedMultiplier : 1f;

    public void DrawSight()
    {
    }

    public void Draw()
    {
        if (!Alive) return;

        Span<Vector2> points = stackalloc Vector2[6];
        for (var i = 0; i < 6; i++)
        {
            var a = i / 6f * MathF.Tau;
            points[i] = Position + new Vector2(MathF.Cos(a), MathF.Sin(a)) * 15f;
        }

        for (var i = 1; i < 5; i++) Raylib.DrawTriangle(points[0], points[i], points[i + 1], Palette.C(0, 0, 0, 128));
        for (var i = 0; i < 6; i++) Raylib.DrawLineV(points[i], points[(i + 1) % 6], Color.Maroon);

        var hp = Health / MaxHealth;
        Raylib.DrawRectangle((int)Position.X - 22, (int)Position.Y - 28, 44, 5, Palette.C(20, 20, 20, 220));
        Raylib.DrawRectangle((int)Position.X - 22, (int)Position.Y - 28, (int)(44 * hp), 5, Color.Green);
    }
}
