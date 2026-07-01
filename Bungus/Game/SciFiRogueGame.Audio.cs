using Raylib_cs;

namespace Bungus.Game;

public sealed partial class SciFiRogueGame
{
    private Sound _baseShotSound;
    private Sound _enemyShotSound;
    private Sound _baseSlashSound;
    private Sound[] _baseShotAliases = [];
    private Sound[] _enemyShotAliases = [];
    private Sound[] _baseSlashAliases = [];
    private int _baseShotAliasCursor;
    private int _enemyShotAliasCursor;
    private int _baseSlashAliasCursor;
    private bool _audioReady;
    private bool _baseShotSoundReady;
    private bool _enemyShotSoundReady;
    private bool _baseSlashSoundReady;

    private void InitializeAudio()
    {
        try
        {
            Raylib.InitAudioDevice();
            _audioReady = Raylib.IsAudioDeviceReady();
            if (!_audioReady) return;

            _baseShotSoundReady = TryLoadSound(
                Path.Combine(AppContext.BaseDirectory, "Assets", "Sounds", "Weapons", "base_shot.wav"),
                out _baseShotSound,
                0.55f);
            if (_baseShotSoundReady) _baseShotAliases = CreateSoundAliases(_baseShotSound, 32, 0.55f);
            _enemyShotSoundReady = TryLoadSound(
                Path.Combine(AppContext.BaseDirectory, "Assets", "Sounds", "Enemies", "enemy_base_sound.wav"),
                out _enemyShotSound,
                0.42f);
            if (_enemyShotSoundReady) _enemyShotAliases = CreateSoundAliases(_enemyShotSound, 128, 0.42f);
            _baseSlashSoundReady = TryLoadSound(
                Path.Combine(AppContext.BaseDirectory, "Assets", "Sounds", "Weapons", "base_slash.wav"),
                out _baseSlashSound,
                0.62f);
            if (_baseSlashSoundReady) _baseSlashAliases = CreateSoundAliases(_baseSlashSound, 16, 0.62f);
        }
        catch
        {
            _audioReady = false;
            _baseShotSoundReady = false;
            _enemyShotSoundReady = false;
            _baseSlashSoundReady = false;
        }
    }

    private static bool TryLoadSound(string path, out Sound sound, float volume)
    {
        sound = default;
        if (!File.Exists(path)) return false;

        sound = Raylib.LoadSound(path);
        if (!Raylib.IsSoundReady(sound)) return false;

        Raylib.SetSoundVolume(sound, volume);
        return true;
    }

    private static Sound[] CreateSoundAliases(Sound source, int count, float volume)
    {
        var aliases = new List<Sound>(count);
        for (var i = 0; i < count; i++)
        {
            var alias = Raylib.LoadSoundAlias(source);
            if (!Raylib.IsSoundReady(alias)) continue;
            Raylib.SetSoundVolume(alias, volume);
            aliases.Add(alias);
        }

        return aliases.ToArray();
    }

    private void PlayPlayerShotSound()
    {
        if (!_audioReady || !_baseShotSoundReady) return;
        PlayOverlappingSound(_baseShotAliases, ref _baseShotAliasCursor);
    }

    private void PlayPlayerShotSounds(int count)
    {
        for (var i = 0; i < count; i++) PlayPlayerShotSound();
    }

    private void PlayEnemyShotSound()
    {
        if (!_audioReady || !_enemyShotSoundReady) return;
        PlayOverlappingSound(_enemyShotAliases, ref _enemyShotAliasCursor);
    }

    private void PlayEnemyShotSounds(int count)
    {
        for (var i = 0; i < count; i++) PlayEnemyShotSound();
    }

    private void PlayPlayerSlashSound()
    {
        if (!_audioReady || !_baseSlashSoundReady) return;
        PlayOverlappingSound(_baseSlashAliases, ref _baseSlashAliasCursor);
    }

    private void PlayPlayerSlashSounds(int count)
    {
        for (var i = 0; i < count; i++) PlayPlayerSlashSound();
    }

    private static void PlayOverlappingSound(Sound[] aliases, ref int cursor)
    {
        if (aliases.Length == 0) return;

        for (var i = 0; i < aliases.Length; i++)
        {
            var index = (cursor + i) % aliases.Length;
            if (Raylib.IsSoundPlaying(aliases[index])) continue;

            Raylib.PlaySound(aliases[index]);
            cursor = (index + 1) % aliases.Length;
            return;
        }
    }

    private void DisposeAudio()
    {
        if (!_audioReady) return;
        UnloadSoundAliases(_baseShotAliases);
        UnloadSoundAliases(_enemyShotAliases);
        UnloadSoundAliases(_baseSlashAliases);
        if (_baseShotSoundReady) Raylib.UnloadSound(_baseShotSound);
        if (_enemyShotSoundReady) Raylib.UnloadSound(_enemyShotSound);
        if (_baseSlashSoundReady) Raylib.UnloadSound(_baseSlashSound);
        Raylib.CloseAudioDevice();
        _baseShotSoundReady = false;
        _enemyShotSoundReady = false;
        _baseSlashSoundReady = false;
        _baseShotAliases = [];
        _enemyShotAliases = [];
        _baseSlashAliases = [];
        _audioReady = false;
    }

    private static void UnloadSoundAliases(Sound[] aliases)
    {
        foreach (var alias in aliases)
        {
            if (Raylib.IsSoundPlaying(alias)) Raylib.StopSound(alias);
            Raylib.UnloadSoundAlias(alias);
        }
    }
}
