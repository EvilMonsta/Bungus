using System.Numerics;
using System.Runtime.CompilerServices;
using Raylib_cs;

namespace Bungus.Game;

public sealed class Projectile(Vector2 pos, Vector2 dir, float speed, float life, Color color, bool ownerEnemy, float damage, ProjectileKind kind = ProjectileKind.Bullet, float explosionRadius = 0f, float explosionDamage = 0f, float drawRadius = 4f, bool highlighted = false, Vector2? sourcePosition = null, float poisonDamagePerSecond = 0f, float poisonDuration = 0f, float playerPoisonDuration = 0f, int ricochetRemaining = 0, object? ignoreTarget = null)
{
    public Vector2 Position { get; private set; } = pos;
    public Vector2 PreviousPosition { get; private set; } = pos;
    public Vector2 SourcePosition { get; } = sourcePosition ?? pos;
    public Vector2 Direction { get; } = dir;
    public Color Color { get; } = color;
    public bool OwnerEnemy { get; } = ownerEnemy;
    public float Damage { get; } = damage;
    public ProjectileKind Kind { get; } = kind;
    public float ExplosionRadius { get; } = explosionRadius;
    public float ExplosionDamage { get; } = explosionDamage;
    public float DrawRadius { get; } = drawRadius;
    public bool Highlighted { get; } = highlighted;
    public float PoisonDamagePerSecond { get; } = poisonDamagePerSecond;
    public float PoisonDuration { get; } = poisonDuration;
    public float PlayerPoisonDuration { get; } = playerPoisonDuration;
    public int RicochetRemaining { get; } = ricochetRemaining;
    public object? IgnoreTarget { get; } = ignoreTarget;
    private float _life = life;
    public bool Alive => _life > 0f;

    public void Update(float dt)
    {
        PreviousPosition = Position;
        Position += Direction * speed * dt;
        _life -= dt;
    }
}

public sealed class Explosion(Vector2 pos, float radius, Color color, bool filled = false, bool outlined = true, float fillAlpha = 0.22f)
{
    public Vector2 Position { get; } = pos;
    public float Radius { get; } = radius;
    public float MaxLife { get; } = 0.24f;
    public float Life { get; set; } = 0.24f;
    public Color Color { get; } = color;
    public bool Filled { get; } = filled;
    public bool Outlined { get; } = outlined;
    public float FillAlpha { get; } = fillAlpha;
}

public sealed class BeamEffect(Vector2 start, Vector2 end, Color color, float life, float thickness, bool flowing)
{
    public Vector2 Start { get; } = start;
    public Vector2 End { get; } = end;
    public Color Color { get; } = color;
    public float MaxLife { get; } = life;
    public float Life { get; set; } = life;
    public float Thickness { get; } = thickness;
    public bool Flowing { get; } = flowing;

    public void Draw()
    {
        var ratio = MaxLife <= 0f ? 0f : Math.Clamp(Life / MaxLife, 0f, 1f);
        if (ratio <= 0f) return;

        var main = new Color(Color.R, Color.G, Color.B, (byte)(210 * ratio));
        Raylib.DrawLineEx(Start, End, Thickness, main);

        if (!Flowing) return;

        var dir = End - Start;
        if (dir.LengthSquared() <= 0.001f) return;

        var normal = Vector2.Normalize(new Vector2(-dir.Y, dir.X));
        var pulse = (float)Raylib.GetTime() * 18f;
        for (var i = 0; i < 3; i++)
        {
            var offset = normal * MathF.Sin(pulse + i * 1.7f) * (2f + i);
            var c = new Color((byte)Math.Min(255, Color.R + 35), (byte)Math.Min(255, Color.G + 35), (byte)Math.Min(255, Color.B + 35), (byte)(90 * ratio));
            Raylib.DrawLineEx(Start + offset, End + offset, MathF.Max(1f, Thickness * 0.35f), c);
        }
    }
}

public sealed class LightningEffect(Vector2 start, Vector2 end, float life = 0.18f)
{
    private readonly Vector2[] _points = BuildPoints(start, end);
    public float MaxLife { get; } = life;
    public float Life { get; set; } = life;
    public bool Alive => Life > 0f;

