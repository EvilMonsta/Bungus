using System.Numerics;
using Raylib_cs;

namespace Bungus.Game;

public sealed class ToxicTriangleEnemy
{
    public Vector2 Position;
    public float MaxHealth = 300f;
    public float Health = 300f;
    public int ZoneId = -1;
    public bool Alive => Health > 0f;
    public bool KillAwarded;
    public float ChallengeSpeedMultiplier { get; private set; } = 1f;
    public float ChallengeDamageMultiplier { get; private set; } = 1f;

    private bool _alert;
    private bool _investigating;
    private bool _returningFromInvestigation;
    private Vector2 _investigateTarget;
    private Vector2 _investigateReturnPoint;
    private float _investigateWait;
    private float _investigateReturnStartDistance;
    private float _investigateReturnStartHealth;
    private float _fireCd;
    private int _burstLeft;
    private float _burstShotCd;
    private float _slowTimer;
    private float _chillTimer;
    private float _slowSpeedMultiplier = 0.7f;
    private float _chillSpeedMultiplier = 0.75f;
    private float _poisonTimer;
    private float _poisonDamagePerSecond;
    private Vector2 _facing = new(1f, 0f);

    private const float ViewDistance = 350f;
    private const float AlertDistance = 600f;
    private const float FovHalf = MathF.PI / 3f;

    public ToxicTriangleEnemy(Vector2 position, int zoneId)
    {
        Position = position;
        ZoneId = zoneId;
    }

