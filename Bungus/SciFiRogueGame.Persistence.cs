using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Raylib_cs;

namespace Bungus.Game;

public sealed partial class SciFiRogueGame
{
    private void LoadPersistentState()
    {
        try
        {
            if (!File.Exists(SaveFilePath))
            {
                RefreshArmoryOffers();
                RefreshTokenStoreOffers();
                SavePersistentState();
                return;
            }

            var json = File.ReadAllText(SaveFilePath);
            var data = DeserializePersistentStateFile(json, out var migratedLegacySave);
            if (data is null)
            {
                ShowNotice("Save tampering detected. Profile was reset.");
                SavePersistentState();
                return;
            }

            _themeIndex = Math.Clamp(data.ThemeIndex, 0, Math.Max(0, _themes.Count - 1));
            _displayMode = Enum.IsDefined(data.DisplayMode) ? data.DisplayMode : DisplayMode.Windowed;
            _selectedMapName = string.IsNullOrWhiteSpace(data.SelectedMapName) ? "Baselands" : data.SelectedMapName;
            _isFunnyNextRun = data.IsFunnyNextRun;
            _promoCodeUses.Clear();
            foreach (var pair in data.PromoCodeUses ?? [])
            {
                if (!string.IsNullOrWhiteSpace(pair.Key) && pair.Value > 0) _promoCodeUses[pair.Key] = pair.Value;
            }
            _sessionActiveCodes.Clear();
            ApplyMetaSaveData(data.Meta);
            ApplyDisplayMode();
            if (migratedLegacySave || EnsureArmoryHeavyAmmoOffer() || EnsureTokenStoreOffers()) SavePersistentState();
        }
        catch
        {
            _themeIndex = 0;
            _displayMode = DisplayMode.Windowed;
            _selectedMapName = "Baselands";
            _isFunnyNextRun = false;
            _promoCodeUses.Clear();
            _sessionActiveCodes.Clear();
            ApplyMetaSaveData(null);
            ApplyDisplayMode();
            SavePersistentState();
        }
    }

