using System.Numerics;
using Raylib_cs;

namespace Bungus.Game;

public sealed class MiniBossEnemySquare
{
    public Vector2 Position;
    public float MaxHealth = 1750f;
    public float Health = 1750f;
    public int ZoneId = -1;
    public bool Alive => Health > 0;
    public bool KillAwarded;
    public bool IsFast { get; }
    public float ChallengeSpeedMultiplier { get; private set; } = 1f;
    public float ChallengeDamageMultiplier { get; private set; } = 1f;

    private float _ramCd = 4f;
    private float _shootCd = 1.2f;
    private int _burstShotsLeft;
    private float _burstShotCd;
    private float _slamCd = 3.5f;
    private float _slamVisual;
    private bool _alert;
    private bool _investigating;
    private bool _returningFromInvestigation;
    private Vector2 _investigateTarget;
    private Vector2 _investigateReturnPoint;
    private float _investigateWait;
    private float _investigateReturnStartDistance;
    private float _investigateReturnStartHealth;
    private Vector2 _facing = new(1f, 0f);
    private float _slowTimer;
    private float _chillTimer;
    private float _slowSpeedMultiplier = 0.85f;
    private float _chillSpeedMultiplier = 0.75f;
    private float _poisonTimer;
    private float _poisonDamagePerSecond;
    private List<Vector2> _navigationPath = [];
    private int _navigationPathIndex;
    private Vector2 _navigationGoal;
    private float _navigationRefreshTimer;

    private const float ViewDistance = 600f;
    private const float AlertDistance = 850f;
    private const float FovHalf = MathF.PI / 3f;
    private const float FastHealthMultiplier = 0.7f;

    public MiniBossEnemySquare(Vector2 pos, int zoneId = -1, bool isFast = false)
    {
        Position = pos;
        ZoneId = zoneId;
        IsFast = isFast;
        if (!IsFast) return;

        MaxHealth *= FastHealthMultiplier;
        Health = MaxHealth;
        _shootCd *= 1.2f;
        _slamCd *= GetSlamCooldownMultiplier();
    }

