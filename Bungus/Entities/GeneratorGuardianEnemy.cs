using System.Numerics;
using Raylib_cs;

namespace Bungus.Game;

public sealed class GeneratorGuardianEnemy
{
    public Vector2 Position;
    public float MaxHealth = 1000f;
    public float Health = 1000f;
    public int ZoneId = -1;
    public bool Alive => Health > 0f;
    public bool KillAwarded;
    public float ChallengeSpeedMultiplier { get; private set; } = 1f;
    public float ChallengeDamageMultiplier { get; private set; } = 1f;

    private readonly Vector2 _spawn;
    private bool _alert;
    private float _attackCd;
    private float _sideDashCd;
    private float _playerDashCd;
    private float _slowTimer;
    private float _chillTimer;
    private float _slowSpeedMultiplier = 0.7f;
    private float _chillSpeedMultiplier = 0.75f;
    private float _poisonTimer;
    private float _poisonDamagePerSecond;
    private float _spearVisualTimer;
    private Vector2 _spearStart;
    private Vector2 _spearEnd;
    private Vector2 _facing = new(1f, 0f);

    private const float SpearStartDistance = 24f;
    private const float SpearEndDistance = 140f;
    private const float SpearHitRadius = 15f;
    private const float SpearVisualDuration = 0.18f;
    private const float AggroRange = 980f;

    public GeneratorGuardianEnemy(Vector2 position, int zoneId)
    {
        Position = position;
        _spawn = position;
        ZoneId = zoneId;
        _sideDashCd = NextSideDashCooldown();
        _playerDashCd = NextPlayerDashCooldown();
    }

    public void Update(float dt, Vector2 playerPos, Player player, List<Obstacle> obstacles, int worldSize, List<DashAfterImage> afterImages, bool infiniteAggro = false)
    {
        _slowTimer = MathF.Max(0f, _slowTimer - dt);
        _chillTimer = MathF.Max(0f, _chillTimer - dt);
        _spearVisualTimer = MathF.Max(0f, _spearVisualTimer - dt);
        TickPoison(dt);
        if (!Alive) return;

        var toPlayer = playerPos - Position;
        var playerDistance = toPlayer.Length();
        if (infiniteAggro)
        {
            ForceAggro(playerPos);
        }
        else if (_alert && playerDistance > AggroRange)
        {
            ReturnToSpawn(dt, obstacles, worldSize);
            return;
        }

        if (!_alert)
        {
            _facing = Vector2.Normalize(_spawn - Position == Vector2.Zero ? _facing : _spawn - Position);
            return;
        }

        var dir = playerDistance <= 0.001f ? _facing : Vector2.Normalize(toPlayer);
        _facing = dir;
        Position = MovementUtils.MoveWithCollisions(Position, dir * 150f * GetMovementSpeedMultiplier() * ChallengeSpeedMultiplier * dt, 16f, obstacles, worldSize);

        _sideDashCd -= dt;
        _playerDashCd -= dt;
        if (_sideDashCd <= 0f)
        {
            var side = VisibilityUtils.Rotate(dir, Random.Shared.NextSingle() < 0.5f ? MathF.PI * 0.5f : -MathF.PI * 0.5f);
            Position = MovementUtils.MoveWithCollisions(Position, side * 110f * GetMovementSpeedMultiplier() * ChallengeSpeedMultiplier, 16f, obstacles, worldSize);
            DashAfterImage.Spawn(afterImages, Position, side, 110f, Palette.C(80, 220, 255), false);
            _sideDashCd = NextSideDashCooldown();
        }

        if (_playerDashCd <= 0f)
        {
            Position = MovementUtils.MoveWithCollisions(Position, dir * 170f * GetMovementSpeedMultiplier() * ChallengeSpeedMultiplier, 16f, obstacles, worldSize);
            DashAfterImage.Spawn(afterImages, Position, dir, 170f, Palette.C(120, 230, 255), false);
            _playerDashCd = NextPlayerDashCooldown();
        }

        _attackCd -= dt;
        if (_attackCd <= 0f && playerDistance <= SpearEndDistance + 16f)
        {
            _spearStart = Position + dir * SpearStartDistance;
            _spearEnd = Position + dir * SpearEndDistance;
            _spearVisualTimer = SpearVisualDuration;
            if (DistanceToSegment(player.Position, _spearStart, _spearEnd) <= SpearHitRadius)
            {
                player.TakeDamage(30f * ChallengeDamageMultiplier);
            }
            _attackCd = 0.8f;
        }
    }

    private void ReturnToSpawn(float dt, List<Obstacle> obstacles, int worldSize)
    {
        var toSpawn = _spawn - Position;
        if (toSpawn.LengthSquared() <= 36f)
        {
            _alert = false;
            Health = MathF.Min(MaxHealth, Health + MaxHealth * 0.30f * dt);
            return;
        }

        var dir = Vector2.Normalize(toSpawn);
        _facing = dir;
        Position = MovementUtils.MoveWithCollisions(Position, dir * 150f * ChallengeSpeedMultiplier * dt, 16f, obstacles, worldSize);
        Health = MathF.Min(MaxHealth, Health + MaxHealth * 0.30f * dt);
    }

