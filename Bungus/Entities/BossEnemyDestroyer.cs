using System.Numerics;
using Raylib_cs;

namespace Bungus.Game;

public sealed class BossEnemyDestroyer
{
    public Vector2 Position;
    public float MaxHealth = 4500f;
    public float Health = 4500f;
    public bool Alive => Health > 0f;
    public bool KillAwarded;
    public bool PhaseTwo => Health <= MaxHealth * 0.5f;

    private const float ShieldNodeMaxHealth = 175f;
    private const float ShieldNodeSize = 28f;
    private const float DestroyedShieldNodeSize = ShieldNodeSize * 0.5f;

    private Vector2 _facing = new(1f, 0f);
    private float _forwardDashCd = 1.5f;
    private float _sideDashCd = 2.3f;
    private float _shootCd = 1.2f;
    private float _grenadeCd = 4.6f;
    private float _radialShotCd = 3.3f;
    private float _strafeSwitch;
    private int _burstShotsLeft;
    private float _burstShotCd;
    private bool _alert;
    private bool _investigating;
    private bool _returningFromInvestigation;
    private Vector2 _investigateTarget;
    private Vector2 _investigateReturnPoint;
    private float _investigateWait;
    private float _investigateReturnStartDistance;
    private float _investigateReturnStartHealth;
    private bool _phaseTwoShieldReset;
    private float _slowTimer;
    private float _chillTimer;
    private float _slowSpeedMultiplier = 0.95f;
    private float _chillSpeedMultiplier = 0.75f;
    private float _poisonTimer;
    private float _poisonDamagePerSecond;
    private List<Vector2> _navigationPath = [];
    private int _navigationPathIndex;
    private Vector2 _navigationGoal;
    private float _navigationRefreshTimer;

    private const float ViewDistance = 825f;
    private const float AlertViewMultiplier = 1.25f;
    private const float PhaseOneSpeed = 72.5f;
    private const float PhaseTwoSpeed = 190.625f;
    private const float DesiredDistance = 270f;
    private const float DashDistance = 152.5f;
    private const float SideDashDistance = DashDistance * 0.5f;
    private const float CollisionRadius = 52f;
    private const float BulletSpeed = 520f;
    private const float BulletDamage = 16f;
    private const float BulletLifetime = 1.25f;
    private const float PhaseTwoRangeMultiplier = 1.25f;
    private readonly float[] _shieldNodeHealth = [ShieldNodeMaxHealth, ShieldNodeMaxHealth, ShieldNodeMaxHealth, ShieldNodeMaxHealth];

    public BossEnemyDestroyer(Vector2 pos)
    {
        Position = pos;
    }

    public void Update(float dt, Vector2 playerPos, List<Projectile> projectiles, Player player, List<Obstacle> obstacles, int worldSize, List<DashAfterImage> afterImages)
    {
        if (!Alive) return;

        _slowTimer = MathF.Max(0f, _slowTimer - dt);
        _chillTimer = MathF.Max(0f, _chillTimer - dt);
        TickPoison(dt);
        if (!Alive) return;
        _forwardDashCd -= dt;
        _sideDashCd -= dt;
        _shootCd -= dt;
        _grenadeCd -= dt;
        if (PhaseTwo) _radialShotCd -= dt;
        else _radialShotCd = 3f;

        var toPlayer = playerPos - Position;
        if (toPlayer == Vector2.Zero) toPlayer = new Vector2(1f, 0f);
        var distance = toPlayer.Length();
        var dir = Vector2.Normalize(toPlayer);
        _facing = dir;

        if (PhaseTwo && !_phaseTwoShieldReset)
        {
            RestoreShieldNodes();
            _phaseTwoShieldReset = true;
        }

        if (_alert)
        {
            if (distance > GetAlertViewDistance()) _alert = false;
        }
        else if (VisibilityUtils.HasLineOfSight(Position, playerPos, obstacles) && distance <= ViewDistance)
        {
            _alert = true;
        }

        if (!_alert && _investigating)
        {
            if (CanSeePoint(playerPos, obstacles))
            {
                ForceAggro(playerPos);
            }
            else
            {
                UpdateInvestigation(dt, obstacles, worldSize);
                return;
            }
        }

        if (!_alert) return;

        if (PhaseTwo)
        {
            UpdatePhaseTwoMovement(dt, dir, distance, playerPos, obstacles, worldSize);
        }
        else
        {
            MoveTowardNavigated(playerPos, PhaseOneSpeed * GetMovementSpeedMultiplier(), dt, obstacles, worldSize);
        }

        if (_forwardDashCd <= 0f)
        {
            ExecuteDash(player, dir, DashDistance, 34f, afterImages, obstacles, worldSize);
            _forwardDashCd = 1f + Random.Shared.NextSingle() * 2f;
        }

        if (_sideDashCd <= 0f)
        {
            var sideDir = VisibilityUtils.Rotate(dir, Random.Shared.NextSingle() < 0.5f ? MathF.PI / 2f : -MathF.PI / 2f);
            ExecuteDash(player, sideDir, SideDashDistance, 22f, afterImages, obstacles, worldSize);
            _sideDashCd = 1f + Random.Shared.NextSingle() * 3f;
        }

        if (_shootCd <= 0f && _burstShotsLeft <= 0)
        {
            _burstShotsLeft = PhaseTwo ? 8 : 6;
            _burstShotCd = 0f;
            _shootCd = PhaseTwo ? 1.5f : 2f;
        }

        _burstShotCd -= dt;
        while (_burstShotsLeft > 0 && _burstShotCd <= 0f)
        {
            FireBurst(projectiles, dir);
            _burstShotsLeft--;
            _burstShotCd += 0.08f;
        }

        if (!PhaseTwo && _grenadeCd <= 0f)
        {
            projectiles.Add(new Projectile(
                Position + dir * 42f,
                dir,
                340f,
                0.68f,
                Palette.C(255, 90, 40),
                true,
                0f,
                ProjectileKind.Grenade,
                120f,
                80f,
                8f));
            _grenadeCd = 3f + Random.Shared.NextSingle() * 4f;
        }

        if (PhaseTwo && _radialShotCd <= 0f)
        {
            FireRadialBurst(projectiles);
            _radialShotCd = 3f;
        }
    }