    public void Update(float dt, Vector2 playerPos, List<Projectile> projectiles, Player player, List<Obstacle> obstacles, int worldSize, List<DashAfterImage> afterImages, bool infiniteAggro = false)
    {
        if (!Alive) return;

        _slowTimer = MathF.Max(0f, _slowTimer - dt);
        _chillTimer = MathF.Max(0f, _chillTimer - dt);
        TickPoison(dt);
        if (!Alive) return;
        _ramCd -= dt;
        _shootCd -= dt;
        _slamCd -= dt;
        _slamVisual -= dt;

        var toPlayer = playerPos - Position;
        var distanceToPlayer = toPlayer.Length();

        if (infiniteAggro)
        {
            ForceAggro(playerPos);
        }
        else if (_alert)
        {
            if (distanceToPlayer > GetAlertViewDistance())
            {
                _alert = false;
                StartLostAggroReturn();
            }
        }
        else if (CanSeePoint(playerPos, obstacles))
        {
            ForceAggro(playerPos);
        }

        if (!_alert)
        {
            if (_investigating) UpdateInvestigation(dt, obstacles, worldSize);
            return;
        }

        if (toPlayer == Vector2.Zero) return;

        var dir = Vector2.Normalize(toPlayer);
        MoveTowardNavigated(playerPos, 52.5f * GetFastSpeedMultiplier() * GetMovementSpeedMultiplier() * ChallengeSpeedMultiplier, dt, obstacles, worldSize);

        if (_ramCd <= 0f)
        {
            Position = MovementUtils.MoveWithCollisions(Position, dir * 150f * GetMovementSpeedMultiplier() * ChallengeSpeedMultiplier, 28f, obstacles, worldSize);
            DashAfterImage.Spawn(afterImages, Position, dir, 150f, Palette.C(230, 100, 100), true);
            _ramCd = 4f;
            if (Vector2.Distance(Position, playerPos) < 56f) player.TakeDamage(24f * ChallengeDamageMultiplier);
        }

        if (_shootCd <= 0f && _burstShotsLeft <= 0)
        {
            _burstShotsLeft = 6;
            _burstShotCd = 0f;
            _shootCd = 1.9f * GetShootCooldownMultiplier();
        }

        if (_burstShotsLeft > 0)
        {
            _burstShotCd -= dt;
            while (_burstShotsLeft > 0 && _burstShotCd <= 0f)
            {
                var spread = ((Random.Shared.NextSingle() * 4f) - 2f) * (MathF.PI / 180f);
                var shotDir = VisibilityUtils.Rotate(dir, spread);
                projectiles.Add(new Projectile(Position + shotDir * 28f, shotDir, 560f, 1.62f, Palette.C(255, 150, 120), true, 13f * ChallengeDamageMultiplier));
                _burstShotsLeft--;
                _burstShotCd += 0.08f;
            }
        }

        if (_slamCd <= 0f)
        {
            _slamVisual = 0.7f;
            _slamCd = 3.6f * GetSlamCooldownMultiplier();
            if (Vector2.Distance(Position, playerPos) < GetSlamRadius()) player.TakeDamage(20f * ChallengeDamageMultiplier);
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
        if (to.LengthSquared() < 0.01f) return false;

        var dir = Vector2.Normalize(to);
        var angle = MathF.Acos(Math.Clamp(Vector2.Dot(_facing, dir), -1f, 1f));
        return angle <= FovHalf && VisibilityUtils.HasLineOfSight(Position, point, obstacles);
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

    public bool ReactToShot(Vector2 shotSource, List<Obstacle> obstacles)
    {
        if (CanSeePoint(shotSource, obstacles))
        {
            ForceAggro(shotSource);
            return true;
        }

        StartInvestigation(shotSource);
        return false;
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
        if (to.Length() < 14f)
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

        MoveTowardNavigated(target, 47.5f * GetFastSpeedMultiplier() * GetMovementSpeedMultiplier() * ChallengeSpeedMultiplier, dt, obstacles, worldSize);
        if (_returningFromInvestigation) HealDuringInvestigationReturn();
    }

    private void MoveTowardNavigated(Vector2 desiredTarget, float speed, float dt, List<Obstacle> obstacles, int worldSize)
    {
        const float radius = 28f;
        const float goalRefreshDistance = 120f;

        _navigationRefreshTimer -= dt;
        var waypoint = desiredTarget;
        var goalChanged = Vector2.DistanceSquared(_navigationGoal, desiredTarget) > goalRefreshDistance * goalRefreshDistance;
        var shouldEvaluateNavigation = _navigationPath.Count > 0
            || _navigationRefreshTimer <= 0f
            || goalChanged;

        if (!shouldEvaluateNavigation)
        {
            waypoint = desiredTarget;
        }
        else if (PathfindingUtils.HasClearPath(Position, desiredTarget, radius, obstacles, worldSize))
        {
            _navigationPath.Clear();
            _navigationPathIndex = 0;
            _navigationGoal = desiredTarget;
            _navigationRefreshTimer = 0.4f;
        }
        else
        {
            var shouldRefresh = _navigationPath.Count == 0
                || _navigationPathIndex >= _navigationPath.Count
                || _navigationRefreshTimer <= 0f
                || goalChanged;

            if (shouldRefresh)
            {
                if (PathfindingUtils.TryFindPath(Position, desiredTarget, radius, obstacles, worldSize, out var path))
                {
                    _navigationPath = path;
                    _navigationPathIndex = 0;
                    _navigationGoal = desiredTarget;
                    _navigationRefreshTimer = 0.5f;
                }
                else
                {
                    _navigationPath.Clear();
                    _navigationPathIndex = 0;
                    _navigationRefreshTimer = 0.25f;
                }
            }

            if (_navigationPathIndex < _navigationPath.Count)
            {
                while (_navigationPathIndex < _navigationPath.Count - 1
                       && Vector2.DistanceSquared(Position, _navigationPath[_navigationPathIndex]) < 26f * 26f)
                {
                    _navigationPathIndex++;
                }

                waypoint = _navigationPath[_navigationPathIndex];
            }
        }

        var to = waypoint - Position;
        if (to.LengthSquared() <= 0.01f) return;

        var dir = Vector2.Normalize(to);
        _facing = dir;

        var previous = Position;
        Position = MovementUtils.MoveWithCollisions(Position, dir * speed * dt, radius, obstacles, worldSize);
        if (Vector2.DistanceSquared(previous, Position) < 0.0025f)
        {
            _navigationRefreshTimer = 0f;
        }
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
        var multiplier = MathF.Max(0f, 1f - 0.15f * strengthMultiplier);
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
    private float GetFastSpeedMultiplier() => IsFast ? 2.175f : 1f;
    private float GetShootCooldownMultiplier() => IsFast ? 1.2f : 1f;
    private float GetSlamCooldownMultiplier() => IsFast ? 0.7f : 1f;
    private float GetSlamRadius() => IsFast ? 144f : 120f;

    private static float GetAlertViewDistance() => AlertDistance;

    public void DrawSight()
    {
        if (!Alive) return;

        var c = Palette.C(255, 130, 110, 24);
        var sightLineLength = 100f;
        VisibilityUtils.DrawDashedLine(Position, Position + VisibilityUtils.Rotate(_facing, -FovHalf) * sightLineLength, 24, c);
        VisibilityUtils.DrawDashedLine(Position, Position + VisibilityUtils.Rotate(_facing, FovHalf) * sightLineLength, 24, c);
    }

    public void Draw(VisualTheme theme)
    {
        if (!Alive) return;

        var size = 42;
        if (_alert)
        {
            var pulse = 0.5f + 0.5f * MathF.Sin((float)Raylib.GetTime() * 5.5f);
            Raylib.BeginBlendMode(BlendMode.Additive);
            Raylib.DrawCircleGradient((int)Position.X, (int)Position.Y, 52f + pulse * 5f, Palette.C(255, 96, 72, 24), Palette.C(255, 96, 72, 0));
            Raylib.EndBlendMode();
        }

        Raylib.DrawRectangle((int)Position.X - size / 2, (int)Position.Y - size / 2, size, size, Palette.C(132, 34, 50));
        Raylib.DrawRectangleLines((int)Position.X - size / 2, (int)Position.Y - size / 2, size, size, Palette.C(232, 104, 92));

        if (_slamVisual > 0)
        {
            var alpha = (byte)(82 * (_slamVisual / 0.7f));
            Raylib.DrawCircle((int)Position.X, (int)Position.Y, GetSlamRadius(), Palette.C(255, 122, 86, alpha));
        }

        if (IsFast)
        {
            DrawFastChevrons();
        }

        var hp = Health / MaxHealth;
        var bar = new Rectangle(Position.X - 36, Position.Y - 34, 72, 6);
        Raylib.DrawRectangleRec(bar, Palette.C(20, 20, 20, 220));
        Raylib.DrawRectangle((int)bar.X, (int)bar.Y, (int)(bar.Width * hp), (int)bar.Height, Color.Green);
    }

    private void DrawFastChevrons()
    {
        var color = Palette.C(80, 255, 120);
        for (var i = 0; i < 2; i++)
        {
            var y = Position.Y - 8f + i * 15f;
            var tip = new Vector2(Position.X, y - 7f);
            var left = new Vector2(Position.X - 7f, y + 5f);
            var right = new Vector2(Position.X + 7f, y + 5f);
            Raylib.DrawLineEx(left, tip, 3f, color);
            Raylib.DrawLineEx(tip, right, 3f, color);
        }
    }
}
