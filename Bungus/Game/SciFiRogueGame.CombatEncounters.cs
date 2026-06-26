using System.Numerics;
using System.Text.Json;
using Raylib_cs;

namespace Bungus.Game;

public sealed partial class SciFiRogueGame : IDisposable
{
    private void StartRunIntro()
    {
        _runIntroTimer = RunIntroDuration;
    }

    private bool UpdateRunIntro(float dt)
    {
        if (_runIntroTimer <= 0f) return false;

        _runIntroTimer = MathF.Max(0f, _runIntroTimer - dt);
        return _runIntroTimer > RunIntroDuration - RunIntroFadeInDuration;
    }

    private float GetRunIntroAlpha()
    {
        if (_runIntroTimer <= 0f) return 0f;

        var elapsed = RunIntroDuration - _runIntroTimer;
        if (elapsed < RunIntroFadeInDuration) return Math.Clamp(elapsed / RunIntroFadeInDuration, 0f, 1f);

        var fadeOutStart = RunIntroFadeInDuration + RunIntroHoldDuration;
        if (elapsed < fadeOutStart) return 1f;

        return Math.Clamp(1f - (elapsed - fadeOutStart) / RunIntroFadeOutDuration, 0f, 1f);
    }

    private bool ShouldHideWorldForRunIntro()
        => _runIntroTimer > RunIntroFadeOutDuration;

    private bool IsPlayerInvisibleForRunIntro()
        => _runIntroTimer > 0f;

    private Vector2 GetEnemyPlayerTarget()
        => IsPlayerInvisibleForRunIntro() ? new Vector2(-100000f, -100000f) : _player.Position;

    private Vector2 GetDesiredCameraTarget(Vector2 mouseWorld)
    {
        var toCursor = mouseWorld - _player.Position;
        if (toCursor.LengthSquared() <= 0.001f) return _player.Position;

        var dir = Vector2.Normalize(toCursor);
        if (!_player.IsSniperEquipped || _player.InventoryOpen)
        {
            var mouseScreen = GetUiMousePosition();
            var screenDelta = mouseScreen - GetUiScreenCenter();
            var screenDistance = screenDelta.Length();
            if (screenDistance <= 0.001f) return _player.Position;

            var screenDir = screenDelta / screenDistance;
            var maxScreenDistance = GetDistanceFromCenterToScreenEdge(screenDir);
            var offsetRatio = Math.Clamp(screenDistance / MathF.Max(maxScreenDistance, 0.001f), 0f, 1f);
            return _player.Position + dir * (50f * offsetRatio);
        }

        var desiredOffset = toCursor * 0.5f;
        var maxOffset = GetMaxSniperCameraOffset(dir);
        if (desiredOffset.Length() > maxOffset) desiredOffset = dir * maxOffset;
        return _player.Position + desiredOffset;
    }

    private static float GetDistanceFromCenterToScreenEdge(Vector2 dir)
    {
        var halfWidth = GetUiScreenWidth() * 0.5f;
        var halfHeight = GetUiScreenHeight() * 0.5f;
        var xLimit = MathF.Abs(dir.X) < 0.001f ? float.PositiveInfinity : halfWidth / MathF.Abs(dir.X);
        var yLimit = MathF.Abs(dir.Y) < 0.001f ? float.PositiveInfinity : halfHeight / MathF.Abs(dir.Y);
        return MathF.Min(xLimit, yLimit);
    }

    private float GetMaxSniperCameraOffset(Vector2 dir)
    {
        var distanceFromCenterToEdge = GetDistanceFromCenterToScreenEdge(dir);
        return distanceFromCenterToEdge * 0.5f / MathF.Max(_camera.Zoom, 0.001f);
    }