    public void Draw()
    {
        var ratio = MaxLife <= 0f ? 0f : Math.Clamp(Life / MaxLife, 0f, 1f);
        if (ratio <= 0f) return;

        for (var line = 0; line < 5; line++)
        {
            var alpha = (0.22f + line * 0.11f) * ratio;
            var color = new Color((byte)145, (byte)235, (byte)255, (byte)(255 * alpha));
            var thickness = line == 4 ? 2.6f : 1.2f;
            for (var i = 1; i < _points.Length; i++)
            {
                var wobble = new Vector2(MathF.Sin(i * 1.7f + line) * line, MathF.Cos(i * 1.3f + line) * line);
                Raylib.DrawLineEx(_points[i - 1] + wobble, _points[i] + wobble, thickness, color);
            }
        }
    }

    private static Vector2[] BuildPoints(Vector2 start, Vector2 end)
    {
        const int segments = 7;
        var points = new Vector2[segments + 1];
        var delta = end - start;
        var normal = delta.LengthSquared() <= 0.001f ? new Vector2(0f, 1f) : Vector2.Normalize(new Vector2(-delta.Y, delta.X));
        for (var i = 0; i <= segments; i++)
        {
            var t = i / (float)segments;
            var zig = i is 0 or segments ? 0f : (i % 2 == 0 ? -1f : 1f) * (8f + Random.Shared.NextSingle() * 10f);
            points[i] = Vector2.Lerp(start, end, t) + normal * zig;
        }

        return points;
    }
}

public sealed class FreezeZone(Vector2 position)
{
    public const float Radius = 110f;
    public const float FreezeDuration = 5f;
    public const float FadeDuration = 1f;
    public const float ChillDuration = 10f;
    public Vector2 Position { get; } = position;
    public float Life { get; private set; } = FreezeDuration + FadeDuration;
    public bool Freezing => Life > FadeDuration;
    public bool Alive => Life > 0f;
    public float Alpha => Math.Clamp(Life / FadeDuration, 0f, 1f);

    public void Update(float dt) => Life -= dt;
    public bool Contains(Vector2 point, float radius = 0f) => Vector2.Distance(point, Position) <= Radius + radius;
}

public sealed class MidaMiniTurret(Vector2 position)
{
    public const float Range = 500f;
    public const float Lifetime = 15f;
    public const float Damage = 10f;
    public const float FireRate = 4f;
    private float _shotTimer;
    public Vector2 Position { get; } = position;
    public float Life { get; private set; } = Lifetime;
    public bool Alive => Life > 0f;
    public float LifeRatio => Math.Clamp(Life / Lifetime, 0f, 1f);

    public void Update(float dt)
    {
        Life -= dt;
        _shotTimer -= dt;
    }

    public bool ReadyToShoot => _shotTimer <= 0f;
    public void MarkShot() => _shotTimer = 1f / FireRate;
}

public sealed class SwingArc
{
    private readonly List<object> _hitTargets = [];
    private readonly Vector2 _originOffset;
    private readonly Vector2 _lineStartOffset;
    private readonly Vector2 _lineEndOffset;

    public Vector2 Origin { get; private set; }
    public float Radius { get; }
    public float AngleStart { get; }
    public float AngleEnd { get; }
    public float Life { get; set; }
    public float MaxLife { get; }
    public Color Color { get; }
    public bool IsLine { get; }
    public Vector2 LineStart { get; private set; }
    public Vector2 LineEnd { get; private set; }
    public bool ReverseSweep { get; }
    public float DashLengthRatio { get; }
    public SwingVisualStyle VisualStyle { get; }
    public float Progress => MaxLife <= 0f ? 1f : 1f - Math.Clamp(Life / MaxLife, 0f, 1f);

    private SwingArc(Vector2 anchorPosition, Vector2 origin, float radius, float angleStart, float angleEnd, float life, Color color, bool reverseSweep)
    {
        Origin = origin;
        _originOffset = origin - anchorPosition;
        Radius = radius;
        AngleStart = angleStart;
        AngleEnd = angleEnd;
        Life = life;
        MaxLife = life;
        Color = color;
        ReverseSweep = reverseSweep;
        VisualStyle = SwingVisualStyle.ArcSlash;
    }

