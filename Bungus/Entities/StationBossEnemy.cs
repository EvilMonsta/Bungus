using System.Numerics;
using Raylib_cs;

namespace Bungus.Game;

public sealed class StationBossEnemy
{
    public Vector2 Position;
    public float MaxHealth = 4000f;
    public float Health = 4000f;
    public bool Alive => Health > 0f;
    public bool KillAwarded;
    public bool Active { get; private set; }
    public bool PhaseTwo { get; private set; }
    public float ChallengeSpeedMultiplier { get; private set; } = 1f;
    public float ChallengeDamageMultiplier { get; private set; } = 1f;

    private readonly Rectangle _arena;
    private Vector2 _dashDir;
    private float _dashWindup;
    private bool _dashing;
    private bool _dashHitPlayer;
    private float _stunTimer;
    private float _fireCd;
    private int _burstShotsLeft;
    private float _burstShotCd;
    private float _grenadeCd = 3f;
    private int _grenadesLeft;
    private float _grenadeShotCd;
    private bool _grayHealUsed;
    private float _grayHealTimer;
    private int _queuedRadialBursts;
    private float _queuedRadialBurstTimer;
    private float _queuedRadialBurstAngleOffset;
    private readonly List<DashTrailPoint> _dashTrail = [];
    private float _dashTrailSpawnTimer;
    private float _slowTimer;
    private float _chillTimer;
    private float _slowSpeedMultiplier = 0.95f;
    private float _chillSpeedMultiplier = 0.75f;
    private float _poisonTimer;
    private float _poisonDamagePerSecond;

    public StationBossEnemy(Vector2 position, Rectangle arena)
    {
        Position = position;
        _arena = arena;
    }

    public void Activate() => Active = true;

    public void ApplyChallengeModifiers(float healthMultiplier, float speedMultiplier, float damageMultiplier)
    {
        var healthRatio = MaxHealth <= 0f ? 1f : Health / MaxHealth;
        MaxHealth *= healthMultiplier;
        Health = MaxHealth * healthRatio;
        ChallengeSpeedMultiplier = speedMultiplier;
        ChallengeDamageMultiplier = damageMultiplier;
    }

    public void Update(float dt, Vector2 playerPos, List<Projectile> projectiles, Player player, List<Obstacle> obstacles, int worldSize)
    {
        _slowTimer = MathF.Max(0f, _slowTimer - dt);
        _chillTimer = MathF.Max(0f, _chillTimer - dt);
        TickPoison(dt);
        if (!Alive || !Active) return;
        UpdateDashTrail(dt);

        var toPlayer = playerPos - Position;
        var dir = toPlayer == Vector2.Zero ? new Vector2(1f, 0f) : Vector2.Normalize(toPlayer);

        if (_grayHealTimer > 0f)
        {
            _grayHealTimer -= dt;
            Health = MathF.Min(MaxHealth, Health + MaxHealth * 0.03f * dt);
            if (_grayHealTimer <= 0f && !PhaseTwo) EnterPhaseTwo(dir);
            return;
        }

        if (!_grayHealUsed && Health <= MaxHealth * 0.2f)
        {
            _grayHealUsed = true;
            _grayHealTimer = 5f;
            return;
        }

        if (_stunTimer > 0f)
        {
            UpdateQueuedRadialBursts(dt, projectiles);
            _stunTimer -= dt;
            if (PhaseTwo && _stunTimer <= 0f) StartDash(dir, 0.15f);
            return;
        }

        if (_dashWindup > 0f)
        {
            _dashWindup -= dt;
            if (_dashWindup <= 0f)
            {
                _dashing = true;
            }
            return;
        }

        if (_dashing)
        {
            var dashSpeed = (PhaseTwo ? 1350f : 1100f) * ChallengeSpeedMultiplier;
            var (next, hitWall) = MoveDashUntilCollision(Position, _dashDir * dashSpeed * dt, 32f, obstacles, worldSize);
            Position = next;
            SpawnDashTrailPoint(dt);
            if (!_dashHitPlayer && Vector2.Distance(Position, player.Position) <= 96f)
            {
                player.TakeDamage(100f * ChallengeDamageMultiplier);
                _dashHitPlayer = true;
            }
            if (hitWall)
            {
                _dashing = false;
                if (PhaseTwo) QueuePhaseTwoRadialBursts();
                else FireRadialBurst(projectiles);
                _stunTimer = PhaseTwo ? 0.75f : 4f;
            }
            return;
        }

        Position = MovementUtils.MoveWithCollisions(Position, dir * 300f * GetMovementSpeedMultiplier() * ChallengeSpeedMultiplier * dt, 32f, obstacles, worldSize);
        Position = Vector2.Clamp(Position, new Vector2(_arena.X + 36f, _arena.Y + 36f), new Vector2(_arena.X + _arena.Width - 36f, _arena.Y + _arena.Height - 36f));

        if (PhaseTwo)
        {
            if (_dashWindup <= 0f && !_dashing) StartDash(dir, 0.15f);
            return;
        }

        _fireCd -= dt;
        if (_fireCd <= 0f && _burstShotsLeft <= 0)
        {
            _burstShotsLeft = 4;
            _burstShotCd = 0f;
            _fireCd = 0.95f;
        }

        _burstShotCd -= dt;
        while (_burstShotsLeft > 0 && _burstShotCd <= 0f)
        {
            projectiles.Add(new Projectile(Position + dir * 28f, dir, 620f, 2.25f, Palette.C(255, 120, 120), true, 20f * ChallengeDamageMultiplier));
            _burstShotsLeft--;
            _burstShotCd += _burstShotsLeft == 2 ? 0.18f : 0.06f;
        }

        _grenadeCd -= dt;
        if (_grenadeCd <= 0f && _grenadesLeft <= 0)
        {
            _grenadesLeft = 3;
            _grenadeShotCd = 0f;
            _grenadeCd = 2.67f;
        }

        _grenadeShotCd -= dt;
        while (_grenadesLeft > 0 && _grenadeShotCd <= 0f)
        {
            var spread = ((Random.Shared.NextSingle() * 60f) - 30f) * MathF.PI / 180f;
            var grenadeDir = VisibilityUtils.Rotate(dir, spread);
            projectiles.Add(new Projectile(Position + grenadeDir * 32f, grenadeDir, 300f, 1.6875f, Palette.C(255, 155, 90), true, 0f, ProjectileKind.Grenade, 40f * ChallengeDamageMultiplier, 25f * ChallengeDamageMultiplier, 5f));
            _grenadesLeft--;
            _grenadeShotCd += 0.107f;
        }

        if (Random.Shared.NextSingle() < dt * 0.22f)
        {
            StartDash(dir, 1f);
        }
    }

