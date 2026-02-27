using System.Numerics;
using Raylib_cs;

namespace Bungus.Game;

public static class Program
{
    public static void Main()
    {
        using var game = new SciFiRogueGame();
        game.Run();
    }
}

public enum GameState { MainMenu, Settings, Playing, Paused, Death }
public enum WeaponClass { Melee, Ranged }
public enum ItemType { Weapon, Armor, Consumable }
public enum ConsumableType { Medkit, Stim }
public enum ArmorRarity { Common, Rare, Epic, Legendary }
public enum StatType { Strength, Dexterity, Speed, Gunsmith }
public enum WeaponPattern { Standard, PulseRifle, EnergySpear }

public static class Palette
{
    public static Color C(int r, int g, int b, int a = 255) => new((byte)r, (byte)g, (byte)b, (byte)a);

    public static Color Rarity(ArmorRarity r) => r switch
    {
        ArmorRarity.Common => Color.LightGray,
        ArmorRarity.Rare => Color.SkyBlue,
        ArmorRarity.Epic => C(191, 120, 255),
        ArmorRarity.Legendary => Color.Gold,
        _ => Color.White
    };
}

public sealed record VisualTheme(
    string Name,
    Color Background,
    Color Grid,
    Color BuildingFill,
    Color BuildingLine,
    Color OutpostFill,
    Color OutpostLine,
    Color ObstacleFill,
    Color ObstacleLine,
    Color Player,
    Color Enemy,
    Color EnemyStrong,
    Color Boss);

public sealed class SciFiRogueGame : IDisposable
{
    private const int W = 1280;
    private const int H = 720;
    private const int World = 6000;
    private const float MinZoneGap = 300f;
    private const float CenterNoZoneRadius = 850f;

    private readonly Random _rng = new();
    private Camera2D _camera;

    private GameState _state = GameState.MainMenu;
    private Player _player = null!;

    private List<Enemy> _enemies = [];
    private List<BossEnemy> _bosses = [];
    private List<Projectile> _projectiles = [];
    private List<Explosion> _explosions = [];
    private List<SwingArc> _swings = [];
    private List<DashAfterImage> _dashAfterImages = [];

    private List<LootZone> _buildings = [];
    private List<LootZone> _outposts = [];
    private List<Obstacle> _obstacles = [];
    private List<LootChest> _chests = [];

    private DragPayload? _drag;
    private ItemStack? _hovered;
    private bool _pendingUpgrade;
    private StatType _pendingStat;
    private int? _openedChestIndex;
    private bool _requestExit;
    private readonly List<VisualTheme> _themes;
    private int _themeIndex;

    public SciFiRogueGame()
    {
        Raylib.SetConfigFlags(ConfigFlags.Msaa4xHint);
        Raylib.InitWindow(W, H, "Bungus");
        Raylib.SetTargetFPS(60);
        Raylib.SetExitKey(KeyboardKey.Null);
        Raylib.ToggleFullscreen();

        _camera = new Camera2D { Zoom = 1.08f, Rotation = 0f };
        _themes = BuildThemes();
        StartRun();
    }

    private VisualTheme Theme => _themes[_themeIndex];

    private void StartRun()
    {
        _player = Player.Create(new Vector2(World / 2f, World / 2f));
        _projectiles = [];
        _explosions = [];
        _swings = [];

        (_buildings, _outposts) = GenerateZones(_rng.Next(14, 21), _rng.Next(7, 11));
        _obstacles = GenerateObstacles();
        _chests = GenerateChestsInZones();
        _enemies = GenerateEnemies();
        _bosses = GenerateBosses();

        _camera.Offset = new Vector2(Raylib.GetScreenWidth() / 2f, Raylib.GetScreenHeight() / 2f);
        _camera.Target = _player.Position;
    }

    public void Run()
    {
        while (!Raylib.WindowShouldClose())
        {
            var dt = Raylib.GetFrameTime();
            Update(dt);
            if (_requestExit) break;
            Draw();
        }
    }

    private void Update(float dt)
    {
        switch (_state)
        {
            case GameState.MainMenu: UpdateMainMenu(); break;
            case GameState.Settings: UpdateSettings(); break;
            case GameState.Playing: UpdatePlaying(dt); break;
            case GameState.Paused: UpdatePause(); break;
            case GameState.Death: UpdateDeath(); break;
        }
    }

    private void UpdateMainMenu()
    {
        if (Clicked(CenterRect(0, 250, 320, 62))) { StartRun(); _state = GameState.Playing; }
        if (Clicked(CenterRect(0, 330, 320, 62))) _state = GameState.Settings;
        if (Clicked(CenterRect(0, 410, 320, 62))) _requestExit = true;
    }


    private void UpdateSettings()
    {
        for (var i = 0; i < _themes.Count; i++)
        {
            if (Clicked(CenterRect(0, 220 + i * 68, 360, 56))) _themeIndex = i;
        }

        if (Clicked(CenterRect(0, 620, 280, 56)) || Raylib.IsKeyPressed(KeyboardKey.Escape)) _state = GameState.MainMenu;
    }