    private SwingArc(Vector2 anchorPosition, Vector2 lineStart, Vector2 lineEnd, float life, Color color, float dashLengthRatio)
    {
        IsLine = true;
        LineStart = lineStart;
        LineEnd = lineEnd;
        _lineStartOffset = lineStart - anchorPosition;
        _lineEndOffset = lineEnd - anchorPosition;
        Life = life;
        MaxLife = life;
        Color = color;
        DashLengthRatio = dashLengthRatio;
        VisualStyle = SwingVisualStyle.SpearThrust;
    }

    public static SwingArc Arc(Vector2 anchorPosition, Vector2 origin, float radius, float angleStart, float angleEnd, float life, Color color, bool reverseSweep = false)
        => new(anchorPosition, origin, radius, angleStart, angleEnd, life, color, reverseSweep);

    public static SwingArc Line(Vector2 anchorPosition, Vector2 lineStart, Vector2 lineEnd, float life, Color color, float dashLengthRatio = 0.4f)
        => new(anchorPosition, lineStart, lineEnd, life, color, dashLengthRatio);

    public void UpdateAnchor(Vector2 anchorPosition)
    {
        if (IsLine)
        {
            LineStart = anchorPosition + _lineStartOffset;
            LineEnd = anchorPosition + _lineEndOffset;
            return;
        }

        Origin = anchorPosition + _originOffset;
    }

    public bool TryRegisterHit(object target)
    {
        if (_hitTargets.Contains(target)) return false;
        _hitTargets.Add(target);
        return true;
    }
}

public sealed class LootZone
{
    public LootZone(int id, Rectangle rect, bool isOutpost)
        : this(id, rect, isOutpost ? LootZoneKind.Outpost : LootZoneKind.City)
    {
    }

    public LootZone(int id, Rectangle rect, LootZoneKind kind)
    {
        Id = id;
        Rect = rect;
        Kind = kind;
    }

    public int Id { get; }
    public Rectangle Rect { get; }
    public LootZoneKind Kind { get; }
    public bool IsOutpost => Kind == LootZoneKind.Outpost;
    public Vector2 Center => new(Rect.X + Rect.Width / 2f, Rect.Y + Rect.Height / 2f);
}

public sealed class Obstacle(Rectangle rect)
{
    public Rectangle Rect { get; } = rect;
}

public sealed class GeneratorNode(Vector2 position, int zoneId)
{
    public Vector2 Position { get; } = position;
    public int ZoneId { get; } = zoneId;
    public float MaxHealth { get; } = 500f;
    public float Health { get; private set; } = 500f;
    public bool GuardDefeated { get; set; }
    public bool Destroyed => Health <= 0f;
    public bool Vulnerable => GuardDefeated && !Destroyed;

    public void Damage(float amount)
    {
        if (!Vulnerable || amount <= 0f) return;
        Health = MathF.Max(0f, Health - amount);
    }
}

public sealed class ToxicPool(Vector2 position, float radiusX, float radiusY)
{
    public Vector2 Position { get; } = position;
    public float RadiusX { get; } = radiusX;
    public float RadiusY { get; } = radiusY;

    public bool Contains(Vector2 point)
    {
        var dx = (point.X - Position.X) / MathF.Max(RadiusX, 0.001f);
        var dy = (point.Y - Position.Y) / MathF.Max(RadiusY, 0.001f);
        return dx * dx + dy * dy <= 1f;
    }
}

public sealed class ProtectiveDome(Vector2 position)
{
    public const float Radius = 80f;
    public const float MaxHealth = 300f;
    private const float DecayTickInterval = 1f;
    private const float DecayPercentPerTick = 0.0333f;

    private readonly Dictionary<int, float> _contactCooldowns = [];
    private float _decayTimer;

    public Vector2 Position { get; } = position;
    public float Health { get; private set; } = MaxHealth;
    public bool Alive => Health > 0f;

    public void Update(float dt)
    {
        _decayTimer += dt;
        while (_decayTimer >= DecayTickInterval && Alive)
        {
            _decayTimer -= DecayTickInterval;
            Damage(MaxHealth * DecayPercentPerTick);
        }

        if (_contactCooldowns.Count == 0) return;

        var keys = _contactCooldowns.Keys.ToArray();
        foreach (var key in keys)
        {
            var value = _contactCooldowns[key] - dt;
            if (value <= 0f) _contactCooldowns.Remove(key);
            else _contactCooldowns[key] = value;
        }
    }