    public void ApplyChallengeModifiers(float healthMultiplier, float speedMultiplier, float damageMultiplier)
    {
        var healthRatio = MaxHealth <= 0f ? 1f : Health / MaxHealth;
        MaxHealth *= healthMultiplier;
        Health = MaxHealth * healthRatio;
        ChallengeSpeedMultiplier = speedMultiplier;
        ChallengeDamageMultiplier = damageMultiplier;
    }

    public bool CanSeePoint(Vector2 point, List<Obstacle> obstacles)
    {
        var to = point - Position;
        if (to.Length() > 702f || to.LengthSquared() < 0.01f) return false;
        var dir = Vector2.Normalize(to);
        var angle = MathF.Acos(Math.Clamp(Vector2.Dot(_facing, dir), -1f, 1f));
        return angle <= MathF.PI / 3f && VisibilityUtils.HasLineOfSight(Position, point, obstacles);
    }

    public void ForceAggro(Vector2 target)
    {
        _alert = true;
        if (target != Position) _facing = Vector2.Normalize(target - Position);
    }

    public bool TryAggroFromPlayerHit(Vector2 playerPosition)
    {
        if (Vector2.Distance(Position, playerPosition) > AggroRange)
        {
            _alert = false;
            return false;
        }

        ForceAggro(playerPosition);
        return true;
    }

    public void Damage(float amount)
    {
        if (!Alive) return;
        Health = MathF.Max(0f, Health - amount);
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

    private float GetMovementSpeedMultiplier() => _slowTimer > 0f ? _slowSpeedMultiplier : _chillTimer > 0f ? _chillSpeedMultiplier : 1f;
    private static float NextSideDashCooldown() => 2f + Random.Shared.NextSingle() * 2f;
    private static float NextPlayerDashCooldown() => 1f + Random.Shared.NextSingle() * 2f;

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var denom = ab.LengthSquared();
        if (denom <= 0.0001f) return Vector2.Distance(p, a);
        var t = Math.Clamp(Vector2.Dot(p - a, ab) / denom, 0f, 1f);
        return Vector2.Distance(p, a + ab * t);
    }

    public void DrawSight()
    {
        if (!Alive) return;
        var left = VisibilityUtils.Rotate(_facing, -MathF.PI / 3f);
        var right = VisibilityUtils.Rotate(_facing, MathF.PI / 3f);
        var c = Palette.C(130, 230, 255, 90);
        VisibilityUtils.DrawDashedLine(Position, Position + left * 526f, 22, c);
        VisibilityUtils.DrawDashedLine(Position, Position + right * 526f, 22, c);
    }

    public void Draw()
    {
        if (!Alive) return;
        Raylib.DrawPoly(Position, 3, 20f, 30f, Palette.C(120, 225, 255));
        Raylib.DrawPoly(Position, 3, 20f, 210f, Palette.C(120, 225, 255));
        Raylib.DrawCircleLines((int)Position.X, (int)Position.Y, 22f, Color.White);
        DrawSpearVisual();
        DrawHealthBar(Position, Health, MaxHealth, 52);
    }

    private void DrawSpearVisual()
    {
        if (_spearVisualTimer <= 0f) return;

        var alpha = Math.Clamp(_spearVisualTimer / SpearVisualDuration, 0f, 1f);
        var color = new Color((byte)120, (byte)225, (byte)255, (byte)(255 * alpha));
        var dir = _spearEnd - _spearStart;
        if (dir.LengthSquared() <= 0.001f) return;
        dir = Vector2.Normalize(dir);
        var angle = MathF.Atan2(dir.Y, dir.X) * 180f / MathF.PI;
        var length = Vector2.Distance(_spearStart, _spearEnd);
        var center = (_spearStart + _spearEnd) * 0.5f;

        Raylib.DrawLineEx(_spearStart, _spearEnd, 5f, color);
        Raylib.DrawRectanglePro(
            new Rectangle(center.X, center.Y, length, 8f),
            new Vector2(length * 0.5f, 4f),
            angle,
            color);
        Raylib.DrawCircleV(_spearEnd, 7f, Color.White);
    }

    private static void DrawHealthBar(Vector2 position, float health, float maxHealth, int width)
    {
        var ratio = Math.Clamp(health / MathF.Max(maxHealth, 0.001f), 0f, 1f);
        Raylib.DrawRectangle((int)position.X - width / 2, (int)position.Y - 34, width, 5, Palette.C(20, 20, 20, 220));
        Raylib.DrawRectangle((int)position.X - width / 2, (int)position.Y - 34, (int)(width * ratio), 5, Color.Green);
    }
}