    private void UpdatePause()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape)) _state = GameState.Playing;
        if (Clicked(CenterRect(0, 350, 320, 62))) _state = GameState.MainMenu;
    }

    private void UpdateDeath()
    {
        if (Clicked(CenterRect(0, 320, 320, 62))) { StartRun(); _state = GameState.Playing; }
        if (Clicked(CenterRect(0, 400, 320, 62))) _state = GameState.MainMenu;
    }

    private void UpdatePlaying(float dt)
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape)) { _state = GameState.Paused; return; }
        if (Raylib.IsKeyPressed(KeyboardKey.Tab))
        {
            _player.InventoryOpen = !_player.InventoryOpen;
            if (!_player.InventoryOpen) _openedChestIndex = null;
        }

        _player.Update(dt, _obstacles, World, _dashAfterImages);
        if (Raylib.IsKeyPressed(KeyboardKey.Q)) _player.UseMedkit();
        if (Raylib.IsKeyPressed(KeyboardKey.R)) _player.UseStim();
        if (Raylib.IsKeyPressed(KeyboardKey.E)) _player.SwitchActiveWeapon();

        var mouseWorld = Raylib.GetScreenToWorld2D(Raylib.GetMousePosition(), _camera);
        if (Raylib.IsMouseButtonDown(MouseButton.Left) && !_player.InventoryOpen)
        {
            _player.Attack(mouseWorld, _projectiles, _swings);
        }

        UpdateEnemies(dt);
        UpdateBosses(dt);
        UpdateProjectiles(dt);
        UpdateSwings(dt);
        UpdateEffects(dt);
        UpdateChests();
        UpdateInventoryUi();
        UpdateLevelUi();

        _camera.Target = Vector2.Lerp(_camera.Target, _player.Position, 0.2f);
        if (_player.Health <= 0) _state = GameState.Death;
    }

    private void UpdateEnemies(float dt)
    {
        foreach (var e in _enemies)
        {
            e.UpdateVisionSweep(dt);
            e.UpdateAwareness(_player.Position, dt, _obstacles);
            e.UpdateMovement(dt, _player.Position, _obstacles, World);
            e.TryShootBurst(_player.Position, _projectiles);

            if (e.TryMeleeHit(_player) && _rng.NextSingle() <= _player.GetStatusEffectChance(0.05f))
            {
                _player.ApplyBleed(3f);
            }

            if (!e.Alive && !e.KillAwarded)
            {
                e.KillAwarded = true;
                _player.RegisterKill();
            }
        }

        // aggro propagation from visible combat
        foreach (var src in _enemies.Where(x => x.JustHitByPlayer))
        {
            src.JustHitByPlayer = false;
            foreach (var other in _enemies.Where(x => x != src && x.Alive))
            {
                if (other.CanSeePoint(src.Position, _obstacles)) other.ForceAggro(src.Position);
            }
        }
    }

    private void UpdateBosses(float dt)
    {
        foreach (var b in _bosses)
        {
            b.Update(dt, _player.Position, _projectiles, _player, _obstacles, World, _dashAfterImages);
            if (!b.Alive && !b.KillAwarded)
            {
                b.KillAwarded = true;
                _player.RegisterKill();
                _player.RegisterKill();
                _player.RegisterKill();
            }
        }
    }

    private void UpdateProjectiles(float dt)
    {
        for (var i = _projectiles.Count - 1; i >= 0; i--)
        {
            var p = _projectiles[i];
            p.Update(dt);

            if (p.Position.X < 0 || p.Position.Y < 0 || p.Position.X > World || p.Position.Y > World || MovementUtils.CircleHitsObstacle(p.Position, 3f, _obstacles))
            {
                _projectiles.RemoveAt(i);
                continue;
            }

            if (p.OwnerEnemy)
            {
                if (Vector2.Distance(p.Position, _player.Position) < 14f)
                {
                    _player.TakeDamage(p.Damage);
                    _explosions.Add(new Explosion(p.Position, 26f, p.Color));
                    _projectiles.RemoveAt(i);
                }
                else if (!p.Alive) _projectiles.RemoveAt(i);
                continue;
            }

            Enemy? enemyHit = _enemies.FirstOrDefault(e => e.Alive && Vector2.Distance(e.Position, p.Position) < 15f);
            if (enemyHit is not null)
            {
                enemyHit.Damage(p.Damage);
                enemyHit.ForceAggro(_player.Position);
                enemyHit.JustHitByPlayer = true;
                _explosions.Add(new Explosion(p.Position, 34f, p.Color));
                _projectiles.RemoveAt(i);
                continue;
            }

            BossEnemy? bossHit = _bosses.FirstOrDefault(b => b.Alive && Vector2.Distance(b.Position, p.Position) < 30f);
            if (bossHit is not null)
            {
                bossHit.Damage(p.Damage);
                _explosions.Add(new Explosion(p.Position, 34f, p.Color));
                _projectiles.RemoveAt(i);
                continue;
            }

            if (!p.Alive) _projectiles.RemoveAt(i);
        }
    }

    private void UpdateSwings(float dt)
    {
        for (var i = _swings.Count - 1; i >= 0; i--)
        {
            var s = _swings[i];
            s.Life -= dt;
            if (s.Life <= 0f)
            {
                _swings.RemoveAt(i);
                continue;
            }

            foreach (var e in _enemies.Where(x => x.Alive))
            {
                var hit = s.IsLine
                    ? DistanceToSegment(e.Position, s.LineStart, s.LineEnd) < 16f
                    : IsInArc(e.Position, s, 8f);
                if (!hit) continue;
                e.Damage(_player.GetMeleeDamage());
                e.ForceAggro(_player.Position);
                e.JustHitByPlayer = true;
            }

            foreach (var b in _bosses.Where(x => x.Alive))
            {
                var hit = s.IsLine
                    ? DistanceToSegment(b.Position, s.LineStart, s.LineEnd) < 28f
                    : IsInArc(b.Position, s, 24f);
                if (hit) b.Damage(_player.GetMeleeDamage() * 0.75f);
            }
        }
    }

    private void UpdateEffects(float dt)
    {
        _player.TickEffects(dt);
        for (var i = _explosions.Count - 1; i >= 0; i--)
        {
            _explosions[i].Life -= dt;
            if (_explosions[i].Life <= 0) _explosions.RemoveAt(i);
        }

        for (var i = _dashAfterImages.Count - 1; i >= 0; i--)
        {
            _dashAfterImages[i].Life -= dt * 3f;
            if (_dashAfterImages[i].Life <= 0f) _dashAfterImages.RemoveAt(i);
        }
    }

    private void UpdateChests()
    {
        for (var i = 0; i < _chests.Count; i++)
        {
            var chest = _chests[i];
            if (Vector2.Distance(chest.Position, _player.Position) > 28f) continue;
            if (!Raylib.IsKeyPressed(KeyboardKey.F)) continue;

            chest.Opened = true;
            _openedChestIndex = i;
            _player.InventoryOpen = true;
            break;
        }

        if (_openedChestIndex is null) return;

        var openedChest = _chests[_openedChestIndex.Value];
        if (Vector2.Distance(openedChest.Position, _player.Position) > 120f)
        {
            _openedChestIndex = null;
            return;
        }

        if (openedChest.Items.Count == 0)
        {
            _openedChestIndex = null;
        }
    }

    private void UpdateInventoryUi()
    {
        _hovered = null;
        if (!_player.InventoryOpen) return;

        var slots = BuildSlots();
        var m = Raylib.GetMousePosition();

        foreach (var s in slots)
        {
            if (Raylib.CheckCollisionPointRec(m, s.Rect)) _hovered = s.Item;
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            var from = slots.FirstOrDefault(s => s.Item is not null && Raylib.CheckCollisionPointRec(m, s.Rect));
            if (from is not null)
            {
                _drag = new DragPayload(from.Kind, from.Index, from.Item!);
            }
        }

        if (Raylib.IsMouseButtonReleased(MouseButton.Left) && _drag is not null)
        {
            var to = slots.FirstOrDefault(s => Raylib.CheckCollisionPointRec(m, s.Rect));
            if (to is not null) ApplyDrop(_drag, to);
            _drag = null;
        }
    }

    private void UpdateLevelUi()
    {
        if (!_player.InventoryOpen || _player.StatPoints <= 0) return;
        if (Clicked(new Rectangle(252, 174, 22, 22))) { _pendingUpgrade = true; _pendingStat = StatType.Strength; }
        if (Clicked(new Rectangle(252, 204, 22, 22))) { _pendingUpgrade = true; _pendingStat = StatType.Dexterity; }
        if (Clicked(new Rectangle(252, 234, 22, 22))) { _pendingUpgrade = true; _pendingStat = StatType.Speed; }
        if (Clicked(new Rectangle(252, 264, 22, 22))) { _pendingUpgrade = true; _pendingStat = StatType.Gunsmith; }
        if (_pendingUpgrade && Clicked(new Rectangle(54, 326, 120, 30)))
        {
            _player.ApplyPoint(_pendingStat);
            _pendingUpgrade = false;
        }
    }

    private List<UiSlot> BuildSlots()
    {
        var list = new List<UiSlot>();

        var backpackOrigin = _openedChestIndex is null ? new Vector2(700, 118) : new Vector2(70, 190);
        for (var i = 0; i < _player.Inventory.BackpackSlots.Count; i++)
        {
            var c = i % 6;
            var r = i / 6;
            list.Add(new UiSlot(new Rectangle(backpackOrigin.X + c * 62, backpackOrigin.Y + r * 62, 58, 58), SlotKind.Backpack, i, _player.Inventory.BackpackSlots[i], i));
        }

        if (_openedChestIndex is null)
        {
            list.AddRange(
            [
                new UiSlot(new Rectangle(560, 118, 58, 58), SlotKind.Armor, null, _player.Armor, -1),
                new UiSlot(new Rectangle(560, 186, 58, 58), SlotKind.RangedWeapon, null, _player.RangedWeapon, -1),
                new UiSlot(new Rectangle(560, 250, 58, 58), SlotKind.MeleeWeapon, null, _player.MeleeWeapon, -1),
                new UiSlot(new Rectangle(560, 348, 58, 58), SlotKind.MedkitSlot, null, _player.Inventory.MedkitSlot, -1),
                new UiSlot(new Rectangle(624, 348, 58, 58), SlotKind.StimSlot, null, _player.Inventory.StimSlot, -1),
                new UiSlot(new Rectangle(1160, 470, 58, 58), SlotKind.Trash, null, _player.Inventory.Trash, -1)
            ]);
        }

        if (_openedChestIndex is not null)
        {
            var chest = _chests[_openedChestIndex.Value];
            for (var i = 0; i < 5; i++)
            {
                var item = i < chest.Items.Count ? chest.Items[i] : null;
                list.Add(new UiSlot(new Rectangle(760 + i * 62, 190, 58, 58), SlotKind.Chest, i, item, i));
            }
        }

        return list;
    }

    private void ApplyDrop(DragPayload drag, UiSlot target)
    {
        if (target.Kind == SlotKind.Trash)
        {
            _player.Inventory.Trash = null;
            _player.Inventory.Trash = drag.Item;
            RemoveFromSource(drag);
            return;
        }

        if (target.Kind == SlotKind.Armor && drag.Item.Type == ItemType.Armor)
        {
            var old = _player.Armor;
            _player.Armor = drag.Item;
            RemoveFromSource(drag);
            if (old is not null) _player.Inventory.AddToBackpack(old);
            return;
        }

        if (target.Kind == SlotKind.RangedWeapon && drag.Item.Type == ItemType.Weapon && drag.Item.WeaponKind == WeaponClass.Ranged)
        {
            var old = _player.RangedWeapon;
            _player.RangedWeapon = drag.Item;
            RemoveFromSource(drag);
            if (old is not null) _player.Inventory.AddToBackpack(old);
            return;
        }

        if (target.Kind == SlotKind.MeleeWeapon && drag.Item.Type == ItemType.Weapon && drag.Item.WeaponKind == WeaponClass.Melee)
        {
            var old = _player.MeleeWeapon;
            _player.MeleeWeapon = drag.Item;
            RemoveFromSource(drag);
            if (old is not null) _player.Inventory.AddToBackpack(old);
            return;
        }

        if (target.Kind == SlotKind.MedkitSlot && drag.Item.Type == ItemType.Consumable && drag.Item.ConsumableKind == ConsumableType.Medkit)
        {
            var old = _player.Inventory.MedkitSlot;
            _player.Inventory.MedkitSlot = drag.Item;
            RemoveFromSource(drag);
            if (old is not null) _player.Inventory.AddToBackpack(old);
            return;
        }

        if (target.Kind == SlotKind.StimSlot && drag.Item.Type == ItemType.Consumable && drag.Item.ConsumableKind == ConsumableType.Stim)
        {
            var old = _player.Inventory.StimSlot;
            _player.Inventory.StimSlot = drag.Item;
            RemoveFromSource(drag);
            if (old is not null) _player.Inventory.AddToBackpack(old);
            return;
        }

        if (target.Kind == SlotKind.Backpack && drag.Kind == SlotKind.Backpack && drag.Index >= 0 && target.Index >= 0)
        {
            (_player.Inventory.BackpackSlots[drag.Index], _player.Inventory.BackpackSlots[target.Index]) =
                (_player.Inventory.BackpackSlots[target.Index], _player.Inventory.BackpackSlots[drag.Index]);
            return;
        }

        if (target.Kind == SlotKind.Backpack && target.Index >= 0)
        {
            if (_player.Inventory.BackpackSlots[target.Index] is null)
            {
                _player.Inventory.BackpackSlots[target.Index] = drag.Item;
                RemoveFromSource(drag);
            }
            return;
        }

        if (target.Kind == SlotKind.Chest && _openedChestIndex is not null)
        {
            var chest = _chests[_openedChestIndex.Value];
            if (drag.Kind == SlotKind.Chest && drag.Index >= 0 && target.Index >= 0 && drag.Index < chest.Items.Count && target.Index < chest.Items.Count)
            {
                (chest.Items[drag.Index], chest.Items[target.Index]) = (chest.Items[target.Index], chest.Items[drag.Index]);
                return;
            }

            if (drag.Kind != SlotKind.Chest && chest.Items.Count < 5)
            {
                var insertAt = Math.Clamp(target.Index, 0, chest.Items.Count);
                chest.Items.Insert(insertAt, drag.Item);
                RemoveFromSource(drag);
            }
        }
    }

    private void RemoveFromSource(DragPayload drag)
    {
        if (drag.Kind == SlotKind.Backpack && drag.Index >= 0 && drag.Index < _player.Inventory.BackpackSlots.Count)
        {
            _player.Inventory.BackpackSlots[drag.Index] = null;
        }
        else if (drag.Kind == SlotKind.Armor)
        {
            _player.Armor = null;
        }
        else if (drag.Kind == SlotKind.RangedWeapon)
        {
            _player.RangedWeapon = null;
        }
        else if (drag.Kind == SlotKind.MeleeWeapon)
        {
            _player.MeleeWeapon = null;
        }
        else if (drag.Kind == SlotKind.MedkitSlot)
        {
            _player.Inventory.MedkitSlot = null;
        }
        else if (drag.Kind == SlotKind.StimSlot)
        {
            _player.Inventory.StimSlot = null;
        }
        else if (drag.Kind == SlotKind.Chest && _openedChestIndex is not null && drag.Index >= 0)
        {
            _chests[_openedChestIndex.Value].Items.RemoveAt(drag.Index);
        }
    }

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

    private void Draw()
    {
        Raylib.BeginDrawing();
        Raylib.ClearBackground(Theme.Background);

        switch (_state)
        {
            case GameState.MainMenu:
                DrawMainMenu();
                break;
            case GameState.Settings:
                DrawSettings();
                break;
            case GameState.Playing:
                DrawWorld();
                DrawHud();
                DrawInventory();
                break;
            case GameState.Paused:
                DrawWorld();
                DrawHud();
                DrawPause();
                break;
            case GameState.Death:
                DrawWorld();
                DrawDeath();
                break;
        }

        Raylib.EndDrawing();
    }

    private void DrawWorld()
    {
        Raylib.BeginMode2D(_camera);
        DrawGrid();

        foreach (var b in _buildings)
        {
            Raylib.DrawRectangleRec(b.Rect, Theme.BuildingFill);
            Raylib.DrawRectangleLinesEx(b.Rect, 2f, Theme.BuildingLine);
        }

        foreach (var o in _outposts)
        {
            Raylib.DrawRectangleRec(o.Rect, Theme.OutpostFill);
            Raylib.DrawRectangleLinesEx(o.Rect, 2f, Theme.OutpostLine);
        }

        foreach (var obstacle in _obstacles)
        {
            Raylib.DrawRectangleRec(obstacle.Rect, Theme.ObstacleFill);
            Raylib.DrawRectangleLinesEx(obstacle.Rect, 1.5f, Theme.ObstacleLine);
        }

        foreach (var chest in _chests)
        {
            var rect = new Rectangle(chest.Position.X - 14, chest.Position.Y - 10, 28, 20);
            Raylib.DrawRectangleRec(rect, chest.Opened ? Palette.C(65, 65, 65, 180) : Palette.C(122, 82, 38, 240));
            Raylib.DrawRectangleLinesEx(rect, 1.5f, chest.Opened ? Color.Gray : Color.Gold);
            Raylib.DrawLine((int)rect.X, (int)(rect.Y + rect.Height / 2), (int)(rect.X + rect.Width), (int)(rect.Y + rect.Height / 2), Color.Black);

            if (Vector2.Distance(chest.Position, _player.Position) < 30f)
            {
                Raylib.DrawText("F", (int)rect.X + 10, (int)rect.Y - 18, 18, Color.Gold);
            }
        }

        foreach (var ghost in _dashAfterImages) ghost.Draw();

        foreach (var e in _enemies) e.DrawSight();
        foreach (var b in _bosses) b.DrawSight();
        foreach (var e in _enemies) e.Draw(Theme);
        foreach (var b in _bosses) b.Draw(Theme);

        foreach (var p in _projectiles)
        {
            Raylib.DrawCircleV(p.Position, 4f, p.Color);
        }

        foreach (var ex in _explosions)
        {
            var t = ex.Life / ex.MaxLife;
            var r = ex.Radius * (1f - t);
            Raylib.DrawCircleLines((int)ex.Position.X, (int)ex.Position.Y, r, ex.Color);
        }

        foreach (var s in _swings)
        {
            if (s.IsLine)
            {
                Raylib.DrawLineEx(s.LineStart, s.LineEnd, 8f, s.Color);
            }
            else
            {
                for (var i = 0; i < 18; i++)
                {
                    var p = i / 18f;
                    var a = s.AngleStart + (s.AngleEnd - s.AngleStart) * p;
                    var point = s.Origin + new Vector2(MathF.Cos(a), MathF.Sin(a)) * s.Radius;
                    Raylib.DrawCircleV(point, 3f, s.Color);
                }
            }
        }

        Raylib.DrawRectangleLinesEx(new Rectangle(0, 0, World, World), 6f, Palette.C(120, 160, 220));
        Raylib.DrawCircleV(_player.Position, 16f, Theme.Player);
        DrawZoneArrows();
        Raylib.EndMode2D();
    }

    private void DrawHud()
    {
        Raylib.DrawText($"HP {_player.Health:0}/{_player.MaxHealth:0} | Level {_player.Level} ({_player.Kills}/{_player.KillsTarget})", 20, 14, 24, Color.White);

        var activeWeapon = _player.ActiveWeaponClass == WeaponClass.Ranged ? _player.RangedWeapon : _player.MeleeWeapon;
        Raylib.DrawText($"Current: {activeWeapon?.Name ?? "None"} {BuildWeaponDamageText(activeWeapon, _player.ActiveWeaponClass)}", 20, 48, 22, activeWeapon?.Color ?? Color.LightGray);
        Raylib.DrawText($"Consumables: Q [{(_player.Inventory.MedkitSlot is null ? "-" : "Medkit")}]  R [{(_player.Inventory.StimSlot is null ? "-" : "Stim")}]", 20, 78, 20, Color.White);
        Raylib.DrawText("WASD move | LMB attack | E switch active weapon | TAB inventory | ESC menu", 20, Raylib.GetScreenHeight() - 28, 18, Color.Gray);
    }

    private void DrawInventory()
    {
        if (!_player.InventoryOpen) return;

        var slots = BuildSlots();
        if (_openedChestIndex is null)
        {
            Raylib.DrawRectangle(32, 104, 1216, 460, Palette.C(6, 10, 20, 220));
            Raylib.DrawRectangleLines(32, 104, 1216, 460, Color.SkyBlue);
            Raylib.DrawText("Inventory", 42, 116, 24, Color.White);

            DrawBackpackGrid(new Vector2(700, 118), 6, 5);
            Raylib.DrawText("Backpack", 700, 86, 20, Color.LightGray);
            Raylib.DrawText("Equipment", 560, 86, 20, Color.LightGray);
            Raylib.DrawText("Stats", 54, 146, 20, Color.LightGray);

            Raylib.DrawText($"STR {_player.Str}", 54, 176, 20, Color.LightGray);
            Raylib.DrawText($"DEX {_player.Dex}", 54, 206, 20, Color.LightGray);
            Raylib.DrawText($"SPD {_player.Spd}", 54, 236, 20, Color.LightGray);
            Raylib.DrawText($"GUN {_player.Guns}", 54, 266, 20, Color.LightGray);
            Raylib.DrawText($"Points {_player.StatPoints}", 54, 296, 20, Color.Yellow);

            if (_player.StatPoints > 0)
            {
                DrawPlus(new Rectangle(252, 174, 22, 22));
                DrawPlus(new Rectangle(252, 204, 22, 22));
                DrawPlus(new Rectangle(252, 234, 22, 22));
                DrawPlus(new Rectangle(252, 264, 22, 22));
                if (_pendingUpgrade) DrawButton(new Rectangle(54, 326, 120, 30), "Confirm");
            }

            DrawStatTooltip();
        }
        else
        {
            Raylib.DrawRectangle(40, 138, 430, 370, Palette.C(6, 10, 20, 220));
            Raylib.DrawRectangleLines(40, 138, 430, 370, Color.SkyBlue);
            Raylib.DrawText("Backpack", 50, 150, 24, Color.White);
            DrawBackpackGrid(new Vector2(70, 190), 6, 5);

            Raylib.DrawRectangle(730, 138, 350, 170, Palette.C(6, 10, 20, 220));
            Raylib.DrawRectangleLines(730, 138, 350, 170, Color.SkyBlue);
            Raylib.DrawText("Chest", 740, 150, 24, Color.White);
            DrawBackpackGrid(new Vector2(760, 190), 5, 1);
        }

        foreach (var slot in slots)
        {
            Raylib.DrawRectangleRec(slot.Rect, Palette.C(22, 28, 42, 255));
            Raylib.DrawRectangleLinesEx(slot.Rect, 1f, Color.SkyBlue);
            if (slot.Kind == SlotKind.Trash) Raylib.DrawText("TR", (int)slot.Rect.X + 16, (int)slot.Rect.Y + 18, 20, Color.Orange);
            if (slot.Kind == SlotKind.MedkitSlot) Raylib.DrawText("Q", (int)slot.Rect.X + 20, (int)slot.Rect.Y - 18, 16, Color.Green);
            if (slot.Kind == SlotKind.StimSlot) Raylib.DrawText("R", (int)slot.Rect.X + 20, (int)slot.Rect.Y - 18, 16, Color.Yellow);
            if (slot.Item is not null) DrawItemIcon(slot.Item, new Rectangle(slot.Rect.X + 8, slot.Rect.Y + 8, 42, 42));
        }

        if (_drag is not null)
        {
            var m = Raylib.GetMousePosition();
            DrawItemIcon(_drag.Item, new Rectangle(m.X + 8, m.Y + 8, 34, 34));
        }

        if (_hovered is not null) DrawTooltip(_hovered, Raylib.GetMousePosition());
    }

    private static void DrawBackpackGrid(Vector2 origin, int cols, int rows)
    {
        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                var rect = new Rectangle(origin.X + c * 62, origin.Y + r * 62, 58, 58);
                Raylib.DrawRectangleLinesEx(rect, 1f, Palette.C(70, 90, 130, 170));
            }
        }
    }

    private static void DrawItemIcon(ItemStack item, Rectangle rect)
    {
        if (item.Type == ItemType.Armor)
        {
            Raylib.DrawRectangleRec(rect, item.Color);
            Raylib.DrawText("AR", (int)rect.X + 8, (int)rect.Y + 10, 18, Color.Black);
        }
        else if (item.Type == ItemType.Weapon)
        {
            Raylib.DrawRectangleRec(rect, item.Color);
            Raylib.DrawText(item.WeaponKind == WeaponClass.Ranged ? "RW" : "MW", (int)rect.X + 4, (int)rect.Y + 10, 18, Color.Black);
        }
        else
        {
            Raylib.DrawRectangleRec(rect, Palette.C(130, 210, 120));
            Raylib.DrawText("CS", (int)rect.X + 8, (int)rect.Y + 10, 18, Color.Black);
        }
    }

    private static void DrawTooltip(ItemStack item, Vector2 mouse)
    {
        var x = (int)mouse.X + 20;
        var y = (int)mouse.Y + 14;
        Raylib.DrawRectangle(x, y, 300, 96, Palette.C(0, 0, 0, 220));
        Raylib.DrawRectangleLines(x, y, 300, 96, Color.SkyBlue);
        Raylib.DrawText(item.Name, x + 8, y + 8, 18, Color.White);
        Raylib.DrawText(item.Description, x + 8, y + 32, 16, Color.LightGray);
        if (item.Type == ItemType.Armor) Raylib.DrawText($"Defense: {item.Defense:0} | {item.Rarity}", x + 8, y + 58, 16, item.Color);
        if (item.Type == ItemType.Weapon) Raylib.DrawText($"Damage bonus: {item.PowerBonus:0} | {item.WeaponKind}", x + 8, y + 58, 16, item.Color);
        if (item.Type == ItemType.Consumable) Raylib.DrawText("Use by Q/R", x + 8, y + 58, 16, Color.Green);
    }

    private static void DrawPlus(Rectangle r)
    {
        Raylib.DrawRectangleRec(r, Palette.C(42, 95, 180));
        Raylib.DrawText("+", (int)r.X + 5, (int)r.Y - 1, 24, Color.White);
    }

    private void DrawGrid()
    {
        for (var x = 0; x < World; x += 80) Raylib.DrawLine(x, 0, x, World, Theme.Grid);
        for (var y = 0; y < World; y += 80) Raylib.DrawLine(0, y, World, y, Theme.Grid);
    }

    private void DrawMainMenu()
    {
        DrawTitle("BUNGUS", 120, 80);
        DrawButton(CenterRect(0, 250, 320, 62), "Play");
        DrawButton(CenterRect(0, 330, 320, 62), "Settings");
        DrawButton(CenterRect(0, 410, 320, 62), "Exit");
    }

    private void DrawSettings()
    {
        DrawTitle("Settings", 100, 66);
        Raylib.DrawText("Choose theme", (Raylib.GetScreenWidth() - Raylib.MeasureText("Choose theme", 28)) / 2, 170, 28, Color.LightGray);
        for (var i = 0; i < _themes.Count; i++)
        {
            var name = i == _themeIndex ? $"> {_themes[i].Name} <" : _themes[i].Name;
            DrawButton(CenterRect(0, 220 + i * 68, 360, 56), name);
        }

        DrawButton(CenterRect(0, 620, 280, 56), "Back");
    }

    private void DrawPause()
    {
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), Palette.C(0, 0, 0, 175));
        DrawTitle("Paused", 170, 64);
        DrawButton(CenterRect(0, 350, 320, 62), "Back to menu");
    }

    private void DrawDeath()
    {
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), Palette.C(0, 0, 0, 180));
        DrawTitle("You Died", 170, 72);
        DrawButton(CenterRect(0, 320, 320, 62), "Restart");
        DrawButton(CenterRect(0, 400, 320, 62), "Main menu");
    }

    private static void DrawTitle(string text, int y, int size)
    {
        var x = (Raylib.GetScreenWidth() - Raylib.MeasureText(text, size)) / 2;
        Raylib.DrawText(text, x, y, size, Color.White);
    }

    private static void DrawButton(Rectangle rect, string text)
    {
        var hover = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), rect);
        Raylib.DrawRectangleRec(rect, hover ? Palette.C(68, 112, 186) : Palette.C(36, 56, 90));
        Raylib.DrawRectangleLinesEx(rect, 2f, Color.White);
        const int fs = 24;
        Raylib.DrawText(text, (int)(rect.X + rect.Width / 2 - Raylib.MeasureText(text, fs) / 2f), (int)(rect.Y + rect.Height / 2 - fs / 2f), fs, Color.White);
    }

    private static Rectangle CenterRect(int offsetX, int y, int w, int h) => new((Raylib.GetScreenWidth() - w) / 2f + offsetX, y, w, h);
    private static bool Clicked(Rectangle rect) => Raylib.IsMouseButtonPressed(MouseButton.Left) && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), rect);

    private void DrawZoneArrows()
    {
        DrawZoneArrow(_buildings, Palette.C(80, 170, 255));
        DrawZoneArrow(_outposts, Palette.C(245, 90, 90));
    }

    private void DrawZoneArrow(List<LootZone> zones, Color color)
    {
        var nearest = zones
            .OrderBy(zone => Vector2.DistanceSquared(_player.Position, zone.Center))
            .FirstOrDefault();
        if (nearest is null) return;

        var to = nearest.Center - _player.Position;
        if (to.LengthSquared() < 0.01f) return;

        var dir = Vector2.Normalize(to);
        var normal = new Vector2(-dir.Y, dir.X);
        var tip = _player.Position + dir * 46f;
        var backCenter = _player.Position + dir * 28f;
        Raylib.DrawTriangle(tip, backCenter + normal * 9f, backCenter - normal * 9f, color);
    }

    private void DrawStatTooltip()
    {
        var mouse = Raylib.GetMousePosition();
        var hints = new (Rectangle Rect, string Header, string Body)[]
        {
            (new Rectangle(54, 176, 180, 24), "STR", "Увеличивает урон ближнего боя."),
            (new Rectangle(54, 206, 180, 24), "DEX", "Усиливает ближний урон, снижает входящий урон и шанс негативных эффектов."),
            (new Rectangle(54, 236, 180, 24), "SPD", "Увеличивает скорость передвижения и рывка."),
            (new Rectangle(54, 266, 180, 24), "GUN", "Увеличивает бонусный урон дальнего оружия.")
        };

        var hit = hints.FirstOrDefault(h => Raylib.CheckCollisionPointRec(mouse, h.Rect));
        if (string.IsNullOrEmpty(hit.Header)) return;

        var x = (int)mouse.X + 20;
        var y = (int)mouse.Y + 14;
        Raylib.DrawRectangle(x, y, 420, 72, Palette.C(0, 0, 0, 225));
        Raylib.DrawRectangleLines(x, y, 420, 72, Color.SkyBlue);
        Raylib.DrawText(hit.Header, x + 8, y + 8, 18, Color.White);
        Raylib.DrawText(hit.Body, x + 8, y + 34, 16, Color.LightGray);
    }

    private string BuildWeaponDamageText(ItemStack? weapon, WeaponClass kind)
    {
        if (weapon is null) return string.Empty;

        var bonus = kind == WeaponClass.Ranged
            ? _player.GetRangedStatBonus()
            : _player.GetMeleeStatBonus();

        var total = kind == WeaponClass.Ranged
            ? _player.GetRangedDamage()
            : _player.GetMeleeDamage();

        return $"dmg {total:0}(+{bonus:0})";
    }

    private (List<LootZone> buildings, List<LootZone> outposts) GenerateZones(int buildingCount, int outpostCount)
    {
        var all = new List<LootZone>();

        PlaceZones(all, buildingCount, false);
        PlaceZones(all, outpostCount, true);

        return (all.Where(x => !x.IsOutpost).ToList(), all.Where(x => x.IsOutpost).ToList());
    }

    private void PlaceZones(List<LootZone> all, int count, bool outpost)
    {
        var created = 0;
        var attempts = 0;
        while (created < count && attempts < count * 180)
        {
            attempts++;
            var size = outpost
                ? new Vector2(_rng.Next(520, 780), _rng.Next(520, 780))
                : new Vector2(_rng.Next(420, 620), _rng.Next(420, 620));
            var pos = new Vector2(_rng.Next(80, World - (int)size.X - 80), _rng.Next(80, World - (int)size.Y - 80));
            var rect = new Rectangle(pos, size);
            if (!IsZonePlacementValid(rect, all)) continue;
            all.Add(new LootZone(rect, outpost));
            created++;
        }
    }

    private bool IsZonePlacementValid(Rectangle rect, List<LootZone> existing)
    {
        var center = new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);
        var worldCenter = new Vector2(World / 2f, World / 2f);
        if (Vector2.Distance(center, worldCenter) < CenterNoZoneRadius + MathF.Max(rect.Width, rect.Height) * 0.5f)
        {
            return false;
        }

        foreach (var zone in existing)
        {
            if (RectDistance(rect, zone.Rect) < MinZoneGap) return false;
        }

        return true;
    }

    private List<LootChest> GenerateChestsInZones()
    {
        var list = new List<LootChest>();

        foreach (var zone in _buildings.Concat(_outposts))
        {
            var chestCount = _rng.Next(1, 4);
            var itemsHighTier = zone.IsOutpost;
            for (var i = 0; i < chestCount; i++)
            {
                var pos = RandomPointInZoneSafe(zone.Rect, 20f);
                var lootCount = _rng.Next(1, 6);
                var loot = new List<ItemStack>();
                for (var l = 0; l < lootCount; l++) loot.Add(RollLoot(itemsHighTier));
                list.Add(new LootChest(pos, loot));
            }
        }

        return list;
    }

    private ItemStack RollLoot(bool highTier)
    {
        var r = _rng.NextSingle();
        if (r < 0.35f) return ItemStack.Consumable(_rng.NextSingle() < 0.5f ? ConsumableType.Medkit : ConsumableType.Stim);
        if (r < 0.70f) return ItemStack.Armor(RollRarity(highTier), _rng);
        return ItemStack.Weapon(_rng.NextSingle() < 0.5f ? WeaponClass.Ranged : WeaponClass.Melee, RollRarity(highTier), _rng);
    }

    private ArmorRarity RollRarity(bool highTier)
    {
        var r = _rng.NextSingle();
        if (!highTier)
        {
            if (r < 0.55f) return ArmorRarity.Common;
            if (r < 0.82f) return ArmorRarity.Rare;
            if (r < 0.96f) return ArmorRarity.Epic;
            return ArmorRarity.Legendary;
        }

        if (r < 0.20f) return ArmorRarity.Rare;
        if (r < 0.75f) return ArmorRarity.Epic;
        return ArmorRarity.Legendary;
    }

    private Vector2 RandomPointIn(Rectangle r)
        => new(_rng.Next((int)r.X + 18, (int)(r.X + r.Width - 18)), _rng.Next((int)r.Y + 18, (int)(r.Y + r.Height - 18)));

    private Vector2 RandomPointInZoneSafe(Rectangle zoneRect, float radius)
    {
        for (var i = 0; i < 100; i++)
        {
            var point = RandomPointIn(zoneRect);
            if (!MovementUtils.CircleHitsObstacle(point, radius, _obstacles)) return point;
        }

        return new Vector2(zoneRect.X + zoneRect.Width / 2f, zoneRect.Y + zoneRect.Height / 2f);
    }

    private Vector2 RandomOutdoorPoint(float radius = 14f)
    {
        while (true)
        {
            var point = new Vector2(_rng.Next(100, World - 100), _rng.Next(100, World - 100));
            if (_buildings.Any(z => Raylib.CheckCollisionPointRec(point, z.Rect))) continue;
            if (_outposts.Any(z => Raylib.CheckCollisionPointRec(point, z.Rect))) continue;
            if (MovementUtils.CircleHitsObstacle(point, radius, _obstacles)) continue;
            return point;
        }
    }

    private List<Obstacle> GenerateObstacles()
    {
        var list = new List<Obstacle>();

        foreach (var zone in _buildings.Concat(_outposts))
        {
            var count = zone.IsOutpost ? _rng.Next(6, 10) : _rng.Next(4, 7);
            for (var i = 0; i < count; i++)
            {
                var tries = 0;
                while (tries++ < 60)
                {
                    var w = zone.IsOutpost ? _rng.Next(70, 128) : _rng.Next(52, 96);
                    var h = zone.IsOutpost ? _rng.Next(70, 128) : _rng.Next(52, 96);
                    var x = _rng.Next((int)zone.Rect.X + 18, (int)(zone.Rect.X + zone.Rect.Width - w - 18));
                    var y = _rng.Next((int)zone.Rect.Y + 18, (int)(zone.Rect.Y + zone.Rect.Height - h - 18));
                    var rect = new Rectangle(x, y, w, h);

                    if (list.Any(o => RectDistance(rect, o.Rect) < 10f)) continue;

                    list.Add(new Obstacle(rect));
                    break;
                }
            }
        }

        return list;
    }

    private List<Enemy> GenerateEnemies()
    {
        var list = new List<Enemy>();

        foreach (var b in _buildings)
        {
            var count = _rng.Next(2, 4);
            for (var i = 0; i < count; i++)
            {
                var patrolA = RandomPointInZoneSafe(b.Rect, 14f);
                var patrolB = RandomPointInZoneSafe(b.Rect, 14f);
                list.Add(Enemy.CreatePatrol(patrolA, patrolB, false));
            }

            var strongCount = _rng.Next(1, 3);
            for (var i = 0; i < strongCount; i++)
            {
                list.Add(Enemy.CreateStrong(RandomPointInZoneSafe(b.Rect, 14f)));
            }
        }

        foreach (var o in _outposts)
        {
            var count = _rng.Next(5, 8);
            for (var i = 0; i < count; i++)
            {
                var patrolA = RandomPointInZoneSafe(o.Rect, 14f);
                var patrolB = RandomPointInZoneSafe(o.Rect, 14f);
                list.Add(Enemy.CreatePatrol(patrolA, patrolB, true));
            }
            var strong = _rng.Next(3, 5);
            for (var i = 0; i < strong; i++) list.Add(Enemy.CreateStrong(RandomPointInZoneSafe(o.Rect, 14f)));
        }

        var outdoorPatrols = _rng.Next(12, 19);
        for (var i = 0; i < outdoorPatrols; i++)
        {
            var patrolA = RandomOutdoorPoint();
            var patrolB = patrolA + new Vector2(_rng.Next(-160, 161), _rng.Next(-160, 161));
            patrolB = Vector2.Clamp(patrolB, new Vector2(40f, 40f), new Vector2(World - 40f, World - 40f));
            if (MovementUtils.CircleHitsObstacle(patrolB, 14f, _obstacles)) patrolB = patrolA;
            list.Add(Enemy.CreatePatrol(patrolA, patrolB, false));
        }

        var outdoorStrong = _rng.Next(6, 11);
        for (var i = 0; i < outdoorStrong; i++) list.Add(Enemy.CreateStrong(RandomOutdoorPoint()));

        var outdoorGuards = _rng.Next(10, 17);
        for (var i = 0; i < outdoorGuards; i++)
        {
            var point = RandomOutdoorPoint();
            list.Add(Enemy.CreatePatrol(point, point, false));
        }

        return list;
    }

    private List<BossEnemy> GenerateBosses()
    {
        var list = new List<BossEnemy>();
        foreach (var o in _outposts)
        {
            var center = new Vector2(o.Rect.X + o.Rect.Width / 2f, o.Rect.Y + o.Rect.Height / 2f);
            list.Add(new BossEnemy(center));
        }
        return list;
    }


    private static List<VisualTheme> BuildThemes()
    {
        return
        [
            new VisualTheme("Neon Night", Palette.C(13, 17, 28), Palette.C(26, 32, 44), Palette.C(45, 85, 180, 45), Palette.C(60, 110, 220, 130), Palette.C(180, 45, 45, 40), Palette.C(220, 80, 80, 110), Palette.C(52, 56, 68, 245), Palette.C(88, 96, 116, 255), Color.SkyBlue, Palette.C(235, 95, 95), Palette.C(240, 110, 110), Palette.C(180, 60, 60)),
            new VisualTheme("Amber Dusk", Palette.C(35, 21, 16), Palette.C(64, 42, 28), Palette.C(112, 74, 38, 55), Palette.C(180, 118, 62, 130), Palette.C(140, 52, 34, 50), Palette.C(198, 94, 60, 120), Palette.C(84, 58, 46, 245), Palette.C(124, 90, 70, 255), Palette.C(240, 202, 120), Palette.C(205, 84, 65), Palette.C(230, 112, 78), Palette.C(175, 66, 42)),
            new VisualTheme("Toxic Bloom", Palette.C(14, 30, 23), Palette.C(28, 52, 38), Palette.C(46, 108, 82, 48), Palette.C(82, 170, 122, 140), Palette.C(90, 62, 128, 42), Palette.C(130, 95, 190, 128), Palette.C(40, 72, 60, 245), Palette.C(74, 130, 108, 255), Palette.C(122, 255, 196), Palette.C(224, 110, 185), Palette.C(244, 132, 208), Palette.C(160, 88, 172)),
            new VisualTheme("Frostline", Palette.C(11, 24, 34), Palette.C(20, 44, 62), Palette.C(48, 96, 130, 48), Palette.C(80, 144, 192, 132), Palette.C(62, 82, 118, 48), Palette.C(102, 132, 176, 130), Palette.C(48, 66, 86, 245), Palette.C(92, 126, 160, 255), Palette.C(176, 236, 255), Palette.C(235, 124, 124), Palette.C(244, 150, 150), Palette.C(170, 88, 88)),
            new VisualTheme("Synthwave", Palette.C(24, 8, 34), Palette.C(54, 24, 74), Palette.C(108, 42, 156, 46), Palette.C(166, 84, 222, 140), Palette.C(52, 108, 170, 44), Palette.C(92, 166, 232, 132), Palette.C(54, 46, 88, 245), Palette.C(112, 92, 164, 255), Palette.C(255, 152, 246), Palette.C(255, 124, 164), Palette.C(255, 154, 188), Palette.C(196, 90, 162))
        ];
    }

    private static float RectDistance(Rectangle a, Rectangle b)
    {
        var dx = MathF.Max(0f, MathF.Max(b.X - (a.X + a.Width), a.X - (b.X + b.Width)));
        var dy = MathF.Max(0f, MathF.Max(b.Y - (a.Y + a.Height), a.Y - (b.Y + b.Height)));
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    public void Dispose() => Raylib.CloseWindow();
}