    public void Damage(float amount)
    {
        if (amount <= 0f || !Alive) return;
        Health = MathF.Max(0f, Health - amount);
    }

    public bool TryApplyContactDamage(object source, float amount, float cooldown)
    {
        if (!Alive) return false;

        var key = RuntimeHelpers.GetHashCode(source);
        if (_contactCooldowns.TryGetValue(key, out var timeLeft) && timeLeft > 0f) return false;

        Damage(amount);
        _contactCooldowns[key] = cooldown;
        return true;
    }
}

public static class MovementUtils
{
    public static Vector2 MoveWithCollisions(Vector2 position, Vector2 delta, float radius, List<Obstacle> obstacles, int worldSize)
    {
        var steps = Math.Max(1, (int)MathF.Ceiling(delta.Length() / MathF.Max(4f, radius * 0.5f)));
        if (steps > 1)
        {
            var nextStepped = position;
            var step = delta / steps;
            for (var i = 0; i < steps; i++)
            {
                var moved = MoveWithCollisionsSingleStep(nextStepped, step, radius, obstacles, worldSize);
                if (Vector2.DistanceSquared(moved, nextStepped) < 0.0001f) break;
                nextStepped = moved;
            }

            return nextStepped;
        }

        return MoveWithCollisionsSingleStep(position, delta, radius, obstacles, worldSize);
    }

    private static Vector2 MoveWithCollisionsSingleStep(Vector2 position, Vector2 delta, float radius, List<Obstacle> obstacles, int worldSize)
    {
        var next = position;
        var xTry = new Vector2(position.X + delta.X, position.Y);
        if (!CircleHitsObstacle(xTry, radius, obstacles)) next.X = xTry.X;

        var yTry = new Vector2(next.X, position.Y + delta.Y);
        if (!CircleHitsObstacle(yTry, radius, obstacles)) next.Y = yTry.Y;

        next.X = Math.Clamp(next.X, radius, worldSize - radius);
        next.Y = Math.Clamp(next.Y, radius, worldSize - radius);
        return next;
    }

    public static Vector2 MoveWithCollisions(Vector2 position, Vector2 delta, float radius, List<Obstacle> obstacles, List<ProtectiveDome> domes, int worldSize)
    {
        var steps = Math.Max(1, (int)MathF.Ceiling(delta.Length() / MathF.Max(4f, radius * 0.5f)));
        if (steps > 1)
        {
            var nextStepped = position;
            var step = delta / steps;
            for (var i = 0; i < steps; i++)
            {
                var moved = MoveWithCollisionsSingleStep(nextStepped, step, radius, obstacles, domes, worldSize);
                if (Vector2.DistanceSquared(moved, nextStepped) < 0.0001f) break;
                nextStepped = moved;
            }

            return nextStepped;
        }

        return MoveWithCollisionsSingleStep(position, delta, radius, obstacles, domes, worldSize);
    }

    private static Vector2 MoveWithCollisionsSingleStep(Vector2 position, Vector2 delta, float radius, List<Obstacle> obstacles, List<ProtectiveDome> domes, int worldSize)
    {
        var next = position;
        var xTry = new Vector2(position.X + delta.X, position.Y);
        if (!CircleHitsObstacle(xTry, radius, obstacles, domes)) next.X = xTry.X;

        var yTry = new Vector2(next.X, position.Y + delta.Y);
        if (!CircleHitsObstacle(yTry, radius, obstacles, domes)) next.Y = yTry.Y;

        next.X = Math.Clamp(next.X, radius, worldSize - radius);
        next.Y = Math.Clamp(next.Y, radius, worldSize - radius);
        return next;
    }

    public static bool CircleHitsObstacle(Vector2 center, float radius, List<Obstacle> obstacles)
    {
        foreach (var o in obstacles)
        {
            var nx = Math.Clamp(center.X, o.Rect.X, o.Rect.X + o.Rect.Width);
            var ny = Math.Clamp(center.Y, o.Rect.Y, o.Rect.Y + o.Rect.Height);
            var dx = center.X - nx;
            var dy = center.Y - ny;
            if (dx * dx + dy * dy < radius * radius) return true;
        }

        return false;
    }

