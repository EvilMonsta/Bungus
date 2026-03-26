using System.Numerics;
using Raylib_cs;

namespace Bungus.Game;

public sealed class Projectile(Vector2 pos, Vector2 dir, float speed, float life, Color color, bool ownerEnemy, float damage, ProjectileKind kind = ProjectileKind.Bullet, float explosionRadius = 0f, float explosionDamage = 0f, float drawRadius = 4f)
{
    public Vector2 Position { get; private set; } = pos;
    public Vector2 PreviousPosition { get; private set; } = pos;
    public Color Color { get; } = color;
    public bool OwnerEnemy { get; } = ownerEnemy;
    public float Damage { get; } = damage;
    public ProjectileKind Kind { get; } = kind;
    public float ExplosionRadius { get; } = explosionRadius;
    public float ExplosionDamage { get; } = explosionDamage;
    public float DrawRadius { get; } = drawRadius;
    private float _life = life;
    public bool Alive => _life > 0f;

    public void Update(float dt)
    {
        PreviousPosition = Position;
        Position += dir * speed * dt;
        _life -= dt;
    }
}

public sealed class Explosion(Vector2 pos, float radius, Color color)
{
    public Vector2 Position { get; } = pos;
    public float Radius { get; } = radius;
    public float MaxLife { get; } = 0.24f;
    public float Life { get; set; } = 0.24f;
    public Color Color { get; } = color;
}

public sealed class SwingArc
{
    public Vector2 Origin { get; }
    public float Radius { get; }
    public float AngleStart { get; }
    public float AngleEnd { get; }
    public float Life { get; set; }
    public Color Color { get; }
    public bool IsLine { get; }
    public Vector2 LineStart { get; }
    public Vector2 LineEnd { get; }

    private SwingArc(Vector2 origin, float radius, float angleStart, float angleEnd, float life, Color color)
    {
        Origin = origin;
        Radius = radius;
        AngleStart = angleStart;
        AngleEnd = angleEnd;
        Life = life;
        Color = color;
    }

    private SwingArc(Vector2 lineStart, Vector2 lineEnd, float life, Color color)
    {
        IsLine = true;
        LineStart = lineStart;
        LineEnd = lineEnd;
        Life = life;
        Color = color;
    }

    public static SwingArc Arc(Vector2 origin, float radius, float angleStart, float angleEnd, float life, Color color)
        => new(origin, radius, angleStart, angleEnd, life, color);

    public static SwingArc Line(Vector2 lineStart, Vector2 lineEnd, float life, Color color)
        => new(lineStart, lineEnd, life, color);
}

public sealed class LootZone(int id, Rectangle rect, bool isOutpost)
{
    public int Id { get; } = id;
    public Rectangle Rect { get; } = rect;
    public bool IsOutpost { get; } = isOutpost;
    public Vector2 Center => new(Rect.X + Rect.Width / 2f, Rect.Y + Rect.Height / 2f);
}

public sealed class Obstacle(Rectangle rect)
{
    public Rectangle Rect { get; } = rect;
}

public static class MovementUtils
{
    public static Vector2 MoveWithCollisions(Vector2 position, Vector2 delta, float radius, List<Obstacle> obstacles, int worldSize)
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
            (9f, 0.5f),
            (8f, 0.45f),
            (7f, 0.35f),
            (6f, 0.25f),
            (5f, 0.15f),
            (4f, 0.05f)
        };

        foreach (var (ratio, alpha) in steps)
        {
            target.Add(new DashAfterImage(endPosition - dir * (distance * (10f - ratio) / 10f), color, alpha, square));
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

    public void Draw(float time)
    {
        Raylib.DrawEllipse((int)Position.X, (int)Position.Y, 28f, 42f, Palette.C(60, 150, 255, 110));
        Raylib.DrawEllipseLines((int)Position.X, (int)Position.Y, 30f, 44f, Palette.C(120, 220, 255));

        for (var i = 0; i < 4; i++)
        {
            var speed = 0.6f + i * 0.32f;
            var angle = Seed + time * speed + i * MathF.PI * 0.5f;
            var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (8f + i * 3f);
            var size = 8f - i;
            Raylib.DrawPoly(Position + offset, 4, size, time * 100f * speed, Palette.C(150 - i * 12, 220 - i * 10, 255));
        }
    }
}