public sealed class Player
{
    private const float MaxHp = 120f;

    private float _attackCd;
    private float _dodgeCd;
    private float _stim;
    private float _bleed;

    public Vector2 Position { get; private set; }
    public float Health { get; private set; }
    public float MaxHealth => MaxHp;

    public bool InventoryOpen { get; set; }

    public int Str { get; private set; } = 6;
    public int Dex { get; private set; } = 8;
    public int Spd { get; private set; } = 7;
    public int Guns { get; private set; } = 7;

    public int Level { get; private set; } = 1;
    public int Kills { get; private set; }
    public int KillsTarget => Level * 10;
    public int StatPoints { get; private set; }

    public Inventory Inventory { get; } = new();

    public ItemStack? RangedWeapon { get; set; }
    public ItemStack? MeleeWeapon { get; set; }
    public ItemStack? Armor { get; set; }

    public WeaponClass ActiveWeaponClass { get; private set; } = WeaponClass.Ranged;

    private Player(Vector2 p)
    {
        Position = p;
        Health = MaxHp;

        RangedWeapon = ItemStack.Weapon(WeaponClass.Ranged, ArmorRarity.Common, new Random());
        MeleeWeapon = ItemStack.Weapon(WeaponClass.Melee, ArmorRarity.Common, new Random());
        Armor = ItemStack.Armor(ArmorRarity.Common, new Random());
    }

