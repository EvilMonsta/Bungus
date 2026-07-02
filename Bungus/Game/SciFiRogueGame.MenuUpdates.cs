using System.Numerics;
using System.Text.Json;
using Raylib_cs;

namespace Bungus.Game;

public sealed partial class SciFiRogueGame : IDisposable
{
    private void UpdateCursorVisibility()
    {
        if (_state == GameState.Playing
            && !_player.InventoryOpen
            && !_mapOpen
            && !_pitRewardOpen
            && !_pitDifficultyOpen
            && !_terminalOpen
            && _openTerminalNoteIndex is null)
        {
            Raylib.HideCursor();
        }
        else
        {
            Raylib.ShowCursor();
        }
    }

    private void UpdateMainMenu()
    {
        if (_changelogPopupOpen)
        {
            UpdateChangelogPopup();
            return;
        }

        if (_aboutPopupOpen)
        {
            UpdateAboutPopup();
            return;
        }

        if (_codesPopupOpen)
        {
            UpdateCodesPopup();
            return;
        }

        if (Clicked(MainMenuButtonRect(0))) { ClearUiInteraction(); _state = GameState.MapSelect; }
        if (Clicked(MainMenuButtonRect(1))) { ClearUiInteraction(); ClearStorageSelection(); _state = GameState.Storage; }
        if (Clicked(MainMenuButtonRect(2))) { ClearUiInteraction(); _state = GameState.Armory; }
        if (Clicked(MainMenuButtonRect(3))) { ClearUiInteraction(); _state = GameState.Cradle; }
        if (Clicked(MainMenuButtonRect(4))) { ClearUiInteraction(); _state = GameState.Settings; }
        if (Clicked(MainMenuCodesButtonRect())) OpenCodesPopup();
        if (Clicked(MainMenuChangelogButtonRect())) OpenChangelogPopup();
        if (Clicked(MainMenuAboutButtonRect())) OpenAboutPopup();
        if (Clicked(MainMenuButtonRect(5))) _requestExit = true;
    }

    private void UpdateMapSelect()
    {
        if (_pitNightmareInfoOpen)
        {
            if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Clicked(PitNightmareInfoCloseRect()))
            {
                _pitNightmareInfoOpen = false;
            }

            return;
        }

        if (Clicked(DeploymentToggleRect()))
        {
            _deploymentListMode = _deploymentListMode == DeploymentListMode.Expeditions
                ? DeploymentListMode.Challenges
                : DeploymentListMode.Expeditions;
            ClearUiInteraction();
            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Clicked(MapSelectBackButtonRect()))
        {
            ClearUiInteraction();
            _state = GameState.MainMenu;
            return;
        }

        if (_deploymentListMode == DeploymentListMode.Challenges)
        {
            if (Clicked(ChallengeInfoButtonRect(1)))
            {
                _pitNightmareInfoOpen = true;
                return;
            }

            if (Clicked(MapCardRect(0)))
            {
                ClearUiInteraction();
                StartPitChallenge();
                _state = GameState.Playing;
            }

            if (Clicked(MapCardRect(1)))
            {
                ClearUiInteraction();
                StartPitChallenge(nightmare: true);
                _state = GameState.Playing;
            }

            return;
        }

