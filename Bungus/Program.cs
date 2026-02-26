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

public enum GameState { MainMenu, Playing, Paused, Death }
public enum WeaponClass { Melee, Ranged }
public enum ItemType { Weapon, Armor, Consumable }
public enum ConsumableType { Medkit, Stim }
public enum ArmorRarity { Common, Rare, Epic, Legendary }
public enum StatType { Strength, Agility, Tech }

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

public sealed class SciFiRogueGame : IDisposable
{
    private const int W = 1280;
    private const int H = 720;
    private const int World = 2400;

    private readonly Random _rng = new();
    private Camera2D _camera;

    private GameState _state = GameState.MainMenu;
    private Player _player = null!;

    private List<Enemy> _enemies = [];
    private List<BossEnemy> _bosses = [];
    private List<Projectile> _projectiles = [];
    private List<Explosion> _explosions = [];
    private List<SwingArc> _swings = [];

    private List<LootZone> _buildings = [];
    private List<LootZone> _outposts = [];
    private List<Obstacle> _obstacles = [];
    private List<Pickup> _pickups = [];

    private DragPayload? _drag;
    private ItemStack? _hovered;
    private bool _pendingUpgrade;
    private StatType _pendingStat;

    public SciFiRogueGame()
    {
        Raylib.SetConfigFlags(ConfigFlags.Msaa4xHint);
        Raylib.InitWindow(W, H, "Bungus");
        Raylib.SetTargetFPS(60);
        Raylib.ToggleFullscreen();

        _camera = new Camera2D { Zoom = 1.08f, Rotation = 0f };
        StartRun();
    }