    public void Update(float dt, Vector2 playerPos, List<Projectile> projectiles, List<Obstacle> obstacles, int worldSize, bool infiniteAggro = false)
    {
        _slowTimer = MathF.Max(0f, _slowTimer - dt);
        _chillTimer = MathF.Max(0f, _chillTimer - dt);
        TickPoison(dt);
        if (!Alive) return;

        var toPlayer = playerPos - Position;
        if (toPlayer == Vector2.Zero) toPlayer = new Vector2(1f, 0f);
        var dist = toPlayer.Length();
        var dir = Vector2.Normalize(toPlayer);

        if (infiniteAggro)
        {
            ForceAggro(playerPos);
        }
        else if (_alert && dist > GetAlertViewDistance())
        {
            _alert = false;
            StartLostAggroReturn();
        }

        if (!_alert)
        {
            if (CanSeePoint(playerPos, obstacles)) ForceAggro(playerPos);
            else if (_investigating) UpdateInvestigation(dt, obstacles, worldSize);
            else return;
        }

        _facing = dir;

        var desiredDistance = 193f;
        var radial = dist > desiredDistance + 16f ? 182f : dist < desiredDistance - 16f ? -143f : 0f;
        var strafeDir = new Vector2(-dir.Y, dir.X) * (MathF.Sin((float)Raylib.GetTime() * 7f + Position.X * 0.01f) > 0f ? 1f : -1f);
        Position = MovementUtils.MoveWithCollisions(Position, (dir * radial + strafeDir * 104f) * GetMovementSpeedMultiplier() * ChallengeSpeedMultiplier * dt, 16f, obstacles, worldSize);

        _fireCd -= dt;
        if (_fireCd <= 0f && _burstLeft <= 0)
        {
            _burstLeft = Random.Shared.Next(2, 10);
            _burstShotCd = 0f;
            _fireCd = 1.15f;
        }

        _burstShotCd -= dt;
        while (_burstLeft > 0 && _burstShotCd <= 0f)
        {
            var spread = ((Random.Shared.NextSingle() * 50f) - 25f) * MathF.PI / 180f;
            var shotDir = VisibilityUtils.Rotate(dir, spread);
            projectiles.Add(new Projectile(Position + shotDir * 18f, shotDir, 560f, 1.44f, Palette.C(80, 210, 70), true, 5f * ChallengeDamageMultiplier, playerPoisonDuration: 2f));
            _burstLeft--;
            _burstShotCd += 0.07f;
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

    public bool CanSeePoint(Vector2 point, List<Obstacle> obstacles)
    {
        var to = point - Position;
        var dist = to.Length();
        if (dist > ViewDistance || dist < 0.01f) return false;

        var dir = Vector2.Normalize(to);
        var angle = MathF.Acos(Math.Clamp(Vector2.Dot(_facing, dir), -1f, 1f));
        return angle <= FovHalf && VisibilityUtils.HasLineOfSight(Position, point, obstacles);
    }

    public bool CanNoticeCombatPoint(Vector2 point, List<Obstacle> obstacles)
    {
        var to = point - Position;
        var dist = to.Length();
        if (dist > ViewDistance || dist < 0.01f) return false;

        var dir = Vector2.Normalize(to);
        var angle = MathF.Acos(Math.Clamp(Vector2.Dot(_facing, dir), -1f, 1f));
        return angle <= FovHalf && VisibilityUtils.HasLineOfSight(Position, point, obstacles);
    }

    public bool ReactToShot(Vector2 shotSource, List<Obstacle> obstacles)
    {
        if (!CanSeePoint(shotSource, obstacles))
        {
            StartInvestigation(shotSource);
            return false;
        }

        ForceAggro(shotSource);
        return true;
    }

    public void ForceAggro(Vector2 target)
    {
        if (!_alert && !_investigating) _investigateReturnPoint = Position;
        _alert = true;
        _investigating = false;
        _returningFromInvestigation = false;
        var dir = target - Position;
        if (dir != Vector2.Zero) _facing = Vector2.Normalize(dir);
    }

    private void StartLostAggroReturn()
    {
        _investigating = true;
        _returningFromInvestigation = false;
        _investigateTarget = Position;
        _investigateWait = 3f;
    }

    private void StartInvestigation(Vector2 target)
    {
        if (_alert) return;

        if (!_investigating) _investigateReturnPoint = Position;
        _investigating = true;
        _returningFromInvestigation = false;
        _investigateTarget = target;
        _investigateWait = 0f;
    }

    private void UpdateInvestigation(float dt, List<Obstacle> obstacles, int worldSize)
    {
        if (!_returningFromInvestigation && _investigateWait > 0f)
        {
            _investigateWait -= dt;
            if (_investigateWait <= 0f) StartInvestigationReturn();
            return;
        }

        var target = _returningFromInvestigation ? _investigateReturnPoint : _investigateTarget;
        var to = target - Position;
        if (to.Length() < 12f)
        {
            if (_returningFromInvestigation)
            {
                Health = MaxHealth;
                _investigating = false;
                return;
            }

            _investigateWait = 1f;
            return;
        }

        var dir = Vector2.Normalize(to);
        _facing = dir;
        Position = MovementUtils.MoveWithCollisions(Position, dir * 120f * GetMovementSpeedMultiplier() * ChallengeSpeedMultiplier * dt, 16f, obstacles, worldSize);
        if (_returningFromInvestigation) HealDuringInvestigationReturn();
    }

    private void StartInvestigationReturn()
    {
        _returningFromInvestigation = true;
        _investigateReturnStartDistance = MathF.Max(1f, Vector2.Distance(Position, _investigateReturnPoint));
        _investigateReturnStartHealth = Health;
    }

    private void HealDuringInvestigationReturn()
    {
        var remaining = Vector2.Distance(Position, _investigateReturnPoint);
        var progress = 1f - Math.Clamp(remaining / _investigateReturnStartDistance, 0f, 1f);
        Health = MathF.Max(Health, _investigateReturnStartHealth + (MaxHealth - _investigateReturnStartHealth) * progress);
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
    private static float GetAlertViewDistance() => AlertDistance;

    public void DrawSight()
    {
        if (!Alive || _alert) return;
        var c = Palette.C(60, 180, 70, 28);
        var left = VisibilityUtils.Rotate(_facing, -FovHalf);
        var right = VisibilityUtils.Rotate(_facing, FovHalf);
        var sightLineLength = 100f;
        VisibilityUtils.DrawDashedLine(Position, Position + left * sightLineLength, 22, c);
        VisibilityUtils.DrawDashedLine(Position, Position + right * sightLineLength, 22, c);
    }

    public void Draw()
    {
        if (!Alive) return;
        Raylib.DrawPoly(Position, 3, 18f, MathF.Atan2(_facing.Y, _facing.X) * 180f / MathF.PI + 90f, Palette.C(220, 110, 100));
        Raylib.DrawCircleLines((int)Position.X, (int)Position.Y, 20f, Palette.C(30, 120, 45));
        var hp = Health / MaxHealth;
        Raylib.DrawRectangle((int)Position.X - 24, (int)Position.Y - 30, 48, 5, Palette.C(20, 20, 20, 220));
        Raylib.DrawRectangle((int)Position.X - 24, (int)Position.Y - 30, (int)(48 * hp), 5, Color.Green);
    }
}
