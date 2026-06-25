using System.Numerics;
using Raylib_cs;

namespace Bungus.Game;

public sealed class BunkerAssaultEnemy(int roomId, Rectangle room, Vector2 position)
{
    public const float Radius = 16f;
    public int RoomId { get; } = roomId;
    public Vector2 Position { get; private set; } = position;
    public Vector2 Facing => _facing;
    public float Health { get; private set; } = 250f;
    public bool Alive => Health > 0f;
    public bool KillAwarded { get; set; }
    private readonly BunkerNavigator _navigator = new(Radius);
    private float _abilityCooldown;
    private float _shotCooldown;
    private float _attackVisualTimer;
    private Vector2 _facing = new(1f, 0f);
    private float _freezeChillTimer;
    private float _stickySlowTimer;
    private float _freezeChillSpeedMultiplier = 0.75f;
    private float _stickySlowSpeedMultiplier = 0.7f;
    private float _poisonTimer;
    private float _poisonDamagePerSecond;
    private readonly BunkerAwareness _awareness = new(room);

    public void Update(float dt, Player player, List<Obstacle> obstacles, List<Projectile> projectiles)
    {
        if (!Alive) return;
        TickStatusEffects(dt);
        if (!Alive) return;
        _freezeChillTimer = MathF.Max(0f, _freezeChillTimer - dt);
        _stickySlowTimer = MathF.Max(0f, _stickySlowTimer - dt);
        var moveMultiplier = (_freezeChillTimer > 0f ? _freezeChillSpeedMultiplier : 1f) * (_stickySlowTimer > 0f ? _stickySlowSpeedMultiplier : 1f);
        _attackVisualTimer = MathF.Max(0f, _attackVisualTimer - dt);
        _awareness.Update(Position, player.Position, obstacles, dt);
        if (!_awareness.Aggroed)
        {
            var before = Position;
            Position = _navigator.Move(Position, _awareness.GetPatrolTarget(Position), 72f * moveMultiplier, dt, obstacles);
            _awareness.ObserveMovement(before, Position);
            _facing = _awareness.Facing;
            return;
        }

        _abilityCooldown = MathF.Max(0f, _abilityCooldown - dt);
        _shotCooldown -= dt;
        var toPlayer = player.Position - Position;
        var distance = toPlayer.Length();
        var direction = distance <= 0.001f ? new Vector2(1f, 0f) : toPlayer / distance;
        _facing = direction;

        if (distance <= 100f && _abilityCooldown <= 0f)
        {
            player.TakeDamage(25f);
            player.ApplyMovementSlow(5f);
            if (player.ApplyKnockback(direction, 150f, obstacles, 4000)) player.TakeDamage(20f);
            _abilityCooldown = 10f;
            _attackVisualTimer = 0.3f;
        }

        if (distance > 40f) Position = _navigator.Move(Position, player.Position, 285f * moveMultiplier, dt, obstacles);

        if (_shotCooldown <= 0f)
        {
            var aim = player.Position - Position;
            if (aim.LengthSquared() <= 0.001f) aim = new Vector2(1f, 0f);
            aim = Vector2.Normalize(aim);
            projectiles.Add(new Projectile(Position + aim * 20f, aim, 500f, 8f, Palette.C(255, 92, 70), true, 20f));
            _shotCooldown = 1f;
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
    public void ForceAggro(Vector2 playerPosition)
    {
        _awareness.ForceAggro(Position, playerPosition);
        FacePlayer(playerPosition);
    }
    public void ResetAggro() => _awareness.ResetAggro();
    public void FacePlayer(Vector2 playerPosition)
    {
        var direction = playerPosition - Position;
        if (direction.LengthSquared() > 0.001f) _facing = Vector2.Normalize(direction);
    }

    public void Draw(Texture2D? texture = null)
    {
        if (texture is { Id: not 0 } activeTexture)
        {
            const float size = 44f;
            var source = new Rectangle(0f, 0f, activeTexture.Width, activeTexture.Height);
            var destination = new Rectangle(Position.X, Position.Y, size, size);
            var rotation = MathF.Atan2(_facing.Y, _facing.X) * 180f / MathF.PI;
            Raylib.DrawTexturePro(activeTexture, source, destination, new Vector2(size * 0.5f), rotation, Color.White);
        }
        else
        {
            Raylib.DrawCircleV(Position, Radius, Palette.C(205, 62, 52));
            Raylib.DrawCircleLines((int)Position.X, (int)Position.Y, Radius, Palette.C(255, 135, 105));
        }
        if (_attackVisualTimer > 0f)
        {
            var angle = MathF.Atan2(_facing.Y, _facing.X) * 180f / MathF.PI;
            Raylib.DrawCircleSector(Position, 100f, angle - 90f, angle + 90f, 24, Palette.C(145, 105, 20, 82));
        }
        BunkerSiegeEnemy.DrawHealthBar(Position, Health / 250f, 44f, 29f, Palette.C(235, 72, 62));
    }

    private void TickStatusEffects(float dt)
    {
        if (_poisonTimer <= 0f) return;
        Health = MathF.Max(0f, Health - _poisonDamagePerSecond * dt);
        _poisonTimer = MathF.Max(0f, _poisonTimer - dt);
        if (_poisonTimer <= 0f) _poisonDamagePerSecond = 0f;
    }
}
