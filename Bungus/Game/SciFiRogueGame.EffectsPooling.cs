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
        count = Math.Max(1, (int)MathF.Round(count * GetVisualEffectsMultiplier()));
        speed *= GetVisualEffectsSpeedMultiplier();
        for (var i = 0; i < count; i++)
        {
            var angle = _visualRng.NextSingle() * MathF.Tau;
            var velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (speed * (0.35f + _visualRng.NextSingle()));
            AddVisualParticle(
                position,
                velocity,
                color,
                (2f + _visualRng.NextSingle() * 3f) * GetVisualEffectsSizeMultiplier(),
                (0.14f + _visualRng.NextSingle() * 0.18f) * GetVisualEffectsLifeMultiplier(),
                VisualParticleShape.Spark,
                angle * 180f / MathF.PI,
                (_visualRng.NextSingle() - 0.5f) * 360f);
        }
    }

    private void SpawnExplosionParticles(Vector2 position, float radius, Color color, bool heavy)
    {
        var multiplier = GetVisualEffectsMultiplier();
        var sparkCount = Math.Clamp((int)(radius / 12f), 6, heavy ? 42 : 26);
        SpawnImpactParticles(position, color, sparkCount, 130f + radius * 1.1f);

        var smokeCount = Math.Clamp((int)(radius / 30f * multiplier), 1, heavy ? 16 : 8);
        for (var i = 0; i < smokeCount; i++)
        {
            var angle = _visualRng.NextSingle() * MathF.Tau;
            var distance = radius * (0.08f + _visualRng.NextSingle() * 0.34f);
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            AddVisualParticle(
                position + direction * distance,
                direction * (12f + _visualRng.NextSingle() * 42f),
                Palette.C(Math.Min(255, color.R + 24), Math.Min(255, color.G + 24), Math.Min(255, color.B + 24), heavy ? 92 : 68),
                MathF.Max(8f, radius * (0.05f + _visualRng.NextSingle() * 0.045f)) * GetVisualEffectsSizeMultiplier(),
                (0.32f + _visualRng.NextSingle() * 0.36f) * GetVisualEffectsLifeMultiplier(),
                VisualParticleShape.Smoke);
        }

        if (_visualEffectsIntensity != VisualEffectsIntensity.Low)
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

    private void AddDamageText(object target, float amount, Color color)
    {
        if (amount <= 0f) return;
        AddFloatingCombatText(target, amount.ToString("0.0"), color, amount >= 100f ? 24f : 20f);
    }

    private void AddFloatingCombatText(object target, string value, Color color, float size = 20f)
    {
        if (!_damageNumbersEnabled) return;
        if (string.IsNullOrWhiteSpace(value)) return;
        var position = GetTargetPosition(target);
        if (position is null) return;

        FloatingCombatText text;
        if (_floatingCombatTextPool.Count > 0)
        {
            text = _floatingCombatTextPool.Pop();
        }
        else
        {
            text = new FloatingCombatText();
        }

        var drift = new Vector2((_visualRng.NextSingle() - 0.5f) * 32f, -62f - _visualRng.NextSingle() * 22f);
        var start = position.Value + new Vector2((_visualRng.NextSingle() - 0.5f) * 24f, -22f - _visualRng.NextSingle() * 16f);
        text.Reset(start, drift, value, color, 0.72f, size);
        _floatingCombatTexts.Add(text);

        if (_floatingCombatTexts.Count <= 96) return;
        ReleaseFloatingCombatTextAt(0);
    }

    private void AddDamageText(object target, float amount)
        => AddDamageText(target, amount, GetDamageTextColor(target, amount));

    private static Color GetDamageTextColor(object target, float amount)
        => target switch
        {
            Player => Palette.C(255, 156, 118),
            ProtectiveDome => Palette.C(120, 205, 255),
            _ when amount >= 100f => Palette.C(255, 174, 72),
            GeneratorNode => Palette.C(255, 220, 96),
            BunkerTyrant or BossEnemyDestroyer or StationBossEnemy or MiniBossEnemySquare => Palette.C(255, 118, 118),
            _ => Palette.C(255, 238, 176)
        };

    private void AddHealingText(object target, float amount)
    {
        if (amount <= 0.01f) return;
        AddFloatingCombatText(target, $"+{amount:0.0}", Palette.C(118, 255, 148), amount >= 50f ? 23f : 20f);
    }

    private void AddShieldText(object target, float amount)
    {
        if (amount <= 0.01f) return;
        AddFloatingCombatText(target, $"{amount:0.0}", Palette.C(120, 205, 255), amount >= 50f ? 23f : 20f);
    }

    private void AddImmuneText(object target)
        => AddFloatingCombatText(target, "IMMUNE", Palette.C(170, 205, 255), 18f);

    private void ReleaseFloatingCombatTextAt(int index)
    {
        var text = _floatingCombatTexts[index];
        _floatingCombatTexts.RemoveAt(index);
        if (_floatingCombatTextPool.Count < 192) _floatingCombatTextPool.Push(text);
    }

    private void AddScreenShake(float strength, float duration)
    {
        if (!_screenShakeEnabled) return;
        _screenShakeStrength = MathF.Max(_screenShakeStrength, strength);
        _screenShakeDuration = MathF.Max(_screenShakeDuration, duration);
        _screenShakeTimer = MathF.Max(_screenShakeTimer, duration);
    }

    private void ClearCombatFeedback()
    {
        ClearFloatingCombatTexts();

        _screenShakeTimer = 0f;
        _screenShakeDuration = 0f;
        _screenShakeStrength = 0f;
        _playerDamageFlash = 0f;
        _lastObservedPlayerHealth = _player is null ? -1f : _player.Health;
        _lastObservedPlayerShield = _player is null ? -1f : _player.Shield;
    }

    private void ClearFloatingCombatTexts()
    {
        for (var i = _floatingCombatTexts.Count - 1; i >= 0; i--)
        {
            ReleaseFloatingCombatTextAt(i);
        }
    }

    private void SpawnFreezeAmbientParticles(Vector2 center, float radius, float dt)
    {
        var expected = dt * 18f * GetVisualEffectsMultiplier();
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
                (2.5f + _visualRng.NextSingle() * 4f) * GetVisualEffectsSizeMultiplier(),
                (0.22f + _visualRng.NextSingle() * 0.28f) * GetVisualEffectsLifeMultiplier(),
                VisualParticleShape.Shard,
                _visualRng.NextSingle() * 360f,
                (_visualRng.NextSingle() - 0.5f) * 220f);
        }
    }

    private void SpawnToxicAmbientParticles(Vector2 center, float radiusX, float radiusY, float dt)
    {
        var expected = dt * 10f * GetVisualEffectsMultiplier();
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
                (8f + _visualRng.NextSingle() * 10f) * GetVisualEffectsSizeMultiplier(),
                (0.55f + _visualRng.NextSingle() * 0.5f) * GetVisualEffectsLifeMultiplier(),
                VisualParticleShape.Smoke);
        }
    }

    private float GetVisualEffectsMultiplier()
        => _visualEffectsIntensity switch
        {
            VisualEffectsIntensity.Low => 0.28f,
            VisualEffectsIntensity.High => 1.25f,
            _ => 1f
        };

    private float GetVisualEffectsSizeMultiplier()
        => _visualEffectsIntensity switch
        {
            VisualEffectsIntensity.Low => 0.65f,
            VisualEffectsIntensity.High => 1.08f,
            _ => 1f
        };

    private float GetVisualEffectsLifeMultiplier()
        => _visualEffectsIntensity switch
        {
            VisualEffectsIntensity.Low => 0.72f,
            VisualEffectsIntensity.High => 1.08f,
            _ => 1f
        };

    private float GetVisualEffectsSpeedMultiplier()
        => _visualEffectsIntensity switch
        {
            VisualEffectsIntensity.Low => 0.72f,
            VisualEffectsIntensity.High => 1.05f,
            _ => 1f
        };
}