    public static Player Create(Vector2 p) => new(p);

    public void Update(float dt, List<Obstacle> obstacles, int worldSize, List<DashAfterImage> afterImages)
    {
        _attackCd -= dt;
        _dodgeCd -= dt;
        _stim -= dt;

        if (_bleed > 0)
        {
            _bleed -= dt;
            Health = MathF.Max(0f, Health - 2.4f * dt);
        }

        var d = Vector2.Zero;
        if (Raylib.IsKeyDown(KeyboardKey.W)) d.Y -= 1;
        if (Raylib.IsKeyDown(KeyboardKey.S)) d.Y += 1;
        if (Raylib.IsKeyDown(KeyboardKey.A)) d.X -= 1;
        if (Raylib.IsKeyDown(KeyboardKey.D)) d.X += 1;

        if (Raylib.IsKeyPressed(KeyboardKey.Space) && _dodgeCd <= 0f)
        {
            var dir = d == Vector2.Zero ? new Vector2(1f, 0f) : Vector2.Normalize(d);
            var dist = 150f + Spd * 8f;
            Position = MovementUtils.MoveWithCollisions(Position, dir * dist, 16f, obstacles, worldSize);
            DashAfterImage.Spawn(afterImages, Position, dir, dist, Palette.C(120, 200, 255), false);
            _dodgeCd = 1.1f;
        }

        if (d != Vector2.Zero)
        {
            var speed = 210f + Spd * 6f;
            if (_stim > 0) speed *= 1.25f;
            var delta = Vector2.Normalize(d) * speed * dt;
            Position = MovementUtils.MoveWithCollisions(Position, delta, 16f, obstacles, worldSize);
        }
    }

