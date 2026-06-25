using System.Numerics;
using System.Text.Json;
using Raylib_cs;

namespace Bungus.Game;

public sealed partial class SciFiRogueGame : IDisposable
{
    private void UpdateGroundConsumables()
    {
        for (var i = _groundConsumables.Count - 1; i >= 0; i--)
        {
            var pickup = _groundConsumables[i];
            if (Vector2.Distance(pickup.Position, _player.Position) > 26f) continue;
            if (!Raylib.IsKeyPressed(KeyboardKey.F)) continue;
            if (!TryPickGroundItem(pickup.Item)) continue;

            MarkPitConsumablePicked(pickup);
            _groundConsumables.RemoveAt(i);
            break;
        }
    }

    private void UpdateSecuredTerminalInteractions()
    {
        if (_player.InventoryOpen || _mapOpen || _pitRewardOpen || _pitDifficultyOpen) return;
        if (!Raylib.IsKeyPressed(KeyboardKey.F)) return;

        if (_securedTerminalZone?.Unlocked == true
            && Vector2.Distance(_player.Position, _securedTerminalZone.HatchPosition) <= _securedTerminalZone.InteractionRadius)
        {
            EnterBunker();
            return;
        }

        if (_secondaryBunkerHatchUnlocked
            && Vector2.Distance(_player.Position, _secondaryBunkerHatchPosition) <= 34f)
        {
            EnterBunker(true);
            return;
        }

        if (_securedTerminalZone is not null
            && Vector2.Distance(_player.Position, _securedTerminalZone.TerminalPosition) <= _securedTerminalZone.InteractionRadius)
        {
            _terminalOpen = true;
            _terminalInput = string.Empty;
            _terminalScreenText = _securedTerminalZone.Unlocked ? "ACCESS ALLOWED" : "ACCESS DENIED";
            ClearUiInteraction();
            return;
        }

        for (var i = 0; i < _terminalNotes.Count; i++)
        {
            var note = _terminalNotes[i];
            if (Vector2.Distance(_player.Position, note.Position) > 28f) continue;

            _terminalNotesRead[note.Index] = true;
            _openTerminalNoteIndex = note.Index;
            ClearUiInteraction();
            return;
        }
    }

