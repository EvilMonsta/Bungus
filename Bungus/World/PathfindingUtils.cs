using System.Numerics;
using System.Runtime.CompilerServices;
using Raylib_cs;

namespace Bungus.Game;

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
        var clearanceRadius = radius + 2f;
        for (var travelled = step; travelled < distance; travelled += step)
        {
            var point = start + dir * travelled;
            if (!IsWalkable(point, clearanceRadius, obstacles, worldSize)) return false;
        }

        return IsWalkable(goal, clearanceRadius, obstacles, worldSize);
    }

    public static bool TryFindPath(
        Vector2 start,
        Vector2 goal,
        float radius,
        List<Obstacle> obstacles,
        int worldSize,
        out List<Vector2> path,
        bool allowDirectShortcut = true)
    {
        path = [];
        if (!TryFindNearestWalkable(start, radius, obstacles, worldSize, out var safeStart)) return false;
        if (!TryFindNearestWalkable(goal, radius, obstacles, worldSize, out var safeGoal)) return false;

        if (allowDirectShortcut && HasClearPath(safeStart, safeGoal, radius, obstacles, worldSize))
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
                path = BuildPath(cameFrom, current, safeStart, safeGoal, radius, obstacles, worldSize, allowDirectShortcut);
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
        int worldSize,
        bool allowDirectShortcut)
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
                if (!allowDirectShortcut && j == raw.Count - 1 && i == 0) continue;
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
