using System.Numerics;
using Raylib_cs;

namespace Bungus.Game;

public sealed class Enemy
{
    public Vector2 Position;
    public float MaxHealth;
    public float Health;
    public int ZoneId = -1;
    public bool IsStrong;
    public bool IsPatrol;
    public bool IsEnhanced;
    public bool Alive => Health > 0f;
    public float ChallengeSpeedMultiplier { get; private set; } = 1f;
    public float ChallengeDamageMultiplier { get; private set; } = 1f;

    public bool KillAwarded;
    public bool JustHitByPlayer;

    private Vector2 _facing;
    private Vector2 _baseFacing;
    private float _attackCd;

    private Vector2 _patrolA;
    private Vector2 _patrolB;
    private bool _toB = true;

    private bool _alert;
    private Vector2 _target;
    private bool _investigating;
    private bool _returningFromInvestigation;
    private Vector2 _investigateTarget;
    private Vector2 _investigateReturnPoint;
    private float _investigateWait;
    private float _investigateReturnStartDistance;
    private float _investigateReturnStartHealth;

    private float _sweepPhase;
    private float _sweepDir = 1f;

    private float _burstCd;
    private float _patrolTurnTimer;
    private bool _patrolTurning;
    private int _burstShotsLeft;
    private float _burstShotCd;
    private List<Vector2> _navigationPath = [];
    private int _navigationPathIndex;
    private Vector2 _navigationGoal;
    private float _navigationRefreshTimer;
    private float _navigationForcePathTimer;

    private float _deathAnim = 0.45f;
    private float _slowTimer;
    private float _chillTimer;
    private float _slowSpeedMultiplier = 0.7f;
    private float _chillSpeedMultiplier = 0.75f;
    private float _poisonTimer;
    private float _poisonDamagePerSecond;

    private const float BaseView = 450f;
    private const float StrongView = 500f;
    private const float AlertDistance = 750f;
    private const float FovHalf = MathF.PI / 3f; // 120 total

    private Enemy(Vector2 pos)
    {
        Position = pos;
        _facing = new Vector2(1f, 0f);
        _baseFacing = _facing;
    }

    public static Enemy CreatePatrol(Vector2 a, Vector2 b, bool outpost, int zoneId = -1, bool enhanced = false)
    {
        var maxHealth = enhanced ? 125f : 100f;
        var e = new Enemy(a)
        {
            ZoneId = zoneId,
            IsPatrol = true,
            IsEnhanced = enhanced,
            _patrolA = a,
            _patrolB = b,
            _navigationGoal = b,
            _navigationRefreshTimer = Random.Shared.NextSingle() * 0.5f,
            MaxHealth = maxHealth,
            Health = maxHealth
        };
        return e;
    }

    public static Enemy CreateStrong(Vector2 pos, int zoneId = -1, bool enhanced = false)
    {
        var maxHealth = enhanced ? 375f : 300f;
        var e = new Enemy(pos)
        {
            ZoneId = zoneId,
            IsStrong = true,
            IsEnhanced = enhanced,
            MaxHealth = maxHealth,
            Health = maxHealth
        };
        return e;
    }

    public void UpdateVisionSweep(float dt)
    {
        if (!Alive) { _deathAnim -= dt; return; }
        if (_patrolTurning) return;

        _sweepPhase += dt * 0.70f * _sweepDir;
        if (_sweepPhase > 1f) { _sweepPhase = 1f; _sweepDir = -1f; }
        if (_sweepPhase < -1f) { _sweepPhase = -1f; _sweepDir = 1f; }

        var baseAngle = MathF.Atan2(_baseFacing.Y, _baseFacing.X);
        var sweepOffset = _sweepPhase * (MathF.PI * 0.07f);
        var a = baseAngle + sweepOffset;
        _facing = Vector2.Normalize(new Vector2(MathF.Cos(a), MathF.Sin(a)));
    }