    private void StartRun()
    {
        _player = Player.Create(new Vector2(World / 2f, World / 2f));
        _projectiles = [];
        _explosions = [];
        _swings = [];

        _buildings = GenerateZones(6, false);
        _outposts = GenerateZones(3, true);
        _obstacles = GenerateObstacles();

        _pickups = GeneratePickupsInZones();
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
            Draw();
        }
    }

    private void Update(float dt)
    {
        switch (_state)
        {
            case GameState.MainMenu: UpdateMainMenu(); break;
            case GameState.Playing: UpdatePlaying(dt); break;
            case GameState.Paused: UpdatePause(); break;
            case GameState.Death: UpdateDeath(); break;
        }
    }

    private void UpdateMainMenu()
    {
        if (Clicked(CenterRect(0, 250, 320, 62))) { StartRun(); _state = GameState.Playing; }
        if (Clicked(CenterRect(0, 330, 320, 62))) { }
        if (Clicked(CenterRect(0, 410, 320, 62))) Raylib.CloseWindow();
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
        if (Raylib.IsKeyPressed(KeyboardKey.Tab)) _player.InventoryOpen = !_player.InventoryOpen;

        _player.Update(dt);
        if (Raylib.IsKeyPressed(KeyboardKey.Q)) _player.UseMedkit();
        if (Raylib.IsKeyPressed(KeyboardKey.R)) _player.UseStim();
        if (Raylib.IsKeyPressed(KeyboardKey.E)) _player.SwitchActiveWeapon();

        var mouseWorld = Raylib.GetScreenToWorld2D(Raylib.GetMousePosition(), _camera);
        if (Raylib.IsMouseButtonPressed(MouseButton.Left) && !_player.InventoryOpen)
        {
            _player.Attack(mouseWorld, _projectiles, _swings);
        }

        UpdateEnemies(dt);
        UpdateBosses(dt);
        UpdateProjectiles(dt);
        UpdateSwings(dt);
        UpdateEffects(dt);
        UpdatePickups();
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
            e.UpdateMovement(dt, _player.Position);
            e.TryShootBurst(_player.Position, _projectiles);

            if (e.TryMeleeHit(_player) && _rng.NextSingle() <= 0.05f)
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
            b.Update(dt, _player.Position, _projectiles, _player, _obstacles);
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

            if (p.OwnerEnemy)
            {
                if (Vector2.Distance(p.Position, _player.Position) < 14f)
                {
                    _player.TakeDamage(8f + p.DamageBonus);
                    _explosions.Add(new Explosion(p.Position, 26f, p.Color));
                    _projectiles.RemoveAt(i);
                }
                else if (!p.Alive) _projectiles.RemoveAt(i);
                continue;
            }

            Enemy? enemyHit = _enemies.FirstOrDefault(e => e.Alive && Vector2.Distance(e.Position, p.Position) < 15f);
            if (enemyHit is not null)
            {
                enemyHit.Damage(enemyHit.MaxHealth / 3f + p.DamageBonus);
                enemyHit.ForceAggro(_player.Position);
                enemyHit.JustHitByPlayer = true;
                _explosions.Add(new Explosion(p.Position, 34f, p.Color));
                _projectiles.RemoveAt(i);
                continue;
            }

            BossEnemy? bossHit = _bosses.FirstOrDefault(b => b.Alive && Vector2.Distance(b.Position, p.Position) < 30f);
            if (bossHit is not null)
            {
                bossHit.Damage(16f + p.DamageBonus);
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
                var rel = e.Position - s.Origin;
                if (rel.Length() > s.Radius + 8f) continue;
                var a = MathF.Atan2(rel.Y, rel.X);
                if (a >= s.AngleStart && a <= s.AngleEnd)
                {
                    e.Damage(_player.GetMeleeDamage());
                    e.ForceAggro(_player.Position);
                    e.JustHitByPlayer = true;
                }
            }

            foreach (var b in _bosses.Where(x => x.Alive))
            {
                var rel = b.Position - s.Origin;
                if (rel.Length() > s.Radius + 24f) continue;
                var a = MathF.Atan2(rel.Y, rel.X);
                if (a >= s.AngleStart && a <= s.AngleEnd) b.Damage(_player.GetMeleeDamage() * 0.75f);
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
    }

    private void UpdatePickups()
    {
        foreach (var p in _pickups.Where(x => !x.Picked))
        {
            if (Vector2.Distance(p.Position, _player.Position) > 22f) continue;
            p.Picked = true;

            if (p.Item.Type == ItemType.Consumable)
            {
                _player.Inventory.AddConsumable(p.Item.ConsumableKind!.Value);
            }
            else
            {
                _player.Inventory.Items.Add(p.Item);
            }
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
        if (Clicked(new Rectangle(218, 170, 22, 22))) { _pendingUpgrade = true; _pendingStat = StatType.Strength; }
        if (Clicked(new Rectangle(218, 200, 22, 22))) { _pendingUpgrade = true; _pendingStat = StatType.Agility; }
        if (Clicked(new Rectangle(218, 230, 22, 22))) { _pendingUpgrade = true; _pendingStat = StatType.Tech; }
        if (_pendingUpgrade && Clicked(new Rectangle(20, 264, 96, 30)))
        {
            _player.ApplyPoint(_pendingStat);
            _pendingUpgrade = false;
        }
    }

    private List<UiSlot> BuildSlots()
    {
        var list = new List<UiSlot>
        {
            new(new Rectangle(430, 144, 58, 58), SlotKind.RangedWeapon, null, _player.RangedWeapon, -1),
            new(new Rectangle(496, 144, 58, 58), SlotKind.MeleeWeapon, null, _player.MeleeWeapon, -1),
            new(new Rectangle(562, 144, 58, 58), SlotKind.Armor, null, _player.Armor, -1),
            new(new Rectangle(628, 144, 58, 58), SlotKind.Trash, null, _player.Inventory.Trash, -1)
        };

        for (var i = 0; i < _player.Inventory.Items.Count; i++)
        {
            var c = i % 7;
            var r = i / 7;
            list.Add(new UiSlot(new Rectangle(720 + c * 62, 144 + r * 62, 58, 58), SlotKind.Backpack, i, _player.Inventory.Items[i], i));
        }

        // dedicated consumable slots (stacked counters)
        list.Add(new UiSlot(new Rectangle(430, 210, 58, 58), SlotKind.MedkitStack, null, ItemStack.Consumable(ConsumableType.Medkit), -1));
        list.Add(new UiSlot(new Rectangle(496, 210, 58, 58), SlotKind.StimStack, null, ItemStack.Consumable(ConsumableType.Stim), -1));

        return list;
    }

    private void ApplyDrop(DragPayload drag, UiSlot target)
    {
        if (drag.Item.Type == ItemType.Consumable) return;

        if (target.Kind == SlotKind.Trash)
        {
            if (_player.Inventory.Trash is not null)
            {
                // previous in trash is deleted
                _player.Inventory.Trash = null;
            }

            _player.Inventory.Trash = drag.Item;
            RemoveFromSource(drag);
            return;
        }

        if (target.Kind == SlotKind.Armor && drag.Item.Type == ItemType.Armor)
        {
            var old = _player.Armor;
            _player.Armor = drag.Item;
            RemoveFromSource(drag);
            if (old is not null) _player.Inventory.Items.Add(old);
            return;
        }

        if (target.Kind == SlotKind.RangedWeapon && drag.Item.Type == ItemType.Weapon && drag.Item.WeaponKind == WeaponClass.Ranged)
        {
            var old = _player.RangedWeapon;
            _player.RangedWeapon = drag.Item;
            RemoveFromSource(drag);
            if (old is not null) _player.Inventory.Items.Add(old);
            return;
        }

        if (target.Kind == SlotKind.MeleeWeapon && drag.Item.Type == ItemType.Weapon && drag.Item.WeaponKind == WeaponClass.Melee)
        {
            var old = _player.MeleeWeapon;
            _player.MeleeWeapon = drag.Item;
            RemoveFromSource(drag);
            if (old is not null) _player.Inventory.Items.Add(old);
            return;
        }

        if (target.Kind == SlotKind.Backpack && drag.Kind == SlotKind.Backpack && drag.Index >= 0 && target.Index >= 0)
        {
            (_player.Inventory.Items[drag.Index], _player.Inventory.Items[target.Index]) =
                (_player.Inventory.Items[target.Index], _player.Inventory.Items[drag.Index]);
        }
    }

    private void RemoveFromSource(DragPayload drag)
    {
        if (drag.Kind == SlotKind.Backpack && drag.Index >= 0 && drag.Index < _player.Inventory.Items.Count)
        {
            _player.Inventory.Items.RemoveAt(drag.Index);
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
    }

    private void Draw()
    {
        Raylib.BeginDrawing();
        Raylib.ClearBackground(Palette.C(13, 17, 28));

        switch (_state)
        {
            case GameState.MainMenu:
                DrawMainMenu();
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
            Raylib.DrawRectangleRec(b.Rect, Palette.C(45, 85, 180, 45));
            Raylib.DrawRectangleLinesEx(b.Rect, 2f, Palette.C(60, 110, 220, 130));
        }

        foreach (var o in _outposts)
        {
            Raylib.DrawRectangleRec(o.Rect, Palette.C(180, 45, 45, 40));
            Raylib.DrawRectangleLinesEx(o.Rect, 2f, Palette.C(220, 80, 80, 110));
        }

        foreach (var obstacle in _obstacles)
        {
            Raylib.DrawRectangleRec(obstacle.Rect, Palette.C(52, 56, 68, 245));
            Raylib.DrawRectangleLinesEx(obstacle.Rect, 1.5f, Palette.C(88, 96, 116, 255));
        }

        foreach (var p in _pickups.Where(x => !x.Picked))
        {
            var rect = new Rectangle(p.Position.X - 12, p.Position.Y - 12, 24, 24);
            Raylib.DrawRectangleRec(rect, Palette.C(20, 26, 36, 230));
            Raylib.DrawRectangleLinesEx(rect, 1f, Color.Gray);
            DrawItemIcon(p.Item, new Rectangle(rect.X + 4, rect.Y + 4, 16, 16));
        }

        foreach (var e in _enemies) e.DrawSight();
        foreach (var b in _bosses) b.DrawSight();
        foreach (var e in _enemies) e.Draw();
        foreach (var b in _bosses) b.Draw();

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
            for (var i = 0; i < 18; i++)
            {
                var p = i / 18f;
                var a = s.AngleStart + (s.AngleEnd - s.AngleStart) * p;
                var point = s.Origin + new Vector2(MathF.Cos(a), MathF.Sin(a)) * s.Radius;
                Raylib.DrawCircleV(point, 3f, s.Color);
            }
        }

        Raylib.DrawCircleV(_player.Position, 16f, Color.SkyBlue);
        Raylib.EndMode2D();
    }

    private void DrawHud()
    {
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), 116, Palette.C(0, 0, 0, 170));
        Raylib.DrawText($"HP {_player.Health:0}/{_player.MaxHealth:0} | Level {_player.Level} ({_player.Kills}/{_player.KillsTarget})", 20, 14, 24, Color.White);

        Raylib.DrawText("Weapons:", 20, 46, 20, Color.LightGray);
        Raylib.DrawText($"Ranged: {_player.RangedWeapon?.Name ?? "None"}", 120, 46, 20, _player.RangedWeapon?.Color ?? Color.LightGray);
        Raylib.DrawText($"Melee: {_player.MeleeWeapon?.Name ?? "None"}", 420, 46, 20, _player.MeleeWeapon?.Color ?? Color.LightGray);

        Raylib.DrawText("Armor:", 20, 74, 20, Color.LightGray);
        Raylib.DrawText(_player.Armor?.Name ?? "None", 120, 74, 20, _player.Armor?.Color ?? Color.LightGray);

        Raylib.DrawText($"Consumables: Q Medkit({_player.Inventory.Medkits}) R Stim({_player.Inventory.Stims})", 660, 20, 20, Color.White);
        Raylib.DrawText($"Stats: STR {_player.Str} AGI {_player.Agi} TECH {_player.Tech} Points {_player.StatPoints}", 660, 50, 20, Color.White);
        Raylib.DrawText("WASD move | LMB attack | E switch active weapon | TAB inventory | ESC menu", 20, Raylib.GetScreenHeight() - 28, 18, Color.Gray);
    }

    private void DrawInventory()
    {
        if (!_player.InventoryOpen) return;

        Raylib.DrawRectangle(10, 124, 1240, 380, Palette.C(6, 10, 20, 230));
        Raylib.DrawRectangleLines(10, 124, 1240, 380, Color.SkyBlue);
        Raylib.DrawText("Inventory (drag/drop). Trash slot replaces previous item.", 20, 132, 20, Color.White);

        var slots = BuildSlots();
        foreach (var slot in slots)
        {
            Raylib.DrawRectangleRec(slot.Rect, Palette.C(22, 28, 42, 255));
            Raylib.DrawRectangleLinesEx(slot.Rect, 1f, Color.SkyBlue);
            if (slot.Kind == SlotKind.Trash)
            {
                Raylib.DrawText("TR", (int)slot.Rect.X + 16, (int)slot.Rect.Y + 18, 20, Color.Orange);
            }
            if (slot.Item is not null)
            {
                DrawItemIcon(slot.Item, new Rectangle(slot.Rect.X + 8, slot.Rect.Y + 8, 42, 42));
            }
        }

        Raylib.DrawText($"Medkit x{_player.Inventory.Medkits}", 430, 272, 16, Color.Green);
        Raylib.DrawText($"Stim x{_player.Inventory.Stims}", 496, 272, 16, Color.Yellow);

        Raylib.DrawText($"STR {_player.Str}", 20, 172, 20, Color.LightGray);
        Raylib.DrawText($"AGI {_player.Agi}", 20, 202, 20, Color.LightGray);
        Raylib.DrawText($"TECH {_player.Tech}", 20, 232, 20, Color.LightGray);
        Raylib.DrawText($"Points {_player.StatPoints}", 20, 260, 20, Color.Yellow);

        if (_player.StatPoints > 0)
        {
            DrawPlus(new Rectangle(218, 170, 22, 22));
            DrawPlus(new Rectangle(218, 200, 22, 22));
            DrawPlus(new Rectangle(218, 230, 22, 22));
            if (_pendingUpgrade) DrawButton(new Rectangle(20, 264, 96, 30), "Confirm");
        }

        if (_drag is not null)
        {
            var m = Raylib.GetMousePosition();
            DrawItemIcon(_drag.Item, new Rectangle(m.X + 8, m.Y + 8, 34, 34));
        }

        if (_hovered is not null)
        {
            DrawTooltip(_hovered, Raylib.GetMousePosition());
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

    private static void DrawGrid()
    {
        for (var x = 0; x < World; x += 80) Raylib.DrawLine(x, 0, x, World, Palette.C(26, 32, 44));
        for (var y = 0; y < World; y += 80) Raylib.DrawLine(0, y, World, y, Palette.C(26, 32, 44));
    }

    private void DrawMainMenu()
    {
        DrawTitle("BUNGUS", 120, 80);
        DrawButton(CenterRect(0, 250, 320, 62), "Play");
        DrawButton(CenterRect(0, 330, 320, 62), "Settings (placeholder)");
        DrawButton(CenterRect(0, 410, 320, 62), "Exit");
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

    private List<LootZone> GenerateZones(int count, bool outpost)
    {
        var list = new List<LootZone>();
        for (var i = 0; i < count; i++)
        {
            var size = outpost ? new Vector2(_rng.Next(330, 450), _rng.Next(330, 450)) : new Vector2(_rng.Next(250, 360), _rng.Next(250, 360));
            var pos = new Vector2(_rng.Next(120, World - (int)size.X - 120), _rng.Next(120, World - (int)size.Y - 120));
            list.Add(new LootZone(new Rectangle(pos, size), outpost));
        }
        return list;
    }

    private List<Pickup> GeneratePickupsInZones()
    {
        var list = new List<Pickup>();

        foreach (var b in _buildings)
        {
            var amount = _rng.Next(1, 4);
            for (var i = 0; i < amount; i++)
            {
                var p = RandomPointIn(b.Rect);
                list.Add(new Pickup(p, RollLoot(false)));
            }
        }

        foreach (var o in _outposts)
        {
            var amount = _rng.Next(3, 6);
            for (var i = 0; i < amount; i++)
            {
                var p = RandomPointIn(o.Rect);
                list.Add(new Pickup(p, RollLoot(true)));
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

    private Vector2 RandomOutdoorPoint()
    {
        while (true)
        {
            var point = new Vector2(_rng.Next(100, World - 100), _rng.Next(100, World - 100));
            if (_buildings.Any(z => Raylib.CheckCollisionPointRec(point, z.Rect))) continue;
            if (_outposts.Any(z => Raylib.CheckCollisionPointRec(point, z.Rect))) continue;
            return point;
        }
    }

    private List<Obstacle> GenerateObstacles()
    {
        var list = new List<Obstacle>();

        foreach (var zone in _buildings.Concat(_outposts))
        {
            var count = zone.IsOutpost ? _rng.Next(5, 8) : _rng.Next(3, 6);
            for (var i = 0; i < count; i++)
            {
                var w = zone.IsOutpost ? _rng.Next(60, 110) : _rng.Next(44, 84);
                var h = zone.IsOutpost ? _rng.Next(60, 110) : _rng.Next(44, 84);
                var x = _rng.Next((int)zone.Rect.X + 18, (int)(zone.Rect.X + zone.Rect.Width - w - 18));
                var y = _rng.Next((int)zone.Rect.Y + 18, (int)(zone.Rect.Y + zone.Rect.Height - h - 18));
                list.Add(new Obstacle(new Rectangle(x, y, w, h)));
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
                var patrolA = RandomPointIn(b.Rect);
                var patrolB = RandomPointIn(b.Rect);
                list.Add(Enemy.CreatePatrol(patrolA, patrolB, false));
            }

            var strongCount = _rng.Next(1, 3);
            for (var i = 0; i < strongCount; i++)
            {
                list.Add(Enemy.CreateStrong(RandomPointIn(b.Rect)));
            }
        }

        foreach (var o in _outposts)
        {
            var count = _rng.Next(5, 8);
            for (var i = 0; i < count; i++)
            {
                var patrolA = RandomPointIn(o.Rect);
                var patrolB = RandomPointIn(o.Rect);
                list.Add(Enemy.CreatePatrol(patrolA, patrolB, true));
            }
            var strong = _rng.Next(3, 5);
            for (var i = 0; i < strong; i++) list.Add(Enemy.CreateStrong(RandomPointIn(o.Rect)));
        }

        var outdoorPatrols = _rng.Next(8, 13);
        for (var i = 0; i < outdoorPatrols; i++)
        {
            var patrolA = RandomOutdoorPoint();
            var patrolB = patrolA + new Vector2(_rng.Next(-160, 161), _rng.Next(-160, 161));
            patrolB = Vector2.Clamp(patrolB, new Vector2(40f, 40f), new Vector2(World - 40f, World - 40f));
            list.Add(Enemy.CreatePatrol(patrolA, patrolB, false));
        }

        var outdoorStrong = _rng.Next(4, 8);
        for (var i = 0; i < outdoorStrong; i++) list.Add(Enemy.CreateStrong(RandomOutdoorPoint()));

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
    public int Agi { get; private set; } = 8;
    public int Tech { get; private set; } = 7;

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

        Inventory.Medkits = 2;
        Inventory.Stims = 2;
    }

    public static Player Create(Vector2 p) => new(p);

    public void Update(float dt)
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
            Position += dir * (140f + Agi * 6f);
            _dodgeCd = 1.1f;
        }

        if (d != Vector2.Zero)
        {
            var speed = 220f + Agi * 2f + (_stim > 0 ? 70f : 0f);
            Position += Vector2.Normalize(d) * speed * dt;
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
            projectiles.Add(new Projectile(Position + dir * 18f, dir, 520f, 1.15f, weapon.Color, false, bonus * 0.15f));
            _attackCd = 0.22f;
        }
        else
        {
            var angle = MathF.Atan2(dir.Y, dir.X);
            swings.Add(new SwingArc(Position, 78f, angle - 0.64f, angle + 0.64f, 0.14f, weapon.Color));
            _attackCd = 0.32f;
        }
    }

    public float GetMeleeDamage()
    {
        var power = MeleeWeapon?.PowerBonus ?? 0f;
        return (14f + Str * 1.8f + Agi * 0.8f + power) * 0.5f;
    }

    public float GetRangedDamage()
    {
        var power = RangedWeapon?.PowerBonus ?? 0f;
        return (10f + Tech * 1.9f + power) * 1.25f;
    }

    public void SwitchActiveWeapon() => ActiveWeaponClass = ActiveWeaponClass == WeaponClass.Ranged ? WeaponClass.Melee : WeaponClass.Ranged;

    public void UseMedkit()
    {
        if (Inventory.Medkits <= 0 || Health >= MaxHp) return;
        Inventory.Medkits--;
        Health = MathF.Min(MaxHp, Health + 36f);
    }

    public void UseStim()
    {
        if (Inventory.Stims <= 0) return;
        Inventory.Stims--;
        _stim = 6f;
    }

    public void ApplyBleed(float duration) => _bleed = MathF.Max(_bleed, duration);
    public void TickEffects(float dt) { }

    public void TakeDamage(float value)
    {
        var armor = Armor?.Defense ?? 0f;
        var reduced = MathF.Max(1f, value - armor * 0.75f - Tech * 0.2f);
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
        if (stat == StatType.Agility) Agi++;
        if (stat == StatType.Tech) Tech++;
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
    private int _burstShotsLeft;
    private float _burstShotCd;

    private float _deathAnim = 0.45f;

    private const float BaseView = 290f;
    private const float StrongView = 360f;
    private const float FovHalf = MathF.PI * 0.75f; // 270 total -> blind 90

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
            MaxHealth = outpost ? 130f : 100f,
            Health = outpost ? 130f : 100f
        };
        return e;
    }

    public static Enemy CreateStrong(Vector2 pos)
    {
        var e = new Enemy(pos)
        {
            IsStrong = true,
            MaxHealth = 220f,
            Health = 220f
        };
        return e;
    }

    public void UpdateVisionSweep(float dt)
    {
        if (!Alive) { _deathAnim -= dt; return; }

        // sweep left-right in 270° window (no full circles)
        _sweepPhase += dt * 1.4f * _sweepDir;
        if (_sweepPhase > 1f) { _sweepPhase = 1f; _sweepDir = -1f; }
        if (_sweepPhase < -1f) { _sweepPhase = -1f; _sweepDir = 1f; }

        var baseAngle = MathF.Atan2(_facing.Y, _facing.X);
        var sweepOffset = _sweepPhase * (MathF.PI * 0.45f);
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

    public void UpdateMovement(float dt, Vector2 playerPos)
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
                Position += dir * (IsStrong ? 95f : 118f) * dt;
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
            var target = _toB ? _patrolB : _patrolA;
            var to = target - Position;
            if (to.Length() < 8f)
            {
                _toB = !_toB;
            }
            else
            {
                var dir = Vector2.Normalize(to);
                _facing = dir;
                Position += dir * 86f * dt;
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
            projectiles.Add(new Projectile(Position + dir * 16f, dir, 420f, 1.4f, Palette.C(255, 120, 120), true, 2f));
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

    public void Draw()
    {
        if (Alive)
        {
            if (IsStrong)
            {
                var p1 = Position + new Vector2(0, -16);
                var p2 = Position + new Vector2(-14, 14);
                var p3 = Position + new Vector2(14, 14);
                Raylib.DrawTriangle(p1, p2, p3, Palette.C(240, 110, 110));
                Raylib.DrawTriangleLines(p1, p2, p3, Color.Maroon);
            }
            else
            {
                Raylib.DrawCircleV(Position, 14f, Palette.C(235, 95, 95));
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
    public float MaxHealth = 900f;
    public float Health = 900f;
    public bool Alive => Health > 0;
    public bool KillAwarded;

    private float _ramCd = 4f;
    private float _shootCd = 1.2f;
    private float _slamCd = 3.5f;
    private float _slamVisual;
    private bool _alert;
    private Vector2 _facing = new(1f, 0f);

    private const float ViewDistance = 460f;
    private const float FovHalf = MathF.PI * 0.72f;

    public BossEnemy(Vector2 pos) { Position = pos; }

    public void Update(float dt, Vector2 playerPos, List<Projectile> projectiles, Player player, List<Obstacle> obstacles)
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
        Position += dir * 42f * dt;

        if (_ramCd <= 0f)
        {
            Position += dir * 120f;
            _ramCd = 4f;
            if (Vector2.Distance(Position, playerPos) < 56f) player.TakeDamage(24f);
        }

        if (_shootCd <= 0f)
        {
            projectiles.Add(new Projectile(Position + dir * 28f, dir, 460f, 1.3f, Palette.C(255, 150, 120), true, 4f));
            _shootCd = 1.1f;
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

    public void Draw()
    {
        if (!Alive) return;

        var size = 42;
        Raylib.DrawRectangle((int)Position.X - size / 2, (int)Position.Y - size / 2, size, size, Palette.C(180, 60, 60));
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

public sealed class Projectile(Vector2 pos, Vector2 dir, float speed, float life, Color color, bool ownerEnemy, float damageBonus)
{
    public Vector2 Position { get; private set; } = pos;
    public Color Color { get; } = color;
    public bool OwnerEnemy { get; } = ownerEnemy;
    public float DamageBonus { get; } = damageBonus;
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

public sealed class SwingArc(Vector2 origin, float radius, float angleStart, float angleEnd, float life, Color color)
{
    public Vector2 Origin { get; } = origin;
    public float Radius { get; } = radius;
    public float AngleStart { get; } = angleStart;
    public float AngleEnd { get; } = angleEnd;
    public float Life { get; set; } = life;
    public Color Color { get; } = color;
}

public sealed class LootZone(Rectangle rect, bool isOutpost)
{
    public Rectangle Rect { get; } = rect;
    public bool IsOutpost { get; } = isOutpost;
}

public sealed class Obstacle(Rectangle rect)
{
    public Rectangle Rect { get; } = rect;
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

public sealed class Pickup(Vector2 position, ItemStack item)
{
    public Vector2 Position { get; } = position;
    public ItemStack Item { get; } = item;
    public bool Picked { get; set; }
}

public sealed class Inventory
{
    public List<ItemStack> Items { get; } = [];
    public int Medkits { get; set; }
    public int Stims { get; set; }

    public ItemStack? Trash { get; set; }

    public void AddConsumable(ConsumableType type)
    {
        if (type == ConsumableType.Medkit) Medkits++;
        if (type == ConsumableType.Stim) Stims++;
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
    public ConsumableType? ConsumableKind { get; }

    public float Defense { get; }
    public float PowerBonus { get; }

    private ItemStack(ItemType type, string name, string description, ArmorRarity rarity, Color color, WeaponClass? weaponClass, ConsumableType? consumableType, float defense, float powerBonus)
    {
        Type = type;
        Name = name;
        Description = description;
        Rarity = rarity;
        Color = color;
        WeaponKind = weaponClass;
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

        return new ItemStack(ItemType.Armor, name, "Armor. Drag into armor slot.", rarity, Palette.Rarity(rarity), null, null, baseDef + rng.NextSingle() * 4f, 0f);
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
        var name = kind == WeaponClass.Ranged ? "Rail Pistol" : "Plasma Blade";
        return new ItemStack(ItemType.Weapon, name, "Weapon. Drag to matching slot.", rarity, Palette.Rarity(rarity), kind, null, 0f, p);
    }

    public static ItemStack Consumable(ConsumableType t)
    {
        return t == ConsumableType.Medkit
            ? new ItemStack(ItemType.Consumable, "Medkit", "Restore HP. Hotkey Q.", ArmorRarity.Common, Palette.C(130, 210, 120), null, t, 0f, 0f)
            : new ItemStack(ItemType.Consumable, "Stim", "Move speed boost. Hotkey R.", ArmorRarity.Common, Palette.C(220, 220, 120), null, t, 0f, 0f);
    }
}

public enum SlotKind
{
    RangedWeapon,
    MeleeWeapon,
    Armor,
    Trash,
    Backpack,
    MedkitStack,
    StimStack
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
