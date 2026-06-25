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
        SpawnExplosionParticles(position, radius, color, filled);
        AddScreenShake(MathF.Min(18f, 2f + radius * 0.028f), MathF.Min(0.32f, 0.08f + radius * 0.00045f));
    }

    private void ReleaseExplosionAt(int index)
    {
        var explosion = _explosions[index];
        _explosions.RemoveAt(index);
        if (_explosionPool.Count < 512) _explosionPool.Push(explosion);
    }

    private void SpawnImpactParticles(Vector2 position, Color color, int count = 8, float speed = 160f)
    {
        for (var i = 0; i < count; i++)
        {
            var angle = _visualRng.NextSingle() * MathF.Tau;
            var velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (speed * (0.35f + _visualRng.NextSingle()));
            AddVisualParticle(
                position,
                velocity,
                color,
                2f + _visualRng.NextSingle() * 3f,
                0.14f + _visualRng.NextSingle() * 0.18f,
                VisualParticleShape.Spark,
                angle * 180f / MathF.PI,
                (_visualRng.NextSingle() - 0.5f) * 360f);
        }
    }

    private void SpawnExplosionParticles(Vector2 position, float radius, Color color, bool heavy)
    {
        var sparkCount = Math.Clamp((int)(radius / 12f), 6, heavy ? 42 : 26);
        SpawnImpactParticles(position, color, sparkCount, 130f + radius * 1.1f);

        var smokeCount = Math.Clamp((int)(radius / 30f), 2, heavy ? 16 : 8);
        for (var i = 0; i < smokeCount; i++)
        {
            var angle = _visualRng.NextSingle() * MathF.Tau;
            var distance = radius * (0.08f + _visualRng.NextSingle() * 0.34f);
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            AddVisualParticle(
                position + direction * distance,
                direction * (12f + _visualRng.NextSingle() * 42f),
                Palette.C(Math.Min(255, color.R + 24), Math.Min(255, color.G + 24), Math.Min(255, color.B + 24), heavy ? 92 : 68),
                MathF.Max(8f, radius * (0.05f + _visualRng.NextSingle() * 0.045f)),
                0.32f + _visualRng.NextSingle() * 0.36f,
                VisualParticleShape.Smoke);
        }

        AddVisualParticle(position, Vector2.Zero, Palette.C(color.R, color.G, color.B, 120), MathF.Max(20f, radius * 0.16f), 0.18f, VisualParticleShape.Glow);
    }

    private void AddVisualParticle(Vector2 position, Vector2 velocity, Color color, float size, float life, VisualParticleShape shape, float rotation = 0f, float spin = 0f)
    {
        VisualParticle particle;
        if (_visualParticlePool.Count > 0)
        {
            particle = _visualParticlePool.Pop();
        }
        else
        {
            particle = new VisualParticle();
        }

        particle.Reset(position, velocity, color, size, life, shape, rotation, spin);
        _visualParticles.Add(particle);
    }

    private void ReleaseVisualParticleAt(int index)
    {
        var particle = _visualParticles[index];
        _visualParticles.RemoveAt(index);
        if (_visualParticlePool.Count < 2048) _visualParticlePool.Push(particle);
    }

    private void AddScreenShake(float strength, float duration)
    {
        _screenShakeStrength = MathF.Max(_screenShakeStrength, strength);
        _screenShakeDuration = MathF.Max(_screenShakeDuration, duration);
        _screenShakeTimer = MathF.Max(_screenShakeTimer, duration);
    }

    private void SpawnFreezeAmbientParticles(Vector2 center, float radius, float dt)
    {
        var expected = dt * 18f;
        while (expected > 0f)
        {
            if (_visualRng.NextSingle() > MathF.Min(1f, expected)) break;
            expected -= 1f;
            var angle = _visualRng.NextSingle() * MathF.Tau;
            var distance = radius * MathF.Sqrt(_visualRng.NextSingle());
            var position = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
            AddVisualParticle(
                position,
                new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (12f + _visualRng.NextSingle() * 34f),
                Palette.C(150, 235, 255, 105),
                2.5f + _visualRng.NextSingle() * 4f,
                0.22f + _visualRng.NextSingle() * 0.28f,
                VisualParticleShape.Shard,
                _visualRng.NextSingle() * 360f,
                (_visualRng.NextSingle() - 0.5f) * 220f);
        }
    }

    private void SpawnToxicAmbientParticles(Vector2 center, float radiusX, float radiusY, float dt)
    {
        var expected = dt * 10f;
        while (expected > 0f)
        {
            if (_visualRng.NextSingle() > MathF.Min(1f, expected)) break;
            expected -= 1f;
            var angle = _visualRng.NextSingle() * MathF.Tau;
            var distance = MathF.Sqrt(_visualRng.NextSingle());
            var position = center + new Vector2(MathF.Cos(angle) * radiusX, MathF.Sin(angle) * radiusY) * distance;
            AddVisualParticle(
                position,
                new Vector2((_visualRng.NextSingle() - 0.5f) * 18f, -18f - _visualRng.NextSingle() * 18f),
                Palette.C(88, 190, 72, 54),
                8f + _visualRng.NextSingle() * 10f,
                0.55f + _visualRng.NextSingle() * 0.5f,
                VisualParticleShape.Smoke);
        }
    }
}
