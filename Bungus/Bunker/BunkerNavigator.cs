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