    public void Attack(Vector2 target, List<Projectile> projectiles, List<SwingArc> swings)
    {
        if (_attackCd > 0f) return;

        var weapon = ActiveWeaponClass == WeaponClass.Ranged ? RangedWeapon : MeleeWeapon;
        if (weapon is null) return;

        var dir = target - Position;
        if (dir == Vector2.Zero) dir = new Vector2(1f, 0f);
        dir = Vector2.Normalize(dir);

        if (ActiveWeaponClass == WeaponClass.Ranged)
        {
            var bonus = GetRangedDamage();
            if (weapon.Pattern == WeaponPattern.PulseRifle)
            {
                for (var i = 0; i < 3; i++)
                {
                    var spread = (i - 1) * (MathF.PI / 120f);
                    var shotDir = VisibilityUtils.Rotate(dir, spread);
                    projectiles.Add(new Projectile(Position + shotDir * 18f, shotDir, 560f, 1.0f, weapon.Color, false, bonus * 0.72f));
                }
                _attackCd = 0.34f;
            }
            else
            {
                projectiles.Add(new Projectile(Position + dir * 18f, dir, 520f, 1.15f, weapon.Color, false, bonus));
                _attackCd = 0.22f;
            }
        }
        else
        {
            var angle = MathF.Atan2(dir.Y, dir.X);
            if (weapon.Pattern == WeaponPattern.EnergySpear)
            {
                swings.Add(SwingArc.Line(Position + dir * 24f, Position + dir * 125f, 0.14f, weapon.Color));
                _attackCd = 0.35f;
            }
            else
            {
                swings.Add(SwingArc.Arc(Position, 78f, angle - 0.64f, angle + 0.64f, 0.14f, weapon.Color));
                _attackCd = 0.32f;
            }
        }
    }