    private void UpdatePhaseTwoMovement(float dt, Vector2 dir, float distance, Vector2 playerPos, List<Obstacle> obstacles, int worldSize)
    {
        var radial = 0f;
        if (distance > DesiredDistance + 25f) radial = PhaseTwoSpeed;
        else if (distance < DesiredDistance - 20f) radial = -PhaseTwoSpeed * 0.75f;

        if (radial > 0f && !PathfindingUtils.HasClearPath(Position, playerPos, CollisionRadius, obstacles, worldSize))
        {
            MoveTowardNavigated(playerPos, PhaseTwoSpeed * GetMovementSpeedMultiplier(), dt, obstacles, worldSize);
            return;
        }

        _strafeSwitch -= dt;
        if (_strafeSwitch <= 0f) _strafeSwitch = 0.22f + Random.Shared.NextSingle() * 0.55f;
        var strafeSign = MathF.Sin(_strafeSwitch * 8f + Position.X * 0.015f) > 0f ? 1f : -1f;
        var strafeDir = new Vector2(-dir.Y, dir.X) * strafeSign;
        var move = dir * radial + strafeDir * (PhaseTwoSpeed * 0.75f);
        Position = MovementUtils.MoveWithCollisions(Position, move * GetMovementSpeedMultiplier() * dt, CollisionRadius, obstacles, worldSize);
    }

    private void ExecuteDash(Player player, Vector2 dashDir, float distance, float damage, List<DashAfterImage> afterImages, List<Obstacle> obstacles, int worldSize)
    {
        Position = MovementUtils.MoveWithCollisions(Position, dashDir * distance, CollisionRadius, obstacles, worldSize);
        DashAfterImage.Spawn(afterImages, Position, dashDir, distance, Palette.C(255, 85, 85), true);
        if (Vector2.Distance(Position, player.Position) < 76f) player.TakeDamage(damage);
    }

    private void FireBurst(List<Projectile> projectiles, Vector2 dir)
    {
        var burstAngles = PhaseTwo
            ? new[] { -0.2f, -0.1f, 0f, 0.1f, 0.2f }
            : new[] { -0.11f, 0.11f };

        foreach (var offset in burstAngles)
        {
            var spread = ((Random.Shared.NextSingle() * 3f) - 1.5f) * (MathF.PI / 180f);
            var shotDir = VisibilityUtils.Rotate(dir, offset + spread);
            projectiles.Add(CreateBullet(shotDir));
        }
    }

    private void FireRadialBurst(List<Projectile> projectiles)
    {
        for (var i = 0; i < 20; i++)
        {
            var angle = i / 20f * MathF.Tau;
            var shotDir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            projectiles.Add(CreateBullet(shotDir));
        }
    }

