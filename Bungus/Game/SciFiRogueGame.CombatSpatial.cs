using System.Numerics;

namespace Bungus.Game;

public sealed partial class SciFiRogueGame
{
    private void RebuildCombatTargetCache()
    {
        _combatTargets.Clear();
        _combatTargetCells.Clear();

        AddCombatTargets();

        for (var i = 0; i < _combatTargets.Count; i++)
        {
            var target = _combatTargets[i];
            var cell = GetCombatSpatialCell(target.Position);
            var key = GetCombatSpatialKey(cell.X, cell.Y);
            if (!_combatTargetCells.TryGetValue(key, out var indices))
            {
                indices = [];
                _combatTargetCells[key] = indices;
            }

            indices.Add(i);
        }
    }

    private void AddCombatTargets()
    {
        if (_inBunker)
        {
            foreach (var parasite in _bunkerParasites)
                if (parasite.Alive) _combatTargets.Add(new EnemyTarget(parasite, parasite.Position, 8f));
            foreach (var scrib in _bunkerScribs)
                if (scrib.Alive && _revealedBunkerRooms.Contains(scrib.RoomId)) _combatTargets.Add(new EnemyTarget(scrib, scrib.Position, BunkerScrib.Radius));
            foreach (var enemy in _bunkerSiegeEnemies)
                if (enemy.Alive && _revealedBunkerRooms.Contains(enemy.RoomId)) _combatTargets.Add(new EnemyTarget(enemy, enemy.Position, BunkerSiegeEnemy.CollisionRadius));
            foreach (var enemy in _bunkerAssaultEnemies)
                if (enemy.Alive && _revealedBunkerRooms.Contains(enemy.RoomId)) _combatTargets.Add(new EnemyTarget(enemy, enemy.Position, BunkerAssaultEnemy.Radius));
            foreach (var enemy in _bunkerInfectedEnemies)
                if (enemy.Alive && _revealedBunkerRooms.Contains(enemy.RoomId)) _combatTargets.Add(new EnemyTarget(enemy, enemy.Position, BunkerInfectedEnemy.Radius));
            if (_bunkerTyrant is not null && _bunkerTyrant.Alive)
                _combatTargets.Add(new EnemyTarget(_bunkerTyrant, _bunkerTyrant.Position, BunkerTyrant.Radius));
            return;
        }

        foreach (var enemy in _enemies)
            if (enemy.Alive) _combatTargets.Add(new EnemyTarget(enemy, enemy.Position, 14f));
        foreach (var hex in _hexEnemies)
            if (hex.Alive) _combatTargets.Add(new EnemyTarget(hex, hex.Position, 16f));
        foreach (var turret in _turrets)
            if (turret.Alive) _combatTargets.Add(new EnemyTarget(turret, turret.Position, 18f));
        foreach (var boss in _miniBosses)
            if (boss.Alive) _combatTargets.Add(new EnemyTarget(boss, boss.Position, 28f));
        foreach (var guard in _generatorGuards)
            if (guard.Alive) _combatTargets.Add(new EnemyTarget(guard, guard.Position, 18f));
        foreach (var toxic in _toxicEnemies)
            if (toxic.Alive) _combatTargets.Add(new EnemyTarget(toxic, toxic.Position, 16f));
        if (_stationBoss is not null && _stationBoss.Alive)
            _combatTargets.Add(new EnemyTarget(_stationBoss, _stationBoss.Position, 34f));
        foreach (var boss in _pitStationBosses)
            if (boss.Alive) _combatTargets.Add(new EnemyTarget(boss, boss.Position, 34f));
        if (_destroyerBoss is not null && _destroyerBoss.Alive)
            _combatTargets.Add(new EnemyTarget(_destroyerBoss, _destroyerBoss.Position, 52f));
    }

    private List<int> QueryCombatTargetIndices(Vector2 position, float radius)
    {
        _combatQueryIndices.Clear();
        var min = GetCombatSpatialCell(position - new Vector2(radius));
        var max = GetCombatSpatialCell(position + new Vector2(radius));

        for (var y = min.Y; y <= max.Y; y++)
        {
            for (var x = min.X; x <= max.X; x++)
            {
                if (!_combatTargetCells.TryGetValue(GetCombatSpatialKey(x, y), out var indices)) continue;
                _combatQueryIndices.AddRange(indices);
            }
        }

        return _combatQueryIndices;
    }

    private IEnumerable<EnemyTarget> QueryCombatTargets(Vector2 position, float radius)
    {
        foreach (var index in QueryCombatTargetIndices(position, radius))
        {
            var target = _combatTargets[index];
            if (Vector2.DistanceSquared(target.Position, position) <= (radius + target.Radius) * (radius + target.Radius))
                yield return target;
        }
    }

    private IEnumerable<EnemyTarget> EnumerateEnemyTargets()
        => _combatTargets;

    private static (int X, int Y) GetCombatSpatialCell(Vector2 position)
        => ((int)MathF.Floor(position.X / CombatSpatialCellSize), (int)MathF.Floor(position.Y / CombatSpatialCellSize));

    private static long GetCombatSpatialKey(int x, int y)
        => ((long)x << 32) ^ (uint)y;
}
