using System.Numerics;
using System.Runtime.CompilerServices;
using Raylib_cs;

namespace Bungus.Game;

public sealed class Projectile(Vector2 pos, Vector2 dir, float speed, float life, Color color, bool ownerEnemy, float damage, ProjectileKind kind = ProjectileKind.Bullet, float explosionRadius = 0f, float explosionDamage = 0f, float drawRadius = 4f, bool highlighted = false, Vector2? sourcePosition = null, float poisonDamagePerSecond = 0f, float poisonDuration = 0f, float playerPoisonDuration = 0f)
{
    public Vector2 Position { get; private set; } = pos;
    public Vector2 PreviousPosition { get; private set; } = pos;
    public Vector2 SourcePosition { get; } = sourcePosition ?? pos;
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
    private float _life = life;
    public bool Alive => _life > 0f;

    public void Update(float dt)
    {
        PreviousPosition = Position;
        Position += dir * speed * dt;
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
