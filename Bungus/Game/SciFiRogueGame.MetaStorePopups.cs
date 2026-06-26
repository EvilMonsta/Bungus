using System.Numerics;
using System.Text.Json;
using Raylib_cs;

namespace Bungus.Game;

public sealed partial class SciFiRogueGame : IDisposable
{
    private static bool IsInArc(Vector2 point, SwingArc s, float radiusPad)
    {
        var rel = point - s.Origin;
        if (rel.Length() > s.Radius + radiusPad) return false;
        var a = MathF.Atan2(rel.Y, rel.X);
        return a >= s.AngleStart && a <= s.AngleEnd;
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var t = Vector2.Dot(p - a, ab) / MathF.Max(ab.LengthSquared(), 0.0001f);
        t = Math.Clamp(t, 0f, 1f);
        var nearest = a + ab * t;
        return Vector2.Distance(p, nearest);
    }

    private float GetCommonHealthBonus() => _meta.CradleHealth * 5f;

    private float GetCommonDamageBonus() => 0f;

    private int GetAvailableCradleCells() => Math.Max(0, _meta.Level - _meta.SpentCradleCells());

    private static int GetCradleTrackIndex(CradleTrack track)
        => Array.IndexOf(CradleTracks, track);

    private static Rectangle CradlePlusRect(CradleTrack track)
    {
        var row = GetCradleTrackIndex(track);
        return new Rectangle(1320, 176 + row * 54, 32, 32);
    }

    private static Rectangle CradleMinusRect(CradleTrack track)
    {
        var row = GetCradleTrackIndex(track);
        return new Rectangle(1360, 176 + row * 54, 32, 32);
    }

    private static int GetMetaScoreRequired(int level)
    {
        return 500 + Math.Max(0, level - 1) * 250;
    }

    private void SetDisplayMode(DisplayMode mode)
    {
        if (_displayMode == mode) return;
        _displayMode = mode;
        ApplyDisplayMode();
        SavePersistentState();
    }

    private void ApplyDisplayMode()
    {
        s_activeDisplayMode = _displayMode;
        var exclusiveFullscreen = Raylib.IsWindowFullscreen();
        var borderlessFullscreen = Raylib.IsWindowState(ConfigFlags.BorderlessWindowMode);

        if (_displayMode == DisplayMode.Fullscreen)
        {
            if (exclusiveFullscreen) Raylib.ToggleFullscreen();
            if (borderlessFullscreen) return;

            var monitor = Raylib.GetCurrentMonitor();
            var monitorPosition = Raylib.GetMonitorPosition(monitor);
            Raylib.SetWindowPosition((int)monitorPosition.X, (int)monitorPosition.Y);
            Raylib.SetWindowSize(Raylib.GetMonitorWidth(monitor), Raylib.GetMonitorHeight(monitor));
            Raylib.ToggleBorderlessWindowed();
            return;
        }

        if (exclusiveFullscreen) Raylib.ToggleFullscreen();
        if (borderlessFullscreen) Raylib.ToggleBorderlessWindowed();

        if (!exclusiveFullscreen && !borderlessFullscreen)
        {
            Raylib.SetWindowSize(W, H);
            CenterWindow();
            return;
        }

        Raylib.SetWindowSize(W, H);
        CenterWindow();
    }

    private static void CenterWindow()
    {
        var monitor = Raylib.GetCurrentMonitor();
        var x = (Raylib.GetMonitorWidth(monitor) - W) / 2;
        var y = (Raylib.GetMonitorHeight(monitor) - H) / 2;
        Raylib.SetWindowPosition(Math.Max(0, x), Math.Max(0, y));
    }

    private void AddRunScore(int amount) => _runScore += amount;

    private void ApplyIsFunnyNextRunBonus()
    {
        if (!_isFunnyNextRun) return;

        for (var i = 0; i < 250; i++) _player.GrantLevel();
        _isFunnyNextRun = false;
        ShowNotice("ISFUNNY activated. +250 run levels.");
    }

    private void ApplyToBunkerNextRunBonus()
    {
        if (!_toBunkerNextRun) return;

        _player.Inventory.AddToBackpack(ItemStack.DeviceDataFragment(5));
        _terminalNotesRead[0] = true;
        _terminalNotesRead[1] = true;
        _toBunkerNextRun = false;
        SavePersistentState();
        ShowNotice("TOBUNKER activated. Access code revealed and +5 data fragments.");
    }

    private void AddMetaScore(int amount)
    {
        _meta.Score += amount;
        while (_meta.Score >= GetMetaScoreRequired(_meta.Level))
        {
            _meta.Score -= GetMetaScoreRequired(_meta.Level);
            _meta.Level++;
        }

        SavePersistentState();
    }

    private void UpdateExtraction(float dt)
    {
        if (_lastChanceActive)
        {
            _lastChanceTimer -= dt;
            if (_lastChanceTimer <= 0f)
            {
                FailRun("Extraction failed", "The last portal collapsed and all carried items were lost.");
                return;
            }

            if (!_lastChancePortalNotified && IsLastChancePortalOpen())
            {
                _lastChancePortalNotified = true;
                ShowNotice("Final portal is open.");
            }

            if (IsLastChancePortalOpen() && _extractPortals.Any(portal => Vector2.Distance(portal.Position, _player.Position) <= portal.InteractionRadius))
            {
                CompleteExtraction();
            }

            return;
        }

        if (_extractPortals.Count == 0)
        {
            _portalUnlockTimer -= dt;
            if (_portalUnlockTimer <= 0f)
            {
                SpawnExtractionPortals();
                _portalActiveTimer = _currentMap.PortalLifetime;
                ShowNotice("Portals are open.");
            }

            return;
        }

        _portalActiveTimer -= dt;
        if (_portalActiveTimer <= 0f)
        {
            ActivateLastChancePortal();
            return;
        }

        if (_extractPortals.Any(portal => Vector2.Distance(portal.Position, _player.Position) <= portal.InteractionRadius))
        {
            CompleteExtraction();
        }
    }

    private void SpawnExtractionPortals()
    {
        _extractPortals.Clear();
        var attempts = 0;
        while (_extractPortals.Count < _currentMap.PortalCount && attempts++ < 400)
        {
            var point = RandomExtractionPortalPoint(24f);
            if (_extractPortals.Any(portal => Vector2.Distance(portal.Position, point) < 2200f) && _extractPortals.Count > 0) continue;
            _extractPortals.Add(new ExtractPortal(point, _rng.NextSingle() * MathF.Tau));
        }

        while (_extractPortals.Count < _currentMap.PortalCount)
        {
            _extractPortals.Add(new ExtractPortal(RandomExtractionPortalPoint(24f), _rng.NextSingle() * MathF.Tau));
        }
    }

