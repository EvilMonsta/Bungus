using System.Numerics;
using System.Text.Json;
using Raylib_cs;

namespace Bungus.Game;

public sealed partial class SciFiRogueGame : IDisposable
{
    private void UpdatePlaying(float dt)
    {
        if (UpdateRunIntro(dt))
        {
            _player.Invulnerable = true;
            UpdateCursorVisibility();
            return;
        }
        _player.Invulnerable = IsPlayerInvisibleForRunIntro();

        if (_terminalOpen)
        {
            ResetQuickConsumableSelector();
            UpdateTerminalPanel();
            UpdateCursorVisibility();
            return;
        }

        if (_openTerminalNoteIndex is not null)
        {
            ResetQuickConsumableSelector();
            UpdateTerminalNotePopup();
            UpdateCursorVisibility();
            return;
        }

        if (_challengeMode && _pitRewardOpen)
        {
            UpdatePitRewardSelection(dt);
            UpdateCursorVisibility();
            return;
        }

        if (_challengeMode && _pitDifficultyOpen)
        {
            UpdatePitDifficultySelection(dt);
            UpdateCursorVisibility();
            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.M))
        {
            ResetQuickConsumableSelector();
            _mapOpen = !_mapOpen;
            _drag = null;
            ResetInventoryUseHold();
            return;
        }

