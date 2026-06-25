using System.Numerics;
using System.Runtime.CompilerServices;
using Raylib_cs;

namespace Bungus.Game;

internal sealed class ObstacleSpatialIndex
{
    private const float CellSize = 256f;
    private readonly List<Obstacle> _obstacles;
    private readonly Dictionary<(int X, int Y), List<int>> _cells = [];
    private readonly int[] _seen;
    private int _queryStamp;

    public ObstacleSpatialIndex(List<Obstacle> obstacles)
    {
        _obstacles = obstacles;
        SourceCount = obstacles.Count;
        _seen = new int[obstacles.Count];

        for (var i = 0; i < obstacles.Count; i++)
        {
            var rect = obstacles[i].Rect;
            var minX = ToCell(rect.X);
            var maxX = ToCell(rect.X + rect.Width);
            var minY = ToCell(rect.Y);
            var maxY = ToCell(rect.Y + rect.Height);

            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var key = (x, y);
                    if (!_cells.TryGetValue(key, out var list))
                    {
                        list = [];
                        _cells[key] = list;
                    }

                    list.Add(i);
                }
            }
        }
    }

    public int SourceCount { get; }

    public bool CircleHitsObstacle(Vector2 center, float radius)
    {
        unchecked
        {
            _queryStamp++;
            if (_queryStamp == 0)
            {
                Array.Clear(_seen);
                _queryStamp = 1;
            }
        }

        var minX = ToCell(center.X - radius);
        var maxX = ToCell(center.X + radius);
        var minY = ToCell(center.Y - radius);
        var maxY = ToCell(center.Y + radius);

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                if (!_cells.TryGetValue((x, y), out var list)) continue;

                foreach (var index in list)
                {
                    if (_seen[index] == _queryStamp) continue;
                    _seen[index] = _queryStamp;

                    if (CircleHitsRect(center, radius, _obstacles[index].Rect)) return true;
                }
            }
        }

        return false;
    }

    private static int ToCell(float value) => (int)MathF.Floor(value / CellSize);

    private static bool CircleHitsRect(Vector2 center, float radius, Rectangle rect)
    {
        var nx = Math.Clamp(center.X, rect.X, rect.X + rect.Width);
        var ny = Math.Clamp(center.Y, rect.Y, rect.Y + rect.Height);
        var dx = center.X - nx;
        var dy = center.Y - ny;
        return dx * dx + dy * dy < radius * radius;
    }
}
