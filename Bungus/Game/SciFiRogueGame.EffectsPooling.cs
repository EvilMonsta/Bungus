using System.Numerics;
using Raylib_cs;

namespace Bungus.Game;

public sealed partial class SciFiRogueGame
{
    private void AddExplosion(Vector2 position, float radius, Color color, bool filled = false, bool outlined = true, float fillAlpha = 0.22f)
    {
        Explosion explosion;
        if (_explosionPool.Count > 0)
        {
            explosion = _explosionPool.Pop();
            explosion.Reset(position, radius, color, filled, outlined, fillAlpha);
        }
        else
        {
            explosion = new Explosion(position, radius, color, filled, outlined, fillAlpha);
        }

        _explosions.Add(explosion);
    }

    private void ReleaseExplosionAt(int index)
    {
        var explosion = _explosions[index];
        _explosions.RemoveAt(index);
        if (_explosionPool.Count < 512) _explosionPool.Push(explosion);
    }
}