    private void ActivateLastChancePortal()
    {
        _extractPortals.Clear();
        _lastChanceActive = true;
        _lastChanceTimer = _currentMap.LastChanceLifetime;
        _lastChancePortalNotified = false;

        for (var i = 0; i < _currentMap.LastChancePortalCount; i++)
        {
            var portalPos = RandomExtractionPortalPoint(24f);
            _extractPortals.Add(new ExtractPortal(portalPos, _rng.NextSingle() * MathF.Tau));
            SpawnLastChanceEnemies(portalPos);
        }
        _nextHexSpawnTimer = MathF.Min(_nextHexSpawnTimer, NextHexSpawnDelay());
        ShowNotice("Final extraction chance started. Portal opens in the last 10 seconds.");
    }

    private void SpawnLastChanceEnemies(Vector2 portalPos)
    {
        for (var i = 0; i < _rng.Next(0, 5); i++)
        {
            _turrets.Add(new TurretEnemy(RandomPointNear(portalPos, 80f, 200f, 18f), _rng.NextSingle() * MathF.Tau));
        }

        for (var i = 0; i < _rng.Next(1, 3); i++)
        {
            _miniBosses.Add(new MiniBossEnemySquare(RandomPointNear(portalPos, 110f, 250f, 28f)));
        }

        for (var i = 0; i < _rng.Next(2, 6); i++)
        {
            var point = RandomPointNear(portalPos, 90f, 260f, 14f);
            _enemies.Add(Enemy.CreatePatrol(point, point, false, enhanced: _currentMap.IsDeadZone));
        }
    }

    private Vector2 RandomPointNear(Vector2 center, float minDistance, float maxDistance, float radius)
    {
        for (var i = 0; i < 80; i++)
        {
            var angle = _rng.NextSingle() * MathF.Tau;
            var distance = minDistance + _rng.NextSingle() * (maxDistance - minDistance);
            var point = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
            point = Vector2.Clamp(point, new Vector2(radius + 4f, radius + 4f), new Vector2(_worldSize - radius - 4f, _worldSize - radius - 4f));
            if (!MovementUtils.CircleHitsObstacle(point, radius, _obstacles)) return point;
        }

        return RandomOutdoorPoint(radius);
    }

    private bool IsLastChancePortalOpen() => _lastChanceActive && _lastChanceTimer <= 10f;

    private void CompleteExtraction()
    {
        var stored = 0;
        var lostForCapacity = 0;
        foreach (var item in CollectExtractedItems())
        {
            if (item.IsStarter) continue;
            if (_meta.AddToStorage(item)) stored++;
            else lostForCapacity++;
        }

        AddMetaScore(_runScore);
        RefreshStoreAfterQualifiedRun();
        SavePersistentState();
        ClearUiInteraction();
        ClearCombatFeedback();
        _extractPortals.Clear();
        _lastChanceActive = false;
        _lastChanceTimer = 0f;
        _state = GameState.Storage;
        ShowNotice(lostForCapacity > 0
            ? $"Extracted: {stored} items stored, {lostForCapacity} lost. Score +{_runScore}."
            : $"Extracted successfully. Score +{_runScore}.");
    }

    private IEnumerable<ItemStack> CollectExtractedItems()
    {
        if (_player.Armor is not null && !_player.Armor.IsDeviceDataFragment) yield return _player.Armor;
        if (_player.RangedWeapon is not null && !_player.RangedWeapon.IsDeviceDataFragment) yield return _player.RangedWeapon;
        if (_player.HeavyWeapon is not null && !_player.HeavyWeapon.IsDeviceDataFragment) yield return _player.HeavyWeapon;
        if (_player.MeleeWeapon is not null && !_player.MeleeWeapon.IsDeviceDataFragment) yield return _player.MeleeWeapon;
        if (_player.Inventory.QuickSlotQ is not null && !_player.Inventory.QuickSlotQ.IsDeviceDataFragment) yield return _player.Inventory.QuickSlotQ;
        if (_player.Inventory.QuickSlotR is not null && !_player.Inventory.QuickSlotR.IsDeviceDataFragment) yield return _player.Inventory.QuickSlotR;

        foreach (var item in _player.Inventory.BackpackSlots)
        {
            if (item is not null && !item.IsDeviceDataFragment) yield return item;
        }
    }

    private void FailRun(string header, string body)
    {
        var retainedXp = 0;
        if (!_challengeMode && header.Equals("You Died", StringComparison.OrdinalIgnoreCase))
        {
            retainedXp = _runScore / 2;
            AddMetaScore(retainedXp);
        }
        _extractPortals.Clear();
        _lastChanceActive = false;
        _lastChanceTimer = 0f;
        _pitRewardOpen = false;
        _pitRewardOffers.Clear();
        _pitRouletteItems.Clear();
        _pitRewardSpinElapsed = 0f;
        _pitDifficultyOpen = false;
        _pitDifficultySpinElapsed = 0f;
        ClearUiInteraction();
        ClearCombatFeedback();
        UpdateStoreAfterFailedRun();
        SavePersistentState();
        _deathHeader = header;
        _deathBody = _challengeMode
            ? $"{body}\nEarned: {_pitRunXpEarned} XP, {_pitRunCoinsEarned} SynthCoins, {_pitRunTokensEarned} CryptoTokens."
            : retainedXp > 0 ? $"{body}\nRetained XP: {retainedXp}." : body;
        _state = GameState.Death;
    }

    private void ClearUiInteraction()
    {
        _drag = null;
        _hovered = null;
        _openedChestIndex = null;
        ResetInventoryUseHold();
        ClearPendingLevelUpPoints();
    }

    private void ShowNotice(string text)
    {
        _noticeText = text;
        _noticeTimer = 5f;
    }

    private void OpenCodesPopup()
    {
        StartUiTransition();
        _codesPopupOpen = true;
        _codeInput = string.Empty;
        _codeStatusText = string.Empty;
        _codeStatusSuccess = false;
    }

    private void CloseCodesPopup()
    {
        StartUiTransition();
        _codesPopupOpen = false;
        _codeInput = string.Empty;
        _codeStatusText = string.Empty;
        _codeStatusSuccess = false;
    }