    private void EnterPhaseTwo(Vector2 dir)
    {
        PhaseTwo = true;
        _burstShotsLeft = 0;
        _grenadesLeft = 0;
        _fireCd = 999f;
        _grenadeCd = 999f;
        _stunTimer = 0.75f;
        _dashDir = dir;
    }

    private void StartDash(Vector2 dir, float windup)
    {
        _dashDir = dir == Vector2.Zero ? new Vector2(1f, 0f) : Vector2.Normalize(dir);
        _dashWindup = windup;
        _dashHitPlayer = false;
        _dashTrailSpawnTimer = 0f;
    }

    private void UpdateDashTrail(float dt)
    {
        for (var i = _dashTrail.Count - 1; i >= 0; i--)
        {
            var point = _dashTrail[i];
            point.TimeLeft -= dt;
            if (point.TimeLeft <= 0f) _dashTrail.RemoveAt(i);
            else _dashTrail[i] = point;
        }
    }

    private void SpawnDashTrailPoint(float dt)
    {
        _dashTrailSpawnTimer -= dt;
        if (_dashTrailSpawnTimer > 0f) return;

        _dashTrail.Add(new DashTrailPoint(Position, 0.24f));
        if (_dashTrail.Count > 10) _dashTrail.RemoveAt(0);
        _dashTrailSpawnTimer = 0.035f;
    }

    private (Vector2 Position, bool HitWall) MoveDashUntilCollision(Vector2 position, Vector2 delta, float radius, List<Obstacle> obstacles, int worldSize)
    {
        var steps = Math.Max(1, (int)MathF.Ceiling(delta.Length() / MathF.Max(4f, radius * 0.5f)));
        var step = delta / steps;
        var next = position;
        var min = new Vector2(_arena.X + radius, _arena.Y + radius);
        var max = new Vector2(_arena.X + _arena.Width - radius, _arena.Y + _arena.Height - radius);

        for (var i = 0; i < steps; i++)
        {
            var candidate = next + step;
            var clamped = Vector2.Clamp(candidate, min, max);
            if (clamped != candidate) return (next, true);
            if (MovementUtils.CircleHitsObstacle(candidate, radius, obstacles)) return (next, true);
            if (candidate.X < radius || candidate.Y < radius || candidate.X > worldSize - radius || candidate.Y > worldSize - radius) return (next, true);
            next = candidate;
        }

        return (next, false);
    }

    public void Damage(float amount)
    {
        if (!Alive) return;
        if (_grayHealTimer > 0f)
        {
            Health = MathF.Min(MaxHealth, Health + amount);
            return;
        }

        Health = MathF.Max(0f, Health - amount);
    }

    public bool TryApplySegmentDamage(Vector2 from, Vector2 to, float radius, float damage)
    {
        if (!Alive || !Active) return false;
        if (DistanceToSegment(Position, from, to) > radius + 34f) return false;
        Damage(damage);
        return true;
    }

    public bool IntersectsAnyHitZone(Vector2 position, float radius)
        => Alive && Active && Vector2.Distance(Position, position) <= radius + 34f;

    public void ApplyExplosionDamage(Vector2 position, float radius, float damage)
    {
        if (IntersectsAnyHitZone(position, radius)) Damage(damage);
    }