    private Projectile CreateBullet(Vector2 dir)
    {
        var lifetime = BulletLifetime * (PhaseTwo ? PhaseTwoRangeMultiplier : 1f);
        return new Projectile(Position + dir * 40f, dir, BulletSpeed, lifetime, Palette.C(255, 140, 110), true, BulletDamage);
    }

    private float GetMovementSpeedMultiplier() => _slowTimer > 0f ? _slowSpeedMultiplier : _chillTimer > 0f ? _chillSpeedMultiplier : 1f;

    private static float GetAlertViewDistance() => ViewDistance * AlertViewMultiplier;

    public bool CanSeePoint(Vector2 point, List<Obstacle> obstacles)
    {
        var to = point - Position;
        var dist = to.Length();
        return dist <= ViewDistance && VisibilityUtils.HasLineOfSight(Position, point, obstacles);
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
                RestoreShieldNodes();
                _investigating = false;
                return;
            }

            _investigateWait = 1f;
            return;
        }

        MoveTowardNavigated(target, PhaseOneSpeed * GetMovementSpeedMultiplier(), dt, obstacles, worldSize);
        if (_returningFromInvestigation) HealDuringInvestigationReturn();
    }

    private void MoveTowardNavigated(Vector2 desiredTarget, float speed, float dt, List<Obstacle> obstacles, int worldSize)
    {
        _navigationRefreshTimer -= dt;
        var waypoint = desiredTarget;
        var goalChanged = Vector2.DistanceSquared(_navigationGoal, desiredTarget) > 140f * 140f;
        var shouldEvaluateNavigation = _navigationPath.Count > 0
            || _navigationRefreshTimer <= 0f
            || goalChanged;

        if (!shouldEvaluateNavigation)
        {
            waypoint = desiredTarget;
        }
        else if (PathfindingUtils.HasClearPath(Position, desiredTarget, CollisionRadius, obstacles, worldSize))
        {
            _navigationPath.Clear();
            _navigationPathIndex = 0;
            _navigationGoal = desiredTarget;
            _navigationRefreshTimer = 0.45f;
        }
        else
        {
            var shouldRefresh = _navigationPath.Count == 0
                || _navigationPathIndex >= _navigationPath.Count
                || _navigationRefreshTimer <= 0f
                || goalChanged;

            if (shouldRefresh)
            {
                if (PathfindingUtils.TryFindPath(Position, desiredTarget, CollisionRadius, obstacles, worldSize, out var path))
                {
                    _navigationPath = path;
                    _navigationPathIndex = 0;
                    _navigationGoal = desiredTarget;
                    _navigationRefreshTimer = 0.55f;
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
                       && Vector2.DistanceSquared(Position, _navigationPath[_navigationPathIndex]) < 38f * 38f)
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
        Position = MovementUtils.MoveWithCollisions(Position, dir * speed * dt, CollisionRadius, obstacles, worldSize);
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
        if (!ShieldActive) DamageCore(_poisonDamagePerSecond * dt);
    }

    public bool IntersectsAnyHitZone(Vector2 point, float radius)
    {
        if (!Alive) return false;

        for (var i = 0; i < _shieldNodeHealth.Length; i++)
        {
            if (!IsShieldNodeAlive(i)) continue;

            var limit = radius + GetShieldNodeHitRadius(i);
            if (Vector2.DistanceSquared(GetShieldNodePosition(i), point) <= limit * limit) return true;
        }

        var bodyLimit = radius + GetBodyHitRadius();
        return Vector2.DistanceSquared(Position, point) <= bodyLimit * bodyLimit;
    }

    public bool TryApplyPointDamage(Vector2 point, float radius, float amount)
        => TryApplyPointDamage(point, radius, amount, out _);

    public bool TryApplyPointDamage(Vector2 point, float radius, float amount, out float actualDamage)
    {
        actualDamage = 0f;
        if (!Alive) return false;

        var shieldIndex = FindShieldNodeHit(point, radius);
        if (shieldIndex >= 0)
        {
            actualDamage = DamageShieldNode(shieldIndex, amount);
            return true;
        }

        var bodyLimit = radius + GetBodyHitRadius();
        if (Vector2.DistanceSquared(Position, point) > bodyLimit * bodyLimit) return false;

        if (!ShieldActive)
        {
            var healthBefore = Health;
            DamageCore(amount);
            actualDamage = healthBefore - Health;
        }
        return true;
    }

    public bool TryApplySegmentDamage(Vector2 from, Vector2 to, float radius, float amount)
        => TryApplySegmentDamage(from, to, radius, amount, out _);

    public bool TryApplySegmentDamage(Vector2 from, Vector2 to, float radius, float amount, out float actualDamage)
    {
        actualDamage = 0f;
        if (!Alive) return false;

        var shieldIndex = FindShieldNodeHit(from, to, radius);
        if (shieldIndex >= 0)
        {
            actualDamage = DamageShieldNode(shieldIndex, amount);
            return true;
        }

        var bodyLimit = radius + GetBodyHitRadius();
        if (DistanceToSegment(Position, from, to) > bodyLimit) return false;

        if (!ShieldActive)
        {
            var healthBefore = Health;
            DamageCore(amount);
            actualDamage = healthBefore - Health;
        }
        return true;
    }

    public bool ApplyExplosionDamage(Vector2 center, float radius, float amount)
        => ApplyExplosionDamage(center, radius, amount, out _);

    public bool ApplyExplosionDamage(Vector2 center, float radius, float amount, out float actualDamage)
    {
        actualDamage = 0f;
        if (!Alive) return false;

        var hitAny = false;
        var shieldWasActive = ShieldActive;

        for (var i = 0; i < _shieldNodeHealth.Length; i++)
        {
            if (!IsShieldNodeAlive(i)) continue;

            var limit = radius + GetShieldNodeHitRadius(i);
            if (Vector2.DistanceSquared(GetShieldNodePosition(i), center) > limit * limit) continue;

            actualDamage += DamageShieldNode(i, amount);
            hitAny = true;
        }

        var bodyLimit = radius + GetBodyHitRadius();
        if (Vector2.DistanceSquared(Position, center) <= bodyLimit * bodyLimit)
        {
            if (!shieldWasActive)
            {
                var healthBefore = Health;
                DamageCore(amount);
                actualDamage += healthBefore - Health;
            }
            hitAny = true;
        }

        return hitAny;
    }

    public void Damage(float amount)
    {
        if (!Alive || ShieldActive) return;
        DamageCore(amount);
    }

    public void DrawSight()
    {
        if (!Alive) return;
    }

    public void Draw()
    {
        if (!Alive) return;

        var mainSize = GetBodySize();
        var time = (float)Raylib.GetTime();
        var pulse = 0.5f + 0.5f * MathF.Sin(time * (PhaseTwo ? 5.5f : 3.2f));

        if (PhaseTwo)
        {
            Raylib.BeginBlendMode(BlendMode.Additive);
            Raylib.DrawPoly(Position, 4, mainSize * (0.78f + pulse * 0.08f), 45f, Palette.C(255, 116, 54, 34));
            Raylib.EndBlendMode();
            DrawDiamond(Position, mainSize, Palette.C(162, 42, 46), Palette.C(255, 128, 72));
            Raylib.DrawPolyLinesEx(Position, 4, mainSize * 0.74f, 45f, 4f, Palette.C(255, 178, 82, 210));
        }
        else
        {
            DrawSquare(Position, mainSize, Palette.C(94, 24, 34), Palette.C(178, 58, 70));
        }

        if (!PhaseTwo || ShieldActive)
        {
            for (var i = 0; i < _shieldNodeHealth.Length; i++)
            {
                var hpRatio = Math.Clamp(_shieldNodeHealth[i] / ShieldNodeMaxHealth, 0f, 1f);
                var fill = BlendColor(Palette.C(62, 152, 220), Palette.C(224, 242, 255), 1f - hpRatio);
                var line = IsShieldNodeAlive(i) ? Palette.C(188, 226, 255) : Palette.C(180, 180, 180);
                DrawSquare(GetShieldNodePosition(i), GetShieldNodeSize(i), fill, line);
            }
        }

        var hp = Health / MaxHealth;
        var bar = new Rectangle(Position.X - 72, Position.Y - 76, 144, 10);

        if (ShieldActive)
        {
            var shieldFrame = new Rectangle(bar.X - 6f, bar.Y - 4f, bar.Width + 12f, bar.Height + 8f);
            Raylib.DrawRectangleRec(shieldFrame, Palette.C(48, 48, 48, 165));
            Raylib.DrawRectangleLinesEx(shieldFrame, 4f, Palette.C(165, 165, 165, 235));
        }

        Raylib.DrawRectangleRec(bar, Palette.C(20, 20, 20, 220));
        Raylib.DrawRectangle((int)bar.X, (int)bar.Y, (int)(bar.Width * hp), (int)bar.Height, PhaseTwo ? Color.Orange : Color.Red);
    }

    private bool ShieldActive
    {
        get
        {
            for (var i = 0; i < _shieldNodeHealth.Length; i++)
            {
                if (_shieldNodeHealth[i] > 0f) return true;
            }

            return false;
        }
    }

    private static Color BlendColor(Color from, Color to, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Color(
            (byte)(from.R + (to.R - from.R) * t),
            (byte)(from.G + (to.G - from.G) * t),
            (byte)(from.B + (to.B - from.B) * t),
            (byte)(from.A + (to.A - from.A) * t));
    }

    private float GetBodySize() => PhaseTwo ? 92f : 84f;

    private float GetBodyHitRadius() => PhaseTwo ? 46f : 42f;

    private Vector2 GetShieldNodePosition(int index)
    {
        var offset = GetBodySize() * 0.5f;
        return index switch
        {
            0 => Position + new Vector2(-offset, -offset),
            1 => Position + new Vector2(offset, -offset),
            2 => Position + new Vector2(offset, offset),
            _ => Position + new Vector2(-offset, offset)
        };
    }

    private float GetShieldNodeSize(int index) => IsShieldNodeAlive(index) ? ShieldNodeSize : DestroyedShieldNodeSize;

    private float GetShieldNodeHitRadius(int index) => GetShieldNodeSize(index) * 0.58f;

    private bool IsShieldNodeAlive(int index) => _shieldNodeHealth[index] > 0f;

    private int FindShieldNodeHit(Vector2 point, float radius)
    {
        var closestIndex = -1;
        var closestDistance = float.MaxValue;

        for (var i = 0; i < _shieldNodeHealth.Length; i++)
        {
            if (!IsShieldNodeAlive(i)) continue;

            var limit = radius + GetShieldNodeHitRadius(i);
            var distance = Vector2.DistanceSquared(GetShieldNodePosition(i), point);
            if (distance > limit * limit || distance >= closestDistance) continue;

            closestIndex = i;
            closestDistance = distance;
        }

        return closestIndex;
    }

    private int FindShieldNodeHit(Vector2 from, Vector2 to, float radius)
    {
        var closestIndex = -1;
        var closestDistance = float.MaxValue;

        for (var i = 0; i < _shieldNodeHealth.Length; i++)
        {
            if (!IsShieldNodeAlive(i)) continue;

            var limit = radius + GetShieldNodeHitRadius(i);
            var distance = DistanceToSegment(GetShieldNodePosition(i), from, to);
            if (distance > limit || distance >= closestDistance) continue;

            closestIndex = i;
            closestDistance = distance;
        }

        return closestIndex;
    }

    private float DamageShieldNode(int index, float amount)
    {
        if (!IsShieldNodeAlive(index) || amount <= 0f) return 0f;
        var healthBefore = _shieldNodeHealth[index];
        _shieldNodeHealth[index] = MathF.Max(0f, _shieldNodeHealth[index] - amount);
        return healthBefore - _shieldNodeHealth[index];
    }

    private void RestoreShieldNodes()
    {
        for (var i = 0; i < _shieldNodeHealth.Length; i++)
        {
            _shieldNodeHealth[i] = ShieldNodeMaxHealth;
        }
    }

    private void DamageCore(float amount)
    {
        if (amount <= 0f) return;
        Health = MathF.Max(0f, Health - amount);
    }

    private static void DrawSquare(Vector2 center, float size, Color fill, Color line)
    {
        Raylib.DrawPoly(center, 4, size / MathF.Sqrt(2f), 45f, fill);
        Raylib.DrawPolyLinesEx(center, 4, size / MathF.Sqrt(2f), 45f, 2f, line);
    }

    private static void DrawDiamond(Vector2 center, float size, Color fill, Color line)
    {
        Raylib.DrawPoly(center, 4, size / MathF.Sqrt(2f), 0f, fill);
        Raylib.DrawPolyLinesEx(center, 4, size / MathF.Sqrt(2f), 0f, 2f, line);
    }

    private static float DistanceToSegment(Vector2 point, Vector2 from, Vector2 to)
    {
        var delta = to - from;
        var t = Vector2.Dot(point - from, delta) / MathF.Max(delta.LengthSquared(), 0.0001f);
        t = Math.Clamp(t, 0f, 1f);
        return Vector2.Distance(point, from + delta * t);
    }
}