    private void OpenAboutPopup()
    {
        StartUiTransition();
        _aboutPopupOpen = true;
    }

    private void CloseAboutPopup()
    {
        StartUiTransition();
        _aboutPopupOpen = false;
    }

    private void OpenChangelogPopup()
    {
        StartUiTransition();
        _changelogPopupOpen = true;
        _changelogScroll = 0f;
        _changelogLines.Clear();

        var lines = ReadEmbeddedReleaseNotes();
        if (lines is null)
        {
            _changelogLines.Add(("release notes resource was not found.", false));
            return;
        }

        foreach (var line in lines)
        {
            var version = IsReleaseVersionLine(line);
            var displayLine = line.Replace('—', '-');
            foreach (var wrapped in WrapText(displayLine, 20, (int)ChangelogContentRect().Width - 34))
                _changelogLines.Add((wrapped, version));
        }
    }

    private void CloseChangelogPopup()
    {
        StartUiTransition();
        _changelogPopupOpen = false;
        _changelogScroll = 0f;
    }

    private void UpdateChangelogPopup()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Clicked(ChangelogPopupCloseRect()))
        {
            CloseChangelogPopup();
            return;
        }

        var wheel = Raylib.GetMouseWheelMove();
        if (Raylib.CheckCollisionPointRec(GetUiMousePosition(), ChangelogContentRect()))
            _changelogScroll -= wheel * 72f;
        if (Raylib.IsKeyDown(KeyboardKey.Down)) _changelogScroll += 360f * Raylib.GetFrameTime();
        if (Raylib.IsKeyDown(KeyboardKey.Up)) _changelogScroll -= 360f * Raylib.GetFrameTime();
        if (Raylib.IsKeyPressed(KeyboardKey.PageDown)) _changelogScroll += ChangelogContentRect().Height * 0.8f;
        if (Raylib.IsKeyPressed(KeyboardKey.PageUp)) _changelogScroll -= ChangelogContentRect().Height * 0.8f;

        const float lineStep = 27f;
        var maxScroll = MathF.Max(0f, _changelogLines.Count * lineStep - ChangelogContentRect().Height + 12f);
        _changelogScroll = Math.Clamp(_changelogScroll, 0f, maxScroll);
    }

    private static string[]? ReadEmbeddedReleaseNotes()
    {
        var assembly = typeof(SciFiRogueGame).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith("release_notes_en.txt", StringComparison.OrdinalIgnoreCase));
        if (resourceName is null) return null;

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null) return null;

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("\r\n", "\n").Split('\n');
    }

    private static bool IsReleaseVersionLine(string line)
    {
        var token = line.TrimStart().Split([' ', '\t', '—', '-'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (string.IsNullOrEmpty(token) || token.Length < 4 || char.ToLowerInvariant(token[0]) != 'a') return false;
        var parts = token[1..].Split('.');
        return parts.Length is 2 or 3 && parts.All(part => int.TryParse(part, out _));
    }

    private void UpdateAboutPopup()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Clicked(AboutPopupCloseRect()))
        {
            CloseAboutPopup();
        }
    }

    private void UpdateCodesPopup()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Clicked(CodesPopupCloseRect()))
        {
            CloseCodesPopup();
            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Backspace) && _codeInput.Length > 0)
        {
            _codeInput = _codeInput[..^1];
        }

        while (true)
        {
            var key = Raylib.GetCharPressed();
            if (key == 0) break;

            if (_codeInput.Length >= 24) continue;
            var ch = (char)key;
            if (char.IsLetterOrDigit(ch) || ch == '_' || ch == '-')
            {
                _codeInput += char.ToUpperInvariant(ch);
            }
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Clicked(CodesPopupApplyRect()))
        {
            ApplyPromoCodeInput();
        }
    }

    private void ApplyPromoCodeInput()
    {
        var code = NormalizePromoCode(_codeInput);
        if (string.IsNullOrWhiteSpace(code))
        {
            SetCodeStatus(false, "Enter a code first.");
            return;
        }

        if (code == "RAMGUNGUS")
        {
            var result = ApplyRamGunGodCode();
            SetCodeStatus(result.Success, result.Message);
            if (result.Success) _codeInput = string.Empty;
            return;
        }

        if (code == "RAMARMORGUS")
        {
            var result = ApplyRamArmorGodCode();
            SetCodeStatus(result.Success, result.Message);
            if (result.Success) _codeInput = string.Empty;
            return;
        }

        if (code == "BLUEKING")
        {
            var result = ApplyBlueKingCode();
            SetCodeStatus(result.Success, result.Message);
            if (result.Success) _codeInput = string.Empty;
            return;
        }

        if (code == "SKEYFORRAM")
        {
            var result = ApplyStationKeyCode();
            SetCodeStatus(result.Success, result.Message);
            if (result.Success) _codeInput = string.Empty;
            return;
        }

        if (code == "LVL1RAM")
        {
            var result = ApplyLevelResetCode();
            SetCodeStatus(result.Success, result.Message);
            if (result.Success) _codeInput = string.Empty;
            return;
        }

        if (code == "ISFUNNY")
        {
            var result = ApplyIsFunnyCode();
            SetCodeStatus(result.Success, result.Message);
            if (result.Success) _codeInput = string.Empty;
            return;
        }

        if (code == "TESTW")
        {
            var result = ApplyTestWeaponsCode();
            SetCodeStatus(result.Success, result.Message);
            if (result.Success) _codeInput = string.Empty;
            return;
        }

        if (code == "GREEDRAM")
        {
            var result = ApplyGreedRamCode();
            SetCodeStatus(result.Success, result.Message);
            if (result.Success) _codeInput = string.Empty;
            return;
        }

        if (code == "MIBOMBO")
        {
            var result = ApplyMiBomboCode();
            SetCodeStatus(result.Success, result.Message);
            if (result.Success) _codeInput = string.Empty;
            return;
        }

        if (code == "WELCOME")
        {
            var result = ApplyWelcomeCode();
            SetCodeStatus(result.Success, result.Message);
            if (result.Success) _codeInput = string.Empty;
            return;
        }

        if (code == "RUK")
        {
            var result = ApplyRukCode();
            SetCodeStatus(result.Success, result.Message);
            if (result.Success) _codeInput = string.Empty;
            return;
        }
        if (code == "TOBUNKER")
        {
            var result = ApplyToBunkerCode();
            SetCodeStatus(result.Success, result.Message);
            if (result.Success) _codeInput = string.Empty;
            return;
        }

        if (code == "RT")
        {
            var result = ApplyTerrorCode();
            SetCodeStatus(result.Success, result.Message);
            if (result.Success) _codeInput = string.Empty;
            return;
        }

        SetCodeStatus(false, "No such code.");
    }

    private ProtectiveDome? FindHitDome(Vector2 point, float radius)
    {
        var domes = _inBunker ? _bunkerProtectiveDomes : _protectiveDomes;
        foreach (var dome in domes)
        {
            if (!dome.Alive) continue;
            var limit = ProtectiveDome.Radius + radius;
            if (Vector2.DistanceSquared(point, dome.Position) <= limit * limit) return dome;
        }

        return null;
    }

    private (bool Success, string Message) ApplyRamGunGodCode()
    {
        const string code = "RAMGUNGUS";
        if (!CanUsePromoCode(code, null, false, out var error))
        {
            return (false, error);
        }

        var rewards = new List<ItemStack>
        {
            ItemStack.PatternWeapon(WeaponClass.Ranged, WeaponPattern.Standard, ArmorRarity.Legendary, _rng),
            ItemStack.PatternWeapon(WeaponClass.Ranged, WeaponPattern.PulseRifle, ArmorRarity.Legendary, _rng),
            ItemStack.PatternWeapon(WeaponClass.Ranged, WeaponPattern.AutoRifle, ArmorRarity.Legendary, _rng),
            ItemStack.PatternWeapon(WeaponClass.Ranged, WeaponPattern.SniperRifle, ArmorRarity.Legendary, _rng),
            ItemStack.PatternWeapon(WeaponClass.Ranged, WeaponPattern.LinearRifle, ArmorRarity.Legendary, _rng),
            ItemStack.PatternWeapon(WeaponClass.Ranged, WeaponPattern.RocketPulseRifle, ArmorRarity.Legendary, _rng),
            ItemStack.PatternWeapon(WeaponClass.Melee, WeaponPattern.Standard, ArmorRarity.Legendary, _rng),
            ItemStack.PatternWeapon(WeaponClass.Melee, WeaponPattern.EnergySpear, ArmorRarity.Legendary, _rng)
        };

        var stored = 0;
        foreach (var reward in rewards)
        {
            if (_meta.AddToStorage(reward)) stored++;
        }

        if (stored == 0)
        {
            return (false, "Storage is full.");
        }

        RegisterPromoCodeUse(code, false);
        SavePersistentState();
        var lost = rewards.Count - stored;
        return (true, lost > 0 ? $"Success: {stored} weapon(s) delivered, {lost} lost due to full storage." : "Success");
    }

    private (bool Success, string Message) ApplyRamArmorGodCode()
    {
        const string code = "RAMARMORGUS";
        if (!CanUsePromoCode(code, null, false, out var error))
        {
            return (false, error);
        }

        var rewards = new List<ItemStack>
        {
            ItemStack.Armor(ArmorRarity.Epic, _rng),
            ItemStack.Armor(ArmorRarity.Legendary, _rng),
            ItemStack.Armor(ArmorRarity.Red, _rng)
        };

        var stored = 0;
        foreach (var reward in rewards)
        {
            if (_meta.AddToStorage(reward)) stored++;
        }

        if (stored == 0)
        {
            return (false, "Storage is full.");
        }

        RegisterPromoCodeUse(code, false);
        SavePersistentState();
        var lost = rewards.Count - stored;
        return (true, lost > 0 ? $"Success: {stored} armor piece(s) delivered, {lost} lost due to full storage." : "Success");
    }

    private (bool Success, string Message) ApplyBlueKingCode()
    {
        const string code = "BLUEKING";
        if (!CanUsePromoCode(code, 1, false, out var error))
        {
            return (false, error);
        }

        var rewards = new List<ItemStack>
        {
            ItemStack.Weapon(WeaponClass.Ranged, ArmorRarity.Rare, _rng),
            ItemStack.Weapon(WeaponClass.Melee, ArmorRarity.Rare, _rng),
            ItemStack.Armor(ArmorRarity.Rare, _rng)
        };

        var stored = 0;
        foreach (var reward in rewards)
        {
            if (_meta.AddToStorage(reward)) stored++;
        }

        if (stored == 0)
        {
            return (false, "Storage is full.");
        }

        RegisterPromoCodeUse(code, false);
        SavePersistentState();
        var lost = rewards.Count - stored;
        return (true, lost > 0 ? $"Success: {stored} blue item(s) delivered, {lost} lost due to full storage." : "Success");
    }

    private (bool Success, string Message) ApplyStationKeyCode()
    {
        const string code = "SKEYFORRAM";
        if (!CanUsePromoCode(code, null, false, out var error))
        {
            return (false, error);
        }

        if (!_meta.AddToStorage(ItemStack.StationKey()))
        {
            return (false, "Storage is full.");
        }

        RegisterPromoCodeUse(code, false);
        SavePersistentState();
        return (true, "Success");
    }

    private (bool Success, string Message) ApplyLevelResetCode()
    {
        const string code = "LVL1RAM";
        if (!CanUsePromoCode(code, null, false, out var error))
        {
            return (false, error);
        }

        _meta.Level = 1;
        _meta.Score = 0;
        foreach (var track in CradleTracks) _meta.SetCradleTrack(track, 0);
        RegisterPromoCodeUse(code, false);
        SavePersistentState();
        return (true, "Success: account level reset to 1.");
    }

    private (bool Success, string Message) ApplyIsFunnyCode()
    {
        const string code = "ISFUNNY";
        if (!CanUsePromoCode(code, null, false, out var error))
        {
            return (false, error);
        }

        _isFunnyNextRun = true;
        RegisterPromoCodeUse(code, false);
        SavePersistentState();
        return (true, "Success: next run starts with +250 levels.");
    }

    private (bool Success, string Message) ApplyTestWeaponsCode()
    {
        const string code = "TESTW";
        if (!CanUsePromoCode(code, null, false, out var error))
        {
            return (false, error);
        }

        var rewards = new List<ItemStack>
        {
            ItemStack.Toxikus(_rng),
            ItemStack.Lancelot(_rng),
            ItemStack.TraceRifle(_rng),
            ItemStack.RocketLauncher(_rng),
            ItemStack.Pulsar(_rng),
            ItemStack.BossGrenadeLauncher()
        };

        var stored = 0;
        foreach (var reward in rewards)
        {
            if (_meta.AddToStorage(reward)) stored++;
        }

        if (stored == 0)
        {
            return (false, "Storage is full.");
        }

        RegisterPromoCodeUse(code, false);
        SavePersistentState();
        var lost = rewards.Count - stored;
        return (true, lost > 0 ? $"Success: {stored} test weapon(s) delivered, {lost} lost due to full storage." : "Success");
    }

    private (bool Success, string Message) ApplyGreedRamCode()
    {
        const string code = "GREEDRAM";
        if (!CanUsePromoCode(code, null, false, out var error))
        {
            return (false, error);
        }

        _meta.CryptoTokens += 100;
        RegisterPromoCodeUse(code, false);
        SavePersistentState();
        return (true, "Success: +100 CryptoTokens.");
    }

    private (bool Success, string Message) ApplyMiBomboCode()
    {
        const string code = "MIBOMBO";
        if (!CanUsePromoCode(code, null, false, out var error))
        {
            return (false, error);
        }

        if (!_meta.AddToStorage(ItemStack.RamBomber(_rng)))
        {
            return (false, "Storage is full.");
        }

        RegisterPromoCodeUse(code, false);
        SavePersistentState();
        return (true, "Success");
    }

    private (bool Success, string Message) ApplyWelcomeCode()
    {
        const string code = "WELCOME";
        if (!CanUsePromoCode(code, 1, false, out var error))
        {
            return (false, error);
        }

        RegisterPromoCodeUse(code, false);
        AddMetaScore(2500);
        return (true, "Success: +2500 XP.");
    }

    private (bool Success, string Message) ApplyRukCode()
    {
        AddMetaScore(10000);
        return (true, "Success: +10000 XP.");
    }
    private (bool Success, string Message) ApplyToBunkerCode()
    {
        const string code = "TOBUNKER";
        if (_toBunkerNextRun)
        {
            return (false, "This code is already active for the next run.");
        }

        if (!CanUsePromoCode(code, null, false, out var error))
        {
            return (false, error);
        }

        _toBunkerNextRun = true;
        RegisterPromoCodeUse(code, false);
        SavePersistentState();
        return (true, "Success: next expedition starts with 5 data fragments and revealed access code.");
    }

    private (bool Success, string Message) ApplyTerrorCode()
    {
        const string code = "RT";
        if (!CanUsePromoCode(code, null, false, out var error)) return (false, error);
        if (!_meta.AddToStorage(ItemStack.Terror())) return (false, "Storage is full.");
        RegisterPromoCodeUse(code, false);
        SavePersistentState();
        return (true, "Success: Terror delivered to storage.");
    }

    private bool CanUsePromoCode(string code, int? maxUses, bool sessionOnly, out string error)
    {
        error = string.Empty;
        if (maxUses is int limit && GetPromoCodeUseCount(code) >= limit)
        {
            error = "This code can no longer be used.";
            return false;
        }

        if (sessionOnly && _sessionActiveCodes.Contains(code))
        {
            error = "This code is already active for this session.";
            return false;
        }

        return true;
    }

    private void RegisterPromoCodeUse(string code, bool sessionOnly)
    {
        _promoCodeUses[code] = GetPromoCodeUseCount(code) + 1;
        if (sessionOnly) _sessionActiveCodes.Add(code);
    }

    private int GetPromoCodeUseCount(string code)
        => _promoCodeUses.GetValueOrDefault(code, 0);

    private void SetCodeStatus(bool success, string message)
    {
        _codeStatusSuccess = success;
        _codeStatusText = message;
    }

    private static string NormalizePromoCode(string input)
        => (input ?? string.Empty).Trim().ToUpperInvariant();

    private static string FormatTime(float timeLeft)
    {
        var total = Math.Max(0, (int)MathF.Ceiling(timeLeft));
        return $"{total / 60:00}:{total % 60:00}";
    }

    private void DrawZoneArrows()
    {
        DrawScreenZoneArrow(_buildings, Palette.C(80, 170, 255), "T");
        DrawScreenZoneArrow(_outposts, Palette.C(245, 90, 90), "O");
        if (_mapMarker is Vector2 marker)
        {
            DrawScreenPointArrow(marker, Palette.C(255, 220, 80), "M");
        }

        if (_destroyerBoss is not null && _destroyerBoss.Alive)
        {
            DrawScreenPointArrow(_destroyerBoss.Position, Palette.C(230, 45, 45), "B");
        }
    }

    private static Rectangle GetMapRect()
    {
        var size = MathF.Min(GetUiScreenWidth() - 140f, GetUiScreenHeight() - 120f);
        size = MathF.Min(size, 620f);
        return new Rectangle(
            GetUiScreenWidth() * 0.5f - size * 0.5f,
            GetUiScreenHeight() * 0.5f - size * 0.5f + 20f,
            size,
            size);
    }

    private Vector2 WorldToMap(Vector2 worldPoint, Rectangle mapRect)
    {
        var scale = mapRect.Width / GetActiveMapWorldSize();
        return new Vector2(mapRect.X + worldPoint.X * scale, mapRect.Y + worldPoint.Y * scale);
    }

    private Vector2 MapToWorld(Vector2 mapPoint, Rectangle mapRect)
    {
        var worldSize = GetActiveMapWorldSize();
        var scale = worldSize / mapRect.Width;
        var world = new Vector2((mapPoint.X - mapRect.X) * scale, (mapPoint.Y - mapRect.Y) * scale);
        return Vector2.Clamp(world, Vector2.Zero, new Vector2(worldSize, worldSize));
    }

    private int GetActiveMapWorldSize() => _inBunker ? BunkerWorldSize : _worldSize;

    private void DrawScreenZoneArrow(List<LootZone> zones, Color color, string marker)
    {
        var nearest = zones.OrderBy(zone => Vector2.DistanceSquared(_player.Position, zone.Center)).FirstOrDefault();
        if (nearest is null) return;
        DrawScreenPointArrow(nearest.Center, color, marker);
    }

    private void DrawScreenPointArrow(Vector2 target, Color color, string marker)
    {
        var to = target - _player.Position;
        if (to.LengthSquared() < 0.01f) return;

        var dir = Vector2.Normalize(to);
        var center = GetUiScreenCenter();
        var tip = center + dir * 82f;
        var normal = new Vector2(-dir.Y, dir.X);
        var backCenter = center + dir * 54f;

        Raylib.DrawTriangle(tip, backCenter + normal * 11f, backCenter - normal * 11f, color);
        Raylib.DrawText(marker, (int)backCenter.X - 5, (int)backCenter.Y - 8, 16, Color.White);
    }

    private void DrawStatTooltip()
    {
        var mouse = GetUiMousePosition();
        var hints = new (Rectangle Rect, string Header, string Body)[]
        {
            (new Rectangle(54, 176, 220, 24), "STR", "+5 HP, +1 melee damage and +0.25% melee damage per point."),
            (new Rectangle(54, 206, 220, 24), "DEX", "+1% melee damage and +2% melee attack speed per point."),
            (new Rectangle(54, 236, 220, 24), "SPD", "+4% move speed multiplier per point."),
            (new Rectangle(54, 266, 220, 24), "GUN", "+0.3 flat and +1% ranged damage per point.")
        };

        var hit = hints.FirstOrDefault(h => Raylib.CheckCollisionPointRec(mouse, h.Rect));
        if (string.IsNullOrEmpty(hit.Header)) return;

        var x = (int)mouse.X + 20;
        var y = (int)mouse.Y + 14;
        const int width = 320;
        const int padding = 8;
        const int lineHeight = 19;
        var bodyLines = WrapText(hit.Body, 16, width - padding * 2);
        var height = 42 + bodyLines.Count * lineHeight + padding;
        Raylib.DrawRectangle(x, y, width, height, Palette.C(0, 0, 0, 225));
        Raylib.DrawRectangleLines(x, y, width, height, Color.SkyBlue);
        Raylib.DrawText(hit.Header, x + 8, y + 8, 18, Color.White);
        var lineY = y + 34;
        foreach (var line in bodyLines)
        {
            Raylib.DrawText(line, x + padding, lineY, 16, Color.LightGray);
            lineY += lineHeight;
        }
    }

    private int GetStoredItemCount() => _meta.StorageSlots.Count(item => item is not null);

    private static Rectangle StashPanelRect() => new(900, 190, 466, 478);

    private int GetMaxStashScrollRow()
    {
        var totalRows = (int)MathF.Ceiling(_meta.StorageSlots.Count / (float)StashGridColumns);
        return Math.Max(0, totalRows - StashVisibleRows);
    }

    private static int GetSellValue(ItemStack item)
    {
        if (item.IsStationKey) return 900;

        if (item.Type == ItemType.Consumable)
        {
            return item.ConsumableKind switch
            {
                ConsumableType.Medkit => 20,
                ConsumableType.ProtectiveDome => 30,
                ConsumableType.TeslaBullets => 35,
                ConsumableType.FreezeGrenade => 40,
                ConsumableType.HeGrenade => 45,
                ConsumableType.MidaMiniTurret => 55,
                _ => 15
            };
        }

        return item.Rarity switch
        {
            ArmorRarity.Damaged => 25,
            ArmorRarity.Common => 25,
            ArmorRarity.Rare => 100,
            ArmorRarity.Epic => 500,
            ArmorRarity.Legendary => 2000,
            ArmorRarity.Red => 3000,
            _ => 25
        };
    }

    private static int GetArmoryPrice(ItemStack item)
    {
        if (item.IsHeavyAmmo) return 300;
        if (item.Type == ItemType.Consumable) return 200;

        var price = item.Rarity switch
        {
            ArmorRarity.Legendary => 10000,
            ArmorRarity.Epic => 2500,
            _ => 500
        };
        if (item.Type == ItemType.Armor)
        {
            price = (int)MathF.Ceiling(price * (1f + GetArmorModifierCount(item) * 0.2f));
        }

        return price;
    }

    private static int GetArmorModifierCount(ItemStack item)
    {
        if (item.Type != ItemType.Armor) return 0;

        var count = 0;
        if (item.SpeedBonusPercent > 0f) count++;
        if (item.ExplosionResistancePercent > 0f) count++;
        if (item.HealingBonusPercent > 0f) count++;
        if (item.DashRecoveryPercent > 0f) count++;
        if (item.ShieldMax > 0f) count++;
        if (item.RegenPercentPerSecond > 0f) count++;
        return count;
    }

    private void RefreshStoreAfterQualifiedRun()
    {
        _meta.FailedRunsSinceStoreRefresh = 0;
        RefreshArmoryOffers();
        RefreshTokenStoreOffers();
    }

    private void UpdateStoreAfterFailedRun()
    {
        if (_challengeMode && _pitCompletedWaves.Count >= 10)
        {
            RefreshStoreAfterQualifiedRun();
            return;
        }

        _meta.FailedRunsSinceStoreRefresh++;
        if (_meta.FailedRunsSinceStoreRefresh < 3) return;

        RefreshStoreAfterQualifiedRun();
    }

    private void RefreshArmoryOffers()
    {
        _meta.ArmoryOffers.Clear();
        for (var i = 0; i < 5; i++) _meta.ArmoryOffers.Add(new ArmoryOffer { Item = RollArmoryEquipment(ArmorRarity.Rare) });
        for (var i = 0; i < 2; i++) _meta.ArmoryOffers.Add(new ArmoryOffer { Item = RollArmoryEquipment(ArmorRarity.Epic) });
        for (var i = 0; i < 3; i++)
        {
            var rarity = _rng.NextSingle() < 0.30f ? ArmorRarity.Legendary : ArmorRarity.Epic;
            _meta.ArmoryOffers.Add(new ArmoryOffer { Item = RollArmoryEquipment(rarity) });
        }
        _meta.ArmoryOffers.Add(new ArmoryOffer { Item = ItemStack.HeavyAmmo(25f) });
        _meta.ArmoryOffers.Add(new ArmoryOffer { Item = ItemStack.Consumable(RollConsumableType()) });
        for (var i = 0; i < ArmoryConsumableRowCount; i++) _meta.ArmoryOffers.Add(new ArmoryOffer { Item = ItemStack.Consumable(RollConsumableType()) });
    }

    private bool EnsureArmoryHeavyAmmoOffer()
    {
        var changed = false;
        if (!_meta.ArmoryOffers.Any(offer => offer.Item.IsHeavyAmmo))
        {
            _meta.ArmoryOffers.Add(new ArmoryOffer { Item = ItemStack.HeavyAmmo(25f) });
            changed = true;
        }

        if (!_meta.ArmoryOffers.Any(offer => offer.Item.Type == ItemType.Consumable))
        {
            _meta.ArmoryOffers.Add(new ArmoryOffer { Item = ItemStack.Consumable(RollConsumableType()) });
            changed = true;
        }

        while (_meta.ArmoryOffers.Count < ArmoryOfferCount)
        {
            _meta.ArmoryOffers.Add(new ArmoryOffer { Item = ItemStack.Consumable(RollConsumableType()) });
            changed = true;
        }

        return changed;
    }

    private bool EnsureTokenStoreOffers()
    {
        if (_meta.TokenStoreOffers.Any(offer => offer.Item.Pattern == WeaponPattern.LinearRifle))
        {
            RefreshTokenStoreOffers();
            return true;
        }

        if (_meta.TokenStoreOffers.Count >= 3) return false;
        RefreshTokenStoreOffers();
        return true;
    }

    private void RefreshTokenStoreOffers()
    {
        _meta.TokenStoreOffers.Clear();
        var patterns = new List<WeaponPattern>
        {
            WeaponPattern.Pulsar,
            WeaponPattern.Toxikus,
            WeaponPattern.TraceRifle,
            WeaponPattern.RocketLauncher
        };

        for (var i = 0; i < 3 && patterns.Count > 0; i++)
        {
            var patternIndex = _rng.Next(patterns.Count);
            var pattern = patterns[patternIndex];
            patterns.RemoveAt(patternIndex);
            _meta.TokenStoreOffers.Add(new TokenStoreOffer
            {
                Item = CreateTokenStoreWeapon(pattern),
                DiscountPercent = _rng.NextSingle() < 0.5f ? _rng.Next(2, 9) * 5 : 0
            });
        }
    }

    private ItemStack CreateTokenStoreWeapon(WeaponPattern pattern)
        => pattern switch
        {
            WeaponPattern.Pulsar => ItemStack.Pulsar(_rng),
            WeaponPattern.Toxikus => ItemStack.Toxikus(_rng),
            WeaponPattern.TraceRifle => ItemStack.TraceRifle(_rng),
            WeaponPattern.RocketLauncher => ItemStack.RocketLauncher(_rng),
            _ => ItemStack.Pulsar(_rng)
        };

    private static int GetTokenStoreBasePrice(ItemStack item)
        => item.Pattern switch
        {
            WeaponPattern.Pulsar => 100,
            WeaponPattern.Toxikus => 80,
            WeaponPattern.TraceRifle => 125,
            WeaponPattern.RocketLauncher => 175,
            _ => 100
        };

    private static int GetTokenStorePrice(TokenStoreOffer offer)
    {
        var price = GetTokenStoreBasePrice(offer.Item);
        return Math.Max(1, (int)MathF.Ceiling(price * (100 - offer.DiscountPercent) / 100f));
    }

    private ItemStack RollArmoryEquipment(ArmorRarity rarity)
    {
        if (_rng.NextSingle() < 0.35f) return ItemStack.Armor(rarity, _rng);
        if (_rng.NextSingle() < 0.20f) return ItemStack.PatternWeapon(WeaponClass.Ranged, WeaponPattern.LinearRifle, rarity, _rng);
        return ItemStack.Weapon(_rng.NextSingle() < 0.5f ? WeaponClass.Ranged : WeaponClass.Melee, rarity, _rng);
    }

    private ItemStack? TakeMetaLoadoutItem(SlotKind kind)
    {
        var item = GetMetaLoadoutItem(kind);
        SetMetaLoadoutItem(kind, null);
        return item;
    }

    private static bool IsMetaLoadoutSlot(SlotKind kind)
        => kind is SlotKind.Armor or SlotKind.RangedWeapon or SlotKind.HeavyWeapon or SlotKind.MeleeWeapon;

    private static bool CanPlaceIntoSlot(SlotKind kind, ItemStack item)
        => kind switch
        {
            SlotKind.Armor => item.Type == ItemType.Armor,
            SlotKind.RangedWeapon => item.IsPrimaryWeapon,
            SlotKind.HeavyWeapon => item.IsHeavyWeapon,
            SlotKind.MeleeWeapon => item.Type == ItemType.Weapon && item.WeaponKind == WeaponClass.Melee,
            _ => false
        };

    private ItemStack? GetMetaLoadoutItem(SlotKind kind) => kind switch
    {
        SlotKind.Armor => _meta.Armor,
        SlotKind.RangedWeapon => _meta.RangedWeapon,
        SlotKind.HeavyWeapon => _meta.HeavyWeapon,
        SlotKind.MeleeWeapon => _meta.MeleeWeapon,
        _ => null
    };

    private void SetMetaLoadoutItem(SlotKind kind, ItemStack? item)
    {
        if (kind == SlotKind.Armor) _meta.Armor = item;
        if (kind == SlotKind.RangedWeapon) _meta.RangedWeapon = item;
        if (kind == SlotKind.HeavyWeapon) _meta.HeavyWeapon = item;
        if (kind == SlotKind.MeleeWeapon) _meta.MeleeWeapon = item;
    }

    private void LoadMetaRunBackpackIntoPlayer()
    {
        for (var i = 0; i < Inventory.BackpackCapacity; i++)
        {
            _player.Inventory.BackpackSlots[i] = _meta.RunBackpackSlots[i];
            _meta.RunBackpackSlots[i] = null;
        }
    }

    private SlotKind? GetPreferredLoadoutSlot(ItemStack item)
    {
        if (item.Type == ItemType.Armor) return SlotKind.Armor;
        if (item.IsPrimaryWeapon) return SlotKind.RangedWeapon;
        if (item.IsHeavyWeapon) return SlotKind.HeavyWeapon;
        if (item.Type == ItemType.Weapon && item.WeaponKind == WeaponClass.Melee) return SlotKind.MeleeWeapon;
        return null;
    }

    private Player CreateLandingPreviewPlayer()
        => Player.Create(
            Vector2.Zero,
            GetCommonHealthBonus(),
            GetCommonDamageBonus(),
            0,
            0,
            0,
            0,
            _meta.CradleSpeed,
            _meta.CradleMeleeSpeed,
            _meta.CradleDashRecovery,
            _meta.CradleStability,
            _meta.CradleGunsmith,
            _meta.CradleFighter,
            _meta.CradleArcane,
            _meta.RangedWeapon,
            _meta.HeavyWeapon,
            _meta.MeleeWeapon,
            _meta.Armor,
            _meta.QuickSlotQ,
            _meta.QuickSlotR);

    private static string BuildWeaponDamageText(Player player, ItemStack? weapon, WeaponClass kind)
    {
        if (weapon is null) return string.Empty;
        if (weapon.Pattern == WeaponPattern.RamBomber) return "Damage ??? | DPS ???";

        var (totalDamage, bonusDamage, dps, label) = GetDisplayedWeaponStats(player, weapon, kind);
        if (weapon.Pattern == WeaponPattern.Toxikus)
        {
            var basePoisonDamage = 30f + weapon.BaseDamage * 0.4f;
            var poisonDamage = player.GetToxikusPoisonDamage(weapon);
            return $"{label} {totalDamage:0.0}(+{bonusDamage:0.0}) | Poison: {poisonDamage:0.0}(+{poisonDamage - basePoisonDamage:0.0}) | DPS: {dps:0.0}";
        }

        return $"{label} {totalDamage:0.0}(+{bonusDamage:0.0}) | DPS: {dps:0.0}";
    }

    private static (float TotalDamage, float BonusDamage, float Dps, string Label) GetDisplayedWeaponStats(Player player, ItemStack weapon, WeaponClass kind)
    {
        if (weapon.Pattern == WeaponPattern.RamBomber) return (0f, 0f, 0f, "???");

        if (weapon.Pattern == WeaponPattern.AutoRifle)
        {
            var baseBullet = weapon.BaseDamage * 0.53f;
            var bullet = player.GetWeaponDamage(weapon) * 0.53f;
            return (bullet, bullet - baseBullet, bullet * (500f / 60f), "bullet");
        }

        if (weapon.Pattern == WeaponPattern.RocketPulseRifle)
        {
            var baseImpact = weapon.BaseDamage * 1.35f;
            var impact = player.GetWeaponDamage(weapon) * 1.35f;
            var fireRate = player.RocketPulseBurstMode ? (400f / 60f) * 1.3f * 1.1f : 400f / 60f;
            return (impact, impact - baseImpact, impact * fireRate, "x3 rocket");
        }

        if (weapon.Pattern == WeaponPattern.GrenadeLauncher)
        {
            var baseDirect = weapon.BaseDamage + 135f;
            var totalDirect = player.GetWeaponDamage(weapon) + 135f;
            var directDps = totalDirect * 1.5f;
            return (totalDirect, totalDirect - baseDirect, directDps, "direct");
        }

        if (weapon.Pattern == WeaponPattern.RocketLauncher)
        {
            var baseDirect = weapon.BaseDamage + 200f;
            var totalDirect = player.GetWeaponDamage(weapon) + 200f;
            var directDps = totalDirect * (40f / 60f);
            return (totalDirect, totalDirect - baseDirect, directDps, "direct");
        }

        if (weapon.Pattern == WeaponPattern.TraceRifle)
        {
            var traceDamage = player.GetWeaponDamage(weapon);
            return (traceDamage, traceDamage - weapon.BaseDamage, traceDamage / (60f / 1000f), "beam");
        }

        if (weapon.Pattern == WeaponPattern.LinearRifle)
        {
            var baseLinearDamage = weapon.BaseDamage * 9f;
            var linearDamage = player.GetWeaponDamage(weapon) * 9f;
            var cooldown = GetLinearRifleDisplayedCooldown(weapon);
            return (linearDamage, linearDamage - baseLinearDamage, linearDamage / cooldown, "shot");
        }

        if (weapon.Pattern == WeaponPattern.Pulsar)
        {
            var pulsarDamage = player.GetWeaponDamage(weapon);
            return (pulsarDamage, pulsarDamage - weapon.BaseDamage, (pulsarDamage + 2.5f * 15f) * 3f, "bolt");
        }

        if (weapon.Pattern == WeaponPattern.SniperRifle)
        {
            var baseShot = weapon.BaseDamage * 8.325f;
            var shotDamage = player.GetSniperShotDamage(weapon);
            var sniperDps = shotDamage / 1.75f;
            return (shotDamage, shotDamage - baseShot, sniperDps, "shot");
        }

        if (weapon.Pattern is WeaponPattern.PulseRifle or WeaponPattern.Toxikus)
        {
            var basePerShot = weapon.BaseDamage * 0.525f;
            var perShot = player.GetPulseShotDamage(weapon);
            var shots = player.GetPulseBurstShotCount(weapon);
            var cooldown = weapon.Pattern == WeaponPattern.Toxikus ? 1f / 2.2f : 0.374f;
            var poisonDps = weapon.Pattern == WeaponPattern.Toxikus ? player.GetToxikusPoisonDamage(weapon) : 0f;
            var burstDps = perShot * shots / cooldown + poisonDps;
            return (perShot, perShot - basePerShot, burstDps, $"x{shots}");
        }

        if (kind == WeaponClass.Melee)
        {
            var baseHit = weapon.BaseDamage * 6.3f;
            var hitDamage = player.GetMeleeHitDamage(weapon);
            var cooldown = weapon.Pattern is WeaponPattern.EnergySpear or WeaponPattern.Lancelot
                ? player.GetMeleeCooldown(0.70f)
                : player.GetMeleeCooldown(0.64f);
            return (hitDamage, hitDamage - baseHit, hitDamage / cooldown, weapon.Pattern is WeaponPattern.EnergySpear or WeaponPattern.Lancelot ? "thrust" : "slash");
        }

        var total = player.GetWeaponDamage(weapon);
        var expectedShotsPerAttack = weapon.Rarity == ArmorRarity.Legendary ? 1.33f : 1f;
        var dps = total * expectedShotsPerAttack / 0.22f;
        return (total, total - weapon.BaseDamage, dps, "dmg");
    }

    private static float GetLinearRifleDisplayedCooldown(ItemStack weapon)
        => (weapon.Rarity == ArmorRarity.Legendary ? 0.7f : 0.8f) + 0.45f;

    private (List<LootZone> buildings, List<LootZone> outposts) GenerateZones(int buildingCount, int outpostCount)
    {
        var all = new List<LootZone>();

        PlaceZones(all, buildingCount, false);
        PlaceZones(all, outpostCount, true);

        return (all.Where(x => !x.IsOutpost).ToList(), all.Where(x => x.IsOutpost).ToList());
    }

}