    public static bool CircleHitsObstacle(Vector2 center, float radius, List<Obstacle> obstacles, List<ProtectiveDome> domes)
    {
        if (CircleHitsObstacle(center, radius, obstacles)) return true;

        foreach (var dome in domes)
        {
            if (!dome.Alive) continue;
            var limit = radius + ProtectiveDome.Radius;
            if (Vector2.DistanceSquared(center, dome.Position) < limit * limit) return true;
        }

        return false;
    }
}

public static class PathfindingUtils
{
    private const float CellSize = 48f;
    private const int MaxVisitedNodes = 2400;

    public static bool HasClearPath(Vector2 start, Vector2 goal, float radius, List<Obstacle> obstacles, int worldSize)
    {
        var delta = goal - start;
        var distance = delta.Length();
        if (distance <= 0.01f) return true;

        var dir = delta / distance;
        var step = MathF.Max(8f, radius * 0.7f);
        for (var travelled = step; travelled < distance; travelled += step)
        {
            var point = start + dir * travelled;
            if (!IsWalkable(point, radius, obstacles, worldSize)) return false;
        }

        return IsWalkable(goal, radius, obstacles, worldSize);
    }

    public static bool TryFindPath(
        Vector2 start,
        Vector2 goal,
        float radius,
        List<Obstacle> obstacles,
        int worldSize,
        out List<Vector2> path)
    {
        path = [];
        if (!TryFindNearestWalkable(start, radius, obstacles, worldSize, out var safeStart)) return false;
        if (!TryFindNearestWalkable(goal, radius, obstacles, worldSize, out var safeGoal)) return false;

        if (HasClearPath(safeStart, safeGoal, radius, obstacles, worldSize))
        {
            path.Add(safeGoal);
            return true;
        }

        var cols = Math.Max(1, (int)MathF.Ceiling(worldSize / CellSize));
        var rows = cols;
        if (!TryFindNearestWalkableCell(safeStart, radius, obstacles, worldSize, cols, rows, out var startCell)) return false;
        if (!TryFindNearestWalkableCell(safeGoal, radius, obstacles, worldSize, cols, rows, out var goalCell)) return false;
        var open = new PriorityQueue<(int X, int Y), float>();
        var cameFrom = new Dictionary<(int X, int Y), (int X, int Y)>();
        var gScore = new Dictionary<(int X, int Y), float> { [startCell] = 0f };
        var closed = new HashSet<(int X, int Y)>();

        open.Enqueue(startCell, Heuristic(startCell, goalCell));

        while (open.Count > 0 && closed.Count < MaxVisitedNodes)
        {
            var current = open.Dequeue();
            if (!closed.Add(current)) continue;

            if (current == goalCell)
            {
                path = BuildPath(cameFrom, current, safeStart, safeGoal, radius, obstacles, worldSize);
                return path.Count > 0;
            }

            foreach (var neighbor in EnumerateNeighbors(current, cols, rows))
            {
                if (closed.Contains(neighbor)) continue;

                var neighborPoint = ToWorld(neighbor);
                if (!IsWalkable(neighborPoint, radius, obstacles, worldSize)) continue;

                var diagonal = neighbor.X != current.X && neighbor.Y != current.Y;
                if (diagonal)
                {
                    var sideA = (neighbor.X, current.Y);
                    var sideB = (current.X, neighbor.Y);
                    if (!IsWalkable(ToWorld(sideA), radius, obstacles, worldSize)
                        || !IsWalkable(ToWorld(sideB), radius, obstacles, worldSize))
                    {
                        continue;
                    }
                }

                var tentative = gScore[current] + (diagonal ? 1.4142f : 1f);
                if (gScore.TryGetValue(neighbor, out var oldScore) && tentative >= oldScore) continue;

                cameFrom[neighbor] = current;
                gScore[neighbor] = tentative;
                open.Enqueue(neighbor, tentative + Heuristic(neighbor, goalCell));
            }
        }

        return false;
    }