    public float GetMeleeDamage()
    {
        var power = MeleeWeapon?.PowerBonus ?? 0f;
        return (12f + GetMeleeStatBonusRaw() + power) * 0.7f;
    }

    public float GetRangedDamage()
    {
        var power = RangedWeapon?.PowerBonus ?? 0f;
        return (9f + GetRangedStatBonusRaw() + power) * 1.3f;
    }

    public float GetMeleeStatBonus() => GetMeleeStatBonusRaw() * 0.7f;
    public float GetRangedStatBonus() => GetRangedStatBonusRaw() * 1.3f;

    private float GetMeleeStatBonusRaw() => Str * 2.2f + Dex * 0.6f;
    private float GetRangedStatBonusRaw() => Guns * 2.4f;

    public float GetStatusEffectChance(float baseChance)
    {
        var reduction = Math.Clamp(Dex * 0.02f, 0f, 0.6f);
        return baseChance * (1f - reduction);
    }

    public void SwitchActiveWeapon() => ActiveWeaponClass = ActiveWeaponClass == WeaponClass.Ranged ? WeaponClass.Melee : WeaponClass.Ranged;

    public void UseMedkit()
    {
        if (Inventory.MedkitSlot?.ConsumableKind != ConsumableType.Medkit || Health >= MaxHp) return;
        Inventory.MedkitSlot = null;
        Health = MathF.Min(MaxHp, Health + 36f);
    }

    public void UseStim()
    {
        if (Inventory.StimSlot?.ConsumableKind != ConsumableType.Stim) return;
        Inventory.StimSlot = null;
        _stim = 6f;
    }

    public void ApplyBleed(float duration) => _bleed = MathF.Max(_bleed, duration);
    public void TickEffects(float dt) { }

    public void TakeDamage(float value)
    {
        var armor = Armor?.Defense ?? 0f;
        var reduced = MathF.Max(1f, value - armor * 0.75f - Dex * 0.12f);
        Health = MathF.Max(0f, Health - reduced);
    }

    public void RegisterKill()
    {
        Kills++;
        if (Kills < KillsTarget) return;
        Kills = 0;
        Level++;
        StatPoints++;
    }

    public void ApplyPoint(StatType stat)
    {
        if (StatPoints <= 0) return;
        StatPoints--;

        if (stat == StatType.Strength) Str++;
        if (stat == StatType.Dexterity) Dex++;
        if (stat == StatType.Speed) Spd++;
        if (stat == StatType.Gunsmith) Guns++;
    }
}

public sealed class Enemy
{
    public Vector2 Position;
    public float MaxHealth;
    public float Health;
    public bool IsStrong;
    public bool IsPatrol;
    public bool Alive => Health > 0f;

    public bool KillAwarded;
    public bool JustHitByPlayer;

    private Vector2 _facing;
    private float _attackCd;

    private Vector2 _patrolA;
    private Vector2 _patrolB;
    private bool _toB = true;

    private bool _alert;
    private Vector2 _target;

    private float _sweepPhase;
    private float _sweepDir = 1f;

    private float _burstCd;
    private float _patrolTurnTimer;
    private bool _patrolTurning;
    private int _burstShotsLeft;
    private float _burstShotCd;

    private float _deathAnim = 0.45f;

    private const float BaseView = 290f;
    private const float StrongView = 360f;
    private const float FovHalf = MathF.PI / 3f; // 120 total

    private Enemy(Vector2 pos)
    {
        Position = pos;
        _facing = new Vector2(1f, 0f);
    }

    public static Enemy CreatePatrol(Vector2 a, Vector2 b, bool outpost)
    {
        var e = new Enemy(a)
        {
            IsPatrol = true,
            _patrolA = a,
            _patrolB = b,
            MaxHealth = 100f,
            Health = 100f
        };
        return e;
    }

    public static Enemy CreateStrong(Vector2 pos)
    {
        var e = new Enemy(pos)
        {
            IsStrong = true,
            MaxHealth = 300f,
            Health = 300f
        };
        return e;
    }

    public void UpdateVisionSweep(float dt)
    {
        if (!Alive) { _deathAnim -= dt; return; }

        // sweep left-right
        _sweepPhase += dt * 1.2f * _sweepDir;
        if (_sweepPhase > 1f) { _sweepPhase = 1f; _sweepDir = -1f; }
        if (_sweepPhase < -1f) { _sweepPhase = -1f; _sweepDir = 1f; }

        var baseAngle = MathF.Atan2(_facing.Y, _facing.X);
        var sweepOffset = _sweepPhase * (MathF.PI * 0.18f);
        var a = baseAngle + sweepOffset;
        _facing = Vector2.Normalize(new Vector2(MathF.Cos(a), MathF.Sin(a)));
    }

    public void UpdateAwareness(Vector2 playerPos, float dt, List<Obstacle> obstacles)
    {
        if (!Alive) return;

        if (CanSeePoint(playerPos, obstacles))
        {
            _alert = true;
            _target = playerPos;
        }
        else if (_alert && Vector2.Distance(Position, playerPos) > GetViewDistance() * 1.8f)
        {
            _alert = false;
        }
    }

    public bool CanSeePoint(Vector2 point, List<Obstacle> obstacles)
    {
        var to = point - Position;
        var dist = to.Length();
        if (dist > GetViewDistance() || dist < 0.01f) return false;

        var dir = Vector2.Normalize(to);
        var angle = MathF.Acos(Math.Clamp(Vector2.Dot(_facing, dir), -1f, 1f));
        return angle <= FovHalf && VisibilityUtils.HasLineOfSight(Position, point, obstacles);
    }

    public void ForceAggro(Vector2 target)
    {
        _alert = true;
        _target = target;
    }

    public void UpdateMovement(float dt, Vector2 playerPos, List<Obstacle> obstacles, int worldSize)
    {
        _attackCd -= dt;

        if (!Alive) return;

        if (_alert)
        {
            var to = _target - Position;
            if (to.LengthSquared() > 16f)
            {
                var dir = Vector2.Normalize(to);
                _facing = dir;
                Position = MovementUtils.MoveWithCollisions(Position, dir * (IsStrong ? 95f : 118f) * dt, 14f, obstacles, worldSize);
            }

            if (IsStrong)
            {
                _burstCd -= dt;
                _burstShotCd -= dt;
            }

            return;
        }

        if (IsPatrol)
        {
            if (_patrolTurning)
            {
                _patrolTurnTimer -= dt;
                var turned = VisibilityUtils.Rotate(_facing, MathF.PI * dt / 2f);
                if (turned != Vector2.Zero) _facing = Vector2.Normalize(turned);
                if (_patrolTurnTimer <= 0f)
                {
                    _patrolTurning = false;
                    _toB = !_toB;
                }
                return;
            }

            var target = _toB ? _patrolB : _patrolA;
            var to = target - Position;
            if (to.Length() < 8f)
            {
                _patrolTurning = true;
                _patrolTurnTimer = 2f;
            }
            else
            {
                var dir = Vector2.Normalize(to);
                _facing = dir;
                Position = MovementUtils.MoveWithCollisions(Position, dir * 86f * dt, 14f, obstacles, worldSize);
            }
        }
    }