    private void UpdateMapWindow()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            _mapOpen = false;
            return;
        }

        if (_inBunker)
        {
            UpdateBunkerMapWindow();
            return;
        }

        var mapRect = GetMapRect();
        var mouse = GetUiMousePosition();
        if (!Raylib.CheckCollisionPointRec(mouse, mapRect)) return;

        if (Raylib.IsMouseButtonPressed(MouseButton.Right) && _mapMarker is Vector2 marker)
        {
            var markerScreen = WorldToMap(marker, mapRect);
            if (Vector2.Distance(markerScreen, mouse) <= 22f) _mapMarker = null;
            return;
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            _mapMarker = MapToWorld(mouse, mapRect);
        }
    }

    private void UpdateBunkerMapWindow()
    {
        var mapRect = GetMapRect();
        var mouse = GetUiMousePosition();
        if (!Raylib.CheckCollisionPointRec(mouse, mapRect)) return;

        if (Raylib.IsMouseButtonPressed(MouseButton.Right) && _bunkerMapMarker is Vector2 marker)
        {
            var markerScreen = WorldToMap(marker, mapRect);
            if (Vector2.Distance(markerScreen, mouse) <= 22f) _bunkerMapMarker = null;
            return;
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            _bunkerMapMarker = MapToWorld(mouse, mapRect);
        }
    }

    private float NextHexSpawnDelay()
        => _lastChanceActive
            ? 5f + _rng.NextSingle() * 10f
            : 80f + _rng.NextSingle() * 160f;

    private void UpdateEnemies(float dt, List<Obstacle> enemyCollisionObstacles, Vector2 playerTarget)
    {
        var playerInvisible = IsPlayerInvisibleForRunIntro();
        foreach (var e in _enemies)
        {
            var previousPosition = e.Position;
            if (IsFrozenTarget(e)) continue;
            e.UpdateVisionSweep(dt);
            if (_challengeMode && !playerInvisible) e.ForceAggro(playerTarget);
            else e.UpdateAwareness(playerTarget, dt, enemyCollisionObstacles);
            e.UpdateMovement(dt, playerTarget, enemyCollisionObstacles, _worldSize);
            AddMotionTrail(previousPosition, e.Position, e.IsStrong ? Theme.EnemyStrong : Theme.Enemy, e.IsStrong ? 11f : 12f, e.IsStrong ? MotionTrailShape.Triangle : MotionTrailShape.Circle);
            if (!playerInvisible) e.TryShootBurst(playerTarget, _projectiles);

            if (!playerInvisible && e.TryMeleeHit(_player) && _rng.NextSingle() <= 0.05f)
            {
                _player.ApplyBleed(3f);
            }

            if (!e.Alive && !e.KillAwarded)
            {
                e.KillAwarded = true;
                if (_challengeMode)
                {
                    HandlePitEnemyDeath(e);
                    continue;
                }
                TryDropEnemyCache(e.Position);
                _player.RegisterKill(e.IsStrong ? 2 : 1);
                AddRunScore(e.IsStrong ? 20 : 10);
            }
        }

        if (_challengeMode) return;

        _nextHexSpawnTimer -= dt;
        if (_nextHexSpawnTimer <= 0f)
        {
            var packSize = _rng.Next(1, 6);
            for (var i = 0; i < packSize; i++)
            {
                _hexEnemies.Add(HexEnemy.Create(RandomMapPointSafe(16f), _rng));
            }
            _nextHexSpawnTimer = NextHexSpawnDelay();
        }
    }

    private void UpdateHexEnemies(float dt, List<Obstacle> enemyCollisionObstacles, Vector2 playerTarget)
    {
        foreach (var h in _hexEnemies)
        {
            var previousPosition = h.Position;
            if (IsFrozenTarget(h)) continue;
            h.Update(dt, playerTarget, _projectiles, enemyCollisionObstacles, _worldSize);
            AddMotionTrail(previousPosition, h.Position, Palette.C(255, 110, 180), 17f, MotionTrailShape.Hex, 0.08f);
            if (!h.Alive && !h.KillAwarded)
            {
                h.KillAwarded = true;
                if (_challengeMode)
                {
                    HandlePitEnemyDeath(h);
                    continue;
                }
                TryDropEnemyCache(h.Position);
                _player.RegisterKill(2);
                AddRunScore(25);
            }
        }
    }

    private void UpdateTurrets(float dt, List<Obstacle> enemyCollisionObstacles, Vector2 playerTarget)
    {
        var playerInvisible = IsPlayerInvisibleForRunIntro();
        foreach (var turret in _turrets)
        {
            var previousPosition = turret.Position;
            if (IsFrozenTarget(turret)) continue;
            turret.Update(dt, playerTarget, _projectiles, enemyCollisionObstacles, _challengeMode && !playerInvisible);
            AddMotionTrail(previousPosition, turret.Position, Palette.C(230, 80, 80), 15f, MotionTrailShape.Square, rotateWithMovement: false);
            if (!turret.Alive && !turret.KillAwarded)
            {
                turret.KillAwarded = true;
                if (_challengeMode)
                {
                    HandlePitEnemyDeath(turret);
                    continue;
                }
                TryDropEnemyCache(turret.Position);
                _player.RegisterKill(2);
                AddRunScore(20);
            }
        }
    }

    private void UpdateMiniBosses(float dt, List<Obstacle> enemyCollisionObstacles, Vector2 playerTarget)
    {
        var playerInvisible = IsPlayerInvisibleForRunIntro();
        foreach (var b in _miniBosses)
        {
            var previousPosition = b.Position;
            if (IsFrozenTarget(b)) continue;
            b.Update(dt, playerTarget, _projectiles, _player, enemyCollisionObstacles, _worldSize, _dashAfterImages, _challengeMode && !playerInvisible);
            AddMotionTrail(previousPosition, b.Position, Palette.C(230, 100, 100), 21f, MotionTrailShape.Square, 0.2f, rotateWithMovement: false);
            if (!b.Alive && !b.KillAwarded)
            {
                b.KillAwarded = true;
                if (_challengeMode)
                {
                    HandlePitEnemyDeath(b);
                    continue;
                }
                TryDropEnemyCache(b.Position);
                _chests.Add(new LootChest(b.Position, RollMiniBossLoot()));
                _player.RegisterKill(5);
                AddRunScore(200);
            }
        }
    }

    private void UpdateDestroyerBoss(float dt, List<Obstacle> enemyCollisionObstacles, Vector2 playerTarget)
    {
        if (_destroyerBoss is null) return;

        var previousPosition = _destroyerBoss.Position;
        if (IsFrozenTarget(_destroyerBoss)) return;
        _destroyerBoss.Update(dt, playerTarget, _projectiles, _player, enemyCollisionObstacles, _worldSize, _dashAfterImages);
        AddMotionTrail(previousPosition, _destroyerBoss.Position, Theme.Boss, 24f, MotionTrailShape.Square, 0.2f, rotateWithMovement: false);
        if (!_destroyerBoss.Alive && !_destroyerBoss.KillAwarded)
        {
            _destroyerBoss.KillAwarded = true;
            if (_challengeMode)
            {
                HandlePitEnemyDeath(_destroyerBoss);
                return;
            }
            TryDropEnemyCache(_destroyerBoss.Position);
            _player.RegisterKill(25);
            AddRunScore(1150);
            _chests.Add(new LootChest(_destroyerBoss.Position, RollBossLoot()));
        }
    }

    private void UpdateDeadZoneEnemies(float dt, List<Obstacle> enemyCollisionObstacles, Vector2 playerTarget)
    {
        var playerInvisible = IsPlayerInvisibleForRunIntro();
        foreach (var guard in _generatorGuards)
        {
            var previousPosition = guard.Position;
            if (IsFrozenTarget(guard)) continue;
            guard.Update(dt, playerTarget, _player, enemyCollisionObstacles, _worldSize, _dashAfterImages, _challengeMode && !playerInvisible);
            AddMotionTrail(previousPosition, guard.Position, Palette.C(255, 170, 95), 17f, MotionTrailShape.Triangle, 0.2f);
            if (!guard.Alive && !guard.KillAwarded)
            {
                guard.KillAwarded = true;
                if (_challengeMode)
                {
                    HandlePitEnemyDeath(guard);
                    continue;
                }
                var generator = _generators.FirstOrDefault(g => g.ZoneId == guard.ZoneId);
                if (generator is not null) generator.GuardDefeated = true;
                TryDropStationKey(guard.Position);
                TryDropEnemyCache(guard.Position);
                _player.RegisterKill(4);
                AddRunScore(80);
            }
        }

        foreach (var toxic in _toxicEnemies)
        {
            var previousPosition = toxic.Position;
            if (IsFrozenTarget(toxic)) continue;
            toxic.Update(dt, playerTarget, _projectiles, enemyCollisionObstacles, _worldSize, _challengeMode && !playerInvisible);
            AddMotionTrail(previousPosition, toxic.Position, Palette.C(220, 110, 100), 12f, MotionTrailShape.Triangle);
            if (!toxic.Alive && !toxic.KillAwarded)
            {
                toxic.KillAwarded = true;
                if (_challengeMode)
                {
                    HandlePitEnemyDeath(toxic);
                    continue;
                }
                TryDropEnemyCache(toxic.Position);
                _player.RegisterKill(2);
                AddRunScore(25);
            }
        }

        if (_stationBoss is not null)
        {
            var previousPosition = _stationBoss.Position;
            if (!IsFrozenTarget(_stationBoss))
            {
                _stationBoss.Update(dt, playerTarget, _projectiles, _player, enemyCollisionObstacles, _worldSize);
                AddMotionTrail(previousPosition, _stationBoss.Position, Palette.C(255, 40, 40), 30f, MotionTrailShape.Circle, 0.2f);
                if (!_stationBoss.Alive && !_stationBoss.KillAwarded)
                {
                    _stationBoss.KillAwarded = true;
                    if (_challengeMode)
                    {
                        HandlePitEnemyDeath(_stationBoss);
                        return;
                    }
                    _player.RegisterKill(25);
                    AddRunScore(1300);
                    _chests.Add(new LootChest(_stationBoss.Position, RollStationBossLoot()));
                    OpenStationBossDoor();
                }
            }
        }

        foreach (var boss in _pitStationBosses)
        {
            var previousPosition = boss.Position;
            if (IsFrozenTarget(boss)) continue;
            boss.Update(dt, playerTarget, _projectiles, _player, enemyCollisionObstacles, _worldSize);
            AddMotionTrail(previousPosition, boss.Position, Palette.C(255, 40, 40), 30f, MotionTrailShape.Circle, 0.2f);
            if (!boss.Alive && !boss.KillAwarded)
            {
                boss.KillAwarded = true;
                HandlePitEnemyDeath(boss);
            }
        }
    }

    private void UpdateDeadZoneHazards(float dt)
    {
        if (!_currentMap.IsDeadZone) return;

        var playerInToxicPool = false;
        foreach (var pool in _toxicPools)
        {
            SpawnToxicAmbientParticles(pool.Position, pool.RadiusX, pool.RadiusY, dt);
            if (pool.Contains(_player.Position)) playerInToxicPool = true;
        }

        if (playerInToxicPool)
        {
            _player.ApplyPoison(5f);
        }
    }

    private void UpdatePitChallenge(float dt)
    {
        _pitWaveTimer -= dt;
        if (_pitWaveTimer <= 0f) SpawnPitWave();

        if (_challengeKind == ChallengeKind.PitNightmare
            && _pitNightmarePortalActive
            && _extractPortals.Any(portal => Vector2.Distance(portal.Position, _player.Position) <= portal.InteractionRadius))
        {
            CompletePitNightmareExtraction();
            return;
        }

        for (var i = 0; i < _pitConsumableSpawnPoints.Length; i++)
        {
            if (_pitConsumablePickups[i] is not null) continue;
            _pitConsumableSpawnTimers[i] -= dt;
            if (_pitConsumableSpawnTimers[i] <= 0f) SpawnPitConsumable(i);
        }
    }

    private void ResetPitConsumableSpawns()
    {
        var center = _worldSize / 2f;
        _pitConsumableSpawnPoints =
        [
            new Vector2(center - 250f, center),
            new Vector2(center + 250f, center)
        ];

        for (var i = 0; i < _pitConsumableSpawnTimers.Length; i++)
        {
            _pitConsumableSpawnTimers[i] = 30f;
            _pitConsumablePickups[i] = null;
        }
    }

    private void SpawnPitConsumable(int index)
    {
        if (index < 0 || index >= _pitConsumableSpawnPoints.Length) return;
        var pickup = new GroundConsumablePickup(_pitConsumableSpawnPoints[index], RandomPitConsumable());
        _pitConsumablePickups[index] = pickup;
        _groundConsumables.Add(pickup);
    }

    private void SpawnPitWave()
    {
        if (_challengeKind == ChallengeKind.PitNightmare) _pitNightmarePortalActive = false;
        var wave = _pitNextWave++;
        _pitWaveTimer = wave == 100 ? float.PositiveInfinity : wave % 10 == 0 ? 60f : 30f;
        if (_challengeKind == ChallengeKind.PitNightmare && wave % 3 == 0)
        {
            OpenPitDifficultySelection();
        }

        void AddEnemy(object enemy)
        {
            ApplyPitNightmareEnemyModifiers(enemy);
            _pitEnemyWaves[enemy] = wave;
            switch (enemy)
            {
                case Enemy e: _enemies.Add(e); break;
                case HexEnemy h: _hexEnemies.Add(h); break;
                case MiniBossEnemySquare b: _miniBosses.Add(b); break;
                case GeneratorGuardianEnemy g: _generatorGuards.Add(g); break;
                case ToxicTriangleEnemy t: _toxicEnemies.Add(t); break;
                case StationBossEnemy s:
                    s.Activate();
                    _pitStationBosses.Add(s);
                    break;
            }
        }

        void AddCircles(int count, bool enhanced = false)
        {
            for (var i = 0; i < count; i++)
            {
                var point = RandomPitSpawnPoint(14f);
                AddEnemy(Enemy.CreatePatrol(point, point, false, enhanced: enhanced));
            }
        }

        void AddTriangles(int count, bool enhanced = false)
        {
            for (var i = 0; i < count; i++) AddEnemy(Enemy.CreateStrong(RandomPitSpawnPoint(14f), enhanced: enhanced));
        }

        void AddSquares(int count)
        {
            for (var i = 0; i < count; i++) AddEnemy(new MiniBossEnemySquare(RandomPitSpawnPoint(28f)));
        }

        void AddFastSquares(int count)
        {
            for (var i = 0; i < count; i++) AddEnemy(new MiniBossEnemySquare(RandomPitSpawnPoint(28f), isFast: true));
        }

        void AddHexes(int count)
        {
            for (var i = 0; i < count; i++) AddEnemy(HexEnemy.Create(RandomPitSpawnPoint(16f), _rng));
        }

        void AddToxic(int count)
        {
            for (var i = 0; i < count; i++) AddEnemy(new ToxicTriangleEnemy(RandomPitSpawnPoint(16f), -1));
        }

        void AddGuards(int count)
        {
            for (var i = 0; i < count; i++) AddEnemy(new GeneratorGuardianEnemy(RandomPitSpawnPoint(22f), -1));
        }

        void AddStationBosses(int count)
        {
            var arena = new Rectangle(0, 0, _worldSize, _worldSize);
            for (var i = 0; i < count; i++) AddEnemy(new StationBossEnemy(RandomPitSpawnPoint(36f), arena));
        }

        if (wave <= 5)
        {
            AddCircles(3 + wave);
        }
        else if (wave <= 9)
        {
            AddTriangles(wave - 4);
            AddCircles(3);
        }
        else if (wave == 10)
        {
            AddTriangles(6, true);
            AddCircles(3, true);
        }
        else if (wave <= 19)
        {
            AddTriangles(2, true);
            AddCircles(wave - 8, true);
        }
        else if (wave == 20)
        {
            AddHexes(10);
        }
        else if (wave <= 29)
        {
            var growth = wave - 21;
            AddTriangles(1 + (growth + 1) / 2, true);
            AddCircles(3 + growth / 2, true);
            AddHexes(1);
            AddToxic(1);
        }
        else if (wave == 30)
        {
            AddSquares(1);
            AddFastSquares(1);
        }
        else if (wave <= 39)
        {
            AddToxic(3);
            AddTriangles(wave - 30, true);
        }
        else if (wave == 40)
        {
            AddFastSquares(4);
        }
        else if (wave <= 49)
        {
            AddCircles(19 + wave - 40, true);
        }
        else if (wave == 50)
        {
            AddGuards(6);
            AddFastSquares(2);
        }
        else if (wave <= 59)
        {
            AddFastSquares(3);
            AddHexes(wave - 47);
        }
        else if (wave == 60)
        {
            AddStationBosses(2);
            AddHexes(3);
        }
        else if (wave <= 69)
        {
            AddFastSquares(5);
            AddToxic(2);
            AddHexes(wave - 60);
        }
        else if (wave == 70)
        {
            AddStationBosses(2);
            AddGuards(2);
            AddSquares(2);
            AddFastSquares(2);
            AddToxic(2);
        }
        else if (wave <= 79)
        {
            AddGuards(1);
            AddToxic(wave - 61);
        }
        else if (wave == 80)
        {
            AddStationBosses(6);
        }
        else if (wave <= 89)
        {
            AddStationBosses(1);
            AddHexes(wave - 80);
        }
        else if (wave <= 99)
        {
            AddHexes(50);
        }
        else if (wave == 100)
        {
            AddStationBosses(10);
        }
        else
        {
            return;
        }

        ShowNotice($"Wave {wave} started.");
    }

    private void ApplyPitNightmareEnemyModifiers(object enemy)
    {
        if (_challengeKind != ChallengeKind.PitNightmare) return;

        var healthMultiplier = 1f + _pitNightmareHealthBonusPercent / 100f;
        var speedMultiplier = 1f + _pitNightmareSpeedBonusPercent / 100f;
        var damageMultiplier = 1f + _pitNightmareDamageBonusPercent / 100f;

        switch (enemy)
        {
            case Enemy e: e.ApplyChallengeModifiers(healthMultiplier, speedMultiplier, damageMultiplier); break;
            case HexEnemy h: h.ApplyChallengeModifiers(healthMultiplier, speedMultiplier, damageMultiplier); break;
            case MiniBossEnemySquare b: b.ApplyChallengeModifiers(healthMultiplier, speedMultiplier, damageMultiplier); break;
            case GeneratorGuardianEnemy g: g.ApplyChallengeModifiers(healthMultiplier, speedMultiplier, damageMultiplier); break;
            case ToxicTriangleEnemy t: t.ApplyChallengeModifiers(healthMultiplier, speedMultiplier, damageMultiplier); break;
            case StationBossEnemy s: s.ApplyChallengeModifiers(healthMultiplier, speedMultiplier, damageMultiplier); break;
        }
    }

    private void HandlePitEnemyDeath(object enemy)
    {
        if (!_pitEnemyWaves.Remove(enemy, out var wave)) return;
        if (_pitCompletedWaves.Contains(wave)) return;
        if (_pitEnemyWaves.ContainsValue(wave)) return;

        _pitCompletedWaves.Add(wave);
        _player.GrantLevel();
        var rewardXp = wave % 10 == 0 ? 200 : 50;
        var rewardCoins = wave % 10 == 0 ? 50 : 20;
        AddMetaScore(rewardXp);
        _meta.SynthCoins += rewardCoins;
        _runScore += rewardXp;
        _pitRunXpEarned += rewardXp;
        _pitRunCoinsEarned += rewardCoins;
        _player.Inventory.TryAddHeavyAmmo(20f, out _);

        var clearedBeforeNextWave = _pitNextWave == wave + 1 && _pitWaveTimer > 0f;
        if (_challengeKind == ChallengeKind.PitNightmare && wave % 10 == 0)
        {
            AddPitNightmareTokenReward(wave);
            if (clearedBeforeNextWave)
            {
                _pitNightmarePortalActive = true;
                if (!HasAnyPitEnemyAlive() && _pitWaveTimer > 10f) _pitWaveTimer = 10f;
                ShowNotice("Nightmare exit portal active.");
            }
        }

        if (!HasAnyPitEnemyAlive())
        {
            var clearDelay = _challengeKind == ChallengeKind.PitNightmare && wave % 10 == 0 && clearedBeforeNextWave ? 10f : 3f;
            if (_pitWaveTimer > clearDelay) _pitWaveTimer = clearDelay;
        }

        if (wave == 100)
        {
            CompletePitVictory();
            return;
        }

        if (_challengeKind != ChallengeKind.PitNightmare && wave % 3 == 0) OpenPitRewardSelection(wave);
        SavePersistentState();
    }

    private void CompletePitVictory()
    {
        if (_challengeKind == ChallengeKind.PitNightmare)
        {
            CompletePitNightmareExtraction();
            return;
        }

        RefreshStoreAfterQualifiedRun();
        SavePersistentState();
        ClearUiInteraction();
        _extractPortals.Clear();
        _pitNightmarePortalActive = false;
        _pitRewardOpen = false;
        _pitRewardOffers.Clear();
        _pitRouletteItems.Clear();
        _pitRewardSpinElapsed = 0f;
        _pitDifficultyOpen = false;
        _pitDifficultySpinElapsed = 0f;
        _state = GameState.Storage;
        ShowNotice($"Pit cleared. Earned: {_pitRunXpEarned} XP, {_pitRunCoinsEarned} SynthCoins, {_pitRunTokensEarned} CryptoTokens.");
    }

    private void AddPitNightmareTokenReward(int wave)
    {
        var decade = Math.Max(1, wave / 10);
        var tokens = decade switch
        {
            1 => 5,
            2 => 10,
            3 => 20,
            4 => 35,
            _ => 50
        };

        _meta.CryptoTokens += tokens;
        _pitRunTokensEarned += tokens;
    }

    private void OpenPitDifficultySelection()
    {
        _pitDifficultyOffer = RollPitDifficultyOffer();
        _pitDifficultyRouletteItems.Clear();
        for (var i = 0; i < 18; i++) _pitDifficultyRouletteItems.Add(RollPitDifficultyOffer());
        _pitDifficultyRouletteItems.Add(_pitDifficultyOffer);
        _pitDifficultySpinElapsed = 0f;
        ApplyPitDifficultyOffer(_pitDifficultyOffer);
        _pitDifficultyOpen = true;
    }

    private void UpdatePitDifficultySelection(float dt)
    {
        _pitDifficultySpinElapsed += dt;
        if (_pitDifficultySpinElapsed < PitDifficultySpinDuration) return;

        if (Clicked(PitDifficultyOkButtonRect()))
        {
            _pitDifficultyOpen = false;
            _pitDifficultySpinElapsed = 0f;
            _pitDifficultyRouletteItems.Clear();
            SavePersistentState();
        }
    }

    private PitDifficultyOffer RollPitDifficultyOffer()
    {
        return _rng.Next(3) switch
        {
            0 => new PitDifficultyOffer('D', new[] { 5f, 8f, 10f }[_rng.Next(3)]),
            1 => new PitDifficultyOffer('H', new[] { 10f, 13f, 15f }[_rng.Next(3)]),
            _ => new PitDifficultyOffer('S', new[] { 5f, 8f, 10f }[_rng.Next(3)])
        };
    }

    private void ApplyPitDifficultyOffer(PitDifficultyOffer offer)
    {
        if (offer.Kind == 'D') _pitNightmareDamageBonusPercent += offer.Percent;
        else if (offer.Kind == 'H') _pitNightmareHealthBonusPercent += offer.Percent;
        else if (offer.Kind == 'S') _pitNightmareSpeedBonusPercent += offer.Percent;
    }

    private void CompletePitNightmareExtraction()
    {
        var stored = 0;
        var lostForCapacity = 0;
        foreach (var item in CollectExtractedItems())
        {
            if (item.IsStarter) continue;
            if (item.IsDeviceDataFragment) continue;
            if (_meta.AddToStorage(item)) stored++;
            else lostForCapacity++;
        }

        RefreshStoreAfterQualifiedRun();
        SavePersistentState();
        ClearUiInteraction();
        _extractPortals.Clear();
        _pitNightmarePortalActive = false;
        _state = GameState.Storage;
        ShowNotice(lostForCapacity > 0
            ? $"Nightmare ended: {stored} items stored, {lostForCapacity} lost. Earned: {_pitRunTokensEarned} CryptoTokens."
            : $"Nightmare ended: {stored} items stored. Earned: {_pitRunTokensEarned} CryptoTokens.");
    }

    private bool HasAnyPitEnemyAlive()
        => _enemies.Any(e => e.Alive)
           || _hexEnemies.Any(h => h.Alive)
           || _turrets.Any(t => t.Alive)
           || _miniBosses.Any(b => b.Alive)
           || _generatorGuards.Any(g => g.Alive)
           || _toxicEnemies.Any(t => t.Alive)
           || (_destroyerBoss is not null && _destroyerBoss.Alive)
           || (_stationBoss is not null && _stationBoss.Alive)
           || _pitStationBosses.Any(b => b.Alive);

    private void OpenPitRewardSelection(int wave)
    {
        _pitRewardOffers.Clear();
        _pitRewardOffers.Add(RollPitReward(WeaponSlot.Melee, wave));
        _pitRewardOffers.Add(RollPitReward(WeaponSlot.PrimaryRanged, wave));
        _pitRewardOffers.Add(RollPitReward(WeaponSlot.HeavyRanged, wave));
        _pitRewardOffers.Add(RollPitReward(null, wave));
        _pitRouletteItems.Clear();
        _pitRouletteItems.Add(BuildPitRouletteItems(WeaponSlot.Melee, wave, _pitRewardOffers[0]));
        _pitRouletteItems.Add(BuildPitRouletteItems(WeaponSlot.PrimaryRanged, wave, _pitRewardOffers[1]));
        _pitRouletteItems.Add(BuildPitRouletteItems(WeaponSlot.HeavyRanged, wave, _pitRewardOffers[2]));
        _pitRouletteItems.Add(BuildPitRouletteItems(null, wave, _pitRewardOffers[3]));
        Array.Fill(_pitRewardClaimed, false);
        _pitRewardSpinElapsed = 0f;
        _pitRewardOpen = true;
    }

    private void UpdatePitRewardSelection(float dt)
    {
        _hovered = null;
        _pitRewardSpinElapsed += dt;
        if (Raylib.IsKeyPressed(KeyboardKey.Escape)) return;

        var ready = PitRewardReady;
        if (!ready) return;

        if (ready && Clicked(PitRewardSkipButtonRect()))
        {
            ClosePitRewardSelection();
            return;
        }

        for (var i = 0; i < _pitRewardOffers.Count; i++)
        {
            if (!_pitRewardClaimed[i] && Clicked(PitRewardTakeButtonRect(i)))
            {
                EquipPitReward(_pitRewardOffers[i]);
                _pitRewardClaimed[i] = true;
                if (_pitRewardClaimed.Take(_pitRewardOffers.Count).All(claimed => claimed))
                {
                    ClosePitRewardSelection();
                }
                return;
            }
        }
    }

    private bool PitRewardReady
        => _pitRewardOpen && _pitRewardSpinElapsed >= PitRewardSpinDurations[^1];

    private void ClosePitRewardSelection()
    {
        _pitRewardOpen = false;
        _pitRewardOffers.Clear();
        _pitRouletteItems.Clear();
        Array.Fill(_pitRewardClaimed, false);
        _pitRewardSpinElapsed = 0f;
    }

    private void EquipPitReward(ItemStack item)
    {
        if (item.Type == ItemType.Armor) _player.Armor = item;
        else if (item.IsPrimaryWeapon) _player.RangedWeapon = item;
        else if (item.IsHeavyWeapon) _player.HeavyWeapon = item;
        else if (item.WeaponKind == WeaponClass.Melee) _player.MeleeWeapon = item;
        ShowNotice($"Equipped {item.Name}.");
    }

    private ItemStack RollPitReward(WeaponSlot? weaponSlot, int wave)
    {
        var rarity = RollPitRewardRarity(Math.Max(1, wave / 3));
        if (weaponSlot is null && rarity == ArmorRarity.Red) rarity = ArmorRarity.Legendary;
        return weaponSlot switch
        {
            null => ItemStack.Armor(rarity, _rng),
            WeaponSlot.Melee => ItemStack.Weapon(WeaponClass.Melee, rarity, _rng),
            WeaponSlot.PrimaryRanged => RollPitPrimaryWeapon(rarity),
            WeaponSlot.HeavyRanged => RollPitHeavyWeapon(rarity),
            _ => ItemStack.Weapon(WeaponClass.Melee, rarity, _rng)
        };
    }

    private ItemStack RollPitPrimaryWeapon(ArmorRarity rarity)
    {
        var patterns = new[]
        {
            WeaponPattern.Standard,
            WeaponPattern.PulseRifle,
            WeaponPattern.AutoRifle
        };
        return ItemStack.PatternWeapon(WeaponClass.Ranged, patterns[_rng.Next(patterns.Length)], rarity, _rng);
    }

    private ItemStack RollPitHeavyWeapon(ArmorRarity rarity)
    {
        var patterns = new[]
        {
            WeaponPattern.SniperRifle,
            WeaponPattern.LinearRifle,
            WeaponPattern.RocketPulseRifle
        };
        return ItemStack.PatternWeapon(WeaponClass.Ranged, patterns[_rng.Next(patterns.Length)], rarity, _rng);
    }

    private List<ItemStack> BuildPitRouletteItems(WeaponSlot? weaponSlot, int wave, ItemStack finalItem)
    {
        var result = new List<ItemStack>();
        for (var i = 0; i < 18; i++) result.Add(RollPitReward(weaponSlot, wave));
        result.Add(finalItem);
        return result;
    }

    private ArmorRarity RollPitRewardRarity(int roulette)
    {
        var roll = _rng.NextSingle();
        if (roulette <= 5)
        {
            var rareChance = 0.05f * roulette;
            return roll < rareChance ? ArmorRarity.Rare : ArmorRarity.Common;
        }

        if (roulette <= 10)
        {
            if (roll < 0.05f) return ArmorRarity.Epic;
            if (roll < 0.35f) return ArmorRarity.Rare;
            return ArmorRarity.Common;
        }

        if (roulette <= 15)
        {
            if (roll < 0.15f) return ArmorRarity.Epic;
            if (roll < 0.70f) return ArmorRarity.Rare;
            return ArmorRarity.Common;
        }

        if (roulette <= 20)
        {
            if (roll < 0.05f) return ArmorRarity.Legendary;
            if (roll < 0.50f) return ArmorRarity.Epic;
            return ArmorRarity.Rare;
        }

        if (roll < 0.16f) return ArmorRarity.Legendary;
        if (roll < 0.91f) return ArmorRarity.Epic;
        return ArmorRarity.Rare;
    }

    private Vector2 RandomPitSpawnPoint(float radius)
    {
        var center = new Vector2(_worldSize / 2f, _worldSize / 2f);
        for (var i = 0; i < 120; i++)
        {
            var point = RandomMapPointSafe(radius);
            if (MathF.Abs(point.X - center.X) <= 250f && MathF.Abs(point.Y - center.Y) <= 250f) continue;
            return point;
        }

        return new Vector2(radius + 20f, radius + 20f);
    }

    private ItemStack RandomPitConsumable()
    {
        var values = new[] { ConsumableType.Medkit, ConsumableType.Stim, ConsumableType.ProtectiveDome, ConsumableType.StickyBullets };
        return ItemStack.Consumable(values[_rng.Next(values.Length)]);
    }

    private void UpdateDeadZoneProgress(float dt)
    {
        if (!_currentMap.IsDeadZone) return;

        if (!_stationEntranceOpen && _generators.Count(g => g.Destroyed) >= 3)
        {
            OpenStationEntrance("Station entrance unlocked.");
        }

        if (!_stationEntranceOpen
            && _stationEntranceDoor is Rectangle entranceDoor
            && CircleIntersectsRect(_player.Position, 70f, entranceDoor)
            && TryConsumeStationKey())
        {
            OpenStationEntrance("S.T.A.T.I.O.N key used.");
        }

        if (!_stationBossFightStarted
            && _stationBoss is not null
            && _stationBossArena is Rectangle arena
            && Raylib.CheckCollisionPointRec(_player.Position, arena))
        {
            _stationBossFightStarted = true;
            _stationBoss.Activate();
            _stationBossDoorSealTimer = 0.5f;
            ShowNotice("Station arena sealing.");
        }

        if (_stationBossDoorSealTimer > 0f)
        {
            _stationBossDoorSealTimer -= dt;
            if (_stationBossDoorSealTimer <= 0f) SealStationBossDoor();
        }
    }

    private void SealStationBossDoor()
    {
        if (_stationBossDoorSealed) return;
        _stationBossDoorSealTimer = -1f;
        _stationBossDoorSealed = true;

        if (_stationBossDoor is not Rectangle door) return;

        if (CircleIntersectsRect(_player.Position, 16f, door))
        {
            _player.PlaceAt(FindSafeArenaDoorPoint(door));
        }

        _obstacles.Add(new Obstacle(door));
        ShowNotice("Station arena sealed.");
    }

    private void OpenStationEntrance(string notice)
    {
        _stationEntranceOpen = true;
        RemoveObstacle(_stationEntranceDoor);
        ShowNotice(notice);
    }

    private bool TryConsumeStationKey()
    {
        if (_player.Inventory.QuickSlotQ?.IsStationKey == true)
        {
            _player.Inventory.QuickSlotQ = null;
            _player.Inventory.AutoFillConsumableSlots();
            return true;
        }

        if (_player.Inventory.QuickSlotR?.IsStationKey == true)
        {
            _player.Inventory.QuickSlotR = null;
            _player.Inventory.AutoFillConsumableSlots();
            return true;
        }

        for (var i = 0; i < _player.Inventory.BackpackSlots.Count; i++)
        {
            if (_player.Inventory.BackpackSlots[i]?.IsStationKey != true) continue;
            _player.Inventory.BackpackSlots[i] = null;
            return true;
        }

        return false;
    }

    private void OpenStationBossDoor()
    {
        _stationBossDoorSealTimer = -1f;
        _stationBossDoorSealed = false;
        RemoveObstacle(_stationBossDoor);
        ShowNotice("Station arena opened.");
    }

    private Vector2 FindSafeArenaDoorPoint(Rectangle door)
    {
        if (_stationBossArena is not Rectangle arena) return _player.Position;

        var basePoint = new Vector2(arena.X + 28f, Math.Clamp(_player.Position.Y, arena.Y + 24f, arena.Y + arena.Height - 24f));
        if (!MovementUtils.CircleHitsObstacle(basePoint, 16f, _obstacles)) return basePoint;

        for (var offset = 24f; offset <= 240f; offset += 24f)
        {
            var up = new Vector2(basePoint.X, Math.Clamp(basePoint.Y - offset, arena.Y + 24f, arena.Y + arena.Height - 24f));
            if (!MovementUtils.CircleHitsObstacle(up, 16f, _obstacles)) return up;

            var down = new Vector2(basePoint.X, Math.Clamp(basePoint.Y + offset, arena.Y + 24f, arena.Y + arena.Height - 24f));
            if (!MovementUtils.CircleHitsObstacle(down, 16f, _obstacles)) return down;
        }

        return new Vector2(door.X + door.Width + 28f, door.Y + door.Height * 0.5f);
    }

    private static bool CircleIntersectsRect(Vector2 center, float radius, Rectangle rect)
    {
        var nearest = new Vector2(
            Math.Clamp(center.X, rect.X, rect.X + rect.Width),
            Math.Clamp(center.Y, rect.Y, rect.Y + rect.Height));
        return Vector2.DistanceSquared(center, nearest) < radius * radius;
    }

    private void RemoveObstacle(Rectangle? rect)
    {
        if (rect is not Rectangle target) return;
        _obstacles.RemoveAll(o =>
            MathF.Abs(o.Rect.X - target.X) < 0.1f
            && MathF.Abs(o.Rect.Y - target.Y) < 0.1f
            && MathF.Abs(o.Rect.Width - target.Width) < 0.1f
            && MathF.Abs(o.Rect.Height - target.Height) < 0.1f);
    }

}