    public static bool TryFindNearestWalkable(Vector2 point, float radius, List<Obstacle> obstacles, int worldSize, out Vector2 walkable)
    {
        point = new Vector2(
            Math.Clamp(point.X, radius, worldSize - radius),
            Math.Clamp(point.Y, radius, worldSize - radius));

        if (IsWalkable(point, radius, obstacles, worldSize))
        {
            walkable = point;
            return true;
        }

        for (var ring = 1; ring <= 8; ring++)
        {
            var offset = ring * CellSize;
            for (var y = -ring; y <= ring; y++)
            {
                for (var x = -ring; x <= ring; x++)
                {
                    if (Math.Abs(x) != ring && Math.Abs(y) != ring) continue;
                    var candidate = point + new Vector2(x * CellSize, y * CellSize);
                    candidate = new Vector2(
                        Math.Clamp(candidate.X, radius, worldSize - radius),
                        Math.Clamp(candidate.Y, radius, worldSize - radius));
                    if (!IsWalkable(candidate, radius, obstacles, worldSize)) continue;

                    walkable = candidate;
                    return true;
                }
            }
        }

        walkable = point;
        return false;
    }

    private static bool IsWalkable(Vector2 point, float radius, List<Obstacle> obstacles, int worldSize)
        => point.X >= radius
           && point.Y >= radius
           && point.X <= worldSize - radius
           && point.Y <= worldSize - radius
           && !MovementUtils.CircleHitsObstacle(point, radius, obstacles);

    private static (int X, int Y) ToCell(Vector2 point, int cols, int rows)
        => ((int)Math.Clamp(point.X / CellSize, 0, cols - 1), (int)Math.Clamp(point.Y / CellSize, 0, rows - 1));

    private static Vector2 ToWorld((int X, int Y) cell)
        => new(cell.X * CellSize + CellSize * 0.5f, cell.Y * CellSize + CellSize * 0.5f);

    private static bool TryFindNearestWalkableCell(
        Vector2 point,
        float radius,
        List<Obstacle> obstacles,
        int worldSize,
        int cols,
        int rows,
        out (int X, int Y) cell)
    {
        var origin = ToCell(point, cols, rows);
        if (IsWalkable(ToWorld(origin), radius, obstacles, worldSize))
        {
            cell = origin;
            return true;
        }

        for (var ring = 1; ring <= 8; ring++)
        {
            for (var y = -ring; y <= ring; y++)
            {
                for (var x = -ring; x <= ring; x++)
                {
                    if (Math.Abs(x) != ring && Math.Abs(y) != ring) continue;

                    var candidate = (X: origin.X + x, Y: origin.Y + y);
                    if (candidate.X < 0 || candidate.Y < 0 || candidate.X >= cols || candidate.Y >= rows) continue;
                    if (!IsWalkable(ToWorld(candidate), radius, obstacles, worldSize)) continue;

                    cell = candidate;
                    return true;
                }
            }
        }

        cell = origin;
        return false;
    }

    private static float Heuristic((int X, int Y) a, (int X, int Y) b)
    {
        var dx = Math.Abs(a.X - b.X);
        var dy = Math.Abs(a.Y - b.Y);
        return MathF.Max(dx, dy) + (1.4142f - 1f) * MathF.Min(dx, dy);
    }

    private static IEnumerable<(int X, int Y)> EnumerateNeighbors((int X, int Y) cell, int cols, int rows)
    {
        for (var y = -1; y <= 1; y++)
        {
            for (var x = -1; x <= 1; x++)
            {
                if (x == 0 && y == 0) continue;
                var nx = cell.X + x;
                var ny = cell.Y + y;
                if (nx < 0 || ny < 0 || nx >= cols || ny >= rows) continue;
                yield return (nx, ny);
            }
        }
    }