    public void TryShootBurst(Vector2 playerPos, List<Projectile> projectiles)
    {
        if (!Alive || !IsStrong || !_alert) return;

        if (_burstCd <= 0f && _burstShotsLeft <= 0)
        {
            _burstShotsLeft = 3;
            _burstShotCd = 0f;
            _burstCd = 2.8f;
        }

        if (_burstShotsLeft > 0 && _burstShotCd <= 0f)
        {
            var dir = playerPos - Position;
            if (dir != Vector2.Zero) dir = Vector2.Normalize(dir);
            projectiles.Add(new Projectile(Position + dir * 16f, dir, 420f, 1.4f, Palette.C(255, 120, 120), true, 10f));
            _burstShotsLeft--;
            _burstShotCd = 0.13f;
        }
    }

    public bool TryMeleeHit(Player player)
    {
        if (!Alive || _attackCd > 0f || Vector2.Distance(Position, player.Position) > 24f) return false;
        _attackCd = IsStrong ? 1.3f : 0.9f;
        player.TakeDamage(IsStrong ? 18f : 10f);
        return true;
    }

    public float GetViewDistance() => IsStrong ? StrongView : BaseView;

    public void Damage(float amount)
    {
        if (!Alive) return;
        Health = MathF.Max(0f, Health - amount);
    }

    public void Draw(VisualTheme theme)
    {
        if (Alive)
        {
            if (IsStrong)
            {
                var p1 = Position + new Vector2(0, -16);
                var p2 = Position + new Vector2(-14, 14);
                var p3 = Position + new Vector2(14, 14);
                Raylib.DrawTriangle(p1, p2, p3, theme.EnemyStrong);
                Raylib.DrawTriangleLines(p1, p2, p3, Color.Maroon);
            }
            else
            {
                Raylib.DrawCircleV(Position, 14f, theme.Enemy);
                Raylib.DrawCircleLines((int)Position.X, (int)Position.Y, 16f, Color.Maroon);
            }

            var hp = Health / MaxHealth;
            var bar = new Rectangle(Position.X - 22, Position.Y - 26, 44, 5);
            Raylib.DrawRectangleRec(bar, Palette.C(20, 20, 20, 220));
            Raylib.DrawRectangle((int)bar.X, (int)bar.Y, (int)(bar.Width * hp), (int)bar.Height, Color.Green);
        }
        else if (_deathAnim > 0)
        {
            var fade = (byte)(255 * (_deathAnim / 0.45f));
            Raylib.DrawCircle((int)Position.X, (int)Position.Y, 18f * (1f - _deathAnim / 0.45f), Palette.C(255, 90, 60, fade));
        }
    }

    public void DrawSight()
    {
        if (!Alive) return;

        var c = Palette.C(120, 140, 160, 26);
        VisibilityUtils.DrawDashedCircle(Position, GetViewDistance(), 28, c);

        var left = VisibilityUtils.Rotate(_facing, -FovHalf);
        var right = VisibilityUtils.Rotate(_facing, FovHalf);
        VisibilityUtils.DrawDashedLine(Position, Position + left * GetViewDistance(), 22, c);
        VisibilityUtils.DrawDashedLine(Position, Position + right * GetViewDistance(), 22, c);
    }
}

public sealed class BossEnemy
{
    public Vector2 Position;
    public float MaxHealth = 2000f;
    public float Health = 2000f;
    public bool Alive => Health > 0;
    public bool KillAwarded;

    private float _ramCd = 4f;
    private float _shootCd = 1.2f;
    private float _slamCd = 3.5f;
    private float _slamVisual;
    private bool _alert;
    private Vector2 _facing = new(1f, 0f);

    private const float ViewDistance = 460f;
    private const float FovHalf = MathF.PI / 3f;

    public BossEnemy(Vector2 pos) { Position = pos; }

    public void Update(float dt, Vector2 playerPos, List<Projectile> projectiles, Player player, List<Obstacle> obstacles, int worldSize, List<DashAfterImage> afterImages)
    {
        if (!Alive) return;

        _ramCd -= dt;
        _shootCd -= dt;
        _slamCd -= dt;
        _slamVisual -= dt;

        var toPlayer = playerPos - Position;
        if (toPlayer != Vector2.Zero) _facing = Vector2.Normalize(toPlayer);

        if (CanSeePoint(playerPos, obstacles)) _alert = true;
        else if (_alert && Vector2.Distance(Position, playerPos) > ViewDistance * 1.6f) _alert = false;

        if (!_alert || toPlayer == Vector2.Zero) return;

        var dir = Vector2.Normalize(toPlayer);
        Position = MovementUtils.MoveWithCollisions(Position, dir * 42f * dt, 28f, obstacles, worldSize);

        if (_ramCd <= 0f)
        {
            Position = MovementUtils.MoveWithCollisions(Position, dir * 120f, 28f, obstacles, worldSize);
            DashAfterImage.Spawn(afterImages, Position, dir, 120f, Palette.C(230, 100, 100), true);
            _ramCd = 4f;
            if (Vector2.Distance(Position, playerPos) < 56f) player.TakeDamage(24f);
        }

        if (_shootCd <= 0f)
        {
            for (var i = 0; i < 10; i++)
            {
                var spread = ((Random.Shared.NextSingle() * 10f) - 5f) * (MathF.PI / 180f);
                var shotDir = VisibilityUtils.Rotate(dir, spread);
                projectiles.Add(new Projectile(Position + shotDir * 28f, shotDir, 500f, 1.35f, Palette.C(255, 150, 120), true, 16f));
            }
            _shootCd = 1.5f;
        }

        if (_slamCd <= 0f)
        {
            _slamVisual = 0.7f;
            _slamCd = 3.6f;
            if (Vector2.Distance(Position, playerPos) < 120f) player.TakeDamage(20f);
        }
    }

    private bool CanSeePoint(Vector2 point, List<Obstacle> obstacles)
    {
        var to = point - Position;
        var dist = to.Length();
        if (dist > ViewDistance || dist < 0.01f) return false;

        var dir = Vector2.Normalize(to);
        var angle = MathF.Acos(Math.Clamp(Vector2.Dot(_facing, dir), -1f, 1f));
        return angle <= FovHalf && VisibilityUtils.HasLineOfSight(Position, point, obstacles);
    }

    public void Damage(float amount)
    {
        if (!Alive) return;
        Health = MathF.Max(0f, Health - amount);
    }

    public void DrawSight()
    {
        if (!Alive) return;

        var c = Palette.C(255, 130, 110, 24);
        VisibilityUtils.DrawDashedCircle(Position, ViewDistance, 32, c);
        VisibilityUtils.DrawDashedLine(Position, Position + VisibilityUtils.Rotate(_facing, -FovHalf) * ViewDistance, 24, c);
        VisibilityUtils.DrawDashedLine(Position, Position + VisibilityUtils.Rotate(_facing, FovHalf) * ViewDistance, 24, c);
    }

    public void Draw(VisualTheme theme)
    {
        if (!Alive) return;

        var size = 42;
        Raylib.DrawRectangle((int)Position.X - size / 2, (int)Position.Y - size / 2, size, size, theme.Boss);
        Raylib.DrawRectangleLines((int)Position.X - size / 2, (int)Position.Y - size / 2, size, size, Color.Maroon);

        if (_slamVisual > 0)
        {
            var alpha = (byte)(120 * (_slamVisual / 0.7f));
            Raylib.DrawCircle((int)Position.X, (int)Position.Y, 120f, Palette.C(255, 100, 100, alpha));
        }

        var hp = Health / MaxHealth;
        var bar = new Rectangle(Position.X - 36, Position.Y - 34, 72, 6);
        Raylib.DrawRectangleRec(bar, Palette.C(20, 20, 20, 220));
        Raylib.DrawRectangle((int)bar.X, (int)bar.Y, (int)(bar.Width * hp), (int)bar.Height, Color.Green);
    }
}

public sealed class Projectile(Vector2 pos, Vector2 dir, float speed, float life, Color color, bool ownerEnemy, float damage)
{
    public Vector2 Position { get; private set; } = pos;
    public Color Color { get; } = color;
    public bool OwnerEnemy { get; } = ownerEnemy;
    public float Damage { get; } = damage;
    private float _life = life;
    public bool Alive => _life > 0f;

    public void Update(float dt)
    {
        Position += dir * speed * dt;
        _life -= dt;
    }
}

public sealed class Explosion(Vector2 pos, float radius, Color color)
{
    public Vector2 Position { get; } = pos;
    public float Radius { get; } = radius;
    public float MaxLife { get; } = 0.24f;
    public float Life { get; set; } = 0.24f;
    public Color Color { get; } = color;
}

public sealed class SwingArc
{
    public Vector2 Origin { get; }
    public float Radius { get; }
    public float AngleStart { get; }
    public float AngleEnd { get; }
    public float Life { get; set; }
    public Color Color { get; }
    public bool IsLine { get; }
    public Vector2 LineStart { get; }
    public Vector2 LineEnd { get; }

    private SwingArc(Vector2 origin, float radius, float angleStart, float angleEnd, float life, Color color)
    {
        Origin = origin;
        Radius = radius;
        AngleStart = angleStart;
        AngleEnd = angleEnd;
        Life = life;
        Color = color;
    }

    private SwingArc(Vector2 lineStart, Vector2 lineEnd, float life, Color color)
    {
        IsLine = true;
        LineStart = lineStart;
        LineEnd = lineEnd;
        Life = life;
        Color = color;
    }

    public static SwingArc Arc(Vector2 origin, float radius, float angleStart, float angleEnd, float life, Color color)
        => new(origin, radius, angleStart, angleEnd, life, color);

    public static SwingArc Line(Vector2 lineStart, Vector2 lineEnd, float life, Color color)
        => new(lineStart, lineEnd, life, color);
}

public sealed class LootZone(Rectangle rect, bool isOutpost)
{
    public Rectangle Rect { get; } = rect;
    public bool IsOutpost { get; } = isOutpost;
    public Vector2 Center => new(Rect.X + Rect.Width / 2f, Rect.Y + Rect.Height / 2f);
}

public sealed class Obstacle(Rectangle rect)
{
    public Rectangle Rect { get; } = rect;
}