    public void UpdateAwareness(Vector2 playerPos, float dt, List<Obstacle> obstacles)
    {
        if (!Alive) return;

        if (_alert)
        {
            if (Vector2.Distance(Position, playerPos) <= GetAlertViewDistance())
            {
                _target = playerPos;
                return;
            }

            _alert = false;
            StartLostAggroReturn();
            return;
        }

        if (CanSeePoint(playerPos, obstacles))
        {
            ForceAggro(playerPos);
        }
    }

    public bool CanSeePoint(Vector2 point, List<Obstacle> obstacles)
    {
        var to = point - Position;
        var dist = to.Length();
        if (dist > GetViewDistance() || dist < 0.01f) return false;

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
        _target = target;
    }

    public void ApplyChallengeModifiers(float healthMultiplier, float speedMultiplier, float damageMultiplier)
    {
        var healthRatio = MaxHealth <= 0f ? 1f : Health / MaxHealth;
        MaxHealth *= healthMultiplier;
        Health = MaxHealth * healthRatio;
        ChallengeSpeedMultiplier = speedMultiplier;
        ChallengeDamageMultiplier = damageMultiplier;
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

    public void UpdateMovement(float dt, Vector2 playerPos, List<Obstacle> obstacles, int worldSize)
    {
        _attackCd -= dt;
        _slowTimer = MathF.Max(0f, _slowTimer - dt);
        _chillTimer = MathF.Max(0f, _chillTimer - dt);
        TickPoison(dt);

        if (!Alive) return;

        if (_alert)
        {
            var to = _target - Position;
            if (to.LengthSquared() > 16f)
            {
                MoveTowardNavigated(_target, (IsStrong ? 118.75f : 147.5f) * GetMovementSpeedMultiplier() * ChallengeSpeedMultiplier, dt, obstacles, worldSize);
            }

            _burstCd -= dt;
            if (IsStrong) _burstShotCd -= dt;

            return;
        }

        if (_investigating)
        {
            UpdateInvestigation(dt, obstacles, worldSize);
            return;
        }

        if (IsPatrol)
        {
            if (_patrolTurning)
            {
                _patrolTurnTimer -= dt;
                var turned = VisibilityUtils.Rotate(_facing, MathF.PI * dt / 2f);
                if (turned != Vector2.Zero)
                {
                    _facing = Vector2.Normalize(turned);
                    _baseFacing = _facing;
                }
                if (_patrolTurnTimer <= 0f)
                {
                    _patrolTurning = false;
                    _toB = !_toB;
                }
                return;
            }

            var target = _toB ? _patrolB : _patrolA;
            var to = target - Position;
            if (to.Length() < 8f)
            {
                _patrolTurning = true;
                _patrolTurnTimer = 2f;
            }
            else
            {
                MoveTowardNavigated(target, 107.5f * GetMovementSpeedMultiplier() * ChallengeSpeedMultiplier, dt, obstacles, worldSize);
            }
        }
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
        if (to.Length() < 10f)
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

        MoveTowardNavigated(target, (IsStrong ? 102.5f : 120f) * GetMovementSpeedMultiplier() * ChallengeSpeedMultiplier, dt, obstacles, worldSize);
        if (_returningFromInvestigation) HealDuringInvestigationReturn();
    }

    private void MoveTowardNavigated(Vector2 desiredTarget, float speed, float dt, List<Obstacle> obstacles, int worldSize)
    {
        const float radius = 14f;
        const float goalRefreshDistance = 96f;

        _navigationRefreshTimer -= dt;
        _navigationForcePathTimer = MathF.Max(0f, _navigationForcePathTimer - dt);
        var waypoint = desiredTarget;
        var goalChanged = Vector2.DistanceSquared(_navigationGoal, desiredTarget) > goalRefreshDistance * goalRefreshDistance;
        var shouldEvaluateNavigation = _navigationPath.Count > 0
            || _navigationRefreshTimer <= 0f
            || goalChanged
            || _navigationForcePathTimer > 0f;

        if (!shouldEvaluateNavigation)
        {
            waypoint = desiredTarget;
        }
        else if (_navigationForcePathTimer <= 0f && PathfindingUtils.HasClearPath(Position, desiredTarget, radius, obstacles, worldSize))
        {
            _navigationPath.Clear();
            _navigationPathIndex = 0;
            _navigationGoal = desiredTarget;
            _navigationRefreshTimer = 0.35f;
        }
        else
        {
            var shouldRefresh = _navigationPath.Count == 0
                || _navigationPathIndex >= _navigationPath.Count
                || _navigationRefreshTimer <= 0f
                || goalChanged;

            if (shouldRefresh)
            {
                var allowDirectShortcut = _navigationForcePathTimer <= 0f;
                if (PathfindingUtils.TryFindPath(Position, desiredTarget, radius, obstacles, worldSize, out var path, allowDirectShortcut))
                {
                    _navigationPath = path;
                    _navigationPathIndex = 0;
                    _navigationGoal = desiredTarget;
                    _navigationRefreshTimer = 0.45f;
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
                       && Vector2.DistanceSquared(Position, _navigationPath[_navigationPathIndex]) < 18f * 18f)
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
        _baseFacing = dir;

        var previous = Position;
        Position = MovementUtils.MoveWithCollisions(Position, dir * speed * dt, radius, obstacles, worldSize);
        if (Vector2.DistanceSquared(previous, Position) < 0.0025f)
        {
            _navigationRefreshTimer = 0f;
            if (IsStrong) _navigationForcePathTimer = 0.8f;
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

    public void TryShootBurst(Vector2 playerPos, List<Projectile> projectiles)
    {
        if (!Alive || !_alert) return;

        if (!IsStrong)
        {
            if (_burstCd > 0f) return;

            var dir = playerPos - Position;
            if (dir != Vector2.Zero) dir = Vector2.Normalize(dir);
            projectiles.Add(new Projectile(Position + dir * 16f, dir, 420f, 1.68f, Palette.C(255, 120, 120), true, ScaleDamage(8f)));
            _burstCd = 2.8f;
            return;
        }

        if (_burstCd <= 0f && _burstShotsLeft <= 0)
        {
            _burstShotsLeft = 3;
            _burstShotCd = 0f;
            _burstCd = 2.8f;
        }

        if (_burstShotsLeft > 0 && _burstShotCd <= 0f)
        {
            var dir = playerPos - Position;
            if (dir != Vector2.Zero) dir = Vector2.Normalize(dir);
            projectiles.Add(new Projectile(Position + dir * 16f, dir, 420f, 1.68f, Palette.C(255, 120, 120), true, ScaleDamage(10f)));
            _burstShotsLeft--;
            _burstShotCd = 0.13f;
        }
    }

    public bool TryMeleeHit(Player player)
    {
        if (!Alive || _attackCd > 0f || Vector2.Distance(Position, player.Position) > 24f) return false;
        _attackCd = IsStrong ? 1.3f : 0.9f;
        player.TakeDamage(ScaleDamage(IsStrong ? 18f : 10f));
        return true;
    }

    private float ScaleDamage(float damage) => (IsEnhanced ? damage * 1.1f : damage) * ChallengeDamageMultiplier;

    public float GetViewDistance() => IsStrong ? StrongView : BaseView;

    private static float GetAlertViewDistance() => AlertDistance;

    public void Damage(float amount)
    {
        if (!Alive) return;
        Health = MathF.Max(0f, Health - amount);
    }

    public void Draw(
        VisualTheme theme,
        Texture2D? baseEnemyTexture = null,
        Texture2D? enhancedBaseEnemyTexture = null,
        Texture2D? triangleEnemyTexture = null,
        Texture2D? enhancedTriangleEnemyTexture = null)
    {
        if (Alive)
        {
            if (IsStrong)
            {
                var activeTexture = IsEnhanced && enhancedTriangleEnemyTexture is { Id: not 0 }
                    ? enhancedTriangleEnemyTexture
                    : triangleEnemyTexture;
                var drewTexture = activeTexture is { Id: not 0 };
                if (drewTexture)
                {
                    var texture = activeTexture!.Value;
                    var size = 52f;
                    var source = new Rectangle(0f, 0f, texture.Width, texture.Height);
                    var dest = new Rectangle(Position.X, Position.Y, size, size);
                    var origin = new Vector2(size * 0.5f, size * 0.5f);
                    var rotation = MathF.Atan2(_facing.Y, _facing.X) * 180f / MathF.PI;
                    Raylib.DrawTexturePro(texture, source, dest, origin, rotation, Color.White);
                }
                else
                {
                    var tip = Position + _facing * 16f;
                    var left = Position + VisibilityUtils.Rotate(_facing, MathF.PI * 0.78f) * 14f;
                    var right = Position + VisibilityUtils.Rotate(_facing, -MathF.PI * 0.78f) * 14f;
                    Raylib.DrawTriangle(tip, left, right, theme.EnemyStrong);
                    Raylib.DrawTriangleLines(tip, left, right, Color.Maroon);
                }
                if (IsEnhanced && !drewTexture)
                {
                    var innerTip = Position + _facing * 8f;
                    var innerLeft = Position + VisibilityUtils.Rotate(_facing, MathF.PI * 0.78f) * 7f;
                    var innerRight = Position + VisibilityUtils.Rotate(_facing, -MathF.PI * 0.78f) * 7f;
                    Raylib.DrawTriangle(innerTip, innerLeft, innerRight, Color.White);
                    Raylib.DrawTriangleLines(innerTip, innerLeft, innerRight, Color.Black);
                }
            }
            else
            {
                var activeTexture = IsEnhanced && enhancedBaseEnemyTexture is { Id: not 0 }
                    ? enhancedBaseEnemyTexture
                    : baseEnemyTexture;
                var drewTexture = activeTexture is { Id: not 0 };
                if (drewTexture)
                {
                    var texture = activeTexture!.Value;
                    var size = 44f;
                    var source = new Rectangle(0f, 0f, texture.Width, texture.Height);
                    var dest = new Rectangle(Position.X, Position.Y, size, size);
                    var origin = new Vector2(size * 0.5f, size * 0.5f);
                    var rotation = MathF.Atan2(_facing.Y, _facing.X) * 180f / MathF.PI;
                    Raylib.DrawTexturePro(texture, source, dest, origin, rotation, Color.White);
                }
                else
                {
                    Raylib.DrawCircleV(Position, 14f, theme.Enemy);
                }
                if (IsEnhanced && !drewTexture) Raylib.DrawCircleV(Position, 7f, Color.White);
                if (!drewTexture) Raylib.DrawCircleLines((int)Position.X, (int)Position.Y, 16f, Color.Maroon);
            }

            var hp = Health / MaxHealth;
            var bar = new Rectangle(Position.X - 22, Position.Y - 26, 44, 5);
            Raylib.DrawRectangleRec(bar, Palette.C(20, 20, 20, 220));
            Raylib.DrawRectangle((int)bar.X, (int)bar.Y, (int)(bar.Width * hp), (int)bar.Height, Color.Green);
        }
        else if (_deathAnim > 0)
        {
            var fade = (byte)(255 * (_deathAnim / 0.45f));
            Raylib.DrawCircle((int)Position.X, (int)Position.Y, 18f * (1f - _deathAnim / 0.45f), Palette.C(255, 90, 60, fade));
        }
    }

    public void DrawSight()
    {
        if (!Alive) return;

        var c = Palette.C(120, 140, 160, 26);

        var left = VisibilityUtils.Rotate(_facing, -FovHalf);
        var right = VisibilityUtils.Rotate(_facing, FovHalf);
        var sightLineLength = 100f;
        VisibilityUtils.DrawDashedLine(Position, Position + left * sightLineLength, 22, c);
        VisibilityUtils.DrawDashedLine(Position, Position + right * sightLineLength, 22, c);
    }
}
