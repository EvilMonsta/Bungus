using System.Numerics;
using Raylib_cs;

namespace Bungus.Game;

public sealed partial class SciFiRogueGame
{
    private void GenerateWorldDecals()
    {
        _worldDecals.Clear();
        if (_challengeMode) return;

        var count = _currentMap.IsDeadZone ? 110 : 72;
        for (var i = 0; i < count; i++)
        {
            var position = new Vector2(
                _visualRng.NextSingle() * _worldSize,
                _visualRng.NextSingle() * _worldSize);

            if (IsPointInAnyZone(position, 8f) && _visualRng.NextSingle() < 0.55f) continue;

            var roll = _visualRng.NextSingle();
            if (roll < 0.45f)
            {
                _worldDecals.Add(new WorldDecal(position, new Vector2(20f + _visualRng.NextSingle() * 30f, 16f + _visualRng.NextSingle() * 26f), _visualRng.NextSingle() * 180f, Palette.C(24, 30, 36, 44), WorldDecalKind.Plate));
            }
            else if (roll < 0.78f)
            {
                _worldDecals.Add(new WorldDecal(position, new Vector2(14f + _visualRng.NextSingle() * 30f, 0f), 0f, Palette.C(10, 9, 8, _currentMap.IsDeadZone ? 42 : 30), WorldDecalKind.Scorch));
            }
            else
            {
                _worldDecals.Add(new WorldDecal(position, new Vector2(28f, 16f), _visualRng.NextSingle() * 180f, Palette.C(36, 44, 50, 76), WorldDecalKind.Vent));
            }
        }

        foreach (var zone in AllZones())
        {
            var plates = Math.Clamp((int)(MathF.Max(zone.Rect.Width, zone.Rect.Height) / 520f), 1, 3);
            for (var i = 0; i < plates; i++)
            {
                var position = new Vector2(
                    zone.Rect.X + 28f + _visualRng.NextSingle() * MathF.Max(1f, zone.Rect.Width - 56f),
                    zone.Rect.Y + 28f + _visualRng.NextSingle() * MathF.Max(1f, zone.Rect.Height - 56f));
                _worldDecals.Add(new WorldDecal(position, new Vector2(30f + _visualRng.NextSingle() * 34f, 18f + _visualRng.NextSingle() * 20f), _visualRng.NextSingle() * 180f, Palette.C(24, 32, 40, 52), WorldDecalKind.Plate));
            }
        }
    }

    private void GenerateBunkerDecals()
    {
        _bunkerDecals.Clear();
        foreach (var room in _bunkerRooms)
        {
            var area = room.Rect.Width * room.Rect.Height;
            var count = Math.Clamp((int)(area / 140000f), 1, 5);
            for (var i = 0; i < count; i++)
            {
                var position = new Vector2(
                    room.Rect.X + 44f + _visualRng.NextSingle() * MathF.Max(1f, room.Rect.Width - 88f),
                    room.Rect.Y + 44f + _visualRng.NextSingle() * MathF.Max(1f, room.Rect.Height - 88f));
                var roll = _visualRng.NextSingle();
                var color = room.Id == 19 ? Palette.C(90, 28, 48, 58) : Palette.C(46, 54, 62, 70);
                var kind = roll < 0.58f ? WorldDecalKind.Plate : roll < 0.86f ? WorldDecalKind.Vent : WorldDecalKind.Scorch;
                var size = kind switch
                {
                    WorldDecalKind.Vent => new Vector2(34f, 20f),
                    WorldDecalKind.Scorch => new Vector2(22f + _visualRng.NextSingle() * 34f, 0f),
                    _ => new Vector2(30f + _visualRng.NextSingle() * 40f, 20f + _visualRng.NextSingle() * 22f)
                };
                _bunkerDecals.Add(new WorldDecal(position, size, _visualRng.NextSingle() * 180f, color, kind));
            }
        }
    }
}
