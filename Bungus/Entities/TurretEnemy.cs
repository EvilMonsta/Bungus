using System.Numerics;
using Raylib_cs;

namespace Bungus.Game;

public sealed class TurretEnemy
{
    public Vector2 Position;
    public float MaxHealth = 260f;
    public float Health = 260f;
    public int ZoneId = -1;
    public bool Alive => Health > 0f;
    public bool KillAwarded;

    private Vector2 _facing;
    private readonly float _baseFacingAngle;
    private float _scanOffset;
    private float _scanTargetOffset = TurretScanHalfAngle;
    private float _scanWaitLeft;
    private float _shootCd;
    private bool _alert;
    private float _longRangeAlertTimer;
    private Vector2 _lastSeenPlayerPos;
    private bool _hasAim;
    private Vector2 _aimAt;
    private float _poisonTimer;
    private float _poisonDamagePerSecond;

    private const float ViewDistance = 735f;
    private const float FovHalf = MathF.PI / 3f;
    private const float TurretScanHalfAngle = MathF.PI / 3f;
    private const float TurretScanSpeed = MathF.PI / 9f;

    public TurretEnemy(Vector2 pos, float angle, int zoneId = -1)
    {
        Position = pos;
        ZoneId = zoneId;
        _baseFacingAngle = angle;
        _facing = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
    }

    public void Update(float dt, Vector2 playerPos, List<Projectile> projectiles, List<Obstacle> obstacles, bool infiniteAggro = false)
    {
        if (!Alive) return;
        TickPoison(dt);
        if (!Alive) return;

        var toPlayer = playerPos - Position;
        var distToPlayer = toPlayer.Length();
        _shootCd -= dt;
        _longRangeAlertTimer = MathF.Max(0f, _longRangeAlertTimer - dt);
        if (infiniteAggro)
        {
            _alert = true;
            _lastSeenPlayerPos = playerPos;
            _hasAim = true;
            _aimAt = playerPos;
            var aimDir = playerPos - Position;
            if (aimDir != Vector2.Zero) _facing = Vector2.Normalize(aimDir);
            if (_shootCd <= 0f && aimDir != Vector2.Zero)
            {
                var dir = Vector2.Normalize(aimDir);
                projectiles.Add(new Projectile(Position + dir * 20f, dir, 1785f, 1.84f, Palette.C(255, 40, 40), true, 56f));
                _shootCd = 3f;
            }
            return;
        }

        if (!_alert)
        {
            UpdateScanRotation(dt);
        }

        if (_alert && distToPlayer > ViewDistance && _longRangeAlertTimer <= 0f)
        {
            _alert = false;
            var toLast = _lastSeenPlayerPos - Position;
            if (toLast != Vector2.Zero) _facing = Vector2.Normalize(toLast);
        }

        if (CanSee(playerPos, obstacles, _longRangeAlertTimer > 0f))
        {
            if (!_alert) _shootCd = 3f;
            _alert = true;
            _lastSeenPlayerPos = playerPos;
            _hasAim = true;
            _aimAt = playerPos;
            var aimDir = playerPos - Position;
            if (aimDir != Vector2.Zero) _facing = Vector2.Normalize(aimDir);
            if (_shootCd <= 0f)
            {
                var dir = Vector2.Normalize(playerPos - Position);
                projectiles.Add(new Projectile(Position + dir * 20f, dir, 1785f, 1.84f, Palette.C(255, 40, 40), true, 56f));
                _shootCd = 3f;
            }
        }
        else
        {
            _hasAim = false;
        }
    }

    private void UpdateScanRotation(float dt)
    {
        if (_scanWaitLeft > 0f)
        {
            _scanWaitLeft -= dt;
            return;
        }

        var delta = _scanTargetOffset - _scanOffset;
        var step = MathF.Sign(delta) * MathF.Min(MathF.Abs(delta), TurretScanSpeed * dt);
        _scanOffset += step;
        SetFacingFromScan();

        if (MathF.Abs(_scanTargetOffset - _scanOffset) > 0.001f) return;

        _scanTargetOffset = _scanTargetOffset > 0f ? -TurretScanHalfAngle : TurretScanHalfAngle;
        _scanWaitLeft = 1f;
    }

    private void SetFacingFromScan()
    {
        var angle = _baseFacingAngle + _scanOffset;
        _facing = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
    }