        for (var i = 0; i < MapDefinition.All.Length; i++)
        {
            var map = MapDefinition.All[i];
            if (!Clicked(MapCardRect(i))) continue;
            ClearUiInteraction();
            StartRun(map.Name);
            _state = GameState.Playing;
            return;
        }
    }

    private void UpdateStorage()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape) && _selectedStorageSlots.Count > 0)
        {
            ClearStorageSelection();
            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Clicked(StorageBackButtonRect()))
        {
            ClearUiInteraction();
            ClearStorageSelection();
            _state = GameState.MainMenu;
            return;
        }

        UpdateStorageUi();
    }

    private void UpdateArmory()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Clicked(ArmoryBackButtonRect()))
        {
            ClearUiInteraction();
            _state = GameState.MainMenu;
            return;
        }

        UpdateArmoryUi();
    }

    private void UpdateArmoryUi()
    {
        EnsureArmoryHeavyAmmoOffer();
        _hovered = null;
        var mouse = GetUiMousePosition();

        for (var i = 0; i < _meta.ArmoryOffers.Count; i++)
        {
            var offer = _meta.ArmoryOffers[i];
            var rect = ArmoryOfferRect(i);
            if (!Raylib.CheckCollisionPointRec(mouse, rect)) continue;

            _hovered = offer.Item;
            if (Clicked(rect)) TryBuyArmoryOffer(i);
            return;
        }

        for (var i = 0; i < _meta.TokenStoreOffers.Count; i++)
        {
            var offer = _meta.TokenStoreOffers[i];
            var rect = TokenStoreOfferRect(i);
            if (!Raylib.CheckCollisionPointRec(mouse, rect)) continue;

            _hovered = offer.Item;
            if (Clicked(rect)) TryBuyTokenStoreOffer(i);
            return;
        }
    }

    private void TryBuyArmoryOffer(int index)
    {
        if (index < 0 || index >= _meta.ArmoryOffers.Count) return;

        var offer = _meta.ArmoryOffers[index];
        if (offer.Purchased && !offer.Item.IsHeavyAmmo)
        {
            ShowNotice(T("notice.sold"));
            return;
        }

        var price = GetArmoryPrice(offer.Item);
        if (_meta.SynthCoins < price)
        {
            ShowNotice(T("notice.not_enough_coins"));
            return;
        }

        if (offer.Item.IsHeavyAmmo && !CanStorePurchasedHeavyAmmo(offer.Item.AmmoPercent))
        {
            ShowNotice(T("notice.not_enough_storage_ammo"));
            return;
        }

        if (!offer.Item.IsHeavyAmmo && !_meta.HasFreeStorageSlot())
        {
            ShowNotice(T("notice.storage_full"));
            return;
        }

        _meta.SynthCoins -= price;
        if (!offer.Item.IsHeavyAmmo) offer.Purchased = true;
        var stored = offer.Item.IsHeavyAmmo
            ? StorePurchasedHeavyAmmo(offer.Item.AmmoPercent)
            : _meta.AddToStorage(offer.Item);
        if (!stored)
        {
            _meta.SynthCoins += price;
            if (!offer.Item.IsHeavyAmmo) offer.Purchased = false;
            ShowNotice(T("notice.storage_full"));
            return;
        }

        SavePersistentState();
        ShowNotice(string.Format(T("notice.bought"), offer.Item.Name, price, "SynthCoins"));
    }

    private bool CanStorePurchasedHeavyAmmo(float percent)
        => ItemStack.GetHeavyAmmoFreeCapacity(_meta.RunBackpackSlots)
           + ItemStack.GetHeavyAmmoFreeCapacity(_meta.StorageSlots)
           + 0.0001f >= percent;

    private bool StorePurchasedHeavyAmmo(float percent)
    {
        if (!ItemStack.TryAddHeavyAmmoToSlots(_meta.RunBackpackSlots, percent, out var remainingPercent) && remainingPercent > 0f)
        {
            return ItemStack.TryAddHeavyAmmoToSlots(_meta.StorageSlots, remainingPercent, out _);
        }

        return true;
    }

    private void TryBuyTokenStoreOffer(int index)
    {
        if (index < 0 || index >= _meta.TokenStoreOffers.Count) return;

        var offer = _meta.TokenStoreOffers[index];
        if (offer.Purchased)
        {
            ShowNotice(T("notice.sold"));
            return;
        }

        var price = GetTokenStorePrice(offer);
        if (_meta.CryptoTokens < price)
        {
            ShowNotice(T("notice.not_enough_tokens"));
            return;
        }

        if (!_meta.HasFreeStorageSlot())
        {
            ShowNotice(T("notice.storage_full"));
            return;
        }

        _meta.CryptoTokens -= price;
        offer.Purchased = true;
        if (!_meta.AddToStorage(offer.Item))
        {
            _meta.CryptoTokens += price;
            offer.Purchased = false;
            ShowNotice(T("notice.storage_full"));
            return;
        }

        SavePersistentState();
        ShowNotice(string.Format(T("notice.bought"), offer.Item.Name, price, "CryptoTokens"));
    }

    private void UpdateCradle()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Clicked(new Rectangle(70, 620, 220, 52)))
        {
            ClearUiInteraction();
            _state = GameState.MainMenu;
        }

        foreach (var track in CradleTracks)
        {
            var plus = CradlePlusRect(track);
            var minus = CradleMinusRect(track);
            if (Clicked(plus)) AdjustCradleTrack(track, 1);
            if (Clicked(minus)) AdjustCradleTrack(track, -1);
        }
    }

    private void AdjustCradleTrack(CradleTrack track, int delta)
    {
        var current = _meta.GetCradleTrack(track);
        if (delta > 0 && (current >= 15 || GetAvailableCradleCells() <= 0)) return;
        if (delta < 0 && current <= 0) return;

        _meta.SetCradleTrack(track, current + delta);
        SavePersistentState();
    }

    private void UpdateSettings()
    {
        if (Clicked(SettingsDisplayButtonRect(0))) SetDisplayMode(DisplayMode.Windowed);
        if (Clicked(SettingsDisplayButtonRect(1))) SetDisplayMode(DisplayMode.Fullscreen);
        if (Clicked(SettingsAntialiasingButtonRect(0))) SetAntialiasingMode(AntialiasingMode.Off);
        if (Clicked(SettingsAntialiasingButtonRect(1))) SetAntialiasingMode(AntialiasingMode.Msaa4x);
        if (Clicked(SettingsVSyncButtonRect(0))) SetVSyncEnabled(false);
        if (Clicked(SettingsVSyncButtonRect(1))) SetVSyncEnabled(true);
        if (Clicked(SettingsTextureFilterButtonRect(0))) SetTextureFilteringMode(TextureFilteringMode.Point);
        if (Clicked(SettingsTextureFilterButtonRect(1))) SetTextureFilteringMode(TextureFilteringMode.Bilinear);
        if (Clicked(SettingsFpsButtonRect(0))) SetTargetFps(30);
        if (Clicked(SettingsFpsButtonRect(1))) SetTargetFps(60);
        if (Clicked(SettingsFpsButtonRect(2))) SetTargetFps(120);
        if (Clicked(SettingsDamageNumbersButtonRect(0))) SetDamageNumbersEnabled(false);
        if (Clicked(SettingsDamageNumbersButtonRect(1))) SetDamageNumbersEnabled(true);
        if (Clicked(SettingsScreenShakeButtonRect(0))) SetScreenShakeEnabled(false);
        if (Clicked(SettingsScreenShakeButtonRect(1))) SetScreenShakeEnabled(true);
        if (Clicked(SettingsEffectsButtonRect(0))) SetVisualEffectsIntensity(VisualEffectsIntensity.Low);
        if (Clicked(SettingsEffectsButtonRect(1))) SetVisualEffectsIntensity(VisualEffectsIntensity.Normal);
        if (Clicked(SettingsEffectsButtonRect(2))) SetVisualEffectsIntensity(VisualEffectsIntensity.High);
        if (Clicked(SettingsLanguageButtonRect(0))) SetLanguage(GameLanguage.English);
        if (Clicked(SettingsLanguageButtonRect(1))) SetLanguage(GameLanguage.Russian);

        for (var i = 0; i < _themes.Count; i++)
        {
            if (Clicked(SettingsThemeButtonRect(i)))
            {
                _themeIndex = i;
                SavePersistentState();
            }
        }

        if (Clicked(SettingsBackButtonRect()) || Raylib.IsKeyPressed(KeyboardKey.Escape)) _state = GameState.MainMenu;
    }

    private void SetLanguage(GameLanguage language)
    {
        if (_language == language) return;

        _language = language;
        if (_deathHeader == English["death.default_title"] || _deathHeader == Russian["death.default_title"]) _deathHeader = T("death.default_title");
        if (_deathBody == English["death.default_body"] || _deathBody == Russian["death.default_body"]) _deathBody = T("death.default_body");
        SavePersistentState();
        ShowNotice(T("notice.language_changed"));
    }

    private void SetAntialiasingMode(AntialiasingMode mode)
    {
        if (_antialiasingMode == mode) return;

        _antialiasingMode = mode;
        SavePersistentState();
        ShowNotice(T("notice.restart_antialiasing"));
    }

    private void SetVSyncEnabled(bool enabled)
    {
        if (_vsyncEnabled == enabled) return;

        _vsyncEnabled = enabled;
        SavePersistentState();
        ShowNotice(T("notice.restart_vsync"));
    }

    private void SetTextureFilteringMode(TextureFilteringMode mode)
    {
        if (_textureFilteringMode == mode) return;

        _textureFilteringMode = mode;
        ApplyTextureFiltering();
        SavePersistentState();
    }

    private void SetTargetFps(int fps)
    {
        fps = NormalizeTargetFps(fps);
        if (_targetFps == fps) return;

        _targetFps = fps;
        Raylib.SetTargetFPS(_targetFps);
        SavePersistentState();
    }

    private void SetVisualEffectsIntensity(VisualEffectsIntensity intensity)
    {
        if (_visualEffectsIntensity == intensity) return;

        _visualEffectsIntensity = intensity;
        SavePersistentState();
    }

    private void SetDamageNumbersEnabled(bool enabled)
    {
        if (_damageNumbersEnabled == enabled) return;

        _damageNumbersEnabled = enabled;
        if (!enabled) ClearFloatingCombatTexts();
        SavePersistentState();
    }

    private void SetScreenShakeEnabled(bool enabled)
    {
        if (_screenShakeEnabled == enabled) return;

        _screenShakeEnabled = enabled;
        if (!enabled)
        {
            _screenShakeTimer = 0f;
            _screenShakeDuration = 0f;
            _screenShakeStrength = 0f;
        }
        SavePersistentState();
    }

    private void UpdatePause()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape)) _state = GameState.Playing;
        if (Clicked(CenterRect(0, 320, 320, 62))) _state = GameState.Playing;
        if (Clicked(CenterRect(0, 400, 320, 62))) FailRun("Run abandoned", "All carried items were lost.");
    }

    private void UpdateDeath()
    {
        if (Clicked(CenterRect(0, 320, 320, 62)))
        {
            if (_challengeMode) StartPitChallenge(_challengeKind == ChallengeKind.PitNightmare);
            else StartRun(_selectedMapName);
            _state = GameState.Playing;
        }
        if (Clicked(CenterRect(0, 400, 320, 62))) { ClearUiInteraction(); ClearCombatFeedback(); _state = GameState.MainMenu; }
    }

}