    private void SavePersistentState()
    {
        try
        {
            var directory = Path.GetDirectoryName(SaveFilePath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

            var data = new PersistentStateData
            {
                ThemeIndex = _themeIndex,
                DisplayMode = _displayMode,
                SelectedMapName = _selectedMapName,
                IsFunnyNextRun = _isFunnyNextRun,
                PromoCodeUses = new Dictionary<string, int>(_promoCodeUses, StringComparer.OrdinalIgnoreCase),
                Meta = BuildMetaSaveData()
            };

            var protectedSave = ProtectSavePayload(JsonSerializer.Serialize(data, SaveJsonOptions));

            File.WriteAllText(SaveFilePath, JsonSerializer.Serialize(protectedSave, SaveJsonOptions));
        }
        catch
        {
            // Saving failure should not break the session.
        }
    }

    private static PersistentStateData? DeserializePersistentStateFile(string json, out bool migratedLegacySave)
    {
        migratedLegacySave = false;

        try
        {
            var protectedSave = JsonSerializer.Deserialize<ProtectedSaveFile>(json);
            if (!string.IsNullOrWhiteSpace(protectedSave?.ProtectedPayload))
            {
                var payloadJson = UnprotectSavePayload(protectedSave);
                return JsonSerializer.Deserialize<PersistentStateData>(payloadJson);
            }
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }

        try
        {
            var legacy = JsonSerializer.Deserialize<PersistentStateData>(json);
            if (legacy is null) return null;

            migratedLegacySave = true;
            return legacy;
        }
        catch
        {
            return null;
        }
    }

    private static ProtectedSaveFile ProtectSavePayload(string json)
    {
        var plainBytes = Encoding.UTF8.GetBytes(json);

        using var aes = Aes.Create();
        aes.Key = DeriveSaveKeyBytes("enc");
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        var signatureBytes = ComputeSaveSignature(aes.IV, cipherBytes);

        return new ProtectedSaveFile
        {
            Version = ProtectedSaveVersion,
            Iv = Convert.ToBase64String(aes.IV),
            ProtectedPayload = Convert.ToBase64String(cipherBytes),
            Signature = Convert.ToBase64String(signatureBytes)
        };
    }

    private static string UnprotectSavePayload(ProtectedSaveFile protectedSave)
    {
        var ivBytes = Convert.FromBase64String(protectedSave.Iv);
        var cipherBytes = Convert.FromBase64String(protectedSave.ProtectedPayload);
        var signatureBytes = Convert.FromBase64String(protectedSave.Signature);
        var expectedSignature = ComputeSaveSignature(ivBytes, cipherBytes);

        if (!CryptographicOperations.FixedTimeEquals(signatureBytes, expectedSignature))
        {
            throw new CryptographicException("Save signature mismatch.");
        }

        using var aes = Aes.Create();
        aes.Key = DeriveSaveKeyBytes("enc");
        aes.IV = ivBytes;

        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return Encoding.UTF8.GetString(plainBytes);
    }

    private static byte[] DeriveSaveKeyBytes(string purpose)
    {
        var source = $"{Environment.UserName}|{Environment.MachineName}|{AppContext.BaseDirectory}|{purpose}|Bungus.Profile.Save.v2";
        return SHA256.HashData(Encoding.UTF8.GetBytes(source));
    }

    private static byte[] ComputeSaveSignature(byte[] ivBytes, byte[] cipherBytes)
    {
        using var hmac = new HMACSHA256(DeriveSaveKeyBytes("mac"));
        hmac.TransformBlock(ivBytes, 0, ivBytes.Length, null, 0);
        hmac.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return hmac.Hash ?? [];
    }

    private MetaProfileSaveData BuildMetaSaveData()
    {
        return new MetaProfileSaveData
        {
            Level = _meta.Level,
            Score = _meta.Score,
            BaseStrength = _meta.BaseStrength,
            BaseDexterity = _meta.BaseDexterity,
            BaseSpeed = _meta.BaseSpeed,
            BaseGuns = _meta.BaseGuns,
            CradleHealth = _meta.CradleHealth,
            CradleSpeed = _meta.CradleSpeed,
            CradleMeleeSpeed = _meta.CradleMeleeSpeed,
            CradleDashRecovery = _meta.CradleDashRecovery,
            CradleStability = _meta.CradleStability,
            CradleGunsmith = _meta.CradleGunsmith,
            CradleFighter = _meta.CradleFighter,
            CradleArcane = _meta.CradleArcane,
            SynthCoins = _meta.SynthCoins,
            CryptoTokens = _meta.CryptoTokens,
            FailedRunsSinceStoreRefresh = _meta.FailedRunsSinceStoreRefresh,
            StorageSlots = _meta.StorageSlots.Select(ItemStack.ToSaveData).ToList(),
            RunBackpackSlots = _meta.RunBackpackSlots.Select(ItemStack.ToSaveData).ToList(),
            ArmoryOffers = _meta.ArmoryOffers
                .Select(offer => new ArmoryOfferSaveData { Item = ItemStack.ToSaveData(offer.Item), Purchased = offer.Purchased })
                .ToList(),
            TokenStoreOffers = _meta.TokenStoreOffers
                .Select(offer => new TokenStoreOfferSaveData { Item = ItemStack.ToSaveData(offer.Item), DiscountPercent = offer.DiscountPercent, Purchased = offer.Purchased })
                .ToList(),
            Armor = ItemStack.ToSaveData(_meta.Armor),
            RangedWeapon = ItemStack.ToSaveData(_meta.RangedWeapon),
            HeavyWeapon = ItemStack.ToSaveData(_meta.HeavyWeapon),
            MeleeWeapon = ItemStack.ToSaveData(_meta.MeleeWeapon),
            QuickSlotQ = ItemStack.ToSaveData(_meta.QuickSlotQ),
            QuickSlotR = ItemStack.ToSaveData(_meta.QuickSlotR),
            Trash = ItemStack.ToSaveData(_meta.Trash)
        };
    }

    private void ApplyMetaSaveData(MetaProfileSaveData? data)
    {
        _meta.Level = Math.Max(1, data?.Level ?? 1);
        _meta.Score = Math.Max(0, data?.Score ?? 0);
        _meta.BaseStrength = Math.Max(0, data?.BaseStrength ?? 4);
        _meta.BaseDexterity = Math.Max(0, data?.BaseDexterity ?? 4);
        _meta.BaseSpeed = Math.Max(0, data?.BaseSpeed ?? 4);
        _meta.BaseGuns = Math.Max(0, data?.BaseGuns ?? 4);
        _meta.CradleHealth = Math.Clamp(data?.CradleHealth ?? 0, 0, 15);
        _meta.CradleSpeed = Math.Clamp(data?.CradleSpeed ?? 0, 0, 15);
        _meta.CradleMeleeSpeed = Math.Clamp(data?.CradleMeleeSpeed ?? 0, 0, 15);
        _meta.CradleDashRecovery = Math.Clamp(data?.CradleDashRecovery ?? 0, 0, 15);
        _meta.CradleStability = Math.Clamp(data?.CradleStability ?? 0, 0, 15);
        _meta.CradleGunsmith = Math.Clamp(data?.CradleGunsmith ?? 0, 0, 15);
        _meta.CradleFighter = Math.Clamp(data?.CradleFighter ?? 0, 0, 15);
        _meta.CradleArcane = Math.Clamp(data?.CradleArcane ?? 0, 0, 15);
        _meta.SynthCoins = Math.Max(0, data?.SynthCoins ?? 0);
        _meta.CryptoTokens = Math.Max(0, data?.CryptoTokens ?? 0);
        _meta.FailedRunsSinceStoreRefresh = Math.Clamp(data?.FailedRunsSinceStoreRefresh ?? 0, 0, 2);
        _meta.StorageSlots.Clear();
        _meta.RunBackpackSlots.Clear();
        _meta.ArmoryOffers.Clear();
        _meta.TokenStoreOffers.Clear();

        var savedSlots = data?.StorageSlots ?? [];
        for (var i = 0; i < MetaProfile.StorageCapacity; i++)
        {
            _meta.StorageSlots.Add(i < savedSlots.Count ? ItemStack.FromSaveData(savedSlots[i]) : null);
        }

        var runBackpackSlots = data?.RunBackpackSlots ?? [];
        for (var i = 0; i < Inventory.BackpackCapacity; i++)
        {
            _meta.RunBackpackSlots.Add(i < runBackpackSlots.Count ? ItemStack.FromSaveData(runBackpackSlots[i]) : null);
        }

        _meta.Armor = ItemStack.FromSaveData(data?.Armor);
        _meta.RangedWeapon = ItemStack.FromSaveData(data?.RangedWeapon);
        _meta.HeavyWeapon = ItemStack.FromSaveData(data?.HeavyWeapon);
        _meta.MeleeWeapon = ItemStack.FromSaveData(data?.MeleeWeapon);
        _meta.QuickSlotQ = ItemStack.FromSaveData(data?.QuickSlotQ);
        _meta.QuickSlotR = ItemStack.FromSaveData(data?.QuickSlotR);
        _meta.Trash = ItemStack.FromSaveData(data?.Trash);
        NormalizeMetaWeaponLoadoutSlots();

        foreach (var savedOffer in data?.ArmoryOffers ?? [])
        {
            var item = ItemStack.FromSaveData(savedOffer.Item);
            if (item is not null) _meta.ArmoryOffers.Add(new ArmoryOffer { Item = item, Purchased = savedOffer.Purchased });
        }

        if (_meta.ArmoryOffers.Count == 0) RefreshArmoryOffers();

        foreach (var savedOffer in data?.TokenStoreOffers ?? [])
        {
            var item = ItemStack.FromSaveData(savedOffer.Item);
            if (item is not null) _meta.TokenStoreOffers.Add(new TokenStoreOffer { Item = item, DiscountPercent = savedOffer.DiscountPercent, Purchased = savedOffer.Purchased });
        }

        if (_meta.TokenStoreOffers.Count == 0) RefreshTokenStoreOffers();
    }

    private void NormalizeMetaWeaponLoadoutSlots()
    {
        if (_meta.RangedWeapon?.IsHeavyWeapon == true && _meta.HeavyWeapon?.IsPrimaryWeapon == true)
        {
            (_meta.RangedWeapon, _meta.HeavyWeapon) = (_meta.HeavyWeapon, _meta.RangedWeapon);
        }

        if (_meta.RangedWeapon is not null && !_meta.RangedWeapon.IsPrimaryWeapon)
        {
            if (_meta.RangedWeapon.IsHeavyWeapon && _meta.HeavyWeapon is null)
            {
                _meta.HeavyWeapon = _meta.RangedWeapon;
                _meta.RangedWeapon = null;
            }
            else if (_meta.AddToStorage(_meta.RangedWeapon))
            {
                _meta.RangedWeapon = null;
            }
        }

        if (_meta.HeavyWeapon is not null && !_meta.HeavyWeapon.IsHeavyWeapon)
        {
            if (_meta.HeavyWeapon.IsPrimaryWeapon && _meta.RangedWeapon is null)
            {
                _meta.RangedWeapon = _meta.HeavyWeapon;
                _meta.HeavyWeapon = null;
            }
            else if (_meta.AddToStorage(_meta.HeavyWeapon))
            {
                _meta.HeavyWeapon = null;
            }
        }
    }

    public void Dispose()
    {
        SavePersistentState();
        UnloadIconTextures();
        Raylib.ShowCursor();
        Raylib.CloseWindow();
    }
}