    private static List<Vector2> BuildPath(
        Dictionary<(int X, int Y), (int X, int Y)> cameFrom,
        (int X, int Y) current,
        Vector2 safeStart,
        Vector2 safeGoal,
        float radius,
        List<Obstacle> obstacles,
        int worldSize)
    {
        var cells = new List<(int X, int Y)> { current };
        while (cameFrom.TryGetValue(current, out var previous))
        {
            current = previous;
            cells.Add(current);
        }

        cells.Reverse();

        var raw = cells.Select(ToWorld).ToList();
        if (raw.Count > 0) raw.RemoveAt(0);
        raw.Add(safeGoal);

        var simplified = new List<Vector2>();
        var anchor = safeStart;
        var i = 0;
        while (i < raw.Count)
        {
            var best = i;
            for (var j = raw.Count - 1; j > i; j--)
            {
                if (!HasClearPath(anchor, raw[j], radius, obstacles, worldSize)) continue;
                best = j;
                break;
            }

            var point = raw[best];
            simplified.Add(point);
            anchor = point;
            i = best + 1;
        }

        return simplified;
    }
}

public sealed class DashAfterImage(Vector2 position, Color color, float alpha, bool square)
{
    public Vector2 Position { get; } = position;
    public Color Color { get; } = color;
    public float InitialAlpha { get; } = alpha;
    public float Life { get; set; } = 1f;
    public bool Square { get; } = square;

    public void Draw()
    {
        var current = MathF.Max(0f, InitialAlpha * (Life / 1f));
        var c = new Color(Color.R, Color.G, Color.B, (byte)(255 * current));
        if (Square)
            Raylib.DrawRectangle((int)Position.X - 21, (int)Position.Y - 21, 42, 42, c);
        else
            Raylib.DrawCircleV(Position, 16f, c);
    }

    public static void Spawn(List<DashAfterImage> target, Vector2 endPosition, Vector2 dashDir, float distance, Color color, bool square)
    {
        var dir = dashDir == Vector2.Zero ? new Vector2(1f, 0f) : Vector2.Normalize(dashDir);
        var steps = new[]
        {
            (10.0f, 0.66f),
            (9.97f, 0.62f),
            (9.92f, 0.58f),
            (9.85f, 0.54f),
            (9.6f, 0.48f),
            (9.25f, 0.42f),
            (8.8f, 0.34f),
            (8.1f, 0.26f),
            (7.2f, 0.18f),
            (6.1f, 0.10f),
            (5.0f, 0.06f)
        };

        foreach (var (ratio, alpha) in steps)
        {
            target.Add(new DashAfterImage(endPosition - dir * (distance * (10f - ratio) / 10f), color, alpha, square));
        }
    }
}

public sealed class MotionAfterImage(Vector2 position, Color color, float alpha, float radius, MotionTrailShape shape, float rotationDegrees, float minRadius = -1f)
{
    public Vector2 Position { get; } = position;
    public Color Color { get; } = color;
    public float InitialAlpha { get; } = alpha;
    public float Radius { get; } = radius;
    public float MinRadius { get; } = minRadius < 0f ? radius : minRadius;
    public MotionTrailShape Shape { get; } = shape;
    public float RotationDegrees { get; } = rotationDegrees;
    public float Life { get; set; } = 1f;

    public void Draw()
    {
        var current = MathF.Max(0f, InitialAlpha * Life);
        var currentRadius = MinRadius + (Radius - MinRadius) * Life;
        var c = new Color(Color.R, Color.G, Color.B, (byte)(255 * current));
        switch (Shape)
        {
            case MotionTrailShape.Triangle:
                Raylib.DrawPoly(Position, 3, currentRadius, RotationDegrees + 90f, c);
                break;
            case MotionTrailShape.Square:
                Raylib.DrawRectanglePro(
                    new Rectangle(Position.X, Position.Y, currentRadius * 2f, currentRadius * 2f),
                    new Vector2(currentRadius, currentRadius),
                    RotationDegrees,
                    c);
                break;
            case MotionTrailShape.Hex:
                Raylib.DrawPoly(Position, 6, currentRadius, RotationDegrees, c);
                break;
            default:
                Raylib.DrawCircleV(Position, currentRadius, c);
                break;
        }
    }
}

