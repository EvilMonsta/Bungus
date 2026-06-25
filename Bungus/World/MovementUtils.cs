using System.Numerics;
using System.Runtime.CompilerServices;
using Raylib_cs;

namespace Bungus.Game;

public static class MovementUtils
{
    private static readonly ConditionalWeakTable<List<Obstacle>, ObstacleSpatialIndex> ObstacleIndexes = new();

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
        if (obstacles.Count < 24)
        {
            return CircleHitsObstacleLinear(center, radius, obstacles);
        }

        if (!ObstacleIndexes.TryGetValue(obstacles, out var index) || index.SourceCount != obstacles.Count)
        {
            ObstacleIndexes.Remove(obstacles);
            index = new ObstacleSpatialIndex(obstacles);
            ObstacleIndexes.Add(obstacles, index);
        }

        return index.CircleHitsObstacle(center, radius);
    }

    public static void WarmObstacleIndex(List<Obstacle> obstacles)
    {
        if (obstacles.Count < 24) return;
        if (ObstacleIndexes.TryGetValue(obstacles, out var index) && index.SourceCount == obstacles.Count) return;

        ObstacleIndexes.Remove(obstacles);
        ObstacleIndexes.Add(obstacles, new ObstacleSpatialIndex(obstacles));
    }

    private static bool CircleHitsObstacleLinear(Vector2 center, float radius, List<Obstacle> obstacles)
    {
        foreach (var o in obstacles)
        {
            if (CircleHitsRect(center, radius, o.Rect)) return true;
        }

        return false;
    }

    private static bool CircleHitsRect(Vector2 center, float radius, Rectangle rect)
    {
        var nx = Math.Clamp(center.X, rect.X, rect.X + rect.Width);
        var ny = Math.Clamp(center.Y, rect.Y, rect.Y + rect.Height);
        var dx = center.X - nx;
        var dy = center.Y - ny;
        return dx * dx + dy * dy < radius * radius;
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
