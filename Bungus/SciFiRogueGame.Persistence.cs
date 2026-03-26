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
            ApplyMetaSaveData(data.Meta);
            ApplyDisplayMode();
            if (migratedLegacySave) SavePersistentState();
        }
        catch
        {
            _themeIndex = 0;
            _displayMode = DisplayMode.Windowed;
            _selectedMapName = "Baselands";
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
            StorageSlots = _meta.StorageSlots.Select(ItemStack.ToSaveData).ToList(),
            Armor = ItemStack.ToSaveData(_meta.Armor),
            RangedWeapon = ItemStack.ToSaveData(_meta.RangedWeapon),
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
        _meta.StorageSlots.Clear();

        var savedSlots = data?.StorageSlots ?? [];
        for (var i = 0; i < MetaProfile.StorageCapacity; i++)
        {
            _meta.StorageSlots.Add(i < savedSlots.Count ? ItemStack.FromSaveData(savedSlots[i]) : null);
        }

        _meta.Armor = ItemStack.FromSaveData(data?.Armor);
        _meta.RangedWeapon = ItemStack.FromSaveData(data?.RangedWeapon);
        _meta.MeleeWeapon = ItemStack.FromSaveData(data?.MeleeWeapon);
        _meta.QuickSlotQ = ItemStack.FromSaveData(data?.QuickSlotQ);
        _meta.QuickSlotR = ItemStack.FromSaveData(data?.QuickSlotR);
        _meta.Trash = ItemStack.FromSaveData(data?.Trash);
    }

    public void Dispose()
    {
        SavePersistentState();
        Raylib.CloseWindow();
    }
}