public static class VisibilityUtils
{
    public static bool HasLineOfSight(Vector2 from, Vector2 to, List<Obstacle> obstacles)
    {
        foreach (var obstacle in obstacles)
        {
            var r = InflateRect(obstacle.Rect, 2f);
            Vector2 hit = default;

            if (Raylib.CheckCollisionPointRec(from, r) || Raylib.CheckCollisionPointRec(to, r)) continue;
            if (Raylib.CheckCollisionLines(from, to, new Vector2(r.X, r.Y), new Vector2(r.X + r.Width, r.Y), ref hit)) return false;
            if (Raylib.CheckCollisionLines(from, to, new Vector2(r.X + r.Width, r.Y), new Vector2(r.X + r.Width, r.Y + r.Height), ref hit)) return false;
            if (Raylib.CheckCollisionLines(from, to, new Vector2(r.X + r.Width, r.Y + r.Height), new Vector2(r.X, r.Y + r.Height), ref hit)) return false;
            if (Raylib.CheckCollisionLines(from, to, new Vector2(r.X, r.Y + r.Height), new Vector2(r.X, r.Y), ref hit)) return false;
        }

        return true;
    }

    public static Vector2 Rotate(Vector2 v, float a)
    {
        var c = MathF.Cos(a);
        var s = MathF.Sin(a);
        return new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
    }

    public static void DrawDashedLine(Vector2 a, Vector2 b, int segments, Color c)
    {
        for (var i = 0; i < segments; i++)
        {
            if (i % 2 == 1) continue;
            var t1 = i / (float)segments;
            var t2 = (i + 1) / (float)segments;
            Raylib.DrawLineV(Vector2.Lerp(a, b, t1), Vector2.Lerp(a, b, t2), c);
        }
    }

    public static void DrawDashedCircle(Vector2 center, float radius, int segments, Color c)
    {
        for (var i = 0; i < segments; i++)
        {
            if (i % 2 == 1) continue;
            var a1 = i / (float)segments * MathF.Tau;
            var a2 = (i + 1) / (float)segments * MathF.Tau;
            var p1 = center + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * radius;
            var p2 = center + new Vector2(MathF.Cos(a2), MathF.Sin(a2)) * radius;
            Raylib.DrawLineV(p1, p2, c);
        }
    }

    private static Rectangle InflateRect(Rectangle rect, float pad)
        => new(rect.X - pad, rect.Y - pad, rect.Width + pad * 2f, rect.Height + pad * 2f);
}

public enum LootContainerKind { Chest, Crate }

public sealed class LootChest(Vector2 position, List<ItemStack> items, int? zoneId = null, LootContainerKind kind = LootContainerKind.Chest)
{
    public Vector2 Position { get; } = position;
    public List<ItemStack> Items { get; } = items;
    public int? ZoneId { get; } = zoneId;
    public LootContainerKind Kind { get; } = kind;
    public bool Opened { get; set; }
    public bool RequiresClear => Kind == LootContainerKind.Chest && ZoneId is not null;
}

public sealed class GroundConsumablePickup(Vector2 position, ItemStack item)
{
    public Vector2 Position { get; } = position;
    public ItemStack Item { get; } = item;
}

public sealed class ExtractPortal(Vector2 position, float seed)
{
    public Vector2 Position { get; } = position;
    public float Seed { get; } = seed;
    public float InteractionRadius { get; } = 34f;

    public void Draw(float time, bool active = true, bool emergency = false)
    {
        var fill = active
            ? emergency ? Palette.C(255, 90, 90, 110) : Palette.C(60, 150, 255, 110)
            : Palette.C(80, 40, 40, 90);
        var line = active
            ? emergency ? Palette.C(255, 160, 160) : Palette.C(120, 220, 255)
            : Palette.C(180, 90, 90);

        Raylib.DrawEllipse((int)Position.X, (int)Position.Y, 28f, 42f, fill);
        Raylib.DrawEllipseLines((int)Position.X, (int)Position.Y, 30f, 44f, line);

        for (var i = 0; i < 4; i++)
        {
            var speed = 0.6f + i * 0.32f;
            var angle = Seed + time * speed + i * MathF.PI * 0.5f;
            var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (8f + i * 3f);
            var size = 8f - i;
            var poly = active
                ? emergency ? Palette.C(255, 170 - i * 10, 170 - i * 10) : Palette.C(150 - i * 12, 220 - i * 10, 255)
                : Palette.C(165 - i * 10, 90 - i * 8, 90 - i * 8);
            Raylib.DrawPoly(Position + offset, 4, size, time * 100f * speed, poly);
        }
    }
}