    public void ApplyStickySlow(float duration = 1f, float strengthMultiplier = 1f)
    {
        var multiplier = MathF.Max(0f, 1f - 0.05f * strengthMultiplier);
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
        Damage(_poisonDamagePerSecond * dt);
    }

    private float GetMovementSpeedMultiplier() => _slowTimer > 0f ? _slowSpeedMultiplier : _chillTimer > 0f ? _chillSpeedMultiplier : 1f;

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var denom = ab.LengthSquared();
        if (denom <= 0.0001f) return Vector2.Distance(p, a);
        var t = Math.Clamp(Vector2.Dot(p - a, ab) / denom, 0f, 1f);
        return Vector2.Distance(p, a + ab * t);
    }

    private void QueuePhaseTwoRadialBursts()
    {
        _queuedRadialBursts = 4;
        _queuedRadialBurstTimer = 0f;
        _queuedRadialBurstAngleOffset = Random.Shared.NextSingle() * MathF.Tau;
    }

    private void UpdateQueuedRadialBursts(float dt, List<Projectile> projectiles)
    {
        if (_queuedRadialBursts <= 0) return;

        _queuedRadialBurstTimer -= dt;
        while (_queuedRadialBursts > 0 && _queuedRadialBurstTimer <= 0f)
        {
            FireRadialBurst(projectiles, _queuedRadialBurstAngleOffset);
            _queuedRadialBursts--;
            _queuedRadialBurstAngleOffset += MathF.Tau / 36f;
            _queuedRadialBurstTimer += 0.16f;
        }
    }

    private void FireRadialBurst(List<Projectile> projectiles, float angleOffset = 0f)
    {
        const int count = 18;
        for (var i = 0; i < count; i++)
        {
            var angle = angleOffset + i / (float)count * MathF.Tau;
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            projectiles.Add(new Projectile(Position + dir * 34f, dir, 620f, 2.25f, Palette.C(255, 120, 120), true, 16f * ChallengeDamageMultiplier));
        }
    }

    public void DrawSight() { }

    public void Draw()
    {
        if (!Alive) return;
        foreach (var point in _dashTrail)
        {
            var alpha = (byte)Math.Clamp(point.TimeLeft / 0.24f * 118f, 0f, 118f);
            var radius = 28f + point.TimeLeft * 14f;
            Raylib.DrawCircleV(point.Position, radius, Palette.C(150, 205, 255, alpha));
        }

        var time = (float)Raylib.GetTime();
        var pulse = 0.5f + 0.5f * MathF.Sin(time * (PhaseTwo ? 7f : 4f));
        Raylib.BeginBlendMode(BlendMode.Additive);
        Raylib.DrawCircleGradient((int)Position.X, (int)Position.Y, PhaseTwo ? 74f + pulse * 8f : 58f + pulse * 5f, PhaseTwo ? Palette.C(255, 82, 72, 32) : Palette.C(255, 92, 96, 22), Palette.C(255, 82, 72, 0));
        Raylib.EndBlendMode();

        var fill = _grayHealTimer > 0f ? Palette.C(112, 118, 128) : PhaseTwo ? Palette.C(126, 30, 42) : Palette.C(186, 42, 52);
        Raylib.DrawCircleV(Position, 34f, fill);
        if (_dashing) Raylib.DrawCircleV(Position, 62f, Palette.C(255, 126, 70, 48));
        if (_grayHealTimer > 0f) Raylib.DrawCircleLinesV(Position, 48f + pulse * 4f, Palette.C(195, 210, 222, 180));
        var tri = _dashWindup > 0f ? Palette.C(255, 218, 96) : Palette.C(235, 244, 255);
        Raylib.DrawPoly(Position + new Vector2(-8f, 0f), 3, 13f, 90f, tri);
        Raylib.DrawPoly(Position + new Vector2(10f, 0f), 3, 13f, -90f, Color.Black);
        if (PhaseTwo)
        {
            Raylib.DrawPoly(Position + new Vector2(0f, -13f), 3, 11f, 180f, Color.Black);
            Raylib.DrawPoly(Position + new Vector2(0f, 13f), 3, 11f, 0f, Color.Black);
            Raylib.DrawCircleLinesV(Position, 46f + pulse * 3f, Palette.C(255, 156, 88, 190));
        }
        Raylib.DrawCircleLines((int)Position.X, (int)Position.Y, 36f, Palette.C(235, 244, 255));
        var hp = Health / MaxHealth;
        Raylib.DrawRectangle((int)Position.X - 58, (int)Position.Y - 54, 116, 7, Palette.C(20, 20, 20, 230));
        Raylib.DrawRectangle((int)Position.X - 58, (int)Position.Y - 54, (int)(116 * hp), 7, Palette.C(96, 224, 122));
    }

    private struct DashTrailPoint(Vector2 position, float timeLeft)
    {
        public Vector2 Position = position;
        public float TimeLeft = timeLeft;
    }
}