public static class MovementUtils
{
    public static Vector2 MoveWithCollisions(Vector2 position, Vector2 delta, float radius, List<Obstacle> obstacles, int worldSize)
    {
        var next = position;
        var xTry = new Vector2(position.X + delta.X, position.Y);
        if (!CircleHitsObstacle(xTry, radius, obstacles)) next.X = xTry.X;

        var yTry = new Vector2(next.X, position.Y + delta.Y);
        if (!CircleHitsObstacle(yTry, radius, obstacles)) next.Y = yTry.Y;

        next.X = Math.Clamp(next.X, radius, worldSize - radius);
        next.Y = Math.Clamp(next.Y, radius, worldSize - radius);
        return next;
    }

    public static bool CircleHitsObstacle(Vector2 center, float radius, List<Obstacle> obstacles)
    {
        foreach (var o in obstacles)
        {
            var nx = Math.Clamp(center.X, o.Rect.X, o.Rect.X + o.Rect.Width);
            var ny = Math.Clamp(center.Y, o.Rect.Y, o.Rect.Y + o.Rect.Height);
            var dx = center.X - nx;
            var dy = center.Y - ny;
            if (dx * dx + dy * dy < radius * radius) return true;
        }

        return false;
    }
}

public sealed class DashAfterImage(Vector2 position, Color color, float alpha, bool square)
{
    public Vector2 Position { get; } = position;
    public Color Color { get; } = color;
    public float InitialAlpha { get; } = alpha;
    public float Life { get; set; } = 1f;
    public bool Square { get; } = square;

    public void Draw()
    {
        var current = MathF.Max(0f, InitialAlpha * (Life / 1f));
        var c = new Color(Color.R, Color.G, Color.B, (byte)(255 * current));
        if (Square)
            Raylib.DrawRectangle((int)Position.X - 21, (int)Position.Y - 21, 42, 42, c);
        else
            Raylib.DrawCircleV(Position, 16f, c);
    }

    public static void Spawn(List<DashAfterImage> target, Vector2 endPosition, Vector2 dashDir, float distance, Color color, bool square)
    {
        var dir = dashDir == Vector2.Zero ? new Vector2(1f, 0f) : Vector2.Normalize(dashDir);
        var steps = new[]
        {
            (9f, 0.5f),
            (8f, 0.45f),
            (7f, 0.35f),
            (6f, 0.25f),
            (5f, 0.15f),
            (4f, 0.05f)
        };

        foreach (var (ratio, alpha) in steps)
        {
            target.Add(new DashAfterImage(endPosition - dir * (distance * (10f - ratio) / 10f), color, alpha, square));
        }
    }
}

public static class VisibilityUtils
{
    public static bool HasLineOfSight(Vector2 from, Vector2 to, List<Obstacle> obstacles)
    {
        foreach (var obstacle in obstacles)
        {
            var r = InflateRect(obstacle.Rect, 2f);
            Vector2 hit = default;

            if (Raylib.CheckCollisionPointRec(from, r) || Raylib.CheckCollisionPointRec(to, r)) continue;
            if (Raylib.CheckCollisionLines(from, to, new Vector2(r.X, r.Y), new Vector2(r.X + r.Width, r.Y), ref hit)) return false;
            if (Raylib.CheckCollisionLines(from, to, new Vector2(r.X + r.Width, r.Y), new Vector2(r.X + r.Width, r.Y + r.Height), ref hit)) return false;
            if (Raylib.CheckCollisionLines(from, to, new Vector2(r.X + r.Width, r.Y + r.Height), new Vector2(r.X, r.Y + r.Height), ref hit)) return false;
            if (Raylib.CheckCollisionLines(from, to, new Vector2(r.X, r.Y + r.Height), new Vector2(r.X, r.Y), ref hit)) return false;
        }

        return true;
    }

    public static Vector2 Rotate(Vector2 v, float a)
    {
        var c = MathF.Cos(a);
        var s = MathF.Sin(a);
        return new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
    }

    public static void DrawDashedLine(Vector2 a, Vector2 b, int segments, Color c)
    {
        for (var i = 0; i < segments; i++)
        {
            if (i % 2 == 1) continue;
            var t1 = i / (float)segments;
            var t2 = (i + 1) / (float)segments;
            Raylib.DrawLineV(Vector2.Lerp(a, b, t1), Vector2.Lerp(a, b, t2), c);
        }
    }

    public static void DrawDashedCircle(Vector2 center, float radius, int segments, Color c)
    {
        for (var i = 0; i < segments; i++)
        {
            if (i % 2 == 1) continue;
            var a1 = i / (float)segments * MathF.Tau;
            var a2 = (i + 1) / (float)segments * MathF.Tau;
            var p1 = center + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * radius;
            var p2 = center + new Vector2(MathF.Cos(a2), MathF.Sin(a2)) * radius;
            Raylib.DrawLineV(p1, p2, c);
        }
    }

    private static Rectangle InflateRect(Rectangle rect, float pad)
        => new(rect.X - pad, rect.Y - pad, rect.Width + pad * 2f, rect.Height + pad * 2f);
}

public sealed class LootChest(Vector2 position, List<ItemStack> items)
{
    public Vector2 Position { get; } = position;
    public List<ItemStack> Items { get; } = items;
    public bool Opened { get; set; }
}

public sealed class Inventory
{
    public const int BackpackCapacity = 30;

    public List<ItemStack?> BackpackSlots { get; } = Enumerable.Repeat<ItemStack?>(null, BackpackCapacity).ToList();
    public ItemStack? MedkitSlot { get; set; }
    public ItemStack? StimSlot { get; set; }

    public ItemStack? Trash { get; set; }

    public bool AddToBackpack(ItemStack item)
    {
        for (var i = 0; i < BackpackSlots.Count; i++)
        {
            if (BackpackSlots[i] is not null) continue;
            BackpackSlots[i] = item;
            return true;
        }

        return false;
    }
}

public sealed class ItemStack
{
    public ItemType Type { get; }
    public string Name { get; }
    public string Description { get; }
    public ArmorRarity Rarity { get; }
    public Color Color { get; }

    public WeaponClass? WeaponKind { get; }
    public WeaponPattern Pattern { get; }
    public ConsumableType? ConsumableKind { get; }

    public float Defense { get; }
    public float PowerBonus { get; }

    private ItemStack(ItemType type, string name, string description, ArmorRarity rarity, Color color, WeaponClass? weaponClass, WeaponPattern pattern, ConsumableType? consumableType, float defense, float powerBonus)
    {
        Type = type;
        Name = name;
        Description = description;
        Rarity = rarity;
        Color = color;
        WeaponKind = weaponClass;
        Pattern = pattern;
        ConsumableKind = consumableType;
        Defense = defense;
        PowerBonus = powerBonus;
    }

    public static ItemStack Armor(ArmorRarity rarity, Random rng)
    {
        var baseDef = rarity switch
        {
            ArmorRarity.Common => 10f,
            ArmorRarity.Rare => 14f,
            ArmorRarity.Epic => 19f,
            _ => 25f
        };

        var name = rarity switch
        {
            ArmorRarity.Common => "Scrap Vest",
            ArmorRarity.Rare => "Titan Weave",
            ArmorRarity.Epic => "Aegis Fiber",
            _ => "Nova Bulwark"
        };

        return new ItemStack(ItemType.Armor, name, "Armor. Drag into armor slot.", rarity, Palette.Rarity(rarity), null, WeaponPattern.Standard, null, baseDef + rng.NextSingle() * 4f, 0f);
    }

    public static ItemStack Weapon(WeaponClass kind, ArmorRarity rarity, Random rng)
    {
        var p = rarity switch
        {
            ArmorRarity.Common => 0f,
            ArmorRarity.Rare => 3f,
            ArmorRarity.Epic => 6f,
            _ => 10f
        };

        p += rng.NextSingle() * 2f;

        WeaponPattern pattern;
        string name;
        string description;

        if (kind == WeaponClass.Ranged && rng.NextSingle() < 0.35f)
        {
            pattern = WeaponPattern.PulseRifle;
            name = "Pulse Rifle";
            description = "Ranged weapon. Fires a 3-round burst.";
            p += 1.5f;
        }
        else if (kind == WeaponClass.Melee && rng.NextSingle() < 0.35f)
        {
            pattern = WeaponPattern.EnergySpear;
            name = "Energy Spear";
            description = "Melee weapon. Cleaves forward in a line.";
            p += 1.2f;
        }
        else
        {
            pattern = WeaponPattern.Standard;
            name = kind == WeaponClass.Ranged ? "Rail Pistol" : "Plasma Blade";
            description = "Weapon. Drag to matching slot.";
        }

        return new ItemStack(ItemType.Weapon, name, description, rarity, Palette.Rarity(rarity), kind, pattern, null, 0f, p);
    }

    public static ItemStack Consumable(ConsumableType t)
    {
        return t == ConsumableType.Medkit
            ? new ItemStack(ItemType.Consumable, "Medkit", "Restore HP. Hotkey Q.", ArmorRarity.Common, Palette.C(130, 210, 120), null, WeaponPattern.Standard, t, 0f, 0f)
            : new ItemStack(ItemType.Consumable, "Stim", "Move speed boost. Hotkey R.", ArmorRarity.Common, Palette.C(220, 220, 120), null, WeaponPattern.Standard, t, 0f, 0f);
    }
}

public enum SlotKind
{
    RangedWeapon,
    MeleeWeapon,
    Armor,
    Trash,
    Backpack,
    MedkitSlot,
    StimSlot,
    Chest
}

public sealed class UiSlot(Rectangle rect, SlotKind kind, int? index, ItemStack? item, int slotId)
{
    public Rectangle Rect { get; } = rect;
    public SlotKind Kind { get; } = kind;
    public int Index { get; } = index ?? -1;
    public ItemStack? Item { get; } = item;
    public int SlotId { get; } = slotId;
}

public sealed class DragPayload(SlotKind kind, int index, ItemStack item)
{
    public SlotKind Kind { get; } = kind;
    public int Index { get; } = index;
    public ItemStack Item { get; } = item;
}
