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
            if (Raylib.IsMouseButtonPressed(MouseButton.Left)) TryBuyArmoryOffer(i);
            return;
        }

        for (var i = 0; i < _meta.TokenStoreOffers.Count; i++)
        {
            var offer = _meta.TokenStoreOffers[i];
            var rect = TokenStoreOfferRect(i);
            if (!Raylib.CheckCollisionPointRec(mouse, rect)) continue;

            _hovered = offer.Item;
            if (Raylib.IsMouseButtonPressed(MouseButton.Left)) TryBuyTokenStoreOffer(i);
            return;
        }
    }

    private void TryBuyArmoryOffer(int index)
    {
        if (index < 0 || index >= _meta.ArmoryOffers.Count) return;

        var offer = _meta.ArmoryOffers[index];
        if (offer.Purchased && !offer.Item.IsHeavyAmmo)
        {
            ShowNotice("This armory item is already sold.");
            return;
        }

        var price = GetArmoryPrice(offer.Item);
        if (_meta.SynthCoins < price)
        {
            ShowNotice("Not enough SynthCoins.");
            return;
        }

        if (offer.Item.IsHeavyAmmo && !CanStorePurchasedHeavyAmmo(offer.Item.AmmoPercent))
        {
            ShowNotice("Not enough storage space for Heavy Ammo.");
            return;
        }

        if (!offer.Item.IsHeavyAmmo && !_meta.HasFreeStorageSlot())
        {
            ShowNotice("Storage is full.");
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
            ShowNotice("Storage is full.");
            return;
        }

        SavePersistentState();
        ShowNotice($"Bought {offer.Item.Name} for {price} SynthCoins.");
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
            ShowNotice("This token item is already sold.");
            return;
        }

        var price = GetTokenStorePrice(offer);
        if (_meta.CryptoTokens < price)
        {
            ShowNotice("Not enough CryptoTokens.");
            return;
        }

        if (!_meta.HasFreeStorageSlot())
        {
            ShowNotice("Storage is full.");
            return;
        }

        _meta.CryptoTokens -= price;
        offer.Purchased = true;
        if (!_meta.AddToStorage(offer.Item))
        {
            _meta.CryptoTokens += price;
            offer.Purchased = false;
            ShowNotice("Storage is full.");
            return;
        }

        SavePersistentState();
        ShowNotice($"Bought {offer.Item.Name} for {price} CryptoTokens.");
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
        if (Clicked(CenterRect(0, 204, 360, 50))) SetDisplayMode(DisplayMode.Windowed);
        if (Clicked(CenterRect(0, 260, 360, 50))) SetDisplayMode(DisplayMode.Fullscreen);
        if (Clicked(CenterRect(-320, 370, 180, 44))) SetAntialiasingMode(AntialiasingMode.Off);
        if (Clicked(CenterRect(-120, 370, 180, 44))) SetAntialiasingMode(AntialiasingMode.Msaa4x);
        if (Clicked(CenterRect(120, 370, 180, 44))) SetVSyncEnabled(false);
        if (Clicked(CenterRect(320, 370, 180, 44))) SetVSyncEnabled(true);
        if (Clicked(CenterRect(-320, 500, 180, 44))) SetTextureFilteringMode(TextureFilteringMode.Point);
        if (Clicked(CenterRect(-120, 500, 180, 44))) SetTextureFilteringMode(TextureFilteringMode.Bilinear);
        if (Clicked(CenterRect(90, 500, 96, 44))) SetTargetFps(30);
        if (Clicked(CenterRect(202, 500, 96, 44))) SetTargetFps(60);
        if (Clicked(CenterRect(314, 500, 96, 44))) SetTargetFps(120);

        for (var i = 0; i < _themes.Count; i++)
        {
            if (Clicked(CenterRect(0, 620 + i * 50, 390, 44)))
            {
                _themeIndex = i;
                SavePersistentState();
            }
        }

        if (Clicked(CenterRect(0, 900, 280, 52)) || Raylib.IsKeyPressed(KeyboardKey.Escape)) _state = GameState.MainMenu;
    }

    private void SetAntialiasingMode(AntialiasingMode mode)
    {
        if (_antialiasingMode == mode) return;

        _antialiasingMode = mode;
        SavePersistentState();
        ShowNotice("Restart the game to apply antialiasing.");
    }

    private void SetVSyncEnabled(bool enabled)
    {
        if (_vsyncEnabled == enabled) return;

        _vsyncEnabled = enabled;
        SavePersistentState();
        ShowNotice("Restart the game to apply VSync.");
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
        if (Clicked(CenterRect(0, 400, 320, 62))) { ClearUiInteraction(); _state = GameState.MainMenu; }
    }

}
