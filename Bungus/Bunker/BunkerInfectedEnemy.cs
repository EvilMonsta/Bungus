using System.Numerics;
using Raylib_cs;

namespace Bungus.Game;

public sealed class BunkerInfectedEnemy(int roomId, Rectangle room, Vector2 position)
{
    public const float Radius = 15f;
    public int RoomId { get; } = roomId;
    public Vector2 Position { get; private set; } = position;
    public float Health { get; private set; } = 250f;
    public bool Alive => Health > 0f;
    public bool KillAwarded { get; set; }
    public float AttackVisualTimer { get; private set; }
    private readonly BunkerNavigator _navigator = new(Radius);
    private Vector2 _attackDirection = new(1f, 0f);
    private float _attackCooldown;
    private float _trailTimer;
    private float _freezeChillTimer;
    private float _stickySlowTimer;
    private float _freezeChillSpeedMultiplier = 0.75f;
    private float _stickySlowSpeedMultiplier = 0.7f;
    private float _poisonTimer;
    private float _poisonDamagePerSecond;
    private readonly BunkerAwareness _awareness = new(room);

    public void Update(float dt, Player player, List<Obstacle> obstacles, List<BunkerInfectedCloud> clouds)
    {
        if (!Alive) return;
        TickStatusEffects(dt);
        if (!Alive) return;
        _freezeChillTimer = MathF.Max(0f, _freezeChillTimer - dt);
        _stickySlowTimer = MathF.Max(0f, _stickySlowTimer - dt);
        var moveMultiplier = (_freezeChillTimer > 0f ? _freezeChillSpeedMultiplier : 1f) * (_stickySlowTimer > 0f ? _stickySlowSpeedMultiplier : 1f);
        _awareness.Update(Position, player.Position, obstacles, dt);
        if (!_awareness.Aggroed)
        {
            var before = Position;
            Position = _navigator.Move(Position, _awareness.GetPatrolTarget(Position), 78f * moveMultiplier, dt, obstacles);
            _awareness.ObserveMovement(before, Position);
            return;
        }

        _attackCooldown = MathF.Max(0f, _attackCooldown - dt);
        _trailTimer -= dt;
        AttackVisualTimer = MathF.Max(0f, AttackVisualTimer - dt);
        var distance = Vector2.Distance(Position, player.Position);
        var previous = Position;
        if (distance > 40f) Position = _navigator.Move(Position, player.Position, 310f * moveMultiplier, dt, obstacles);

        if (Vector2.DistanceSquared(previous, Position) > 1f && _trailTimer <= 0f)
        {
            clouds.Add(new BunkerInfectedCloud(Position, 50f, 3f));
            _trailTimer = 0.1f;
        }

        if (distance <= 55f && _attackCooldown <= 0f)
        {
            var direction = player.Position - Position;
            if (direction.LengthSquared() > 0.001f) _attackDirection = Vector2.Normalize(direction);
            player.TakeDamage(20f);
            _attackCooldown = 0.75f;
            AttackVisualTimer = 0.18f;
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
    public void ForceAggro(Vector2 playerPosition) => _awareness.ForceAggro(Position, playerPosition);
    public void ResetAggro() => _awareness.ResetAggro();
    public void Draw()
    {
        Raylib.DrawCircleV(Position, Radius, Palette.C(78, 142, 54));
        Raylib.DrawCircleLines((int)Position.X, (int)Position.Y, Radius, Palette.C(145, 225, 95));
        if (AttackVisualTimer > 0f)
        {
            var angle = MathF.Atan2(_attackDirection.Y, _attackDirection.X) * 180f / MathF.PI;
            Raylib.DrawCircleSectorLines(Position, 34f, angle - 55f, angle + 55f, 12, Palette.C(180, 245, 105));
        }
        BunkerSiegeEnemy.DrawHealthBar(Position, Health / 250f, 32f, 23f, Palette.C(105, 205, 72));
    }

    private void TickStatusEffects(float dt)
    {
        if (_poisonTimer <= 0f) return;
        Health = MathF.Max(0f, Health - _poisonDamagePerSecond * dt);
        _poisonTimer = MathF.Max(0f, _poisonTimer - dt);
        if (_poisonTimer <= 0f) _poisonDamagePerSecond = 0f;
    }
}