        if (_mapOpen)
        {
            ResetQuickConsumableSelector();
            UpdateMapWindow();
            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            ResetQuickConsumableSelector();
            if (_player.InventoryOpen) CloseRunInventory();
            else
            {
                ClearCombatFeedback();
                _state = GameState.Paused;
            }
            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Tab))
        {
            ResetQuickConsumableSelector();
            _player.InventoryOpen = !_player.InventoryOpen;
            if (!_player.InventoryOpen) CloseRunInventory();
            else
            {
                ResetInventoryUseHold();
            }
        }

        if (_challengeMode && _player.InventoryOpen)
        {
            ResetQuickConsumableSelector();
            UpdateInventoryUi();
            UpdateLevelUi();
            if (_drag is null) _player.Inventory.AutoFillConsumableSlots();
            return;
        }

        if (_inBunker)
        {
            UpdateBunker(dt);
            return;
        }

        var enemyCollisionObstacles = BuildEnemyCollisionObstacles();
        var playerInvisibleForIntro = IsPlayerInvisibleForRunIntro();
        if (!playerInvisibleForIntro) UpdateQuickConsumableSelectorInput(dt);
        dt = GetConsumableSelectorAdjustedDt(dt);
        var playerPreviousPosition = _player.Position;
        _player.Update(dt, _obstacles, _worldSize, _dashAfterImages);
        AddMotionTrail(playerPreviousPosition, _player.Position, Theme.Player, 15f, MotionTrailShape.Circle, 0.18f, 13f);
        UpdatePlayerQueuedShotsWithSound(dt);
        if (Raylib.IsKeyPressed((KeyboardKey)49)) _player.SelectWeaponSlot(WeaponSlot.Melee);
        if (Raylib.IsKeyPressed((KeyboardKey)50)) _player.SelectWeaponSlot(WeaponSlot.PrimaryRanged);
        if (Raylib.IsKeyPressed((KeyboardKey)51)) _player.SelectWeaponSlot(WeaponSlot.HeavyRanged);
        if (!playerInvisibleForIntro && !_player.InventoryOpen && !IsConsumableSelectorOpen && Raylib.IsMouseButtonPressed(MouseButton.Right)) _player.ToggleRocketPulseMode();

        var mouseWorld = Raylib.GetScreenToWorld2D(GetUiMousePosition(), _camera);
        var linearRelease = _player.IsLinearRifleEquipped && Raylib.IsMouseButtonReleased(MouseButton.Left);
        var activeWeapon = _player.ActiveWeapon;
        if (!playerInvisibleForIntro
            && !_player.InventoryOpen
            && activeWeapon?.Pattern == WeaponPattern.RamBomber
            && !IsConsumableSelectorOpen
            && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            TryPlayerAttackWithSound(mouseWorld, _obstacles, _worldSize);
        }
        else if (activeWeapon?.Pattern != WeaponPattern.RamBomber
            && (Raylib.IsMouseButtonDown(MouseButton.Left) || linearRelease)
            && !playerInvisibleForIntro
            && !IsConsumableSelectorOpen
            && !_player.InventoryOpen)
        {
            TryPlayerAttackWithSound(mouseWorld, _obstacles, _worldSize);
        }

        RebuildCombatTargetCache();
        UpdateFreezeZones(dt);
        RebuildCombatTargetCache();
        UpdateMidaMiniTurrets(dt);
        var enemyPlayerTarget = GetEnemyPlayerTarget();
        UpdateEnemies(dt, enemyCollisionObstacles, enemyPlayerTarget);
        UpdateHexEnemies(dt, enemyCollisionObstacles, enemyPlayerTarget);
        UpdateTurrets(dt, enemyCollisionObstacles, enemyPlayerTarget);
        UpdateMiniBosses(dt, enemyCollisionObstacles, enemyPlayerTarget);
        UpdateDestroyerBoss(dt, enemyCollisionObstacles, enemyPlayerTarget);
        UpdateDeadZoneEnemies(dt, enemyCollisionObstacles, enemyPlayerTarget);
        UpdateDeadZoneHazards(dt);
        UpdateDeadZoneProgress(dt);
        if (_challengeMode && !IsPlayerInvisibleForRunIntro()) UpdatePitChallenge(dt);
        RebuildCombatTargetCache();
        UpdateProjectiles(dt);
        UpdateSwings(dt);
        UpdateEffects(dt);
        RebuildCombatTargetCache();
        UpdateProtectiveDomes(dt);
        UpdateChests();
        UpdateGroundConsumables();
        UpdateSecuredTerminalInteractions();
        UpdateInventoryUi();
        UpdateLevelUi();
        if (_drag is null) _player.Inventory.AutoFillConsumableSlots();
        if (!_challengeMode) UpdateExtraction(dt);
        if (_state != GameState.Playing) return;

        var desiredCameraTarget = GetDesiredCameraTarget(mouseWorld);
        _camera.Target = Vector2.Lerp(_camera.Target, desiredCameraTarget, _player.IsSniperEquipped ? 0.035f : 0.08f);
        if (_player.Health <= 0) FailRun("You Died", "All carried items were lost.");
    }

    private void CloseRunInventory()
    {
        ResetQuickConsumableSelector();
        _player.InventoryOpen = false;
        _openedChestIndex = null;
        ClearPendingLevelUpPoints();
        ResetInventoryUseHold();
    }

    private bool TryPlayerAttackWithSound(Vector2 target, List<Obstacle> obstacles, int worldSize)
    {
        var weaponClass = _player.ActiveWeaponClass;
        var projectileCountBefore = _projectiles.Count;
        var swingCountBefore = _swings.Count;
        var attacked = _player.Attack(target, _projectiles, _swings, obstacles, worldSize, _dashAfterImages);
        if (attacked && weaponClass == WeaponClass.Ranged) PlayPlayerShotSounds(_projectiles.Count - projectileCountBefore);
        else if (attacked && weaponClass == WeaponClass.Melee) PlayPlayerSlashSounds(_swings.Count - swingCountBefore);
        return attacked;
    }

    private void UpdatePlayerQueuedShotsWithSound(float dt)
    {
        var projectileCountBefore = _projectiles.Count;
        _player.UpdateCombat(dt, _projectiles);
        PlayPlayerShotSounds(_projectiles.Count - projectileCountBefore);
    }

    private void PlayEnemyShotSoundIfProjectilesAdded(int projectileCountBefore)
    {
        PlayEnemyShotSounds(_projectiles.Count - projectileCountBefore);
    }

}
