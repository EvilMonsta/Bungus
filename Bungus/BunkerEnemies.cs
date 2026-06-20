using System.Numerics;
using Raylib_cs;

namespace Bungus.Game;

internal sealed class BunkerNavigator(float radius)
{
    private List<Vector2> _path = [];
    private int _pathIndex;
    private float _refreshTimer;
    private Vector2 _lastPosition;
    private float _stuckTimer;

    public Vector2 Move(Vector2 position, Vector2 target, float speed, float dt, List<Obstacle> obstacles)
    {
        if (Vector2.DistanceSquared(position, _lastPosition) < 4f) _stuckTimer += dt;
        else
        {
            _lastPosition = position;
            _stuckTimer = 0f;
        }

        _refreshTimer -= dt;
        var clear = PathfindingUtils.HasClearPath(position, target, radius, obstacles, 4000);
        if (clear)
        {
            _path.Clear();
            _pathIndex = 0;
        }
        else if (_refreshTimer <= 0f || _pathIndex >= _path.Count || _stuckTimer >= 0.25f)
        {
            _refreshTimer = 0.35f;
            _stuckTimer = 0f;
            if (PathfindingUtils.TryFindPath(position, target, radius, obstacles, 4000, out var path, allowDirectShortcut: false))
            {
                _path = path;
                _pathIndex = 0;
            }
            else
            {
                _path.Clear();
                return position;
            }
        }

        while (_pathIndex < _path.Count && Vector2.DistanceSquared(position, _path[_pathIndex]) <= 20f * 20f) _pathIndex++;
        var waypoint = clear ? target : _pathIndex < _path.Count ? _path[_pathIndex] : position;
        var direction = waypoint - position;
        if (direction.LengthSquared() <= 0.001f) return position;
        return MovementUtils.MoveWithCollisions(position, Vector2.Normalize(direction) * speed * dt, radius, obstacles, 4000);
    }
}

internal sealed class BunkerAwareness(Rectangle room, bool aggroed = false)
{
    private const float ViewDistance = 450f;
    private const float HalfViewAngleCos = 0.5f;
    private Vector2 _patrolTarget = RandomPoint(room);
    private float _patrolWait;
    public Vector2 Facing { get; private set; } = new(1f, 0f);
    public bool Aggroed { get; private set; } = aggroed;

    public void Update(Vector2 position, Vector2 playerPosition, List<Obstacle> obstacles, float dt)
    {
        if (Aggroed)
        {
            return;
        }

        var toPlayer = playerPosition - position;
        var distance = toPlayer.Length();
        if (distance <= ViewDistance
            && distance > 0.001f
            && Vector2.Dot(Facing, toPlayer / distance) >= HalfViewAngleCos
            && VisibilityUtils.HasLineOfSight(position, playerPosition, obstacles))
        {
            Aggroed = true;
            return;
        }

        if (Vector2.Distance(position, _patrolTarget) <= 16f)
        {
            _patrolWait -= dt;
            if (_patrolWait <= 0f)
            {
                _patrolTarget = RandomPoint(room);
                _patrolWait = 0.6f + Random.Shared.NextSingle() * 1.2f;
            }
        }
    }

    public Vector2 GetPatrolTarget(Vector2 position)
        => _patrolWait > 0f ? position : _patrolTarget;

    public void ObserveMovement(Vector2 before, Vector2 after)
    {
        var movement = after - before;
        if (movement.LengthSquared() > 0.01f) Facing = Vector2.Normalize(movement);
    }

    public void ForceAggro(Vector2 position, Vector2 playerPosition)
    {
        Aggroed = true;
        var direction = playerPosition - position;
        if (direction.LengthSquared() > 0.001f) Facing = Vector2.Normalize(direction);
    }

    public void ResetAggro() => Aggroed = false;

    private static Vector2 RandomPoint(Rectangle room)
        => new(
            room.X + 34f + Random.Shared.NextSingle() * MathF.Max(1f, room.Width - 68f),
            room.Y + 34f + Random.Shared.NextSingle() * MathF.Max(1f, room.Height - 68f));
}

public sealed class BunkerSiegeEnemy(int roomId, Rectangle room, Vector2 position)
{
    public const float Radius = 16f;
    public const float CollisionHalfSize = 16f;
    public const float CollisionRadius = 22.7f;
    public int RoomId { get; } = roomId;
    public Vector2 Position { get; private set; } = position;
    public Vector2 Facing { get; private set; } = new(1f, 0f);
    public float Health { get; private set; } = 200f;
    public bool Alive => Health > 0f;
    public bool KillAwarded { get; set; }
    private readonly BunkerNavigator _navigator = new(CollisionRadius);
    private float _burstCooldown;
    private int _burstShots;
    private float _burstShotTimer;
    private float _dodgeCooldown;
    private float _strafeSign = 1f;
    private float _freezeChillTimer;
    private readonly BunkerAwareness _awareness = new(room);