    private void UpdateTerminalPanel()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            CloseTerminalPanel();
            return;
        }

        if (_securedTerminalZone?.Unlocked == true) return;

        for (var digit = 0; digit <= 9; digit++)
        {
            if ((Raylib.IsKeyPressed((KeyboardKey)('0' + digit)) || Clicked(TerminalDigitButtonRect(digit)))
                && _terminalInput.Length < 6)
            {
                _terminalInput += digit.ToString();
            }
        }

        if ((Raylib.IsKeyPressed(KeyboardKey.Backspace) || Clicked(TerminalDeleteButtonRect())) && _terminalInput.Length > 0)
        {
            _terminalInput = _terminalInput[..^1];
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Clicked(TerminalEnterButtonRect()))
        {
            SubmitTerminalCode();
        }
    }

    private void UpdateTerminalNotePopup()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Raylib.IsKeyPressed(KeyboardKey.F) || Clicked(TerminalNoteCloseButtonRect()))
        {
            _openTerminalNoteIndex = null;
        }
    }

    private void CloseTerminalPanel()
    {
        _terminalOpen = false;
        _terminalInput = string.Empty;
    }

    private void SubmitTerminalCode()
    {
        if (_securedTerminalZone is null) return;

        if (_terminalInput != _securedTerminalZone.Password)
        {
            _terminalScreenText = "ACCESS DENIED";
            _terminalInput = string.Empty;
            return;
        }

        if (GetDeviceDataFragmentCount() < 5)
        {
            _terminalScreenText = "ACCESS DENIED - 5 FRAGMENTS REQUIRED";
            _terminalInput = string.Empty;
            return;
        }

        _securedTerminalZone.Unlocked = true;
        _terminalScreenText = "ACCESS ALLOWED";
        _terminalInput = string.Empty;
    }

    private int GetDeviceDataFragmentCount()
    {
        var count = 0;
        foreach (var item in _player.Inventory.BackpackSlots)
        {
            if (item?.IsDeviceDataFragment == true) count += item.Quantity;
        }

        return count;
    }

    private string GetKnownTerminalCodeDisplay()
    {
        if (_securedTerminalZone is null) return string.Empty;

        var code = _securedTerminalZone.Password;
        var first = code[..3];
        var last = code[3..];
        var knowsLast = _terminalNotesRead[0];
        var knowsFirst = _terminalNotesRead[1];

        if (knowsFirst && knowsLast) return code;
        if (knowsFirst) return first + "XXX";
        if (knowsLast) return "XXX" + last;
        return string.Empty;
    }

    private void MarkPitConsumablePicked(GroundConsumablePickup pickup)
    {
        if (!_challengeMode) return;

        for (var i = 0; i < _pitConsumablePickups.Length; i++)
        {
            if (!ReferenceEquals(_pitConsumablePickups[i], pickup)) continue;
            _pitConsumablePickups[i] = null;
            _pitConsumableSpawnTimers[i] = 30f;
            return;
        }
    }

    private void HandleConsumedQuickSlot(ConsumableType? consumableType)
    {
        if (consumableType == ConsumableType.ProtectiveDome)
        {
            var domes = _inBunker ? _bunkerProtectiveDomes : _protectiveDomes;
            domes.Add(new ProtectiveDome(_player.Position));
            return;
        }

        if (consumableType == ConsumableType.MidaMiniTurret)
        {
            var turrets = _inBunker ? _bunkerMidaMiniTurrets : _midaMiniTurrets;
            turrets.Add(new MidaMiniTurret(_player.Position));
            return;
        }

        if (consumableType is ConsumableType.FreezeGrenade or ConsumableType.HeGrenade)
        {
            ThrowConsumableGrenade(consumableType.Value);
        }
    }

    private void ThrowConsumableGrenade(ConsumableType type)
    {
        var target = Raylib.GetScreenToWorld2D(GetUiMousePosition(), _camera);
        var dir = target - _player.Position;
        if (dir.LengthSquared() <= 0.001f) dir = new Vector2(1f, 0f);
        dir = Vector2.Normalize(dir);

        var kind = type == ConsumableType.FreezeGrenade ? ProjectileKind.FreezeGrenade : ProjectileKind.HeGrenade;
        var color = type == ConsumableType.FreezeGrenade ? Palette.C(130, 225, 255) : Palette.C(255, 145, 70);
        var radius = type == ConsumableType.FreezeGrenade ? 110f : 150f;
        var damage = type == ConsumableType.FreezeGrenade ? 50f : 275f;
        const float speed = 420f;
        _projectiles.Add(new Projectile(
            _player.Position + dir * 18f,
            dir,
            speed,
            500f / speed,
            color,
            false,
            damage,
            kind,
            radius,
            damage,
            6f,
            true,
            _player.Position));
    }

    private List<Obstacle> BuildEnemyCollisionObstacles()
    {
        var activeDomeCount = 0;
        foreach (var dome in _protectiveDomes)
        {
            if (dome.Alive) activeDomeCount++;
        }

        if (activeDomeCount == 0) return _obstacles;

        var result = new List<Obstacle>(_obstacles.Count + activeDomeCount);
        result.AddRange(_obstacles);

        foreach (var dome in _protectiveDomes)
        {
            if (!dome.Alive) continue;
            result.Add(new Obstacle(new Rectangle(
                dome.Position.X - ProtectiveDome.Radius,
                dome.Position.Y - ProtectiveDome.Radius,
                ProtectiveDome.Radius * 2f,
                ProtectiveDome.Radius * 2f)));
        }

        return result;
    }

    private int GetActiveWorldSize() => _inBunker ? BunkerWorldSize : _worldSize;

    private List<Obstacle> GetActiveProjectileObstacles() => _inBunker ? _bunkerObstacles : _obstacles;

    private void UpdateProjectiles(float dt)
    {
        _fixedUpdateStepsLastFrame = 1;
        for (var i = _projectiles.Count - 1; i >= 0; i--)
        {
            var p = _projectiles[i];
            p.Update(dt);
            _fixedUpdateStepsLastFrame = Math.Max(
                _fixedUpdateStepsLastFrame,
                (int)MathF.Ceiling(Vector2.Distance(p.PreviousPosition, p.Position) / MathF.Max(4f, p.DrawRadius * 0.5f)));

            if (p.Kind == ProjectileKind.RamBlast)
            {
                ExplodeProjectile(p);
                _projectiles.RemoveAt(i);
                continue;
            }

            if (p.Kind == ProjectileKind.TraceBeam)
            {
                var beamEnd = TryGetNearestPlayerSegmentHitPoint(p.SourcePosition, p.Position, p.DrawRadius, out var hitPoint)
                    ? hitPoint
                    : p.Position;
                _beamEffects.Add(new BeamEffect(p.SourcePosition, beamEnd, p.Color, 0.075f, 3.5f, true));
                TryApplyPlayerSegmentDamage(p.SourcePosition, beamEnd, p.DrawRadius, p.Damage, p.SourcePosition, p.PoisonDamagePerSecond, p.PoisonDuration);
                _projectiles.RemoveAt(i);
                continue;
            }

            AddMotionTrail(
                p.PreviousPosition,
                p.Position,
                p.Color,
                IsGrenadeProjectile(p) ? MathF.Max(5f, p.DrawRadius) : MathF.Max(2.5f, p.DrawRadius),
                MotionTrailShape.Circle,
                IsGrenadeProjectile(p) ? 0.2f : 0.26f);

            var activeWorldSize = GetActiveWorldSize();
            var hitWorldBounds = p.Position.X < 0 || p.Position.Y < 0 || p.Position.X > activeWorldSize || p.Position.Y > activeWorldSize;
            var hitObstacle = MovementUtils.SweptCircleHitsObstacle(p.PreviousPosition, p.Position, p.DrawRadius, GetActiveProjectileObstacles());
            var domeHit = p.OwnerEnemy ? FindHitDome(p.Position, p.DrawRadius) : null;

            if (p.Kind == ProjectileKind.MicroCharge)
            {
                if (hitWorldBounds || hitObstacle || !p.Alive)
                {
                    ExplodeProjectile(p);
                    _projectiles.RemoveAt(i);
                }

                continue;
            }

            if (IsGrenadeProjectile(p))
            {
                var directHit = false;
                var hitTarget = false;

                if (p.OwnerEnemy)
                {
                    if (domeHit is not null)
                    {
                        var healthBefore = domeHit.Health;
                        domeHit.Damage(p.ExplosionDamage);
                        AddDamageTextForHealthLoss(domeHit, healthBefore);
                        AddExplosion(p.Position, 26f, p.Color);
                        _projectiles.RemoveAt(i);
                        continue;
                    }

                    hitTarget = Vector2.Distance(p.Position, _player.Position) < 16f;
                }
                else
                {
                    directHit = TryApplyExplosiveDirectDamage(p.PreviousPosition, p.Position, p.Damage, p.SourcePosition, p.PoisonDamagePerSecond, p.PoisonDuration, p.Kind == ProjectileKind.Grenade);
                    hitTarget = directHit || HasEnemyInRadius(p.Position, 22f);
                }

                if (hitWorldBounds || hitObstacle || hitTarget || !p.Alive)
                {
                    ExplodeProjectile(p);
                    _projectiles.RemoveAt(i);
                }

                continue;
            }

            if (domeHit is not null)
            {
                var healthBefore = domeHit.Health;
                domeHit.Damage(p.Damage);
                AddDamageTextForHealthLoss(domeHit, healthBefore);
                AddBulletImpact(p);
                AddLinearShotFadeTrail(p);
                _projectiles.RemoveAt(i);
                continue;
            }

            if (!p.OwnerEnemy && p.Kind == ProjectileKind.LinearShot
                && TryApplyPlayerSegmentDamage(p.PreviousPosition, p.Position, p.DrawRadius + 6f, p.Damage, p.SourcePosition, p.PoisonDamagePerSecond, p.PoisonDuration))
            {
                AddLinearShotFadeTrail(p);
                _projectiles.RemoveAt(i);
                continue;
            }

            if (hitWorldBounds || hitObstacle)
            {
                if (p.Kind == ProjectileKind.PulsarBolt) SpawnPulsarFragments(p.Position, p.Color, p.SourcePosition);
                AddLinearShotFadeTrail(p);
                _projectiles.RemoveAt(i);
                continue;
            }

            if (p.OwnerEnemy)
            {
                if (DistanceToSegment(_player.Position, p.PreviousPosition, p.Position) < 14f + p.DrawRadius)
                {
                    _player.TakeDamage(p.Damage, armorPenetration: p.PlayerArmorPenetration);
                    if (p.PlayerPoisonDuration > 0f) _player.ApplyPoison(p.PlayerPoisonDuration);
                    if (p.PlayerDecompositionDuration > 0f) _player.ApplyRadioactiveDecomposition(p.PlayerDecompositionDuration);
                    AddBulletImpact(p);
                    AddLinearShotFadeTrail(p);
                    _projectiles.RemoveAt(i);
                }
                else if (!p.Alive)
                {
                    AddLinearShotFadeTrail(p);
                    _projectiles.RemoveAt(i);
                }
                continue;
            }

            if (TryApplyPlayerSegmentDamageCore(p.PreviousPosition, p.Position, p.DrawRadius, p.Damage, p.SourcePosition, p.PoisonDamagePerSecond, p.PoisonDuration, p.IgnoreTarget, out var ricochetTarget, enemyDecompositionDuration: p.EnemyDecompositionDuration))
            {
                if (p.Kind == ProjectileKind.PulsarBolt)
                {
                    SpawnPulsarFragments(p.Position, p.Color, p.SourcePosition);
                }
                else
                {
                    TrySpawnRicochet(p, ricochetTarget);
                    AddBulletImpact(p);
                }

                AddLinearShotFadeTrail(p);
                _projectiles.RemoveAt(i);
                continue;
            }

            if (!p.Alive)
            {
                AddLinearShotFadeTrail(p);
                _projectiles.RemoveAt(i);
            }
        }
    }

    private void AddBulletImpact(Projectile projectile)
    {
        AddExplosion(projectile.Position, projectile.DrawRadius * 3f, projectile.Color, filled: true, outlined: false, fillAlpha: 0.2f);
        SpawnImpactParticles(projectile.Position, projectile.Color, projectile.Highlighted ? 10 : 5, projectile.Highlighted ? 180f : 120f);
    }

    private void TrySpawnRicochet(Projectile projectile, object? hitTarget)
    {
        if (projectile.RicochetRemaining <= 0 || hitTarget is null || _rng.NextSingle() >= 0.20f) return;

        var incoming = projectile.Direction.LengthSquared() <= 0.001f ? new Vector2(1f, 0f) : Vector2.Normalize(projectile.Direction);
        var angle = (_rng.NextSingle() - 0.5f) * MathF.PI;
        var dir = VisibilityUtils.Rotate(-incoming, angle);
        _projectiles.Add(new Projectile(
            projectile.Position + dir * (projectile.DrawRadius + 2f),
            dir,
            600f,
            310f / 600f,
            projectile.Color,
            false,
            projectile.Damage,
            ProjectileKind.Bullet,
            drawRadius: projectile.DrawRadius,
            highlighted: projectile.Highlighted,
            sourcePosition: projectile.Position,
            ricochetRemaining: projectile.RicochetRemaining - 1,
            ignoreTarget: hitTarget));
    }

    private void AddLinearShotFadeTrail(Projectile projectile)
    {
        if (projectile.Kind != ProjectileKind.LinearShot) return;
        _beamEffects.Add(new BeamEffect(projectile.SourcePosition, projectile.Position, projectile.Color, 0.75f, 2f, false));
    }

    private static bool IsGrenadeProjectile(Projectile projectile)
        => projectile.Kind is ProjectileKind.Grenade or ProjectileKind.FreezeGrenade or ProjectileKind.HeGrenade;

    private void SpawnPulsarFragments(Vector2 position, Color color, Vector2 sourcePosition)
    {
        var count = _rng.Next(2, 4);
        for (var i = 0; i < count; i++)
        {
            var angle = _rng.NextSingle() * MathF.PI * 2f;
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            _projectiles.Add(new Projectile(
                position + dir * 3f,
                dir,
                35f,
                0.25f,
                Palette.C(150, 235, 255),
                false,
                0f,
                ProjectileKind.MicroCharge,
                14.9625f,
                15f,
                2.5f,
                true,
                sourcePosition));
        }
    }

    private bool TryGetNearestPlayerSegmentHitPoint(Vector2 from, Vector2 to, float radius, out Vector2 hitPoint)
    {
        if (_inBunker)
        {
            hitPoint = to;
            return false;
        }

        if ((to - from).LengthSquared() <= 0.001f)
        {
            hitPoint = to;
            return false;
        }

        var bestDistance = float.MaxValue;
        var bestPoint = to;

        void Consider(Vector2 position, float hitRadius)
        {
            if (DistanceToSegment(position, from, to) > radius + hitRadius) return;

            var along = ProjectDistanceAlongSegment(position, from, to);
            if (along >= bestDistance) return;

            bestDistance = along;
            var dir = Vector2.Normalize(to - from);
            bestPoint = from + dir * MathF.Max(0f, along - hitRadius * 0.35f);
        }

        foreach (var enemy in _enemies.Where(e => e.Alive)) Consider(enemy.Position, 11f);
        foreach (var hex in _hexEnemies.Where(h => h.Alive)) Consider(hex.Position, 15f);
        foreach (var turret in _turrets.Where(t => t.Alive)) Consider(turret.Position, 18f);
        foreach (var miniBoss in _miniBosses.Where(b => b.Alive)) Consider(miniBoss.Position, 26f);
        foreach (var guard in _generatorGuards.Where(g => g.Alive)) Consider(guard.Position, 18f);
        foreach (var toxic in _toxicEnemies.Where(e => e.Alive)) Consider(toxic.Position, 16f);
        foreach (var generator in _generators.Where(g => !g.Destroyed)) Consider(generator.Position, 28f);
        if (_stationBoss is not null) Consider(_stationBoss.Position, 34f);
        foreach (var boss in _pitStationBosses.Where(b => b.Alive)) Consider(boss.Position, 34f);
        if (_destroyerBoss is not null && _destroyerBoss.Alive) Consider(_destroyerBoss.Position, 42f);

        hitPoint = bestPoint;
        return bestDistance < float.MaxValue;
    }

    private bool TryApplyExplosiveDirectDamage(Vector2 from, Vector2 to, float damage, Vector2 shotSource, float poisonDamagePerSecond = 0f, float poisonDuration = 0f, bool rangedWeaponEffects = true)
    {
        const float directHitRadius = 22f;
        if (TryApplyPlayerSegmentDamage(from, to, directHitRadius, damage, shotSource, poisonDamagePerSecond, poisonDuration, rangedWeaponEffects)) return true;

        var endpoint = to + new Vector2(0.01f, 0f);
        return HasEnemyInRadius(to, directHitRadius)
            && TryApplyPlayerSegmentDamage(to, endpoint, directHitRadius, damage, shotSource, poisonDamagePerSecond, poisonDuration, rangedWeaponEffects);
    }

    private static float ProjectDistanceAlongSegment(Vector2 point, Vector2 from, Vector2 to)
    {
        var segment = to - from;
        var lengthSquared = segment.LengthSquared();
        if (lengthSquared <= 0.001f) return 0f;

        var t = Math.Clamp(Vector2.Dot(point - from, segment) / lengthSquared, 0f, 1f);
        return Vector2.Distance(from, from + segment * t);
    }

    private bool HasEnemyInRadius(Vector2 position, float radius)
    {
        if (_inBunker) return false;

        foreach (var index in QueryCombatTargetIndices(position, radius + 56f))
        {
            var target = _combatTargets[index];
            switch (target.Target)
            {
                case Enemy enemy when Vector2.Distance(enemy.Position, position) < radius:
                case HexEnemy hex when Vector2.Distance(hex.Position, position) < radius:
                case TurretEnemy turret when Vector2.Distance(turret.Position, position) < radius + 6f:
                case MiniBossEnemySquare boss when Vector2.Distance(boss.Position, position) < radius + 14f:
                case GeneratorGuardianEnemy guard when Vector2.Distance(guard.Position, position) < radius + 14f:
                case ToxicTriangleEnemy toxic when Vector2.Distance(toxic.Position, position) < radius + 12f:
                case StationBossEnemy stationBoss when stationBoss.IntersectsAnyHitZone(position, radius):
                case BossEnemyDestroyer destroyerBoss when destroyerBoss.IntersectsAnyHitZone(position, radius):
                    return true;
            }
        }

        foreach (var generator in _generators)
        {
            if (!generator.Destroyed && Vector2.Distance(generator.Position, position) < radius + 24f) return true;
        }

        return false;
    }

    private bool TryApplyPlayerSegmentDamage(Vector2 from, Vector2 to, float radius, float damage, Vector2 shotSource, float poisonDamagePerSecond = 0f, float poisonDuration = 0f, bool rangedWeaponEffects = true, float enemyDecompositionDuration = 0f)
        => TryApplyPlayerSegmentDamageCore(from, to, radius, damage, shotSource, poisonDamagePerSecond, poisonDuration, null, out _, rangedWeaponEffects, enemyDecompositionDuration);

    private bool TryApplyPlayerSegmentDamageCore(Vector2 from, Vector2 to, float radius, float damage, Vector2 shotSource, float poisonDamagePerSecond, float poisonDuration, object? ignoreTarget, out object? hitTarget, bool rangedWeaponEffects = true, float enemyDecompositionDuration = 0f)
    {
        hitTarget = null;
        if (poisonDuration > 0f) poisonDuration *= _player.GetArcaneEffectMultiplier();
        if (enemyDecompositionDuration > 0f) enemyDecompositionDuration *= _player.GetArcaneEffectMultiplier();
        if (_inBunker)
            return TryApplyBunkerSegmentDamage(
                from,
                to,
                radius,
                damage,
                poisonDamagePerSecond,
                poisonDuration,
                rangedWeaponEffects,
                enemyDecompositionDuration,
                ignoreTarget,
                out hitTarget);

        var enemyHit = _enemies
            .Where(e => e.Alive && !ReferenceEquals(e, ignoreTarget) && DistanceToSegment(e.Position, from, to) <= radius + 11f)
            .OrderBy(e => DistanceToSegment(e.Position, from, to))
            .FirstOrDefault();
        if (enemyHit is not null)
        {
            hitTarget = enemyHit;
            var actualDamage = GetDamageAgainstTarget(enemyHit, damage);
            var healthBefore = enemyHit.Health;
            enemyHit.Damage(actualDamage);
            AddDamageTextForHealthLoss(enemyHit, healthBefore);
            ApplyEnemyDecomposition(enemyHit, enemyDecompositionDuration);
            ApplyPlayerHitEffects(enemyHit, poisonDamagePerSecond, poisonDuration, rangedWeaponEffects);
            var targetAggroed = enemyHit.ReactToShot(shotSource, _obstacles);
            AggroWitnesses(enemyHit.Position, targetAggroed);
            return true;
        }

        var hexHit = _hexEnemies
            .Where(h => h.Alive && !ReferenceEquals(h, ignoreTarget) && DistanceToSegment(h.Position, from, to) <= radius + 15f)
            .OrderBy(h => DistanceToSegment(h.Position, from, to))
            .FirstOrDefault();
        if (hexHit is not null)
        {
            hitTarget = hexHit;
            var actualDamage = GetDamageAgainstTarget(hexHit, damage);
            var healthBefore = hexHit.Health;
            hexHit.Damage(actualDamage);
            AddDamageTextForHealthLoss(hexHit, healthBefore);
            ApplyEnemyDecomposition(hexHit, enemyDecompositionDuration);
            ApplyPlayerHitEffects(hexHit, poisonDamagePerSecond, poisonDuration, rangedWeaponEffects);
            AggroWitnesses(hexHit.Position, true);
            return true;
        }

        var turretHit = _turrets
            .Where(t => t.Alive && !ReferenceEquals(t, ignoreTarget) && DistanceToSegment(t.Position, from, to) <= radius + 18f)
            .OrderBy(t => DistanceToSegment(t.Position, from, to))
            .FirstOrDefault();
        if (turretHit is not null)
        {
            hitTarget = turretHit;
            var actualDamage = GetDamageAgainstTarget(turretHit, damage);
            var healthBefore = turretHit.Health;
            turretHit.Damage(actualDamage);
            AddDamageTextForHealthLoss(turretHit, healthBefore);
            ApplyEnemyDecomposition(turretHit, enemyDecompositionDuration);
            ApplyPlayerHitEffects(turretHit, poisonDamagePerSecond, poisonDuration, rangedWeaponEffects);
            var targetAggroed = turretHit.ReactToShot(shotSource, _player.Position, _obstacles);
            AggroWitnesses(turretHit.Position, targetAggroed);
            return true;
        }

        var miniBossHit = _miniBosses
            .Where(b => b.Alive && !ReferenceEquals(b, ignoreTarget) && DistanceToSegment(b.Position, from, to) <= radius + 26f)
            .OrderBy(b => DistanceToSegment(b.Position, from, to))
            .FirstOrDefault();
        if (miniBossHit is not null)
        {
            hitTarget = miniBossHit;
            var actualDamage = GetDamageAgainstTarget(miniBossHit, damage);
            var healthBefore = miniBossHit.Health;
            miniBossHit.Damage(actualDamage);
            AddDamageTextForHealthLoss(miniBossHit, healthBefore);
            ApplyEnemyDecomposition(miniBossHit, enemyDecompositionDuration);
            ApplyPlayerHitEffects(miniBossHit, poisonDamagePerSecond, poisonDuration, rangedWeaponEffects);
            var targetAggroed = miniBossHit.ReactToShot(shotSource, _obstacles);
            AggroWitnesses(miniBossHit.Position, targetAggroed);
            return true;
        }

        var guardHit = _generatorGuards
            .Where(g => g.Alive && !ReferenceEquals(g, ignoreTarget) && DistanceToSegment(g.Position, from, to) <= radius + 18f)
            .OrderBy(g => DistanceToSegment(g.Position, from, to))
            .FirstOrDefault();
        if (guardHit is not null)
        {
            hitTarget = guardHit;
            var actualDamage = GetDamageAgainstTarget(guardHit, damage);
            var healthBefore = guardHit.Health;
            guardHit.Damage(actualDamage);
            AddDamageTextForHealthLoss(guardHit, healthBefore);
            ApplyEnemyDecomposition(guardHit, enemyDecompositionDuration);
            ApplyPlayerHitEffects(guardHit, poisonDamagePerSecond, poisonDuration, rangedWeaponEffects);
            var targetAggroed = guardHit.TryAggroFromPlayerHit(_player.Position);
            AggroWitnesses(guardHit.Position, targetAggroed);
            return true;
        }

        var toxicHit = _toxicEnemies
            .Where(e => e.Alive && !ReferenceEquals(e, ignoreTarget) && DistanceToSegment(e.Position, from, to) <= radius + 16f)
            .OrderBy(e => DistanceToSegment(e.Position, from, to))
            .FirstOrDefault();
        if (toxicHit is not null)
        {
            hitTarget = toxicHit;
            var actualDamage = GetDamageAgainstTarget(toxicHit, damage);
            var healthBefore = toxicHit.Health;
            toxicHit.Damage(actualDamage);
            AddDamageTextForHealthLoss(toxicHit, healthBefore);
            ApplyEnemyDecomposition(toxicHit, enemyDecompositionDuration);
            ApplyPlayerHitEffects(toxicHit, poisonDamagePerSecond, poisonDuration, rangedWeaponEffects);
            var targetAggroed = toxicHit.ReactToShot(shotSource, _obstacles);
            AggroWitnesses(toxicHit.Position, targetAggroed);
            return true;
        }

        var generatorHit = _generators
            .Where(g => !g.Destroyed && !ReferenceEquals(g, ignoreTarget) && DistanceToSegment(g.Position, from, to) <= radius + 28f)
            .OrderBy(g => DistanceToSegment(g.Position, from, to))
            .FirstOrDefault();
        if (generatorHit is not null)
        {
            hitTarget = generatorHit;
            if (!generatorHit.Vulnerable)
            {
                AddImmuneText(generatorHit);
                return true;
            }
            var healthBefore = generatorHit.Health;
            generatorHit.Damage(damage);
            AddDamageTextForHealthLoss(generatorHit, healthBefore);
            return true;
        }

        if (_stationBoss is not null && !ReferenceEquals(_stationBoss, ignoreTarget))
        {
            if (!_stationBoss.Active && DistanceToSegment(_stationBoss.Position, from, to) <= radius + 34f)
            {
                hitTarget = _stationBoss;
                AddImmuneText(_stationBoss);
                return true;
            }

            var healthBefore = _stationBoss.Health;
            if (_stationBoss.TryApplySegmentDamage(from, to, radius, damage))
            {
                hitTarget = _stationBoss;
                AddDamageTextForHealthLoss(_stationBoss, healthBefore, showImmuneOnNoLoss: true);
                ApplyPlayerHitEffects(_stationBoss, poisonDamagePerSecond, poisonDuration, rangedWeaponEffects);
                AggroWitnesses(_stationBoss.Position, true);
                return true;
            }
        }

        foreach (var pitStationBoss in _pitStationBosses.Where(b => b.Alive && !ReferenceEquals(b, ignoreTarget)))
        {
            var healthBefore = pitStationBoss.Health;
            if (!pitStationBoss.TryApplySegmentDamage(from, to, radius, damage)) continue;

            hitTarget = pitStationBoss;
            AddDamageTextForHealthLoss(pitStationBoss, healthBefore, showImmuneOnNoLoss: true);
            ApplyPlayerHitEffects(pitStationBoss, poisonDamagePerSecond, poisonDuration, rangedWeaponEffects);
            AggroWitnesses(pitStationBoss.Position, true);
            return true;
        }

        if (_destroyerBoss is not null && _destroyerBoss.Alive && !ReferenceEquals(_destroyerBoss, ignoreTarget))
        {
            if (_destroyerBoss.TryApplySegmentDamage(from, to, radius, damage, out var actualDamage))
            {
                hitTarget = _destroyerBoss;
                if (actualDamage > 0.01f) AddDamageText(_destroyerBoss, actualDamage);
                else AddImmuneText(_destroyerBoss);
                ApplyPlayerHitEffects(_destroyerBoss, poisonDamagePerSecond, poisonDuration, rangedWeaponEffects);
                var targetAggroed = _destroyerBoss.ReactToShot(shotSource, _obstacles);
                AggroWitnesses(_destroyerBoss.Position, targetAggroed);
                return true;
            }
        }

        return false;
    }

    private bool TryApplyBunkerSegmentDamage(
        Vector2 from,
        Vector2 to,
        float radius,
        float damage,
        float poisonDamagePerSecond,
        float poisonDuration,
        bool rangedWeaponEffects,
        float enemyDecompositionDuration,
        object? ignoreTarget,
        out object? hitTarget)
    {
        hitTarget = null;
        var parasite = _bunkerParasites
            .Where(target => target.Alive && !ReferenceEquals(target, ignoreTarget) && DistanceToSegment(target.Position, from, to) <= radius + 8f)
            .OrderBy(target => Vector2.DistanceSquared(from, target.Position))
            .FirstOrDefault();
        if (parasite is not null)
        {
            var actualDamage = GetDamageAgainstTarget(parasite, damage);
            var healthBefore = parasite.Health;
            parasite.Damage(actualDamage);
            AddDamageTextForHealthLoss(parasite, healthBefore);
            ApplyEnemyDecomposition(parasite, enemyDecompositionDuration);
            ApplyBunkerPlayerHitEffects(parasite, poisonDamagePerSecond, poisonDuration, rangedWeaponEffects);
            hitTarget = parasite;
            return true;
        }

        var scrib = _bunkerScribs
            .Where(target => target.Alive && !ReferenceEquals(target, ignoreTarget) && DistanceToSegment(target.Position, from, to) <= radius + BunkerScrib.Radius)
            .OrderBy(target => Vector2.DistanceSquared(from, target.Position))
            .FirstOrDefault();
        if (scrib is not null)
        {
            scrib.ForceAggro(_player.Position);
            var actualDamage = GetDamageAgainstTarget(scrib, damage);
            var healthBefore = scrib.Health;
            if (scrib.Damage(actualDamage)) ExplodeBunkerScrib(scrib.Position);
            AddDamageTextForHealthLoss(scrib, healthBefore);
            ApplyEnemyDecomposition(scrib, enemyDecompositionDuration);
            ApplyBunkerPlayerHitEffects(scrib, poisonDamagePerSecond, poisonDuration, rangedWeaponEffects);
            hitTarget = scrib;
            return true;
        }

        var siege = _bunkerSiegeEnemies
            .Where(target => target.Alive && !ReferenceEquals(target, ignoreTarget) && target.IntersectsSegment(from, to, radius))
            .OrderBy(target => Vector2.DistanceSquared(from, target.Position))
            .FirstOrDefault();
        if (siege is not null)
        {
            siege.ForceAggro(_player.Position);
            var actualDamage = GetDamageAgainstTarget(siege, damage);
            var healthBefore = siege.Health;
            siege.Damage(actualDamage);
            AddDamageTextForHealthLoss(siege, healthBefore);
            ApplyEnemyDecomposition(siege, enemyDecompositionDuration);
            ApplyBunkerPlayerHitEffects(siege, poisonDamagePerSecond, poisonDuration, rangedWeaponEffects);
            hitTarget = siege;
            return true;
        }

        var assault = _bunkerAssaultEnemies
            .Where(target => target.Alive && !ReferenceEquals(target, ignoreTarget) && DistanceToSegment(target.Position, from, to) <= radius + BunkerAssaultEnemy.Radius)
            .OrderBy(target => Vector2.DistanceSquared(from, target.Position))
            .FirstOrDefault();
        if (assault is not null)
        {
            assault.ForceAggro(_player.Position);
            var actualDamage = GetDamageAgainstTarget(assault, damage);
            var healthBefore = assault.Health;
            assault.Damage(actualDamage);
            AddDamageTextForHealthLoss(assault, healthBefore);
            ApplyEnemyDecomposition(assault, enemyDecompositionDuration);
            ApplyBunkerPlayerHitEffects(assault, poisonDamagePerSecond, poisonDuration, rangedWeaponEffects);
            hitTarget = assault;
            return true;
        }

        var infected = _bunkerInfectedEnemies
            .Where(target => target.Alive && !ReferenceEquals(target, ignoreTarget) && DistanceToSegment(target.Position, from, to) <= radius + BunkerInfectedEnemy.Radius)
            .OrderBy(target => Vector2.DistanceSquared(from, target.Position))
            .FirstOrDefault();
        if (infected is not null)
        {
            infected.ForceAggro(_player.Position);
            var actualDamage = GetDamageAgainstTarget(infected, damage);
            var healthBefore = infected.Health;
            infected.Damage(actualDamage);
            AddDamageTextForHealthLoss(infected, healthBefore);
            ApplyEnemyDecomposition(infected, enemyDecompositionDuration);
            ApplyBunkerPlayerHitEffects(infected, poisonDamagePerSecond, poisonDuration, rangedWeaponEffects);
            hitTarget = infected;
            return true;
        }

        if (_bunkerTyrant is not null
            && _bunkerTyrant.Alive
            && !ReferenceEquals(_bunkerTyrant, ignoreTarget)
            && DistanceToSegment(_bunkerTyrant.Position, from, to) <= radius + BunkerTyrant.Radius)
        {
            var actualDamage = GetDamageAgainstTarget(_bunkerTyrant, damage);
            var healthBefore = _bunkerTyrant.Health;
            if (_bunkerTyrant.Damage(actualDamage))
            {
                AddDamageTextForHealthLoss(_bunkerTyrant, healthBefore);
                ApplyEnemyDecomposition(_bunkerTyrant, enemyDecompositionDuration);
                ApplyBunkerPlayerHitEffects(_bunkerTyrant, poisonDamagePerSecond, poisonDuration, rangedWeaponEffects);
            }
            else AddImmuneText(_bunkerTyrant);
            hitTarget = _bunkerTyrant;
            return true;
        }

        return false;
    }

    private void ExplodeProjectile(Projectile projectile)
    {
        AddExplosion(projectile.Position, projectile.ExplosionRadius, projectile.Color, projectile.Kind == ProjectileKind.RamBlast);
        if (projectile.Kind == ProjectileKind.FreezeGrenade)
        {
            var zones = _inBunker ? _bunkerFreezeZones : _freezeZones;
            zones.Add(new FreezeZone(projectile.Position, _player.GetArcaneEffectMultiplier()));
        }

        if (_inBunker)
        {
            if (projectile.OwnerEnemy && Vector2.Distance(projectile.Position, _player.Position) <= projectile.ExplosionRadius + 16f)
            {
                _player.TakeDamage(projectile.ExplosionDamage, true);
                if (projectile.PlayerPoisonDuration > 0f) _player.ApplyPoison(projectile.PlayerPoisonDuration);
                if (projectile.PlayerDecompositionDuration > 0f) _player.ApplyRadioactiveDecomposition(projectile.PlayerDecompositionDuration);
            }
            else if (!projectile.OwnerEnemy)
            {
                foreach (var parasite in _bunkerParasites.Where(target => target.Alive && IsInExplosion(projectile.Position, projectile.ExplosionRadius, target.Position, 8f)))
                {
                    var actualDamage = GetDamageAgainstTarget(parasite, projectile.ExplosionDamage);
                    var healthBefore = parasite.Health;
                    parasite.Damage(actualDamage);
                    AddDamageTextForHealthLoss(parasite, healthBefore);
                    FreezeBunkerTargetIfNeeded(projectile, parasite);
                }
                foreach (var scrib in _bunkerScribs.Where(target => target.Alive && IsInExplosion(projectile.Position, projectile.ExplosionRadius, target.Position, BunkerScrib.Radius)))
                {
                    scrib.ForceAggro(_player.Position);
                    var actualDamage = GetDamageAgainstTarget(scrib, projectile.ExplosionDamage);
                    var healthBefore = scrib.Health;
                    if (scrib.Damage(actualDamage)) ExplodeBunkerScrib(scrib.Position);
                    AddDamageTextForHealthLoss(scrib, healthBefore);
                    FreezeBunkerTargetIfNeeded(projectile, scrib);
                }
                foreach (var enemy in _bunkerSiegeEnemies.Where(target => target.Alive && target.IntersectsCircle(projectile.Position, projectile.ExplosionRadius)))
                {
                    enemy.ForceAggro(_player.Position);
                    var actualDamage = GetDamageAgainstTarget(enemy, projectile.ExplosionDamage);
                    var healthBefore = enemy.Health;
                    enemy.Damage(actualDamage);
                    AddDamageTextForHealthLoss(enemy, healthBefore);
                    FreezeBunkerTargetIfNeeded(projectile, enemy);
                }
                foreach (var enemy in _bunkerAssaultEnemies.Where(target => target.Alive && IsInExplosion(projectile.Position, projectile.ExplosionRadius, target.Position, BunkerAssaultEnemy.Radius)))
                {
                    enemy.ForceAggro(_player.Position);
                    var actualDamage = GetDamageAgainstTarget(enemy, projectile.ExplosionDamage);
                    var healthBefore = enemy.Health;
                    enemy.Damage(actualDamage);
                    AddDamageTextForHealthLoss(enemy, healthBefore);
                    FreezeBunkerTargetIfNeeded(projectile, enemy);
                }
                foreach (var enemy in _bunkerInfectedEnemies.Where(target => target.Alive && IsInExplosion(projectile.Position, projectile.ExplosionRadius, target.Position, BunkerInfectedEnemy.Radius)))
                {
                    enemy.ForceAggro(_player.Position);
                    var actualDamage = GetDamageAgainstTarget(enemy, projectile.ExplosionDamage);
                    var healthBefore = enemy.Health;
                    enemy.Damage(actualDamage);
                    AddDamageTextForHealthLoss(enemy, healthBefore);
                    FreezeBunkerTargetIfNeeded(projectile, enemy);
                }
                if (_bunkerTyrant is not null && IsInExplosion(projectile.Position, projectile.ExplosionRadius, _bunkerTyrant.Position, BunkerTyrant.Radius))
                {
                    var actualDamage = GetDamageAgainstTarget(_bunkerTyrant, projectile.ExplosionDamage);
                    var healthBefore = _bunkerTyrant.Health;
                    if (_bunkerTyrant.Damage(actualDamage))
                    {
                        AddDamageTextForHealthLoss(_bunkerTyrant, healthBefore);
                        FreezeBunkerTargetIfNeeded(projectile, _bunkerTyrant);
                    }
                    else AddImmuneText(_bunkerTyrant);
                }
            }
            return;
        }

        if (projectile.OwnerEnemy)
        {
            if (Vector2.Distance(projectile.Position, _player.Position) <= projectile.ExplosionRadius)
            {
                _player.TakeDamage(projectile.ExplosionDamage, true);
            }

            return;
        }

        var aggroWitnesses = false;

        foreach (var enemy in _enemies.Where(e => e.Alive && IsInExplosion(projectile.Position, projectile.ExplosionRadius, e.Position, 16f)))
        {
            var actualDamage = GetDamageAgainstTarget(enemy, projectile.ExplosionDamage);
            var healthBefore = enemy.Health;
            enemy.Damage(actualDamage);
            AddDamageTextForHealthLoss(enemy, healthBefore);
            if (projectile.Kind == ProjectileKind.FreezeGrenade) _frozenTargets[enemy] = GetPlayerFreezeDuration();
            ApplyPlayerHitEffects(enemy, rangedWeaponEffects: false);
            aggroWitnesses |= enemy.ReactToShot(projectile.SourcePosition, _obstacles);
        }

        foreach (var hex in _hexEnemies.Where(h => h.Alive && IsInExplosion(projectile.Position, projectile.ExplosionRadius, h.Position, 15f)))
        {
            var actualDamage = GetDamageAgainstTarget(hex, projectile.ExplosionDamage);
            var healthBefore = hex.Health;
            hex.Damage(actualDamage);
            AddDamageTextForHealthLoss(hex, healthBefore);
            if (projectile.Kind == ProjectileKind.FreezeGrenade) _frozenTargets[hex] = GetPlayerFreezeDuration();
            ApplyPlayerHitEffects(hex, rangedWeaponEffects: false);
            aggroWitnesses = true;
        }

        foreach (var turret in _turrets.Where(t => t.Alive && IsInExplosion(projectile.Position, projectile.ExplosionRadius, t.Position, 18f)))
        {
            var actualDamage = GetDamageAgainstTarget(turret, projectile.ExplosionDamage);
            var healthBefore = turret.Health;
            turret.Damage(actualDamage);
            AddDamageTextForHealthLoss(turret, healthBefore);
            if (projectile.Kind == ProjectileKind.FreezeGrenade) _frozenTargets[turret] = GetPlayerFreezeDuration();
            ApplyPlayerHitEffects(turret, rangedWeaponEffects: false);
            aggroWitnesses |= turret.ReactToShot(projectile.SourcePosition, _player.Position, _obstacles);
        }

        foreach (var miniBoss in _miniBosses.Where(b => b.Alive && IsInExplosion(projectile.Position, projectile.ExplosionRadius, b.Position, 21f)))
        {
            var actualDamage = GetDamageAgainstTarget(miniBoss, projectile.ExplosionDamage);
            var healthBefore = miniBoss.Health;
            miniBoss.Damage(actualDamage);
            AddDamageTextForHealthLoss(miniBoss, healthBefore);
            if (projectile.Kind == ProjectileKind.FreezeGrenade) _frozenTargets[miniBoss] = GetPlayerFreezeDuration();
            ApplyPlayerHitEffects(miniBoss, rangedWeaponEffects: false);
            aggroWitnesses |= miniBoss.ReactToShot(projectile.SourcePosition, _obstacles);
        }

        foreach (var guard in _generatorGuards.Where(g => g.Alive && IsInExplosion(projectile.Position, projectile.ExplosionRadius, g.Position, 14f)))
        {
            var actualDamage = GetDamageAgainstTarget(guard, projectile.ExplosionDamage);
            var healthBefore = guard.Health;
            guard.Damage(actualDamage);
            AddDamageTextForHealthLoss(guard, healthBefore);
            if (projectile.Kind == ProjectileKind.FreezeGrenade) _frozenTargets[guard] = GetPlayerFreezeDuration();
            ApplyPlayerHitEffects(guard, rangedWeaponEffects: false);
            aggroWitnesses |= guard.TryAggroFromPlayerHit(_player.Position);
        }

        foreach (var toxic in _toxicEnemies.Where(e => e.Alive && IsInExplosion(projectile.Position, projectile.ExplosionRadius, e.Position, 12f)))
        {
            var actualDamage = GetDamageAgainstTarget(toxic, projectile.ExplosionDamage);
            var healthBefore = toxic.Health;
            toxic.Damage(actualDamage);
            AddDamageTextForHealthLoss(toxic, healthBefore);
            if (projectile.Kind == ProjectileKind.FreezeGrenade) _frozenTargets[toxic] = GetPlayerFreezeDuration();
            ApplyPlayerHitEffects(toxic, rangedWeaponEffects: false);
            aggroWitnesses |= toxic.ReactToShot(projectile.SourcePosition, _obstacles);
        }

        foreach (var generator in _generators.Where(g => !g.Destroyed && IsInExplosion(projectile.Position, projectile.ExplosionRadius, g.Position, 24f)))
        {
            if (!generator.Vulnerable)
            {
                AddImmuneText(generator);
                continue;
            }
            var healthBefore = generator.Health;
            generator.Damage(projectile.ExplosionDamage);
            AddDamageTextForHealthLoss(generator, healthBefore);
        }

        if (_stationBoss is not null)
        {
            var hitsStationBoss = Vector2.Distance(_stationBoss.Position, projectile.Position) <= projectile.ExplosionRadius + 34f;
            if (!_stationBoss.Active && hitsStationBoss)
            {
                AddImmuneText(_stationBoss);
            }
            var actualDamage = GetDamageAgainstTarget(_stationBoss, projectile.ExplosionDamage);
            var healthBefore = _stationBoss.Health;
            _stationBoss.ApplyExplosionDamage(projectile.Position, projectile.ExplosionRadius, actualDamage);
            if (_stationBoss.IntersectsAnyHitZone(projectile.Position, projectile.ExplosionRadius))
            {
                AddDamageTextForHealthLoss(_stationBoss, healthBefore, showImmuneOnNoLoss: true);
                if (projectile.Kind == ProjectileKind.FreezeGrenade) _frozenTargets[_stationBoss] = GetPlayerFreezeDuration();
                ApplyPlayerHitEffects(_stationBoss, rangedWeaponEffects: false);
                aggroWitnesses = true;
            }
        }

        foreach (var boss in _pitStationBosses.Where(b => b.Alive))
        {
            var actualDamage = GetDamageAgainstTarget(boss, projectile.ExplosionDamage);
            var healthBefore = boss.Health;
            boss.ApplyExplosionDamage(projectile.Position, projectile.ExplosionRadius, actualDamage);
            if (boss.IntersectsAnyHitZone(projectile.Position, projectile.ExplosionRadius))
            {
                AddDamageTextForHealthLoss(boss, healthBefore, showImmuneOnNoLoss: true);
                if (projectile.Kind == ProjectileKind.FreezeGrenade) _frozenTargets[boss] = GetPlayerFreezeDuration();
                ApplyPlayerHitEffects(boss, rangedWeaponEffects: false);
                aggroWitnesses = true;
            }
        }

        if (_destroyerBoss is not null && _destroyerBoss.Alive)
        {
            var actualDamage = GetDamageAgainstTarget(_destroyerBoss, projectile.ExplosionDamage);
            if (_destroyerBoss.ApplyExplosionDamage(projectile.Position, projectile.ExplosionRadius, actualDamage, out var appliedDamage)
                && _destroyerBoss.IntersectsAnyHitZone(projectile.Position, projectile.ExplosionRadius))
            {
                if (appliedDamage > 0.01f) AddDamageText(_destroyerBoss, appliedDamage);
                else AddImmuneText(_destroyerBoss);
                if (projectile.Kind == ProjectileKind.FreezeGrenade) _frozenTargets[_destroyerBoss] = GetPlayerFreezeDuration();
                ApplyPlayerHitEffects(_destroyerBoss, rangedWeaponEffects: false);
                aggroWitnesses |= _destroyerBoss.ReactToShot(projectile.SourcePosition, _obstacles);
            }
        }

        AggroWitnesses(projectile.Position, aggroWitnesses);
    }

    private static bool IsInExplosion(Vector2 explosionPosition, float explosionRadius, Vector2 targetPosition, float targetRadius)
    {
        return Vector2.Distance(targetPosition, explosionPosition) <= explosionRadius + targetRadius;
    }

    private void ApplyPlayerHitEffects(Enemy enemy, float poisonDamagePerSecond = 0f, float poisonDuration = 0f, bool rangedWeaponEffects = true)
    {
        if (rangedWeaponEffects && _player.StickyBulletsActive) enemy.ApplyStickySlow(_player.GetArcaneEffectMultiplier(), _player.GetArcaneEffectMultiplier());
        if (rangedWeaponEffects && _player.TeslaBulletsActive) TriggerTeslaChain(enemy);
        if (poisonDamagePerSecond > 0f) enemy.ApplyPoison(poisonDamagePerSecond, poisonDuration);
        TrackEnemyEffectVisuals(enemy, poisonDamagePerSecond, poisonDuration, rangedWeaponEffects);
    }

    private void ApplyPlayerHitEffects(HexEnemy enemy, float poisonDamagePerSecond = 0f, float poisonDuration = 0f, bool rangedWeaponEffects = true)
    {
        if (rangedWeaponEffects && _player.StickyBulletsActive) enemy.ApplyStickySlow(_player.GetArcaneEffectMultiplier(), _player.GetArcaneEffectMultiplier());
        if (rangedWeaponEffects && _player.TeslaBulletsActive) TriggerTeslaChain(enemy);
        if (poisonDamagePerSecond > 0f) enemy.ApplyPoison(poisonDamagePerSecond, poisonDuration);
        TrackEnemyEffectVisuals(enemy, poisonDamagePerSecond, poisonDuration, rangedWeaponEffects);
    }

    private void ApplyPlayerHitEffects(TurretEnemy enemy, float poisonDamagePerSecond = 0f, float poisonDuration = 0f, bool rangedWeaponEffects = true)
    {
        if (rangedWeaponEffects && _player.StickyBulletsActive) enemy.ApplyStickySlow(_player.GetArcaneEffectMultiplier(), _player.GetArcaneEffectMultiplier());
        if (rangedWeaponEffects && _player.TeslaBulletsActive) TriggerTeslaChain(enemy);
        if (poisonDamagePerSecond > 0f) enemy.ApplyPoison(poisonDamagePerSecond, poisonDuration);
        TrackEnemyEffectVisuals(enemy, poisonDamagePerSecond, poisonDuration, rangedWeaponEffects);
    }

    private void ApplyPlayerHitEffects(MiniBossEnemySquare enemy, float poisonDamagePerSecond = 0f, float poisonDuration = 0f, bool rangedWeaponEffects = true)
    {
        if (rangedWeaponEffects && _player.StickyBulletsActive) enemy.ApplyStickySlow(_player.GetArcaneEffectMultiplier(), _player.GetArcaneEffectMultiplier());
        if (rangedWeaponEffects && _player.TeslaBulletsActive) TriggerTeslaChain(enemy);
        if (poisonDamagePerSecond > 0f) enemy.ApplyPoison(poisonDamagePerSecond, poisonDuration);
        TrackEnemyEffectVisuals(enemy, poisonDamagePerSecond, poisonDuration, rangedWeaponEffects);
    }

    private void ApplyPlayerHitEffects(BossEnemyDestroyer enemy, float poisonDamagePerSecond = 0f, float poisonDuration = 0f, bool rangedWeaponEffects = true)
    {
        if (rangedWeaponEffects && _player.StickyBulletsActive) enemy.ApplyStickySlow(_player.GetArcaneEffectMultiplier(), _player.GetArcaneEffectMultiplier());
        if (rangedWeaponEffects && _player.TeslaBulletsActive) TriggerTeslaChain(enemy);
        if (poisonDamagePerSecond > 0f) enemy.ApplyPoison(poisonDamagePerSecond, poisonDuration);
        TrackEnemyEffectVisuals(enemy, poisonDamagePerSecond, poisonDuration, rangedWeaponEffects);
    }

    private void ApplyPlayerHitEffects(GeneratorGuardianEnemy enemy, float poisonDamagePerSecond = 0f, float poisonDuration = 0f, bool rangedWeaponEffects = true)
    {
        if (rangedWeaponEffects && _player.StickyBulletsActive) enemy.ApplyStickySlow(_player.GetArcaneEffectMultiplier(), _player.GetArcaneEffectMultiplier());
        if (rangedWeaponEffects && _player.TeslaBulletsActive) TriggerTeslaChain(enemy);
        if (poisonDamagePerSecond > 0f) enemy.ApplyPoison(poisonDamagePerSecond, poisonDuration);
        TrackEnemyEffectVisuals(enemy, poisonDamagePerSecond, poisonDuration, rangedWeaponEffects);
    }

    private void ApplyPlayerHitEffects(ToxicTriangleEnemy enemy, float poisonDamagePerSecond = 0f, float poisonDuration = 0f, bool rangedWeaponEffects = true)
    {
        if (rangedWeaponEffects && _player.StickyBulletsActive) enemy.ApplyStickySlow(_player.GetArcaneEffectMultiplier(), _player.GetArcaneEffectMultiplier());
        if (rangedWeaponEffects && _player.TeslaBulletsActive) TriggerTeslaChain(enemy);
        if (poisonDamagePerSecond > 0f) enemy.ApplyPoison(poisonDamagePerSecond, poisonDuration);
        TrackEnemyEffectVisuals(enemy, poisonDamagePerSecond, poisonDuration, rangedWeaponEffects);
    }

    private void ApplyPlayerHitEffects(StationBossEnemy enemy, float poisonDamagePerSecond = 0f, float poisonDuration = 0f, bool rangedWeaponEffects = true)
    {
        if (rangedWeaponEffects && _player.StickyBulletsActive) enemy.ApplyStickySlow(_player.GetArcaneEffectMultiplier(), _player.GetArcaneEffectMultiplier());
        if (rangedWeaponEffects && _player.TeslaBulletsActive) TriggerTeslaChain(enemy);
        if (poisonDamagePerSecond > 0f) enemy.ApplyPoison(poisonDamagePerSecond, poisonDuration);
        TrackEnemyEffectVisuals(enemy, poisonDamagePerSecond, poisonDuration, rangedWeaponEffects);
    }

    private void FreezeBunkerTargetIfNeeded(Projectile projectile, object target)
    {
        if (projectile.Kind == ProjectileKind.FreezeGrenade && IsTargetAlive(target))
            _frozenTargets[target] = GetPlayerFreezeDuration();
    }

    private float GetPlayerFreezeDuration()
        => FreezeZone.FreezeDuration * _player.GetArcaneEffectMultiplier();

    private float GetPlayerChillDuration()
        => FreezeZone.ChillDuration * _player.GetArcaneEffectMultiplier();

    private void TrackEnemyEffectVisuals(object target, float poisonDamagePerSecond, float poisonDuration, bool rangedWeaponEffects)
    {
        if (rangedWeaponEffects && _player.StickyBulletsActive)
            _slowVisualTargets[target] = MathF.Max(_slowVisualTargets.GetValueOrDefault(target), _player.GetArcaneEffectMultiplier());
        if (poisonDamagePerSecond > 0f && poisonDuration > 0f)
            _poisonVisualTargets[target] = MathF.Max(_poisonVisualTargets.GetValueOrDefault(target), poisonDuration);
    }

    private void ApplyBunkerPlayerHitEffects(object target, float poisonDamagePerSecond, float poisonDuration, bool rangedWeaponEffects)
    {
        if (rangedWeaponEffects && _player.StickyBulletsActive)
        {
            var effectMultiplier = _player.GetArcaneEffectMultiplier();
            switch (target)
            {
                case BunkerSiegeEnemy enemy: enemy.ApplyStickySlow(effectMultiplier, effectMultiplier); break;
                case BunkerAssaultEnemy enemy: enemy.ApplyStickySlow(effectMultiplier, effectMultiplier); break;
                case BunkerInfectedEnemy enemy: enemy.ApplyStickySlow(effectMultiplier, effectMultiplier); break;
                case BunkerScrib scrib: scrib.ApplyStickySlow(effectMultiplier, effectMultiplier); break;
                case BunkerParasite parasite: parasite.ApplyStickySlow(effectMultiplier, effectMultiplier); break;
                case BunkerTyrant boss: boss.ApplyStickySlow(effectMultiplier, effectMultiplier); break;
            }
        }

        if (rangedWeaponEffects && _player.TeslaBulletsActive) TriggerTeslaChain(target);

        if (poisonDamagePerSecond > 0f)
        {
            switch (target)
            {
                case BunkerSiegeEnemy enemy: enemy.ApplyPoison(poisonDamagePerSecond, poisonDuration); break;
                case BunkerAssaultEnemy enemy: enemy.ApplyPoison(poisonDamagePerSecond, poisonDuration); break;
                case BunkerInfectedEnemy enemy: enemy.ApplyPoison(poisonDamagePerSecond, poisonDuration); break;
                case BunkerScrib scrib: scrib.ApplyPoison(poisonDamagePerSecond, poisonDuration); break;
                case BunkerParasite parasite: parasite.ApplyPoison(poisonDamagePerSecond, poisonDuration); break;
                case BunkerTyrant boss: boss.ApplyPoison(poisonDamagePerSecond, poisonDuration); break;
            }
        }

        TrackEnemyEffectVisuals(target, poisonDamagePerSecond, poisonDuration, rangedWeaponEffects);
    }

    private void TriggerTeslaChain(object firstTarget)
    {
        const float range = 175f;
        var damage = 15f * _player.GetArcaneEffectMultiplier();
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance) { firstTarget };
        var from = GetTargetPosition(firstTarget);
        if (from is null) return;

        for (var jump = 0; jump < 2; jump++)
        {
            EnemyTarget? next = null;
            var bestDistance = float.MaxValue;
            foreach (var target in QueryCombatTargets(from.Value, range + 56f))
            {
                if (visited.Contains(target.Target)) continue;
                var maxDistance = range + target.Radius;
                var distanceSquared = Vector2.DistanceSquared(target.Position, from.Value);
                if (distanceSquared > maxDistance * maxDistance || distanceSquared >= bestDistance) continue;
                bestDistance = distanceSquared;
                next = target;
            }
            if (next is null) return;

            var nextTarget = next.Value;
            DamageTarget(nextTarget.Target, damage);
            _lightningEffects.Add(new LightningEffect(from.Value, nextTarget.Position));
            visited.Add(nextTarget.Target);
            from = nextTarget.Position;
        }
    }

    private static Vector2? GetTargetPosition(object target)
        => target switch
        {
            Enemy enemy => enemy.Position,
            HexEnemy hex => hex.Position,
            TurretEnemy turret => turret.Position,
            MiniBossEnemySquare boss => boss.Position,
            GeneratorGuardianEnemy guard => guard.Position,
            ToxicTriangleEnemy toxic => toxic.Position,
            GeneratorNode generator => generator.Position,
            ProtectiveDome dome => dome.Position,
            Player player => player.Position,
            StationBossEnemy boss => boss.Position,
            BossEnemyDestroyer boss => boss.Position,
            BunkerParasite parasite => parasite.Position,
            BunkerScrib scrib => scrib.Position,
            BunkerSiegeEnemy enemy => enemy.Position,
            BunkerAssaultEnemy enemy => enemy.Position,
            BunkerInfectedEnemy enemy => enemy.Position,
            BunkerTyrant boss => boss.Position,
            _ => null
        };

    private void DamageTarget(object target, float damage)
    {
        var actualDamage = target is GeneratorNode ? damage : GetDamageAgainstTarget(target, damage);
        var healthBefore = GetTargetHealth(target);
        switch (target)
        {
            case Enemy enemy: enemy.Damage(actualDamage); break;
            case HexEnemy hex: hex.Damage(actualDamage); break;
            case TurretEnemy turret: turret.Damage(actualDamage); break;
            case MiniBossEnemySquare boss: boss.Damage(actualDamage); break;
            case GeneratorGuardianEnemy guard: guard.Damage(actualDamage); break;
            case ToxicTriangleEnemy toxic: toxic.Damage(actualDamage); break;
            case StationBossEnemy boss:
                if (!boss.Active)
                {
                    AddImmuneText(boss);
                    return;
                }
                boss.Damage(actualDamage);
                break;
            case BossEnemyDestroyer boss: boss.Damage(actualDamage); break;
            case GeneratorNode generator:
                if (!generator.Vulnerable)
                {
                    AddImmuneText(generator);
                    return;
                }
                generator.Damage(actualDamage);
                break;
            case BunkerParasite parasite: parasite.Damage(actualDamage); break;
            case BunkerScrib scrib:
                scrib.ForceAggro(_player.Position);
                if (scrib.Damage(actualDamage)) ExplodeBunkerScrib(scrib.Position);
                break;
            case BunkerTyrant boss:
                if (!boss.Damage(actualDamage)) return;
                break;
            case BunkerSiegeEnemy enemy: enemy.ForceAggro(_player.Position); enemy.Damage(actualDamage); break;
            case BunkerAssaultEnemy enemy: enemy.ForceAggro(_player.Position); enemy.Damage(actualDamage); break;
            case BunkerInfectedEnemy enemy: enemy.ForceAggro(_player.Position); enemy.Damage(actualDamage); break;
        }

        if (healthBefore is not null)
            AddDamageTextForHealthLoss(target, healthBefore.Value, target is BossEnemyDestroyer or StationBossEnemy);
        else AddDamageText(target, actualDamage);
    }

    private void AddDamageTextForHealthLoss(object target, float healthBefore, bool showImmuneOnNoLoss = false)
    {
        var healthAfter = GetTargetHealth(target);
        if (healthAfter is null) return;

        var actualLoss = healthBefore - healthAfter.Value;
        if (actualLoss > 0.01f) AddDamageText(target, actualLoss);
        else if (showImmuneOnNoLoss) AddImmuneText(target);
    }

    private static float? GetTargetHealth(object target)
        => target switch
        {
            Enemy enemy => enemy.Health,
            HexEnemy hex => hex.Health,
            TurretEnemy turret => turret.Health,
            MiniBossEnemySquare boss => boss.Health,
            GeneratorGuardianEnemy guard => guard.Health,
            ToxicTriangleEnemy toxic => toxic.Health,
            GeneratorNode generator => generator.Health,
            StationBossEnemy boss => boss.Health,
            BossEnemyDestroyer boss => boss.Health,
            BunkerParasite parasite => parasite.Health,
            BunkerScrib scrib => scrib.Health,
            BunkerTyrant boss => boss.Health,
            BunkerSiegeEnemy enemy => enemy.Health,
            BunkerAssaultEnemy enemy => enemy.Health,
            BunkerInfectedEnemy enemy => enemy.Health,
            ProtectiveDome dome => dome.Health,
            Player player => player.Health,
            _ => null
        };

    private void UpdateProtectiveDomes(float dt)
    {
        for (var i = _protectiveDomes.Count - 1; i >= 0; i--)
        {
            var dome = _protectiveDomes[i];
            dome.Update(dt);

            foreach (var enemy in _enemies.Where(e => e.Alive && Vector2.Distance(e.Position, dome.Position) <= ProtectiveDome.Radius + 14f))
            {
                var damage = enemy.IsStrong ? 18f : 10f;
                var healthBefore = dome.Health;
                if (dome.TryApplyContactDamage(enemy, damage, enemy.IsStrong ? 1.3f : 0.9f)) AddDamageTextForHealthLoss(dome, healthBefore);
            }

            foreach (var hex in _hexEnemies.Where(h => h.Alive && Vector2.Distance(h.Position, dome.Position) <= ProtectiveDome.Radius + 16f))
            {
                var healthBefore = dome.Health;
                if (dome.TryApplyContactDamage(hex, 10f, 0.9f)) AddDamageTextForHealthLoss(dome, healthBefore);
            }

            foreach (var boss in _miniBosses.Where(b => b.Alive && Vector2.Distance(b.Position, dome.Position) <= ProtectiveDome.Radius + 28f))
            {
                var healthBefore = dome.Health;
                if (dome.TryApplyContactDamage(boss, 20f, 0.8f)) AddDamageTextForHealthLoss(dome, healthBefore);
            }

            foreach (var guard in _generatorGuards.Where(g => g.Alive && Vector2.Distance(g.Position, dome.Position) <= ProtectiveDome.Radius + 18f))
            {
                var healthBefore = dome.Health;
                if (dome.TryApplyContactDamage(guard, 18f, 0.8f)) AddDamageTextForHealthLoss(dome, healthBefore);
            }

            foreach (var toxic in _toxicEnemies.Where(t => t.Alive && Vector2.Distance(t.Position, dome.Position) <= ProtectiveDome.Radius + 16f))
            {
                var healthBefore = dome.Health;
                if (dome.TryApplyContactDamage(toxic, 10f, 0.9f)) AddDamageTextForHealthLoss(dome, healthBefore);
            }

            if (_destroyerBoss is not null && _destroyerBoss.Alive && Vector2.Distance(_destroyerBoss.Position, dome.Position) <= ProtectiveDome.Radius + 52f)
            {
                var healthBefore = dome.Health;
                if (dome.TryApplyContactDamage(_destroyerBoss, 22f, 0.8f)) AddDamageTextForHealthLoss(dome, healthBefore);
            }

            if (_stationBoss is not null && _stationBoss.Alive && Vector2.Distance(_stationBoss.Position, dome.Position) <= ProtectiveDome.Radius + 34f)
            {
                var healthBefore = dome.Health;
                if (dome.TryApplyContactDamage(_stationBoss, 22f, 0.8f)) AddDamageTextForHealthLoss(dome, healthBefore);
            }

            if (dome.Alive) continue;
            _protectiveDomes.RemoveAt(i);
        }
    }

    private void UpdateFreezeZones(float dt)
    {
        var frozenKeys = _frozenTargets.Keys.ToArray();
        foreach (var target in frozenKeys)
        {
            if (!IsTargetAlive(target))
            {
                _frozenTargets.Remove(target);
                continue;
            }

            var left = _frozenTargets[target] - dt;
            if (left <= 0f)
            {
                _frozenTargets.Remove(target);
                _chilledTargets[target] = GetPlayerChillDuration();
            }
            else _frozenTargets[target] = left;
        }

        for (var i = _freezeZones.Count - 1; i >= 0; i--)
        {
            var zone = _freezeZones[i];
            zone.Update(dt);
            if (zone.Alive) SpawnFreezeAmbientParticles(zone.Position, FreezeZone.Radius, dt);
            foreach (var target in QueryCombatTargets(zone.Position, FreezeZone.Radius + 56f))
            {
                if (_frozenTargets.ContainsKey(target.Target)) continue;
                if (zone.Freezing && zone.Contains(target.Position, target.Radius)) _chilledTargets[target.Target] = zone.ChillTime;
            }

            if (!zone.Alive) _freezeZones.RemoveAt(i);
        }

        var chilledKeys = _chilledTargets.Keys.ToArray();
        foreach (var target in chilledKeys)
        {
            if (!IsTargetAlive(target))
            {
                _chilledTargets.Remove(target);
                continue;
            }

            ApplyFreezeChillToTarget(target, 0.12f, _player.GetArcaneEffectMultiplier());
            var left = _chilledTargets[target] - dt;
            if (left <= 0f) _chilledTargets.Remove(target);
            else _chilledTargets[target] = left;
        }
    }

    private void UpdateMidaMiniTurrets(float dt)
    {
        for (var i = _midaMiniTurrets.Count - 1; i >= 0; i--)
        {
            var turret = _midaMiniTurrets[i];
            turret.Update(dt);
            if (!turret.Alive)
            {
                _midaMiniTurrets.RemoveAt(i);
                continue;
            }

            EnemyTarget? target = null;
            var bestDistance = float.MaxValue;
            foreach (var candidate in QueryCombatTargets(turret.Position, MidaMiniTurret.Range + 56f))
            {
                var maxDistance = MidaMiniTurret.Range + candidate.Radius;
                var distanceSquared = Vector2.DistanceSquared(candidate.Position, turret.Position);
                if (distanceSquared > maxDistance * maxDistance || distanceSquared >= bestDistance) continue;
                bestDistance = distanceSquared;
                target = candidate;
            }
            if (target is null) continue;

            var targetValue = target.Value;
            _beamEffects.Add(new BeamEffect(turret.Position, targetValue.Position, Palette.C(255, 60, 60), 0.045f, 1.4f, false));
            if (!turret.ReadyToShoot) continue;

            var dir = targetValue.Position - turret.Position;
            if (dir.LengthSquared() <= 0.001f) dir = new Vector2(1f, 0f);
            dir = Vector2.Normalize(dir);
            _projectiles.Add(new Projectile(
                turret.Position + dir * 14f,
                dir,
                1500f,
                MathF.Max(0.08f, (Vector2.Distance(turret.Position, targetValue.Position) + 60f) / 1500f),
                Palette.C(255, 210, 90),
                false,
                MidaMiniTurret.Damage,
                ProjectileKind.Bullet,
                drawRadius: 3f,
                highlighted: true,
                sourcePosition: turret.Position));
            turret.MarkShot();
        }
    }

    private bool IsFrozenTarget(object target)
        => IsTargetAlive(target) && _frozenTargets.TryGetValue(target, out var left) && left > 0f;

    private static bool IsTargetAlive(object target)
        => target switch
        {
            Enemy enemy => enemy.Alive,
            HexEnemy hex => hex.Alive,
            TurretEnemy turret => turret.Alive,
            MiniBossEnemySquare boss => boss.Alive,
            GeneratorGuardianEnemy guard => guard.Alive,
            ToxicTriangleEnemy toxic => toxic.Alive,
            StationBossEnemy boss => boss.Alive,
            BossEnemyDestroyer boss => boss.Alive,
            BunkerParasite parasite => parasite.Alive,
            BunkerScrib scrib => scrib.Alive,
            BunkerTyrant boss => boss.Alive,
            BunkerSiegeEnemy enemy => enemy.Alive,
            BunkerAssaultEnemy enemy => enemy.Alive,
            BunkerInfectedEnemy enemy => enemy.Alive,
            _ => false
        };

    private static void ApplyFreezeChillToTarget(object target, float duration, float strengthMultiplier)
    {
        switch (target)
        {
            case Enemy enemy: enemy.ApplyFreezeChill(duration, strengthMultiplier); break;
            case HexEnemy hex: hex.ApplyFreezeChill(duration, strengthMultiplier); break;
            case TurretEnemy turret: turret.ApplyFreezeChill(duration, strengthMultiplier); break;
            case MiniBossEnemySquare boss: boss.ApplyFreezeChill(duration, strengthMultiplier); break;
            case GeneratorGuardianEnemy guard: guard.ApplyFreezeChill(duration, strengthMultiplier); break;
            case ToxicTriangleEnemy toxic: toxic.ApplyFreezeChill(duration, strengthMultiplier); break;
            case StationBossEnemy boss: boss.ApplyFreezeChill(duration, strengthMultiplier); break;
            case BossEnemyDestroyer boss: boss.ApplyFreezeChill(duration, strengthMultiplier); break;
            case BunkerSiegeEnemy enemy: enemy.ApplyFreezeChill(duration, strengthMultiplier); break;
            case BunkerAssaultEnemy enemy: enemy.ApplyFreezeChill(duration, strengthMultiplier); break;
            case BunkerInfectedEnemy enemy: enemy.ApplyFreezeChill(duration, strengthMultiplier); break;
            case BunkerScrib scrib: scrib.ApplyFreezeChill(duration, strengthMultiplier); break;
            case BunkerParasite parasite: parasite.ApplyFreezeChill(duration, strengthMultiplier); break;
        }
    }

    private void UpdateSwings(float dt)
    {
        for (var i = _swings.Count - 1; i >= 0; i--)
        {
            var s = _swings[i];
            s.UpdateAnchor(_player.Position);
            s.Life -= dt;
            if (s.Life <= 0f)
            {
                _swings.RemoveAt(i);
                continue;
            }

            if (_inBunker)
            {
                foreach (var parasite in _bunkerParasites.Where(target => target.Alive))
                {
                    var hit = s.IsLine
                        ? DistanceToSegment(parasite.Position, s.LineStart, s.LineEnd) < 12f
                        : IsInArc(parasite.Position, s, 8f);
                    if (hit && s.TryRegisterHit(parasite))
                    {
                        var actualDamage = GetDamageAgainstTarget(parasite, _player.GetMeleeDamage());
                        var healthBefore = parasite.Health;
                        parasite.Damage(actualDamage);
                        AddDamageTextForHealthLoss(parasite, healthBefore);
                    }
                }

                foreach (var scrib in _bunkerScribs.Where(target => target.Alive))
                {
                    var hit = s.IsLine
                        ? DistanceToSegment(scrib.Position, s.LineStart, s.LineEnd) < BunkerScrib.Radius + 4f
                        : IsInArc(scrib.Position, s, BunkerScrib.Radius);
                    if (hit && s.TryRegisterHit(scrib))
                    {
                        var actualDamage = GetDamageAgainstTarget(scrib, _player.GetMeleeDamage());
                        var healthBefore = scrib.Health;
                        if (scrib.Damage(actualDamage)) ExplodeBunkerScrib(scrib.Position);
                        AddDamageTextForHealthLoss(scrib, healthBefore);
                    }
                    if (hit) scrib.ForceAggro(_player.Position);
                }

                foreach (var enemy in _bunkerSiegeEnemies.Where(target => target.Alive))
                {
                    var hit = s.IsLine
                        ? enemy.IntersectsSegment(s.LineStart, s.LineEnd, 4f)
                        : IsInArc(enemy.Position, s, BunkerSiegeEnemy.CollisionRadius);
                    if (hit && s.TryRegisterHit(enemy))
                    {
                        enemy.ForceAggro(_player.Position);
                        var actualDamage = GetDamageAgainstTarget(enemy, _player.GetMeleeDamage());
                        var healthBefore = enemy.Health;
                        enemy.Damage(actualDamage);
                        AddDamageTextForHealthLoss(enemy, healthBefore);
                    }
                }

                foreach (var enemy in _bunkerAssaultEnemies.Where(target => target.Alive))
                {
                    var hit = s.IsLine
                        ? DistanceToSegment(enemy.Position, s.LineStart, s.LineEnd) < BunkerAssaultEnemy.Radius + 4f
                        : IsInArc(enemy.Position, s, BunkerAssaultEnemy.Radius);
                    if (hit && s.TryRegisterHit(enemy))
                    {
                        enemy.ForceAggro(_player.Position);
                        var actualDamage = GetDamageAgainstTarget(enemy, _player.GetMeleeDamage());
                        var healthBefore = enemy.Health;
                        enemy.Damage(actualDamage);
                        AddDamageTextForHealthLoss(enemy, healthBefore);
                    }
                }

                foreach (var enemy in _bunkerInfectedEnemies.Where(target => target.Alive))
                {
                    var hit = s.IsLine
                        ? DistanceToSegment(enemy.Position, s.LineStart, s.LineEnd) < BunkerInfectedEnemy.Radius + 4f
                        : IsInArc(enemy.Position, s, BunkerInfectedEnemy.Radius);
                    if (hit && s.TryRegisterHit(enemy))
                    {
                        enemy.ForceAggro(_player.Position);
                        var actualDamage = GetDamageAgainstTarget(enemy, _player.GetMeleeDamage());
                        var healthBefore = enemy.Health;
                        enemy.Damage(actualDamage);
                        AddDamageTextForHealthLoss(enemy, healthBefore);
                    }
                }

                if (_bunkerTyrant is not null && _bunkerTyrant.Alive)
                {
                    var hit = s.IsLine
                        ? DistanceToSegment(_bunkerTyrant.Position, s.LineStart, s.LineEnd) < BunkerTyrant.Radius + 4f
                        : IsInArc(_bunkerTyrant.Position, s, BunkerTyrant.Radius);
                    if (hit && s.TryRegisterHit(_bunkerTyrant))
                    {
                        var actualDamage = GetDamageAgainstTarget(_bunkerTyrant, _player.GetMeleeDamage() * 0.75f);
                        var healthBefore = _bunkerTyrant.Health;
                        if (_bunkerTyrant.Damage(actualDamage)) AddDamageTextForHealthLoss(_bunkerTyrant, healthBefore);
                        else AddImmuneText(_bunkerTyrant);
                    }
                }
                continue;
            }

            foreach (var e in _enemies.Where(x => x.Alive))
            {
                var hit = s.IsLine
                    ? DistanceToSegment(e.Position, s.LineStart, s.LineEnd) < 16f
                    : IsInArc(e.Position, s, 8f);
                if (!hit || !s.TryRegisterHit(e)) continue;
                var actualDamage = GetDamageAgainstTarget(e, _player.GetMeleeDamage());
                var healthBefore = e.Health;
                e.Damage(actualDamage);
                AddDamageTextForHealthLoss(e, healthBefore);
                ApplyPlayerHitEffects(e, rangedWeaponEffects: false);
                e.ForceAggro(_player.Position);
                AggroWitnesses(e.Position, true);
            }

            foreach (var h in _hexEnemies.Where(x => x.Alive))
            {
                var hit = s.IsLine
                    ? DistanceToSegment(h.Position, s.LineStart, s.LineEnd) < 16f
                    : IsInArc(h.Position, s, 10f);
                if (hit && s.TryRegisterHit(h))
                {
                    var actualDamage = GetDamageAgainstTarget(h, _player.GetMeleeDamage());
                    var healthBefore = h.Health;
                    h.Damage(actualDamage);
                    AddDamageTextForHealthLoss(h, healthBefore);
                    ApplyPlayerHitEffects(h, rangedWeaponEffects: false);
                    AggroWitnesses(h.Position, true);
                }
            }

            foreach (var t in _turrets.Where(x => x.Alive))
            {
                var hit = s.IsLine
                    ? DistanceToSegment(t.Position, s.LineStart, s.LineEnd) < 20f
                    : IsInArc(t.Position, s, 14f);
                if (hit && s.TryRegisterHit(t))
                {
                    var actualDamage = GetDamageAgainstTarget(t, _player.GetMeleeDamage());
                    var healthBefore = t.Health;
                    t.Damage(actualDamage);
                    AddDamageTextForHealthLoss(t, healthBefore);
                    ApplyPlayerHitEffects(t, rangedWeaponEffects: false);
                    t.ForceAggro(_player.Position);
                    AggroWitnesses(t.Position, true);
                }
            }

            foreach (var b in _miniBosses.Where(x => x.Alive))
            {
                var hit = s.IsLine
                    ? DistanceToSegment(b.Position, s.LineStart, s.LineEnd) < 28f
                    : IsInArc(b.Position, s, 24f);
                if (hit && s.TryRegisterHit(b))
                {
                    var actualDamage = GetDamageAgainstTarget(b, _player.GetMeleeDamage() * 0.75f);
                    var healthBefore = b.Health;
                    b.Damage(actualDamage);
                    AddDamageTextForHealthLoss(b, healthBefore);
                    ApplyPlayerHitEffects(b, rangedWeaponEffects: false);
                    b.ForceAggro(_player.Position);
                    AggroWitnesses(b.Position, true);
                }
            }

            foreach (var g in _generatorGuards.Where(x => x.Alive))
            {
                var hit = s.IsLine
                    ? DistanceToSegment(g.Position, s.LineStart, s.LineEnd) < 20f
                    : IsInArc(g.Position, s, 14f);
                if (hit && s.TryRegisterHit(g))
                {
                    var actualDamage = GetDamageAgainstTarget(g, _player.GetMeleeDamage());
                    var healthBefore = g.Health;
                    g.Damage(actualDamage);
                    AddDamageTextForHealthLoss(g, healthBefore);
                    ApplyPlayerHitEffects(g, rangedWeaponEffects: false);
                    g.ForceAggro(_player.Position);
                    AggroWitnesses(g.Position, true);
                }
            }

            foreach (var toxic in _toxicEnemies.Where(x => x.Alive))
            {
                var hit = s.IsLine
                    ? DistanceToSegment(toxic.Position, s.LineStart, s.LineEnd) < 18f
                    : IsInArc(toxic.Position, s, 12f);
                if (hit && s.TryRegisterHit(toxic))
                {
                    var actualDamage = GetDamageAgainstTarget(toxic, _player.GetMeleeDamage());
                    var healthBefore = toxic.Health;
                    toxic.Damage(actualDamage);
                    AddDamageTextForHealthLoss(toxic, healthBefore);
                    ApplyPlayerHitEffects(toxic, rangedWeaponEffects: false);
                    toxic.ForceAggro(_player.Position);
                    AggroWitnesses(toxic.Position, true);
                }
            }

            foreach (var generator in _generators.Where(x => !x.Destroyed))
            {
                var hit = s.IsLine
                    ? DistanceToSegment(generator.Position, s.LineStart, s.LineEnd) < 30f
                    : IsInArc(generator.Position, s, 24f);
                if (hit && s.TryRegisterHit(generator))
                {
                    if (!generator.Vulnerable)
                    {
                        AddImmuneText(generator);
                        continue;
                    }
                    var actualDamage = _player.GetMeleeDamage();
                    var healthBefore = generator.Health;
                    generator.Damage(actualDamage);
                    AddDamageTextForHealthLoss(generator, healthBefore);
                }
            }

            if (_stationBoss is not null && _stationBoss.Alive)
            {
                var hit = s.IsLine
                    ? DistanceToSegment(_stationBoss.Position, s.LineStart, s.LineEnd) < 36f
                    : IsInArc(_stationBoss.Position, s, 30f);
                if (hit && s.TryRegisterHit(_stationBoss))
                {
                    if (!_stationBoss.Active)
                    {
                        AddImmuneText(_stationBoss);
                        continue;
                    }
                    var actualDamage = GetDamageAgainstTarget(_stationBoss, _player.GetMeleeDamage() * 0.75f);
                    var healthBefore = _stationBoss.Health;
                    _stationBoss.Damage(actualDamage);
                    AddDamageTextForHealthLoss(_stationBoss, healthBefore);
                    ApplyPlayerHitEffects(_stationBoss, rangedWeaponEffects: false);
                    AggroWitnesses(_stationBoss.Position, true);
                }
            }

            if (_destroyerBoss is not null && _destroyerBoss.Alive)
            {
                var hit = s.IsLine
                    ? DistanceToSegment(_destroyerBoss.Position, s.LineStart, s.LineEnd) < 54f
                    : IsInArc(_destroyerBoss.Position, s, 50f);
                if (hit && s.TryRegisterHit(_destroyerBoss))
                {
                    var actualDamage = GetDamageAgainstTarget(_destroyerBoss, _player.GetMeleeDamage() * 0.75f);
                    var appliedDamage = 0f;
                    if (s.IsLine) _destroyerBoss.TryApplySegmentDamage(s.LineStart, s.LineEnd, 4f, actualDamage, out appliedDamage);
                    else
                    {
                        var healthBefore = _destroyerBoss.Health;
                        _destroyerBoss.Damage(actualDamage);
                        appliedDamage = healthBefore - _destroyerBoss.Health;
                    }
                    if (appliedDamage > 0.01f) AddDamageText(_destroyerBoss, appliedDamage);
                    else AddImmuneText(_destroyerBoss);
                    ApplyPlayerHitEffects(_destroyerBoss, rangedWeaponEffects: false);
                    _destroyerBoss.ForceAggro(_player.Position);
                    AggroWitnesses(_destroyerBoss.Position, true);
                }
            }
        }
    }

    private void AggroWitnesses(Vector2 impactPosition, bool targetAggroed)
    {
        if (!targetAggroed) return;

        foreach (var enemy in _enemies.Where(x => x.Alive))
        {
            if (enemy.CanNoticeCombatPoint(impactPosition, _obstacles)) enemy.ForceAggro(_player.Position);
        }

        foreach (var turret in _turrets.Where(x => x.Alive))
        {
            if (turret.CanNoticeCombatPoint(impactPosition, _obstacles)) turret.ForceAggro(_player.Position);
        }

        foreach (var miniBoss in _miniBosses.Where(x => x.Alive))
        {
            if (miniBoss.CanNoticeCombatPoint(impactPosition, _obstacles)) miniBoss.ForceAggro(_player.Position);
        }

        foreach (var toxic in _toxicEnemies.Where(x => x.Alive))
        {
            if (toxic.CanNoticeCombatPoint(impactPosition, _obstacles)) toxic.ForceAggro(_player.Position);
        }

        if (_destroyerBoss is not null && _destroyerBoss.Alive && _destroyerBoss.CanSeePoint(impactPosition, _obstacles))
        {
            _destroyerBoss.ForceAggro(_player.Position);
        }
    }

    private void UpdateEffects(float dt)
    {
        foreach (var target in _radioactiveDecompositionTargets.Keys.ToArray())
        {
            var remaining = _radioactiveDecompositionTargets[target] - dt;
            if (remaining <= 0f || !IsTargetAlive(target))
            {
                _radioactiveDecompositionTargets.Remove(target);
                _radioactiveDecompositionDamageMultipliers.Remove(target);
            }
            else _radioactiveDecompositionTargets[target] = remaining;
        }
        UpdateEnemyVisualEffectTimers(_poisonVisualTargets, dt);
        UpdateEnemyVisualEffectTimers(_slowVisualTargets, dt);

        _player.TickEffects(dt);
        for (var i = _explosions.Count - 1; i >= 0; i--)
        {
            _explosions[i].Life -= dt;
            if (_explosions[i].Life <= 0) ReleaseExplosionAt(i);
        }

        for (var i = _beamEffects.Count - 1; i >= 0; i--)
        {
            _beamEffects[i].Life -= dt;
            if (_beamEffects[i].Life <= 0) _beamEffects.RemoveAt(i);
        }

        for (var i = _lightningEffects.Count - 1; i >= 0; i--)
        {
            _lightningEffects[i].Life -= dt;
            if (!_lightningEffects[i].Alive) _lightningEffects.RemoveAt(i);
        }

        for (var i = _dashAfterImages.Count - 1; i >= 0; i--)
        {
            _dashAfterImages[i].Life -= dt * 3.75f;
            if (_dashAfterImages[i].Life <= 0f) _dashAfterImages.RemoveAt(i);
        }

        for (var i = _motionAfterImages.Count - 1; i >= 0; i--)
        {
            _motionAfterImages[i].Life -= dt * 7.5f;
            if (_motionAfterImages[i].Life <= 0f) _motionAfterImages.RemoveAt(i);
        }

        for (var i = _visualParticles.Count - 1; i >= 0; i--)
        {
            _visualParticles[i].Update(dt);
            if (!_visualParticles[i].Alive) ReleaseVisualParticleAt(i);
        }

        for (var i = _floatingCombatTexts.Count - 1; i >= 0; i--)
        {
            _floatingCombatTexts[i].Update(dt);
            if (!_floatingCombatTexts[i].Alive) ReleaseFloatingCombatTextAt(i);
        }

        if (_screenShakeTimer > 0f)
        {
            _screenShakeTimer -= dt;
            if (_screenShakeTimer <= 0f)
            {
                _screenShakeTimer = 0f;
                _screenShakeDuration = 0f;
                _screenShakeStrength = 0f;
            }
        }
    }

    private void AddMotionTrail(Vector2 previous, Vector2 current, Color color, float radius, MotionTrailShape shape, float alpha = 0.16f, float minRadius = -1f, bool rotateWithMovement = true)
    {
        var delta = current - previous;
        if (delta.LengthSquared() < 0.25f) return;

        alpha *= GetVisualEffectsMultiplier();
        if (alpha <= 0.035f) return;

        var sizeMultiplier = GetVisualEffectsSizeMultiplier();
        radius *= sizeMultiplier;
        if (minRadius >= 0f) minRadius *= sizeMultiplier;

        var dir = Vector2.Normalize(delta);
        var trailPosition = current - dir * MathF.Min(radius * 0.75f, delta.Length() * 0.5f);
        var rotation = rotateWithMovement ? MathF.Atan2(dir.Y, dir.X) * 180f / MathF.PI : 0f;
        _motionAfterImages.Add(new MotionAfterImage(trailPosition, color, alpha, radius, shape, rotation, minRadius));

        var maxMotionAfterImages = _visualEffectsIntensity switch
        {
            VisualEffectsIntensity.Low => 240,
            VisualEffectsIntensity.High => 760,
            _ => 520
        };
        if (_motionAfterImages.Count > maxMotionAfterImages)
        {
            _motionAfterImages.RemoveRange(0, _motionAfterImages.Count - maxMotionAfterImages);
        }
    }

}
