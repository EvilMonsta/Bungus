using System.Numerics;
using Raylib_cs;

namespace Bungus.Game;

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