    public void Update(float dt, Vector2 playerPosition, List<Obstacle> obstacles, List<Projectile> projectiles)
    {
        if (!Alive) return;
        _freezeChillTimer = MathF.Max(0f, _freezeChillTimer - dt);
        var moveMultiplier = _freezeChillTimer > 0f ? 0.75f : 1f;
        _awareness.Update(Position, playerPosition, obstacles, dt);
        if (!_awareness.Aggroed)
        {
            var before = Position;
            Position = _navigator.Move(Position, _awareness.GetPatrolTarget(Position), 27f * moveMultiplier, dt, obstacles);
            _awareness.ObserveMovement(before, Position);
            Facing = _awareness.Facing;
            return;
        }

        _dodgeCooldown = MathF.Max(0f, _dodgeCooldown - dt);
        var toPlayer = playerPosition - Position;
        var distance = toPlayer.Length();
        var direction = distance <= 0.001f ? new Vector2(1f, 0f) : toPlayer / distance;
        Facing = direction;

        var dangerousProjectile = projectiles.FirstOrDefault(projectile =>
            !projectile.OwnerEnemy
            && projectile.Alive
            && Vector2.DistanceSquared(projectile.Position, Position) <= 100f * 100f
            && Vector2.Dot(projectile.Direction, Position - projectile.Position) > 0f);
        if (dangerousProjectile is not null && _dodgeCooldown <= 0f)
        {
            var side = new Vector2(-dangerousProjectile.Direction.Y, dangerousProjectile.Direction.X);
            if (Random.Shared.Next(2) == 0) side = -side;
            Position = MovementUtils.MoveWithCollisions(Position, side * 58f, CollisionRadius, obstacles, 4000);
            _dodgeCooldown = 0.75f;
        }
        else
        {
            var desired = Position;
            if (distance > 495f) desired = playerPosition - direction * 427.5f;
            else if (distance < 345f) desired = Position - direction * 90f;
            else
            {
                var side = new Vector2(-direction.Y, direction.X) * _strafeSign;
                desired = Position + side * 100f;
                if (!PathfindingUtils.HasClearPath(Position, desired, Radius, obstacles, 4000)) _strafeSign *= -1f;
            }
            Position = _navigator.Move(Position, desired, 46f * moveMultiplier, dt, obstacles);
        }

        _burstCooldown -= dt;
        if (_burstCooldown <= 0f && _burstShots <= 0)
        {
            _burstShots = 7;
            _burstShotTimer = 0f;
            _burstCooldown = 1.5f;
        }
        _burstShotTimer -= dt;
        while (_burstShots > 0 && _burstShotTimer <= 0f)
        {
            var aim = playerPosition - Position;
            if (aim.LengthSquared() <= 0.001f) aim = new Vector2(1f, 0f);
            aim = Vector2.Normalize(aim);
            var spread = (Random.Shared.NextSingle() * 14f - 7f) * MathF.PI / 180f;
            var shot = VisibilityUtils.Rotate(aim, spread);
            projectiles.Add(new Projectile(
                Position + shot * 20f,
                shot,
                800f,
                5f,
                Palette.C(235, 185, 72),
                true,
                13f,
                playerArmorPenetration: 0.5f));
            _burstShots--;
            _burstShotTimer += 0.05f;
        }
    }

    public void Damage(float amount) => Health = MathF.Max(0f, Health - amount);
    public void ApplyFreezeChill(float duration) => _freezeChillTimer = MathF.Max(_freezeChillTimer, duration);
    public void ForceAggro(Vector2 playerPosition)
    {
        _awareness.ForceAggro(Position, playerPosition);
        FacePlayer(playerPosition);
    }
    public void ResetAggro() => _awareness.ResetAggro();
    public void FacePlayer(Vector2 playerPosition)
    {
        var direction = playerPosition - Position;
        if (direction.LengthSquared() > 0.001f) Facing = Vector2.Normalize(direction);
    }

    public bool IntersectsSegment(Vector2 from, Vector2 to, float radius)
    {
        var localFrom = ToLocal(from);
        var localTo = ToLocal(to);
        var halfSize = CollisionHalfSize + radius;
        return SegmentIntersectsBox(localFrom, localTo, halfSize);
    }

    public bool IntersectsCircle(Vector2 center, float radius)
    {
        var local = ToLocal(center);
        var nearest = new Vector2(
            Math.Clamp(local.X, -CollisionHalfSize, CollisionHalfSize),
            Math.Clamp(local.Y, -CollisionHalfSize, CollisionHalfSize));
        return Vector2.DistanceSquared(local, nearest) <= radius * radius;
    }

