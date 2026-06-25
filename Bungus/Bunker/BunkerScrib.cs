using System.Numerics;
using Raylib_cs;

namespace Bungus.Game;

public sealed class BunkerScrib(Vector2 position, int roomId = 19, Rectangle? patrolRoom = null, bool startAggroed = true)
{
    public const float Radius = 13.2f;
    public int RoomId { get; } = roomId;
    public Vector2 Position { get; private set; } = position;
    public const float MaxHealth = 100f;
    public float Health { get; private set; } = MaxHealth;
    public bool Alive => Health > 0f && !_exploded;
    public bool Exploded => _exploded;
    public bool KillAwarded { get; set; }
    private bool _armed;
    private bool _exploded;
    private float _fuse = 0.5f;
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
    private readonly BunkerAwareness _awareness = new(patrolRoom ?? new Rectangle(1000f, 2600f, 2200f, 1400f), startAggroed);

    public bool Update(float dt, Vector2 playerPosition, List<Obstacle> obstacles)
    {
        if (!Alive) return false;
        TickStatusEffects(dt);
        if (!Alive)
        {
            _exploded = true;
            return true;
        }
        _freezeChillTimer = MathF.Max(0f, _freezeChillTimer - dt);
        _stickySlowTimer = MathF.Max(0f, _stickySlowTimer - dt);
        var moveMultiplier = (_freezeChillTimer > 0f ? _freezeChillSpeedMultiplier : 1f) * (_stickySlowTimer > 0f ? _stickySlowSpeedMultiplier : 1f);
        _awareness.Update(Position, playerPosition, obstacles, dt);
        if (!_awareness.Aggroed)
        {
            var before = Position;
            MoveNavigated(dt, _awareness.GetPatrolTarget(Position), 70f * moveMultiplier, obstacles);
            _awareness.ObserveMovement(before, Position);
            return false;
        }

        var distance = Vector2.Distance(Position, playerPosition);
        if (distance <= 30f) _armed = true;
        if (_armed)
        {
            _fuse -= dt;
            if (_fuse <= 0f)
            {
                _exploded = true;
                return true;
            }
            return false;
        }

        var speed = (distance <= 100f ? 660f : 220f) * moveMultiplier;
        MoveNavigated(dt, playerPosition, speed, obstacles);
        return false;
    }

    private void MoveNavigated(float dt, Vector2 target, float speed, List<Obstacle> obstacles)
    {
        UpdateStuckState(dt);
        _pathRefreshTimer -= dt;
        var clearPath = PathfindingUtils.HasClearPath(Position, target, Radius, obstacles, 4000);
        if (clearPath)
        {
            _path.Clear();
            _pathIndex = 0;
        }
        else if (_pathRefreshTimer <= 0f || _pathIndex >= _path.Count || _stuckTimer >= 0.25f)
        {
            _pathRefreshTimer = 0.3f;
            _stuckTimer = 0f;
            if (PathfindingUtils.TryFindPath(Position, target, Radius, obstacles, 4000, out var path, allowDirectShortcut: false))
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

        while (_pathIndex < _path.Count && Vector2.DistanceSquared(Position, _path[_pathIndex]) <= 20f * 20f) _pathIndex++;
        var waypoint = clearPath ? target : _pathIndex < _path.Count ? _path[_pathIndex] : Position;
        var direction = waypoint - Position;
        if (direction.LengthSquared() <= 0.001f) return;
        Position = MovementUtils.MoveWithCollisions(Position, Vector2.Normalize(direction) * speed * dt, Radius, obstacles, 4000);
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

    public bool Damage(float amount)
    {
        if (!Alive) return false;
        Health = MathF.Max(0f, Health - amount);
        if (Health > 0f) return false;
        _exploded = true;
        return true;
    }

    public void ForceAggro(Vector2 playerPosition) => _awareness.ForceAggro(Position, playerPosition);
    public void ResetAggro() => _awareness.ResetAggro();
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

    public void Draw()
    {
        Raylib.DrawCircleV(Position, Radius, _armed ? Palette.C(230, 50, 70) : Palette.C(116, 30, 64));
        Raylib.DrawCircleLines((int)Position.X, (int)Position.Y, Radius, Palette.C(245, 105, 120));
        DrawHealthBar(Position, Health / MaxHealth, 29f, 20f);
    }

    private static void DrawHealthBar(Vector2 position, float ratio, float width, float yOffset)
    {
        var bar = new Rectangle(position.X - width * 0.5f, position.Y - yOffset, width, 4f);
        Raylib.DrawRectangleRec(bar, Palette.C(24, 16, 22, 230));
        Raylib.DrawRectangle((int)bar.X, (int)bar.Y, (int)(bar.Width * Math.Clamp(ratio, 0f, 1f)), (int)bar.Height, Palette.C(210, 54, 78));
    }

    private void TickStatusEffects(float dt)
    {
        if (_poisonTimer <= 0f) return;
        Health = MathF.Max(0f, Health - _poisonDamagePerSecond * dt);
        _poisonTimer = MathF.Max(0f, _poisonTimer - dt);
        if (_poisonTimer <= 0f) _poisonDamagePerSecond = 0f;
    }
}