    private bool CanSee(Vector2 point, List<Obstacle> obstacles, bool allowLongRange)
    {
        var to = point - Position;
        var dist = to.Length();
        if (dist < 0.01f || (!allowLongRange && dist > ViewDistance)) return false;

        if (!VisibilityUtils.HasLineOfSight(Position, point, obstacles)) return false;

        var dir = Vector2.Normalize(to);
        var angle = MathF.Acos(Math.Clamp(Vector2.Dot(_facing, dir), -1f, 1f));
        return angle <= FovHalf;
    }

    public bool CanSeePoint(Vector2 point, List<Obstacle> obstacles) => CanSee(point, obstacles, _longRangeAlertTimer > 0f);

    public bool CanNoticeCombatPoint(Vector2 point, List<Obstacle> obstacles)
    {
        var to = point - Position;
        if (to.LengthSquared() < 0.01f) return false;
        if (!VisibilityUtils.HasLineOfSight(Position, point, obstacles)) return false;

        var dir = Vector2.Normalize(to);
        var angle = MathF.Acos(Math.Clamp(Vector2.Dot(_facing, dir), -1f, 1f));
        return angle <= FovHalf;
    }

    public void ForceAggro(Vector2 target)
    {
        if (!_alert) _shootCd = 3f;
        _alert = true;
        _lastSeenPlayerPos = target;
        _hasAim = false;
        _aimAt = target;
        var aimDir = target - Position;
        if (aimDir != Vector2.Zero) _facing = Vector2.Normalize(aimDir);
    }

    public bool ReactToShot(Vector2 shotSource, Vector2 playerPos, List<Obstacle> obstacles)
    {
        if (!CanSee(playerPos, obstacles, true)) return false;

        ForceAggro(playerPos);
        if (Vector2.Distance(Position, shotSource) > ViewDistance) _longRangeAlertTimer = 5f;
        return true;
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

    public void ApplyStickySlow(float duration = 1f, float strengthMultiplier = 1f) { }
    public void ApplyFreezeChill(float duration = 10f, float strengthMultiplier = 1f) { }

    public void DrawSight()
    {
        if (!Alive) return;
        if (_alert) return;

        var left = VisibilityUtils.Rotate(_facing, -FovHalf);
        var right = VisibilityUtils.Rotate(_facing, FovHalf);
        var sightLineLength = 100f;
        VisibilityUtils.DrawDashedLine(Position, Position + left * sightLineLength, 24, Palette.C(250, 80, 80, 24));
        VisibilityUtils.DrawDashedLine(Position, Position + right * sightLineLength, 24, Palette.C(250, 80, 80, 24));
    }

    public void DrawAimLine()
    {
        if (!Alive || !_hasAim) return;
        Raylib.DrawLineEx(Position, _aimAt, 1.5f, Palette.C(255, 40, 40, 190));
    }

    public void Draw()
    {
        if (!Alive) return;

        var mainTip = Position + _facing * 18f;
        var mainLeft = Position + VisibilityUtils.Rotate(_facing, MathF.PI * 0.8f) * 14f;
        var mainRight = Position + VisibilityUtils.Rotate(_facing, -MathF.PI * 0.8f) * 14f;

        Raylib.DrawTriangle(mainTip, mainLeft, mainRight, Palette.C(240, 170, 90));
        Raylib.DrawTriangleLines(mainTip, mainLeft, mainRight, Color.Maroon);

        DrawMiniTriangle(mainTip, _facing);
        DrawMiniTriangle(mainLeft, Vector2.Normalize(mainLeft - Position));
        DrawMiniTriangle(mainRight, Vector2.Normalize(mainRight - Position));

        var hp = Health / MaxHealth;
        Raylib.DrawRectangle((int)Position.X - 24, (int)Position.Y - 30, 48, 5, Palette.C(20, 20, 20, 220));
        Raylib.DrawRectangle((int)Position.X - 24, (int)Position.Y - 30, (int)(48f * hp), 5, Color.Green);
    }

    private static void DrawMiniTriangle(Vector2 center, Vector2 dir)
    {
        var tip = center + dir * 6f;
        var left = center + VisibilityUtils.Rotate(dir, MathF.PI * 0.75f) * 4f;
        var right = center + VisibilityUtils.Rotate(dir, -MathF.PI * 0.75f) * 4f;
        Raylib.DrawTriangle(tip, left, right, Palette.C(220, 120, 70));
        Raylib.DrawTriangleLines(tip, left, right, Color.Brown);
    }
}