    public void Draw(Texture2D? texture = null)
    {
        if (texture is { Id: not 0 } activeTexture)
        {
            const float size = 44f;
            var source = new Rectangle(0f, 0f, activeTexture.Width, activeTexture.Height);
            var destination = new Rectangle(Position.X, Position.Y, size, size);
            var rotation = MathF.Atan2(Facing.Y, Facing.X) * 180f / MathF.PI;
            Raylib.DrawTexturePro(activeTexture, source, destination, new Vector2(size * 0.5f), rotation, Color.White);
        }
        else
        {
            var rotation = MathF.Atan2(Facing.Y, Facing.X) * 180f / MathF.PI + 45f;
            Raylib.DrawPoly(Position, 4, CollisionRadius, rotation, Palette.C(150, 105, 58));
            Raylib.DrawPolyLinesEx(Position, 4, CollisionRadius, rotation, 1.5f, Palette.C(245, 190, 105));
        }
        DrawHealthBar(Position, Health / 200f, 44f, 29f, Palette.C(225, 145, 65));
    }

    private Vector2 ToLocal(Vector2 point)
    {
        var relative = point - Position;
        var angle = -MathF.Atan2(Facing.Y, Facing.X);
        return VisibilityUtils.Rotate(relative, angle);
    }

    private static bool SegmentIntersectsBox(Vector2 from, Vector2 to, float halfSize)
    {
        var direction = to - from;
        var minimum = 0f;
        var maximum = 1f;

        if (!ClipAxis(from.X, direction.X, -halfSize, halfSize, ref minimum, ref maximum)) return false;
        return ClipAxis(from.Y, direction.Y, -halfSize, halfSize, ref minimum, ref maximum);
    }

    private static bool ClipAxis(float start, float direction, float minimumBound, float maximumBound, ref float minimum, ref float maximum)
    {
        if (MathF.Abs(direction) < 0.0001f) return start >= minimumBound && start <= maximumBound;
        var first = (minimumBound - start) / direction;
        var second = (maximumBound - start) / direction;
        if (first > second) (first, second) = (second, first);
        minimum = MathF.Max(minimum, first);
        maximum = MathF.Min(maximum, second);
        return minimum <= maximum;
    }

    internal static void DrawHealthBar(Vector2 position, float ratio, float width, float yOffset, Color color)
    {
        var bar = new Rectangle(position.X - width * 0.5f, position.Y - yOffset, width, 4f);
        Raylib.DrawRectangleRec(bar, Palette.C(20, 16, 20, 230));
        Raylib.DrawRectangle((int)bar.X, (int)bar.Y, (int)(bar.Width * Math.Clamp(ratio, 0f, 1f)), (int)bar.Height, color);
    }
}

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
    private readonly BunkerAwareness _awareness = new(room);

    public void Update(float dt, Player player, List<Obstacle> obstacles, List<Projectile> projectiles)
    {
        if (!Alive) return;
        _freezeChillTimer = MathF.Max(0f, _freezeChillTimer - dt);
        var moveMultiplier = _freezeChillTimer > 0f ? 0.75f : 1f;
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
    public void ApplyFreezeChill(float duration) => _freezeChillTimer = MathF.Max(_freezeChillTimer, duration);
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
}

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
    private float _attackCooldown;
    private float _trailTimer;
    private float _freezeChillTimer;
    private readonly BunkerAwareness _awareness = new(room);

    public void Update(float dt, Player player, List<Obstacle> obstacles, List<BunkerInfectedCloud> clouds)
    {
        if (!Alive) return;
        _freezeChillTimer = MathF.Max(0f, _freezeChillTimer - dt);
        var moveMultiplier = _freezeChillTimer > 0f ? 0.75f : 1f;
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
            player.TakeDamage(20f);
            _attackCooldown = 0.75f;
            AttackVisualTimer = 0.18f;
        }
    }

    public void Damage(float amount) => Health = MathF.Max(0f, Health - amount);
    public void ApplyFreezeChill(float duration) => _freezeChillTimer = MathF.Max(_freezeChillTimer, duration);
    public void ForceAggro(Vector2 playerPosition) => _awareness.ForceAggro(Position, playerPosition);
    public void ResetAggro() => _awareness.ResetAggro();
    public void Draw()
    {
        Raylib.DrawCircleV(Position, Radius, Palette.C(78, 142, 54));
        Raylib.DrawCircleLines((int)Position.X, (int)Position.Y, Radius, Palette.C(145, 225, 95));
        if (AttackVisualTimer > 0f)
        {
            Raylib.DrawCircleSectorLines(Position, 34f, -55f, 55f, 12, Palette.C(180, 245, 105));
        }
        BunkerSiegeEnemy.DrawHealthBar(Position, Health / 250f, 32f, 23f, Palette.C(105, 205, 72));
    }
}

public sealed class BunkerInfectedCloud(Vector2 position, float radius, float lifetime)
{
    public Vector2 Position { get; } = position;
    public float Radius { get; } = radius;
    public float Life { get; private set; } = lifetime;
    public bool Alive => Life > 0f;
    public void Update(float dt) => Life = MathF.Max(0f, Life - dt);
    public void Draw()
    {
        Raylib.DrawCircleV(Position, Radius, Palette.C(46, 78, 42));
    }
}
