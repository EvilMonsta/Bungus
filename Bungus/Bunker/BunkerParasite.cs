using System.Numerics;
using Raylib_cs;

namespace Bungus.Game;

public sealed class BunkerParasite(Vector2 position)
{
    public Vector2 Position { get; private set; } = position;
    public const float MaxHealth = 25f;
    public float Health { get; private set; } = MaxHealth;
    public bool Alive => Health > 0f;
    private List<Vector2> _path = [];
    private int _pathIndex;
    private float _pathRefreshTimer;
    private Vector2 _lastNavigationPosition;
    private float _stuckTimer;
    private float _freezeChillTimer;
    private float _stickySlowTimer;
    private float _freezeChillSpeedMultiplier = 0.75f;
    private float _stickySlowSpeedMultiplier = 0.7f;
    private float _poisonTimer;
    private float _poisonDamagePerSecond;

    public bool Update(float dt, Vector2 bossPosition, List<Obstacle> obstacles)
    {
        if (!Alive) return false;
        TickStatusEffects(dt);
        if (!Alive) return false;
        _freezeChillTimer = MathF.Max(0f, _freezeChillTimer - dt);
        _stickySlowTimer = MathF.Max(0f, _stickySlowTimer - dt);
        var toBoss = bossPosition - Position;
        if (toBoss.Length() <= 15f) return true;
        MoveNavigated(dt, bossPosition, obstacles);
        return Vector2.Distance(Position, bossPosition) <= 15f;
    }

    private void MoveNavigated(float dt, Vector2 target, List<Obstacle> obstacles)
    {
        UpdateStuckState(dt);
        _pathRefreshTimer -= dt;
        var clearPath = PathfindingUtils.HasClearPath(Position, target, 8f, obstacles, 4000);
        if (clearPath)
        {
            _path.Clear();
            _pathIndex = 0;
        }
        else if (_pathRefreshTimer <= 0f || _pathIndex >= _path.Count || _stuckTimer >= 0.25f)
        {
            _pathRefreshTimer = 0.4f;
            _stuckTimer = 0f;
            if (PathfindingUtils.TryFindPath(Position, target, 8f, obstacles, 4000, out var path, allowDirectShortcut: false))
            {
                _path = path;
                _pathIndex = 0;
            }
            else
            {
                _path.Clear();
                _pathIndex = 0;
                return;
            }
        }

        while (_pathIndex < _path.Count && Vector2.DistanceSquared(Position, _path[_pathIndex]) <= 14f * 14f) _pathIndex++;
        var waypoint = clearPath ? target : _pathIndex < _path.Count ? _path[_pathIndex] : Position;
        var direction = waypoint - Position;
        if (direction.LengthSquared() <= 0.001f) return;
        var moveMultiplier = (_freezeChillTimer > 0f ? _freezeChillSpeedMultiplier : 1f) * (_stickySlowTimer > 0f ? _stickySlowSpeedMultiplier : 1f);
        Position = MovementUtils.MoveWithCollisions(Position, Vector2.Normalize(direction) * 156f * moveMultiplier * dt, 8f, obstacles, 4000);
    }

    private void UpdateStuckState(float dt)
    {
        if (Vector2.DistanceSquared(Position, _lastNavigationPosition) < 2f * 2f) _stuckTimer += dt;
        else
        {
            _lastNavigationPosition = Position;
            _stuckTimer = 0f;
        }
    }

    public void Damage(float amount) => Health = MathF.Max(0f, Health - amount);
    public void ApplyFreezeChill(float duration, float strengthMultiplier = 1f)
    {
        var multiplier = MathF.Max(0f, 1f - 0.25f * strengthMultiplier);
        _freezeChillSpeedMultiplier = _freezeChillTimer > 0f ? MathF.Min(_freezeChillSpeedMultiplier, multiplier) : multiplier;
        _freezeChillTimer = MathF.Max(_freezeChillTimer, duration);
    }

    public void ApplyStickySlow(float duration, float strengthMultiplier = 1f)
    {
        var multiplier = MathF.Max(0f, 1f - 0.3f * strengthMultiplier);
        _stickySlowSpeedMultiplier = _stickySlowTimer > 0f ? MathF.Min(_stickySlowSpeedMultiplier, multiplier) : multiplier;
        _stickySlowTimer = MathF.Max(_stickySlowTimer, duration);
    }
    public void ApplyPoison(float damagePerSecond, float duration)
    {
        _poisonDamagePerSecond = MathF.Max(_poisonDamagePerSecond, damagePerSecond);
        _poisonTimer = MathF.Max(_poisonTimer, duration);
    }

    private void TickStatusEffects(float dt)
    {
        if (_poisonTimer <= 0f) return;
        Health = MathF.Max(0f, Health - _poisonDamagePerSecond * dt);
        _poisonTimer = MathF.Max(0f, _poisonTimer - dt);
        if (_poisonTimer <= 0f) _poisonDamagePerSecond = 0f;
    }

    public void Draw()
    {
        Raylib.DrawCircleV(Position, 8f, Palette.C(150, 45, 105));
        Raylib.DrawCircleLines((int)Position.X, (int)Position.Y, 8f, Palette.C(235, 95, 170));
        var bar = new Rectangle(Position.X - 11f, Position.Y - 14f, 22f, 4f);
        Raylib.DrawRectangleRec(bar, Palette.C(24, 16, 22, 230));
        Raylib.DrawRectangle((int)bar.X, (int)bar.Y, (int)(bar.Width * Math.Clamp(Health / MaxHealth, 0f, 1f)), (int)bar.Height, Palette.C(210, 54, 128));
    }
}
