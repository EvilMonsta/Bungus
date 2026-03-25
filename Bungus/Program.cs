using System.Numerics;
using System.Text.Json;
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

public enum GameState { MainMenu, MapSelect, Storage, Character, Settings, Playing, Paused, Death }
public enum WeaponClass { Melee, Ranged }
public enum ItemType { Weapon, Armor, Consumable }
public enum ConsumableType { Medkit, Stim }
public enum ArmorRarity { Common, Rare, Epic, Legendary, Red }
public enum StatType { Strength, Dexterity, Speed, Gunsmith }
public enum WeaponPattern { Standard, PulseRifle, EnergySpear, GrenadeLauncher }
public enum ProjectileKind { Bullet, Grenade }

public static class Palette
{
    public static Color C(int r, int g, int b, int a = 255) => new((byte)r, (byte)g, (byte)b, (byte)a);

    public static Color Rarity(ArmorRarity r) => r switch
    {
        ArmorRarity.Common => Color.LightGray,
        ArmorRarity.Rare => Color.SkyBlue,
        ArmorRarity.Epic => C(191, 120, 255),
        ArmorRarity.Legendary => Color.Gold,
        ArmorRarity.Red => C(230, 45, 45),
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
    private const float PortalUnlockDelay = 120f;
    private const float PortalLifetime = 300f;
    private static readonly string SaveFilePath = Path.Combine(AppContext.BaseDirectory, "save", "profile.json");
    private static readonly JsonSerializerOptions SaveJsonOptions = new() { WriteIndented = true };

    private readonly Random _rng = new();
    private Camera2D _camera;

    private GameState _state = GameState.MainMenu;
    private Player _player = null!;

    private List<Enemy> _enemies = [];
    private List<HexEnemy> _hexEnemies = [];
    private List<TurretEnemy> _turrets = [];
    private List<MiniBossEnemySquare> _miniBosses = [];
    private BossEnemyDestroyer? _destroyerBoss;
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
    private SlotKind _lastClickKind;
    private int _lastClickIndex = -1;
    private double _lastClickTime;
    private bool _pendingUpgrade;
    private StatType _pendingStat;
    private int? _openedChestIndex;
    private bool _requestExit;
    private readonly List<VisualTheme> _themes;
    private int _themeIndex;
    private float _nextHexSpawnTimer;
    private readonly MetaProfile _meta = new();
    private readonly List<ExtractPortal> _extractPortals = [];
    private string _selectedMapName = "Baselands";
    private int _runScore;
    private float _portalUnlockTimer;
    private float _portalActiveTimer;
    private string _noticeText = string.Empty;
    private float _noticeTimer;
    private string _deathHeader = "You Died";
    private string _deathBody = "All carried items were lost.";

    private static readonly Rectangle TakeAllButtonRect = new(760, 266, 170, 34);

    public SciFiRogueGame()
    {
        Raylib.SetConfigFlags(ConfigFlags.Msaa4xHint);
        Raylib.InitWindow(W, H, "Bungus");
        Raylib.SetTargetFPS(60);
        Raylib.SetExitKey(KeyboardKey.Null);
        Raylib.ToggleFullscreen();

        _camera = new Camera2D { Zoom = 1.08f, Rotation = 0f };
        _themes = BuildThemes();
        LoadPersistentState();
    }

    private VisualTheme Theme => _themes[_themeIndex];

    private void StartRun(string mapName)
    {
        _selectedMapName = mapName;
        (_buildings, _outposts) = GenerateZones(_rng.Next(14, 21), _rng.Next(7, 11));
        _obstacles = GenerateObstacles();
        _chests = GenerateChestsInZones();
        _player = Player.Create(
            GeneratePlayerSpawnPoint(),
            GetCommonHealthBonus(),
            GetCommonDamageBonus(),
            TakeMetaLoadoutItem(SlotKind.RangedWeapon),
            TakeMetaLoadoutItem(SlotKind.MeleeWeapon),
            TakeMetaLoadoutItem(SlotKind.Armor),
            TakeMetaLoadoutItem(SlotKind.QuickSlotQ),
            TakeMetaLoadoutItem(SlotKind.QuickSlotR));
        _projectiles = [];
        _explosions = [];
        _swings = [];
        _enemies = GenerateEnemies();
        _hexEnemies = [];
        _turrets = GenerateTurrets();
        _miniBosses = GenerateMiniBosses();
        _destroyerBoss = GenerateDestroyerBoss();
        _nextHexSpawnTimer = NextHexSpawnDelay();
        _extractPortals.Clear();
        _runScore = 0;
        _portalUnlockTimer = PortalUnlockDelay;
        _portalActiveTimer = PortalLifetime;
        _player.InventoryOpen = false;
        _openedChestIndex = null;
        _drag = null;
        _hovered = null;
        _pendingUpgrade = false;

        _camera.Offset = new Vector2(Raylib.GetScreenWidth() / 2f, Raylib.GetScreenHeight() / 2f);
        _camera.Target = _player.Position;
        SavePersistentState();
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
            case GameState.MapSelect: UpdateMapSelect(); break;
            case GameState.Storage: UpdateStorage(); break;
            case GameState.Character: UpdateCharacter(); break;
            case GameState.Settings: UpdateSettings(); break;
            case GameState.Playing: UpdatePlaying(dt); break;
            case GameState.Paused: UpdatePause(); break;
            case GameState.Death: UpdateDeath(); break;
        }

        if (_noticeTimer > 0f)
        {
            _noticeTimer -= dt;
            if (_noticeTimer <= 0f) _noticeText = string.Empty;
        }
    }

    private void UpdateMainMenu()
    {
        if (Clicked(MainMenuButtonRect(0))) { ClearUiInteraction(); _state = GameState.MapSelect; }
        if (Clicked(MainMenuButtonRect(1))) { ClearUiInteraction(); _state = GameState.Storage; }
        if (Clicked(MainMenuButtonRect(2))) { ClearUiInteraction(); _state = GameState.Character; }
        if (Clicked(MainMenuButtonRect(3))) { ClearUiInteraction(); _state = GameState.Settings; }
        if (Clicked(MainMenuButtonRect(4))) _requestExit = true;
    }

    private void UpdateMapSelect()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Clicked(new Rectangle(70, 620, 220, 52)))
        {
            ClearUiInteraction();
            _state = GameState.MainMenu;
            return;
        }

        var card = new Rectangle(340, 170, 600, 320);
        var deploy = new Rectangle(340, 520, 280, 58);
        if (Clicked(card) || Clicked(deploy))
        {
            ClearUiInteraction();
            StartRun("Baselands");
            _state = GameState.Playing;
        }
    }

    private void UpdateStorage()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Clicked(new Rectangle(70, 620, 220, 52)))
        {
            ClearUiInteraction();
            _state = GameState.MainMenu;
            return;
        }

        UpdateStorageUi();
    }

    private void UpdateCharacter()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Clicked(new Rectangle(70, 620, 220, 52)))
        {
            ClearUiInteraction();
            _state = GameState.MainMenu;
        }
    }

    private void UpdateSettings()
    {
        for (var i = 0; i < _themes.Count; i++)
        {
            if (Clicked(CenterRect(0, 220 + i * 68, 360, 56)))
            {
                _themeIndex = i;
                SavePersistentState();
            }
        }

        if (Clicked(CenterRect(0, 620, 280, 56)) || Raylib.IsKeyPressed(KeyboardKey.Escape)) _state = GameState.MainMenu;
    }

    private void UpdatePause()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape)) _state = GameState.Playing;
        if (Clicked(CenterRect(0, 320, 320, 62))) _state = GameState.Playing;
        if (Clicked(CenterRect(0, 400, 320, 62))) FailRun("Run abandoned", "All carried items were lost.");
    }

    private void UpdateDeath()
    {
        if (Clicked(CenterRect(0, 320, 320, 62))) { StartRun(_selectedMapName); _state = GameState.Playing; }
        if (Clicked(CenterRect(0, 400, 320, 62))) { ClearUiInteraction(); _state = GameState.MainMenu; }
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
        _player.UpdateCombat(dt, _projectiles);
        if (Raylib.IsKeyPressed(KeyboardKey.Q)) _player.UseQuickSlotQ();
        if (Raylib.IsKeyPressed(KeyboardKey.R)) _player.UseQuickSlotR();
        if (Raylib.IsKeyPressed(KeyboardKey.E)) _player.SwitchActiveWeapon();

        var mouseWorld = Raylib.GetScreenToWorld2D(Raylib.GetMousePosition(), _camera);
        if (Raylib.IsMouseButtonDown(MouseButton.Left) && !_player.InventoryOpen)
        {
            _player.Attack(mouseWorld, _projectiles, _swings);
        }

        UpdateEnemies(dt);
        UpdateHexEnemies(dt);
        UpdateTurrets(dt);
        UpdateMiniBosses(dt);
        UpdateDestroyerBoss(dt);
        UpdateProjectiles(dt);
        UpdateSwings(dt);
        UpdateEffects(dt);
        UpdateChests();
        UpdateInventoryUi();
        UpdateLevelUi();
        if (_drag is null) _player.Inventory.AutoFillConsumableSlots();
        UpdateExtraction(dt);
        if (_state != GameState.Playing) return;

        _camera.Target = Vector2.Lerp(_camera.Target, _player.Position, 0.2f);
        if (_player.Health <= 0) FailRun("You Died", "All carried items were lost.");
    }

    private float NextHexSpawnDelay() => 20f + _rng.NextSingle() * 160f;

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
                AddRunScore(e.IsStrong ? 20 : 10);
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

        _nextHexSpawnTimer -= dt;
        if (_nextHexSpawnTimer <= 0f)
        {
            var packSize = _rng.Next(1, 6);
            for (var i = 0; i < packSize; i++)
            {
                _hexEnemies.Add(HexEnemy.Create(RandomMapPointSafe(16f), _rng));
            }
            _nextHexSpawnTimer = NextHexSpawnDelay();
        }
    }

    private void UpdateHexEnemies(float dt)
    {
        foreach (var h in _hexEnemies)
        {
            h.Update(dt, _player.Position, _projectiles, _obstacles, World);
            if (!h.Alive && !h.KillAwarded)
            {
                h.KillAwarded = true;
                _player.RegisterKill();
                AddRunScore(25);
            }
        }
    }

    private void UpdateTurrets(float dt)
    {
        foreach (var turret in _turrets)
        {
            turret.Update(dt, _player.Position, _projectiles, _obstacles);
            if (!turret.Alive && !turret.KillAwarded)
            {
                turret.KillAwarded = true;
                _player.RegisterKill();
                AddRunScore(20);
            }
        }
    }

    private void UpdateMiniBosses(float dt)
    {
        foreach (var b in _miniBosses)
        {
            b.Update(dt, _player.Position, _projectiles, _player, _obstacles, World, _dashAfterImages);
            if (!b.Alive && !b.KillAwarded)
            {
                b.KillAwarded = true;
                _player.RegisterKill();
                _player.RegisterKill();
                _player.RegisterKill();
                AddRunScore(100);
            }
        }
    }

    private void UpdateDestroyerBoss(float dt)
    {
        if (_destroyerBoss is null) return;

        _destroyerBoss.Update(dt, _player.Position, _projectiles, _player, _obstacles, World, _dashAfterImages);
        if (!_destroyerBoss.Alive && !_destroyerBoss.KillAwarded)
        {
            _destroyerBoss.KillAwarded = true;
            _player.RegisterKill();
            _player.RegisterKill();
            _player.RegisterKill();
            _player.RegisterKill();
            _player.RegisterKill();
            AddRunScore(1000);
            _chests.Add(new LootChest(_destroyerBoss.Position, [ItemStack.BossGrenadeLauncher()]));
        }
    }

    private void UpdateProjectiles(float dt)
    {
        for (var i = _projectiles.Count - 1; i >= 0; i--)
        {
            var p = _projectiles[i];
            p.Update(dt);

            var hitWorldBounds = p.Position.X < 0 || p.Position.Y < 0 || p.Position.X > World || p.Position.Y > World;
            var hitObstacle = MovementUtils.CircleHitsObstacle(p.Position, p.DrawRadius, _obstacles);

            if (p.Kind == ProjectileKind.Grenade)
            {
                var directHit = false;
                var hitTarget = false;

                if (p.OwnerEnemy)
                {
                    hitTarget = Vector2.Distance(p.Position, _player.Position) < 16f;
                }
                else
                {
                    directHit = TryApplyPlayerSegmentDamage(p.PreviousPosition, p.Position, p.DrawRadius, p.Damage);
                    hitTarget = directHit || HasEnemyInRadius(p.Position, 22f);
                }

                if (hitWorldBounds || hitObstacle || hitTarget || !p.Alive)
                {
                    ExplodeProjectile(p);
                    _projectiles.RemoveAt(i);
                }

                continue;
            }

            if (hitWorldBounds || hitObstacle)
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

            if (TryApplyPlayerSegmentDamage(p.PreviousPosition, p.Position, p.DrawRadius, p.Damage))
            {
                _explosions.Add(new Explosion(p.Position, 34f, p.Color));
                _projectiles.RemoveAt(i);
                continue;
            }

            if (!p.Alive) _projectiles.RemoveAt(i);
        }
    }

    private bool HasEnemyInRadius(Vector2 position, float radius)
    {
        if (_enemies.Any(e => e.Alive && Vector2.Distance(e.Position, position) < radius)) return true;
        if (_hexEnemies.Any(h => h.Alive && Vector2.Distance(h.Position, position) < radius)) return true;
        if (_turrets.Any(t => t.Alive && Vector2.Distance(t.Position, position) < radius + 6f)) return true;
        if (_miniBosses.Any(b => b.Alive && Vector2.Distance(b.Position, position) < radius + 14f)) return true;
        return _destroyerBoss is not null
            && _destroyerBoss.Alive
            && _destroyerBoss.IntersectsAnyHitZone(position, radius);
    }

    private bool TryApplyPlayerSegmentDamage(Vector2 from, Vector2 to, float radius, float damage)
    {
        var enemyHit = _enemies
            .Where(e => e.Alive && DistanceToSegment(e.Position, from, to) <= radius + 11f)
            .OrderBy(e => DistanceToSegment(e.Position, from, to))
            .FirstOrDefault();
        if (enemyHit is not null)
        {
            enemyHit.Damage(damage);
            enemyHit.ForceAggro(_player.Position);
            enemyHit.JustHitByPlayer = true;
            return true;
        }

        var hexHit = _hexEnemies
            .Where(h => h.Alive && DistanceToSegment(h.Position, from, to) <= radius + 15f)
            .OrderBy(h => DistanceToSegment(h.Position, from, to))
            .FirstOrDefault();
        if (hexHit is not null)
        {
            hexHit.Damage(damage);
            return true;
        }

        var turretHit = _turrets
            .Where(t => t.Alive && DistanceToSegment(t.Position, from, to) <= radius + 18f)
            .OrderBy(t => DistanceToSegment(t.Position, from, to))
            .FirstOrDefault();
        if (turretHit is not null)
        {
            turretHit.Damage(damage);
            return true;
        }

        var miniBossHit = _miniBosses
            .Where(b => b.Alive && DistanceToSegment(b.Position, from, to) <= radius + 26f)
            .OrderBy(b => DistanceToSegment(b.Position, from, to))
            .FirstOrDefault();
        if (miniBossHit is not null)
        {
            miniBossHit.Damage(damage);
            return true;
        }

        if (_destroyerBoss is not null && _destroyerBoss.Alive && _destroyerBoss.TryApplySegmentDamage(from, to, radius, damage))
        {
            return true;
        }

        return false;
    }

    private void ExplodeProjectile(Projectile projectile)
    {
        _explosions.Add(new Explosion(projectile.Position, projectile.ExplosionRadius, projectile.Color));

        if (projectile.OwnerEnemy)
        {
            if (Vector2.Distance(projectile.Position, _player.Position) <= projectile.ExplosionRadius)
            {
                _player.TakeDamage(projectile.ExplosionDamage);
            }

            return;
        }

        foreach (var enemy in _enemies.Where(e => e.Alive && Vector2.Distance(e.Position, projectile.Position) <= projectile.ExplosionRadius))
        {
            enemy.Damage(projectile.ExplosionDamage);
            enemy.ForceAggro(_player.Position);
            enemy.JustHitByPlayer = true;
        }

        foreach (var hex in _hexEnemies.Where(h => h.Alive && Vector2.Distance(h.Position, projectile.Position) <= projectile.ExplosionRadius))
        {
            hex.Damage(projectile.ExplosionDamage);
        }

        foreach (var turret in _turrets.Where(t => t.Alive && Vector2.Distance(t.Position, projectile.Position) <= projectile.ExplosionRadius))
        {
            turret.Damage(projectile.ExplosionDamage);
        }

        foreach (var miniBoss in _miniBosses.Where(b => b.Alive && Vector2.Distance(b.Position, projectile.Position) <= projectile.ExplosionRadius))
        {
            miniBoss.Damage(projectile.ExplosionDamage);
        }

        if (_destroyerBoss is not null && _destroyerBoss.Alive)
        {
            _destroyerBoss.ApplyExplosionDamage(projectile.Position, projectile.ExplosionRadius, projectile.ExplosionDamage);
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

            foreach (var h in _hexEnemies.Where(x => x.Alive))
            {
                var hit = s.IsLine
                    ? DistanceToSegment(h.Position, s.LineStart, s.LineEnd) < 16f
                    : IsInArc(h.Position, s, 10f);
                if (hit) h.Damage(_player.GetMeleeDamage());
            }

            foreach (var t in _turrets.Where(x => x.Alive))
            {
                var hit = s.IsLine
                    ? DistanceToSegment(t.Position, s.LineStart, s.LineEnd) < 20f
                    : IsInArc(t.Position, s, 14f);
                if (hit) t.Damage(_player.GetMeleeDamage());
            }

            foreach (var b in _miniBosses.Where(x => x.Alive))
            {
                var hit = s.IsLine
                    ? DistanceToSegment(b.Position, s.LineStart, s.LineEnd) < 28f
                    : IsInArc(b.Position, s, 24f);
                if (hit) b.Damage(_player.GetMeleeDamage() * 0.75f);
            }

            if (_destroyerBoss is not null && _destroyerBoss.Alive)
            {
                var hit = s.IsLine
                    ? DistanceToSegment(_destroyerBoss.Position, s.LineStart, s.LineEnd) < 54f
                    : IsInArc(_destroyerBoss.Position, s, 50f);
                if (hit) _destroyerBoss.Damage(_player.GetMeleeDamage() * 0.75f);
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

            if (_openedChestIndex == i)
            {
                _openedChestIndex = null;
                _player.InventoryOpen = false;
                break;
            }

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
            _player.InventoryOpen = false;
            return;
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
            var from = slots.FirstOrDefault(s => Raylib.CheckCollisionPointRec(m, s.Rect));
            if (from is not null)
            {
                var now = Raylib.GetTime();
                var isDoubleClick = from.Item is not null &&
                                    from.Kind == _lastClickKind &&
                                    from.Index == _lastClickIndex &&
                                    now - _lastClickTime <= 0.3;

                _lastClickKind = from.Kind;
                _lastClickIndex = from.Index;
                _lastClickTime = now;

                if (isDoubleClick && from.Item is not null && HandleDoubleClick(from))
                {
                    _drag = null;
                    return;
                }

                if (from.Item is null) return;
                _drag = new DragPayload(from.Kind, from.Index, from.Item!);
            }
        }

        if (_openedChestIndex is not null && Clicked(TakeAllButtonRect))
        {
            MoveAllFromChestToBackpack();
        }

        if (Raylib.IsMouseButtonReleased(MouseButton.Left) && _drag is not null)
        {
            var to = slots.FirstOrDefault(s => Raylib.CheckCollisionPointRec(m, s.Rect));
            if (to is not null) ApplyDrop(_drag, to);
            _drag = null;
        }
    }

    private bool HandleDoubleClick(UiSlot slot)
    {
        if (slot.Kind == SlotKind.Chest && _openedChestIndex is not null)
        {
            return MoveChestItemToBackpack(slot.Index);
        }

        if (slot.Kind == SlotKind.Backpack)
        {
            return EquipFromBackpack(slot.Index);
        }

        return false;
    }

    private void UpdateStorageUi()
    {
        _hovered = null;
        var slots = BuildStorageSlots();
        var mouse = Raylib.GetMousePosition();

        foreach (var slot in slots)
        {
            if (Raylib.CheckCollisionPointRec(mouse, slot.Rect)) _hovered = slot.Item;
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            var from = slots.FirstOrDefault(s => Raylib.CheckCollisionPointRec(mouse, s.Rect));
            if (from is not null)
            {
                var now = Raylib.GetTime();
                var isDoubleClick = from.Item is not null &&
                                    from.Kind == _lastClickKind &&
                                    from.Index == _lastClickIndex &&
                                    now - _lastClickTime <= 0.3;

                _lastClickKind = from.Kind;
                _lastClickIndex = from.Index;
                _lastClickTime = now;

                if (isDoubleClick && from.Item is not null && HandleStorageDoubleClick(from))
                {
                    _drag = null;
                    return;
                }

                if (from.Item is null) return;
                _drag = new DragPayload(from.Kind, from.Index, from.Item);
            }
        }

        if (Raylib.IsMouseButtonReleased(MouseButton.Left) && _drag is not null)
        {
            var to = slots.FirstOrDefault(s => Raylib.CheckCollisionPointRec(mouse, s.Rect));
            if (to is not null) ApplyStorageDrop(_drag, to);
            _drag = null;
        }
    }

    private bool HandleStorageDoubleClick(UiSlot slot)
    {
        if (slot.Kind == SlotKind.Storage)
        {
            return EquipFromStorage(slot.Index);
        }

        if (IsMetaLoadoutSlot(slot.Kind))
        {
            return MoveLoadoutItemToStorage(slot.Kind);
        }

        return false;
    }

    private bool EquipFromStorage(int storageIndex)
    {
        if (storageIndex < 0 || storageIndex >= _meta.StorageSlots.Count) return false;

        var item = _meta.StorageSlots[storageIndex];
        if (item is null) return false;

        var target = GetPreferredLoadoutSlot(item);
        if (target is null) return false;

        var old = GetMetaLoadoutItem(target.Value);
        SetMetaLoadoutItem(target.Value, item);
        _meta.StorageSlots[storageIndex] = old;
        SavePersistentState();
        return true;
    }

    private bool MoveLoadoutItemToStorage(SlotKind kind)
    {
        var item = GetMetaLoadoutItem(kind);
        if (item is null) return false;
        if (!_meta.AddToStorage(item)) return false;
        SetMetaLoadoutItem(kind, null);
        SavePersistentState();
        return true;
    }

    private List<UiSlot> BuildStorageSlots()
    {
        var list = new List<UiSlot>();

        for (var i = 0; i < _meta.StorageSlots.Count; i++)
        {
            var c = i % 10;
            var r = i / 10;
            list.Add(new UiSlot(new Rectangle(414 + c * 48, 174 + r * 46, 42, 42), SlotKind.Storage, i, _meta.StorageSlots[i], i));
        }

        list.AddRange(
        [
            new UiSlot(new Rectangle(238, 226, 58, 58), SlotKind.Armor, -1, _meta.Armor, -1),
            new UiSlot(new Rectangle(238, 294, 58, 58), SlotKind.RangedWeapon, -1, _meta.RangedWeapon, -1),
            new UiSlot(new Rectangle(238, 362, 58, 58), SlotKind.MeleeWeapon, -1, _meta.MeleeWeapon, -1),
            new UiSlot(new Rectangle(206, 454, 58, 58), SlotKind.QuickSlotQ, -1, _meta.QuickSlotQ, -1),
            new UiSlot(new Rectangle(272, 454, 58, 58), SlotKind.QuickSlotR, -1, _meta.QuickSlotR, -1),
            new UiSlot(new Rectangle(130, 536, 58, 58), SlotKind.Trash, -1, _meta.Trash, -1)
        ]);

        return list;
    }

    private void ApplyStorageDrop(DragPayload drag, UiSlot target)
    {
        if (drag.Kind == target.Kind && drag.Index == target.Index) return;

        if (target.Kind == SlotKind.Trash)
        {
            _meta.Trash = drag.Item;
            ReplaceStorageSourceWith(drag, null);
            SavePersistentState();
            return;
        }

        if (target.Kind == SlotKind.Storage)
        {
            var old = _meta.StorageSlots[target.Index];
            if (!CanReplaceStorageSource(drag, old)) return;

            _meta.StorageSlots[target.Index] = drag.Item;
            ReplaceStorageSourceWith(drag, old);
            SavePersistentState();
            return;
        }

        if (!IsMetaLoadoutSlot(target.Kind) || !CanPlaceIntoSlot(target.Kind, drag.Item)) return;

        var existing = GetMetaLoadoutItem(target.Kind);
        if (!CanReplaceStorageSource(drag, existing)) return;

        SetMetaLoadoutItem(target.Kind, drag.Item);
        ReplaceStorageSourceWith(drag, existing);
        SavePersistentState();
    }

    private bool CanReplaceStorageSource(DragPayload drag, ItemStack? replacement)
    {
        if (replacement is null) return true;
        if (drag.Kind == SlotKind.Storage) return true;
        if (IsMetaLoadoutSlot(drag.Kind) && CanPlaceIntoSlot(drag.Kind, replacement)) return true;
        return _meta.HasFreeStorageSlot();
    }

    private void ReplaceStorageSourceWith(DragPayload drag, ItemStack? replacement)
    {
        if (drag.Kind == SlotKind.Storage)
        {
            _meta.StorageSlots[drag.Index] = replacement;
            return;
        }

        if (!IsMetaLoadoutSlot(drag.Kind)) return;

        if (replacement is null || CanPlaceIntoSlot(drag.Kind, replacement))
        {
            SetMetaLoadoutItem(drag.Kind, replacement);
            return;
        }

        SetMetaLoadoutItem(drag.Kind, null);
        _meta.AddToStorage(replacement);
    }

    private bool MoveChestItemToBackpack(int chestIndex)
    {
        if (_openedChestIndex is null || chestIndex < 0) return false;

        var chest = _chests[_openedChestIndex.Value];
        if (chestIndex >= chest.Items.Count) return false;

        var item = chest.Items[chestIndex];
        if (!_player.Inventory.AddToBackpack(item)) return false;

        chest.Items.RemoveAt(chestIndex);
        return true;
    }

    private void MoveAllFromChestToBackpack()
    {
        if (_openedChestIndex is null) return;

        var chest = _chests[_openedChestIndex.Value];
        for (var i = chest.Items.Count - 1; i >= 0; i--)
        {
            var item = chest.Items[i];
            if (_player.Inventory.AddToBackpack(item)) chest.Items.RemoveAt(i);
        }
    }

    private bool EquipFromBackpack(int backpackIndex)
    {
        if (backpackIndex < 0 || backpackIndex >= _player.Inventory.BackpackSlots.Count) return false;
        if (_openedChestIndex is not null) return false;

        var item = _player.Inventory.BackpackSlots[backpackIndex];
        if (item is null) return false;

        if (item.Type == ItemType.Armor)
        {
            (_player.Armor, _player.Inventory.BackpackSlots[backpackIndex]) = (item, _player.Armor);
            return true;
        }

        if (item.Type != ItemType.Weapon || item.WeaponKind is null) return false;

        if (item.WeaponKind == WeaponClass.Ranged)
        {
            (_player.RangedWeapon, _player.Inventory.BackpackSlots[backpackIndex]) = (item, _player.RangedWeapon);
            return true;
        }

        (_player.MeleeWeapon, _player.Inventory.BackpackSlots[backpackIndex]) = (item, _player.MeleeWeapon);
        return true;
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
                new UiSlot(new Rectangle(560, 348, 58, 58), SlotKind.QuickSlotQ, null, _player.Inventory.QuickSlotQ, -1),
                new UiSlot(new Rectangle(624, 348, 58, 58), SlotKind.QuickSlotR, null, _player.Inventory.QuickSlotR, -1),
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

        if (target.Kind == SlotKind.QuickSlotQ && drag.Item.Type == ItemType.Consumable)
        {
            var old = _player.Inventory.QuickSlotQ;
            _player.Inventory.QuickSlotQ = drag.Item;
            RemoveFromSource(drag);
            if (old is not null) _player.Inventory.AddToBackpack(old);
            return;
        }

        if (target.Kind == SlotKind.QuickSlotR && drag.Item.Type == ItemType.Consumable)
        {
            var old = _player.Inventory.QuickSlotR;
            _player.Inventory.QuickSlotR = drag.Item;
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
        else if (drag.Kind == SlotKind.QuickSlotQ)
        {
            _player.Inventory.QuickSlotQ = null;
        }
        else if (drag.Kind == SlotKind.QuickSlotR)
        {
            _player.Inventory.QuickSlotR = null;
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
            case GameState.MapSelect:
                DrawMapSelect();
                break;
            case GameState.Storage:
                DrawStorage();
                break;
            case GameState.Character:
                DrawCharacter();
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

        DrawNotice();

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

        foreach (var portal in _extractPortals) portal.Draw((float)Raylib.GetTime());

        foreach (var ghost in _dashAfterImages) ghost.Draw();

        foreach (var e in _enemies) e.DrawSight();
        foreach (var h in _hexEnemies) h.DrawSight();
        foreach (var t in _turrets) t.DrawSight();
        foreach (var b in _miniBosses) b.DrawSight();
        _destroyerBoss?.DrawSight();
        foreach (var e in _enemies) e.Draw(Theme);
        foreach (var h in _hexEnemies) h.Draw();
        foreach (var t in _turrets) t.Draw();
        foreach (var b in _miniBosses) b.Draw(Theme);
        _destroyerBoss?.Draw();
        foreach (var t in _turrets) t.DrawAimLine();

        foreach (var p in _projectiles)
        {
            Raylib.DrawCircleV(p.Position, p.DrawRadius, p.Color);
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
        Raylib.EndMode2D();
    }

    private void DrawHud()
    {
        Raylib.DrawText($"HP {_player.Health:0}/{_player.MaxHealth:0} | Level {_player.Level} ({_player.Kills}/{_player.KillsTarget})", 20, 14, 24, Color.White);

        var activeWeapon = _player.ActiveWeaponClass == WeaponClass.Ranged ? _player.RangedWeapon : _player.MeleeWeapon;
        Raylib.DrawText($"Current: {activeWeapon?.Name ?? "None"} {BuildWeaponDamageText(activeWeapon, _player.ActiveWeaponClass)}", 20, 48, 22, activeWeapon?.Color ?? Color.LightGray);
        Raylib.DrawText($"Consumables: Q [{(_player.Inventory.QuickSlotQ?.Name ?? "-")}]  R [{(_player.Inventory.QuickSlotR?.Name ?? "-")}]", 20, 78, 20, Color.White);
        Raylib.DrawText($"Run score {_runScore}", 20, 108, 20, Color.Gold);
        DrawExtractionHud();
        Raylib.DrawText("WASD move | LMB attack | E switch active weapon | TAB inventory | ESC menu", 20, Raylib.GetScreenHeight() - 28, 18, Color.Gray);
        DrawZoneArrows();
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
            DrawButton(TakeAllButtonRect, "Take all");
        }

        foreach (var slot in slots)
        {
            Raylib.DrawRectangleRec(slot.Rect, Palette.C(22, 28, 42, 255));
            Raylib.DrawRectangleLinesEx(slot.Rect, 1f, Color.SkyBlue);
            if (slot.Kind == SlotKind.Trash) Raylib.DrawText("TR", (int)slot.Rect.X + 16, (int)slot.Rect.Y + 18, 20, Color.Orange);
            if (slot.Kind == SlotKind.QuickSlotQ) Raylib.DrawText("Q", (int)slot.Rect.X + 20, (int)slot.Rect.Y - 18, 16, Color.Green);
            if (slot.Kind == SlotKind.QuickSlotR) Raylib.DrawText("R", (int)slot.Rect.X + 20, (int)slot.Rect.Y - 18, 16, Color.Yellow);
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
        DrawTitle("BUNGUS", 72, 84);
        Raylib.DrawText("alpha 0.1.0", 86, 150, 24, Palette.C(150, 185, 220));

        var heroPanel = new Rectangle(340, 150, 720, 300);
        Raylib.DrawRectangleRec(heroPanel, Palette.C(8, 16, 28, 220));
        Raylib.DrawRectangleLinesEx(heroPanel, 2f, Palette.C(110, 170, 230));
        Raylib.DrawText("Baselands Deployment Program", 382, 188, 34, Color.White);
        Raylib.DrawText("Prepare your loadout, dive into the wastes and extract before the portals collapse.", 382, 236, 24, Color.LightGray);
        Raylib.DrawText($"General level {_meta.Level}", 382, 300, 28, Color.Gold);
        Raylib.DrawText($"Progress {_meta.Score}/{GetMetaScoreRequired(_meta.Level)}", 382, 338, 24, Color.White);
        Raylib.DrawText($"+{GetCommonHealthBonus():0} max HP on landing", 382, 382, 22, Palette.C(140, 220, 160));
        Raylib.DrawText($"+{GetCommonDamageBonus():0} base damage on landing", 382, 412, 22, Palette.C(255, 210, 120));

        DrawButton(MainMenuButtonRect(0), "Play");
        DrawButton(MainMenuButtonRect(1), "Storage");
        DrawButton(MainMenuButtonRect(2), "Character");
        DrawButton(MainMenuButtonRect(3), "Settings");
        DrawButton(MainMenuButtonRect(4), "Exit");
    }

    private void DrawMapSelect()
    {
        DrawTitle("Select Map", 64, 66);
        Raylib.DrawText("Choose your landing zone", 72, 118, 26, Color.LightGray);

        var card = new Rectangle(340, 170, 600, 320);
        var hover = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), card);
        Raylib.DrawRectangleRec(card, hover ? Palette.C(22, 40, 62) : Palette.C(14, 24, 40));
        Raylib.DrawRectangleLinesEx(card, 2f, Palette.C(116, 180, 235));

        var sky = new Rectangle(card.X + 24, card.Y + 24, card.Width - 48, 120);
        Raylib.DrawRectangleRec(sky, Palette.C(34, 58, 86));
        Raylib.DrawCircleGradient((int)(sky.X + sky.Width - 90), (int)(sky.Y + 48), 42, Palette.C(250, 214, 120), Palette.C(250, 214, 120, 30));
        Raylib.DrawRectangle((int)card.X + 40, (int)card.Y + 220, 160, 54, Palette.C(60, 96, 126));
        Raylib.DrawRectangle((int)card.X + 240, (int)card.Y + 196, 122, 78, Palette.C(112, 74, 58));
        Raylib.DrawRectangle((int)card.X + 408, (int)card.Y + 182, 180, 92, Palette.C(138, 84, 64));
        Raylib.DrawCircle((int)card.X + 178, (int)card.Y + 248, 16, Palette.C(80, 170, 255));
        Raylib.DrawCircle((int)card.X + 498, (int)card.Y + 238, 24, Palette.C(220, 92, 82));
        Raylib.DrawLine((int)card.X + 54, (int)card.Y + 246, (int)card.X + 560, (int)card.Y + 246, Palette.C(210, 190, 160));

        Raylib.DrawText("Baselands", 378, 184, 34, Color.White);
        Raylib.DrawText("Open wasteland, scattered cities, hostile outposts and one central destroyer.", 378, 432, 20, Color.LightGray);
        Raylib.DrawText("Extraction portals open after 2:00 and remain active for 5:00.", 378, 458, 20, Palette.C(126, 210, 255));

        DrawButton(new Rectangle(340, 520, 280, 58), "Deploy");
        DrawButton(new Rectangle(70, 620, 220, 52), "Back");
    }

    private void DrawStorage()
    {
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), Palette.C(8, 12, 20));
        DrawTitle("Storage", 48, 56);
        Raylib.DrawText("Equip items here before deployment. Extracted loot returns to this stash.", 70, 106, 24, Color.LightGray);
        Raylib.DrawText($"Capacity {GetStoredItemCount()}/{MetaProfile.StorageCapacity}", 70, 138, 22, Color.White);

        Raylib.DrawRectangle(52, 170, 300, 380, Palette.C(10, 18, 30, 220));
        Raylib.DrawRectangleLinesEx(new Rectangle(52, 170, 300, 380), 2f, Palette.C(108, 170, 228));
        Raylib.DrawText("Loadout", 72, 184, 24, Color.White);
        Raylib.DrawText("Armor", 72, 236, 18, Color.LightGray);
        Raylib.DrawText("Ranged", 72, 304, 18, Color.LightGray);
        Raylib.DrawText("Melee", 72, 372, 18, Color.LightGray);
        Raylib.DrawText("Consumables", 72, 440, 18, Color.LightGray);

        Raylib.DrawRectangle(392, 116, 510, 530, Palette.C(10, 18, 30, 220));
        Raylib.DrawRectangleLinesEx(new Rectangle(392, 116, 510, 530), 2f, Palette.C(108, 170, 228));
        Raylib.DrawText("Stash", 414, 132, 24, Color.White);
        DrawStorageGrid(new Vector2(414, 174), 10, 10);

        DrawButton(new Rectangle(70, 620, 220, 52), "Back");

        var slots = BuildStorageSlots();
        foreach (var slot in slots)
        {
            Raylib.DrawRectangleRec(slot.Rect, Palette.C(22, 28, 42, 255));
            Raylib.DrawRectangleLinesEx(slot.Rect, 1f, slot.Kind == SlotKind.Trash ? Color.Orange : Color.SkyBlue);
            if (slot.Kind == SlotKind.Trash) Raylib.DrawText("TR", (int)slot.Rect.X + 14, (int)slot.Rect.Y + 14, 18, Color.Orange);
            if (slot.Kind == SlotKind.QuickSlotQ) Raylib.DrawText("Q", (int)slot.Rect.X + 15, (int)slot.Rect.Y - 18, 16, Color.Green);
            if (slot.Kind == SlotKind.QuickSlotR) Raylib.DrawText("R", (int)slot.Rect.X + 15, (int)slot.Rect.Y - 18, 16, Color.Yellow);
            if (slot.Item is not null) DrawItemIcon(slot.Item, new Rectangle(slot.Rect.X + 6, slot.Rect.Y + 6, slot.Rect.Width - 12, slot.Rect.Height - 12));
        }

        if (_drag is not null)
        {
            var m = Raylib.GetMousePosition();
            DrawItemIcon(_drag.Item, new Rectangle(m.X + 8, m.Y + 8, 32, 32));
        }

        if (_hovered is not null) DrawTooltip(_hovered, Raylib.GetMousePosition());
    }

    private void DrawCharacter()
    {
        DrawTitle("Character", 56, 60);
        Raylib.DrawText("Common landing stats", 74, 126, 28, Color.LightGray);

        var panel = new Rectangle(70, 170, 520, 320);
        Raylib.DrawRectangleRec(panel, Palette.C(10, 18, 30, 220));
        Raylib.DrawRectangleLinesEx(panel, 2f, Palette.C(108, 170, 228));
        Raylib.DrawText($"General level: {_meta.Level}", 96, 208, 28, Color.Gold);
        Raylib.DrawText($"Next level: {_meta.Score}/{GetMetaScoreRequired(_meta.Level)}", 96, 250, 24, Color.White);
        Raylib.DrawText($"Common HP bonus: +{GetCommonHealthBonus():0}", 96, 310, 24, Palette.C(140, 220, 160));
        Raylib.DrawText($"Common damage bonus: +{GetCommonDamageBonus():0}", 96, 350, 24, Palette.C(255, 210, 120));
        Raylib.DrawText("Temporary run stat points do not carry over after extraction or death.", 96, 406, 22, Color.LightGray);

        DrawButton(new Rectangle(70, 620, 220, 52), "Back");
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
        DrawButton(CenterRect(0, 320, 320, 62), "Resume");
        DrawButton(CenterRect(0, 400, 320, 62), "Abandon run");
    }

    private void DrawDeath()
    {
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), Palette.C(0, 0, 0, 180));
        DrawTitle(_deathHeader, 150, 68);
        Raylib.DrawText(_deathBody, (Raylib.GetScreenWidth() - Raylib.MeasureText(_deathBody, 24)) / 2, 250, 24, Color.LightGray);
        DrawButton(CenterRect(0, 320, 320, 62), "Deploy again");
        DrawButton(CenterRect(0, 400, 320, 62), "Main menu");
    }

    private void DrawNotice()
    {
        if (string.IsNullOrWhiteSpace(_noticeText)) return;

        var width = Math.Max(360, Raylib.MeasureText(_noticeText, 20) + 36);
        var rect = new Rectangle(Raylib.GetScreenWidth() - width - 30, 26, width, 46);
        Raylib.DrawRectangleRec(rect, Palette.C(12, 22, 36, 220));
        Raylib.DrawRectangleLinesEx(rect, 2f, Palette.C(110, 185, 240));
        Raylib.DrawText(_noticeText, (int)rect.X + 18, (int)rect.Y + 12, 20, Color.White);
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

    private static void DrawStorageGrid(Vector2 origin, int cols, int rows)
    {
        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                var rect = new Rectangle(origin.X + c * 48, origin.Y + r * 46, 42, 42);
                Raylib.DrawRectangleLinesEx(rect, 1f, Palette.C(70, 90, 130, 170));
            }
        }
    }

    private void DrawExtractionHud()
    {
        var timerText = _extractPortals.Count == 0
            ? $"Portals in {FormatTime(_portalUnlockTimer)}"
            : $"Portals active {FormatTime(_portalActiveTimer)}";

        var color = _extractPortals.Count == 0 ? Color.LightGray : Palette.C(110, 215, 255);
        Raylib.DrawText(timerText, 20, 138, 22, color);
        Raylib.DrawText($"Map {_selectedMapName}", 20, 168, 20, Palette.C(165, 195, 220));
    }

    private Rectangle MainMenuButtonRect(int index)
        => new(70, Raylib.GetScreenHeight() - 344 + index * 60, 220, 48);

    private static Rectangle CenterRect(int offsetX, int y, int w, int h) => new((Raylib.GetScreenWidth() - w) / 2f + offsetX, y, w, h);
    private static bool Clicked(Rectangle rect) => Raylib.IsMouseButtonPressed(MouseButton.Left) && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), rect);

    private float GetCommonHealthBonus() => _meta.Level * 10f;

    private float GetCommonDamageBonus() => _meta.Level * 2f;

    private static int GetMetaScoreRequired(int level)
    {
        if (level <= 1) return 3000;
        if (level == 2) return 5000;
        return 5000 + (level - 2) * 1500;
    }

    private void AddRunScore(int amount) => _runScore += amount;

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
        if (_extractPortals.Count == 0)
        {
            _portalUnlockTimer -= dt;
            if (_portalUnlockTimer <= 0f)
            {
                SpawnExtractionPortals();
                _portalActiveTimer = PortalLifetime;
                ShowNotice("Portals are open.");
            }

            return;
        }

        _portalActiveTimer -= dt;
        if (_portalActiveTimer <= 0f)
        {
            FailRun("Extraction failed", "The portals collapsed and all carried items were lost.");
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
        var first = RandomOutdoorPoint(24f);
        var second = first;
        for (var i = 0; i < 120; i++)
        {
            second = RandomOutdoorPoint(24f);
            if (Vector2.Distance(first, second) >= 2200f) break;
        }

        _extractPortals.Add(new ExtractPortal(first, _rng.NextSingle() * MathF.Tau));
        _extractPortals.Add(new ExtractPortal(second, _rng.NextSingle() * MathF.Tau));
    }

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
        SavePersistentState();
        ClearUiInteraction();
        _extractPortals.Clear();
        _state = GameState.Storage;
        ShowNotice(lostForCapacity > 0
            ? $"Extracted: {stored} items stored, {lostForCapacity} lost. Score +{_runScore}."
            : $"Extracted successfully. Score +{_runScore}.");
    }

    private IEnumerable<ItemStack> CollectExtractedItems()
    {
        if (_player.Armor is not null) yield return _player.Armor;
        if (_player.RangedWeapon is not null) yield return _player.RangedWeapon;
        if (_player.MeleeWeapon is not null) yield return _player.MeleeWeapon;
        if (_player.Inventory.QuickSlotQ is not null) yield return _player.Inventory.QuickSlotQ;
        if (_player.Inventory.QuickSlotR is not null) yield return _player.Inventory.QuickSlotR;

        foreach (var item in _player.Inventory.BackpackSlots)
        {
            if (item is not null) yield return item;
        }
    }

    private void FailRun(string header, string body)
    {
        _extractPortals.Clear();
        ClearUiInteraction();
        _deathHeader = header;
        _deathBody = body;
        _state = GameState.Death;
    }

    private void ClearUiInteraction()
    {
        _drag = null;
        _hovered = null;
        _openedChestIndex = null;
        _pendingUpgrade = false;
    }

    private void ShowNotice(string text)
    {
        _noticeText = text;
        _noticeTimer = 5f;
    }

    private static string FormatTime(float timeLeft)
    {
        var total = Math.Max(0, (int)MathF.Ceiling(timeLeft));
        return $"{total / 60:00}:{total % 60:00}";
    }

    private void DrawZoneArrows()
    {
        DrawScreenZoneArrow(_buildings, Palette.C(80, 170, 255), "B");
        DrawScreenZoneArrow(_outposts, Palette.C(245, 90, 90), "O");
        if (_destroyerBoss is not null && _destroyerBoss.Alive)
        {
            DrawScreenPointArrow(_destroyerBoss.Position, Palette.C(230, 45, 45), "D");
        }
        foreach (var portal in _extractPortals)
        {
            DrawScreenPointArrow(portal.Position, Palette.C(90, 210, 255), "P");
        }
    }

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
        var center = new Vector2(Raylib.GetScreenWidth() / 2f, Raylib.GetScreenHeight() / 2f);
        var tip = center + dir * 82f;
        var normal = new Vector2(-dir.Y, dir.X);
        var backCenter = center + dir * 54f;

        Raylib.DrawTriangle(tip, backCenter + normal * 11f, backCenter - normal * 11f, color);
        Raylib.DrawText(marker, (int)backCenter.X - 5, (int)backCenter.Y - 8, 16, Color.White);
    }

    private void DrawStatTooltip()
    {
        var mouse = Raylib.GetMousePosition();
        var hints = new (Rectangle Rect, string Header, string Body)[]
        {
            (new Rectangle(54, 176, 180, 24), "STR", "Увеличивает урон ближнего боя и максимум здоровья на 5."),
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

    private int GetStoredItemCount() => _meta.StorageSlots.Count(item => item is not null);

    private ItemStack? TakeMetaLoadoutItem(SlotKind kind)
    {
        var item = GetMetaLoadoutItem(kind);
        SetMetaLoadoutItem(kind, null);
        return item;
    }

    private static bool IsMetaLoadoutSlot(SlotKind kind)
        => kind is SlotKind.Armor or SlotKind.RangedWeapon or SlotKind.MeleeWeapon or SlotKind.QuickSlotQ or SlotKind.QuickSlotR;

    private static bool CanPlaceIntoSlot(SlotKind kind, ItemStack item)
        => kind switch
        {
            SlotKind.Armor => item.Type == ItemType.Armor,
            SlotKind.RangedWeapon => item.Type == ItemType.Weapon && item.WeaponKind == WeaponClass.Ranged,
            SlotKind.MeleeWeapon => item.Type == ItemType.Weapon && item.WeaponKind == WeaponClass.Melee,
            SlotKind.QuickSlotQ => item.Type == ItemType.Consumable,
            SlotKind.QuickSlotR => item.Type == ItemType.Consumable,
            _ => false
        };

    private ItemStack? GetMetaLoadoutItem(SlotKind kind) => kind switch
    {
        SlotKind.Armor => _meta.Armor,
        SlotKind.RangedWeapon => _meta.RangedWeapon,
        SlotKind.MeleeWeapon => _meta.MeleeWeapon,
        SlotKind.QuickSlotQ => _meta.QuickSlotQ,
        SlotKind.QuickSlotR => _meta.QuickSlotR,
        _ => null
    };

    private void SetMetaLoadoutItem(SlotKind kind, ItemStack? item)
    {
        if (kind == SlotKind.Armor) _meta.Armor = item;
        if (kind == SlotKind.RangedWeapon) _meta.RangedWeapon = item;
        if (kind == SlotKind.MeleeWeapon) _meta.MeleeWeapon = item;
        if (kind == SlotKind.QuickSlotQ) _meta.QuickSlotQ = item;
        if (kind == SlotKind.QuickSlotR) _meta.QuickSlotR = item;
    }

    private SlotKind? GetPreferredLoadoutSlot(ItemStack item)
    {
        if (item.Type == ItemType.Armor) return SlotKind.Armor;
        if (item.Type == ItemType.Weapon && item.WeaponKind == WeaponClass.Ranged) return SlotKind.RangedWeapon;
        if (item.Type == ItemType.Weapon && item.WeaponKind == WeaponClass.Melee) return SlotKind.MeleeWeapon;
        if (item.Type == ItemType.Consumable) return _meta.QuickSlotQ is null ? SlotKind.QuickSlotQ : SlotKind.QuickSlotR;
        return null;
    }

    private string BuildWeaponDamageText(ItemStack? weapon, WeaponClass kind)
    {
        if (weapon is null) return string.Empty;

        var total = _player.GetWeaponDamage(weapon);
        if (weapon.Pattern == WeaponPattern.GrenadeLauncher) return $"blast {total:0} / direct {total * 1.6f:0}";

        var bonus = kind == WeaponClass.Ranged
            ? _player.GetRangedStatBonus()
            : _player.GetMeleeStatBonus();

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
                for (var l = 0; l < lootCount; l++) loot.Add(RollLoot(itemsHighTier, zone.IsOutpost));
                list.Add(new LootChest(pos, loot));
            }
        }

        return list;
    }

    private ItemStack RollLoot(bool highTier, bool isOutpost)
    {
        var r = _rng.NextSingle();
        if (r < 0.35f) return ItemStack.Consumable(_rng.NextSingle() < 0.5f ? ConsumableType.Medkit : ConsumableType.Stim);
        if (r < 0.70f) return ItemStack.Armor(RollRarity(highTier, isOutpost), _rng);
        return ItemStack.Weapon(_rng.NextSingle() < 0.5f ? WeaponClass.Ranged : WeaponClass.Melee, RollRarity(highTier, isOutpost), _rng);
    }

    private ArmorRarity RollRarity(bool highTier, bool isOutpost)
    {
        var r = _rng.NextSingle();
        if (!highTier)
        {
            if (r < 0.55f) return ArmorRarity.Common;
            if (r < 0.84f) return ArmorRarity.Rare;
            if (r < 0.98f) return ArmorRarity.Epic;
            return ArmorRarity.Legendary;
        }

        if (!isOutpost)
        {
            if (r < 0.24f) return ArmorRarity.Rare;
            if (r < 0.84f) return ArmorRarity.Epic;
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


    private Vector2 RandomMapPointSafe(float radius)
    {
        for (var i = 0; i < 200; i++)
        {
            var point = new Vector2(_rng.Next(50, World - 50), _rng.Next(50, World - 50));
            if (MovementUtils.CircleHitsObstacle(point, radius, _obstacles)) continue;
            return point;
        }

        return new Vector2(World / 2f, World / 2f);
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


    private List<TurretEnemy> GenerateTurrets()
    {
        var list = new List<TurretEnemy>();
        foreach (var outpost in _outposts)
        {
            var count = _rng.Next(1, 3);
            for (var i = 0; i < count; i++)
            {
                list.Add(new TurretEnemy(RandomPointInZoneSafe(outpost.Rect, 18f), _rng.NextSingle() * MathF.Tau));
            }
        }

        return list;
    }

    private Vector2 GeneratePlayerSpawnPoint()
    {
        for (var i = 0; i < 200; i++)
        {
            var point = RandomOutdoorPoint(16f);
            if (Vector2.Distance(point, new Vector2(World / 2f, World / 2f)) >= CenterNoZoneRadius + 250f)
            {
                return point;
            }
        }

        return new Vector2(World / 2f, CenterNoZoneRadius + 250f);
    }

    private List<MiniBossEnemySquare> GenerateMiniBosses()
    {
        var list = new List<MiniBossEnemySquare>();
        foreach (var o in _outposts)
        {
            var center = new Vector2(o.Rect.X + o.Rect.Width / 2f, o.Rect.Y + o.Rect.Height / 2f);
            list.Add(new MiniBossEnemySquare(center));
        }

        return list;
    }

    private BossEnemyDestroyer GenerateDestroyerBoss()
        => new(new Vector2(World / 2f, World / 2f));


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
            var data = JsonSerializer.Deserialize<PersistentStateData>(json);
            if (data is null)
            {
                SavePersistentState();
                return;
            }

            _themeIndex = Math.Clamp(data.ThemeIndex, 0, Math.Max(0, _themes.Count - 1));
            _selectedMapName = string.IsNullOrWhiteSpace(data.SelectedMapName) ? "Baselands" : data.SelectedMapName;
            ApplyMetaSaveData(data.Meta);
        }
        catch
        {
            _themeIndex = 0;
            _selectedMapName = "Baselands";
            ApplyMetaSaveData(null);
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
                SelectedMapName = _selectedMapName,
                Meta = BuildMetaSaveData()
            };

            var json = JsonSerializer.Serialize(data, SaveJsonOptions);
            File.WriteAllText(SaveFilePath, json);
        }
        catch
        {
            // Saving failure should not break the session.
        }
    }

    private MetaProfileSaveData BuildMetaSaveData()
    {
        return new MetaProfileSaveData
        {
            Level = _meta.Level,
            Score = _meta.Score,
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

public sealed class Projectile(Vector2 pos, Vector2 dir, float speed, float life, Color color, bool ownerEnemy, float damage, ProjectileKind kind = ProjectileKind.Bullet, float explosionRadius = 0f, float explosionDamage = 0f, float drawRadius = 4f)
{
    public Vector2 Position { get; private set; } = pos;
    public Vector2 PreviousPosition { get; private set; } = pos;
    public Color Color { get; } = color;
    public bool OwnerEnemy { get; } = ownerEnemy;
    public float Damage { get; } = damage;
    public ProjectileKind Kind { get; } = kind;
    public float ExplosionRadius { get; } = explosionRadius;
    public float ExplosionDamage { get; } = explosionDamage;
    public float DrawRadius { get; } = drawRadius;
    private float _life = life;
    public bool Alive => _life > 0f;

    public void Update(float dt)
    {
        PreviousPosition = Position;
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

public sealed class MetaProfile
{
    public const int StorageCapacity = 100;

    public int Level { get; set; } = 1;
    public int Score { get; set; }

    public List<ItemStack?> StorageSlots { get; } = Enumerable.Repeat<ItemStack?>(null, StorageCapacity).ToList();
    public ItemStack? Armor { get; set; }
    public ItemStack? RangedWeapon { get; set; }
    public ItemStack? MeleeWeapon { get; set; }
    public ItemStack? QuickSlotQ { get; set; }
    public ItemStack? QuickSlotR { get; set; }
    public ItemStack? Trash { get; set; }

    public bool AddToStorage(ItemStack item)
    {
        for (var i = 0; i < StorageSlots.Count; i++)
        {
            if (StorageSlots[i] is not null) continue;
            StorageSlots[i] = item;
            return true;
        }

        return false;
    }

    public bool HasFreeStorageSlot() => StorageSlots.Any(item => item is null);
}

public sealed class PersistentStateData
{
    public int ThemeIndex { get; set; }
    public string SelectedMapName { get; set; } = "Baselands";
    public MetaProfileSaveData Meta { get; set; } = new();
}

public sealed class MetaProfileSaveData
{
    public int Level { get; set; } = 1;
    public int Score { get; set; }
    public List<ItemStackSaveData?> StorageSlots { get; set; } = [];
    public ItemStackSaveData? Armor { get; set; }
    public ItemStackSaveData? RangedWeapon { get; set; }
    public ItemStackSaveData? MeleeWeapon { get; set; }
    public ItemStackSaveData? QuickSlotQ { get; set; }
    public ItemStackSaveData? QuickSlotR { get; set; }
    public ItemStackSaveData? Trash { get; set; }
}

public sealed class ItemStackSaveData
{
    public ItemType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ArmorRarity Rarity { get; set; }
    public byte ColorR { get; set; }
    public byte ColorG { get; set; }
    public byte ColorB { get; set; }
    public byte ColorA { get; set; } = 255;
    public WeaponClass? WeaponKind { get; set; }
    public WeaponPattern Pattern { get; set; }
    public ConsumableType? ConsumableKind { get; set; }
    public bool IsStarter { get; set; }
    public float Defense { get; set; }
    public float PowerBonus { get; set; }
}

public sealed class ExtractPortal(Vector2 position, float seed)
{
    public Vector2 Position { get; } = position;
    public float Seed { get; } = seed;
    public float InteractionRadius { get; } = 34f;

    public void Draw(float time)
    {
        Raylib.DrawEllipse((int)Position.X, (int)Position.Y, 28f, 42f, Palette.C(60, 150, 255, 110));
        Raylib.DrawEllipseLines((int)Position.X, (int)Position.Y, 30f, 44f, Palette.C(120, 220, 255));

        for (var i = 0; i < 4; i++)
        {
            var speed = 0.6f + i * 0.32f;
            var angle = Seed + time * speed + i * MathF.PI * 0.5f;
            var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (8f + i * 3f);
            var size = 8f - i;
            Raylib.DrawPoly(Position + offset, 4, size, time * 100f * speed, Palette.C(150 - i * 12, 220 - i * 10, 255));
        }
    }
}

public sealed class Inventory
{
    public const int BackpackCapacity = 30;

    public List<ItemStack?> BackpackSlots { get; } = Enumerable.Repeat<ItemStack?>(null, BackpackCapacity).ToList();
    public ItemStack? QuickSlotQ { get; set; }
    public ItemStack? QuickSlotR { get; set; }

    public ItemStack? Trash { get; set; }

    public bool AddToBackpack(ItemStack item)
    {
        if (TryPlaceIntoConsumableSlot(item)) return true;

        for (var i = 0; i < BackpackSlots.Count; i++)
        {
            if (BackpackSlots[i] is not null) continue;
            BackpackSlots[i] = item;
            return true;
        }

        return false;
    }

    public void AutoFillConsumableSlots()
    {
        if (QuickSlotQ is null) QuickSlotQ = TakeFirstConsumableFromBackpack();
        if (QuickSlotR is null) QuickSlotR = TakeFirstConsumableFromBackpack();
    }

    private bool TryPlaceIntoConsumableSlot(ItemStack item)
    {
        if (item.Type != ItemType.Consumable) return false;

        if (QuickSlotQ is null)
        {
            QuickSlotQ = item;
            return true;
        }

        if (QuickSlotR is null)
        {
            QuickSlotR = item;
            return true;
        }

        return false;
    }

    private ItemStack? TakeFirstConsumableFromBackpack()
    {
        for (var i = 0; i < BackpackSlots.Count; i++)
        {
            var item = BackpackSlots[i];
            if (item?.Type != ItemType.Consumable) continue;
            BackpackSlots[i] = null;
            return item;
        }

        return null;
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
    public bool IsStarter { get; }

    public float Defense { get; }
    public float PowerBonus { get; }

    private ItemStack(ItemType type, string name, string description, ArmorRarity rarity, Color color, WeaponClass? weaponClass, WeaponPattern pattern, ConsumableType? consumableType, float defense, float powerBonus, bool isStarter)
    {
        Type = type;
        Name = name;
        Description = description;
        Rarity = rarity;
        Color = color;
        WeaponKind = weaponClass;
        Pattern = pattern;
        ConsumableKind = consumableType;
        IsStarter = isStarter;
        Defense = defense;
        PowerBonus = powerBonus;
    }

    public static ItemStackSaveData? ToSaveData(ItemStack? item)
    {
        if (item is null) return null;

        return new ItemStackSaveData
        {
            Type = item.Type,
            Name = item.Name,
            Description = item.Description,
            Rarity = item.Rarity,
            ColorR = item.Color.R,
            ColorG = item.Color.G,
            ColorB = item.Color.B,
            ColorA = item.Color.A,
            WeaponKind = item.WeaponKind,
            Pattern = item.Pattern,
            ConsumableKind = item.ConsumableKind,
            IsStarter = item.IsStarter,
            Defense = item.Defense,
            PowerBonus = item.PowerBonus
        };
    }

    public static ItemStack? FromSaveData(ItemStackSaveData? data)
    {
        if (data is null) return null;

        return new ItemStack(
            data.Type,
            data.Name,
            data.Description,
            data.Rarity,
            new Color(data.ColorR, data.ColorG, data.ColorB, data.ColorA),
            data.WeaponKind,
            data.Pattern,
            data.ConsumableKind,
            data.Defense,
            data.PowerBonus,
            data.IsStarter);
    }

    public static ItemStack Armor(ArmorRarity rarity, Random rng)
    {
        var baseDef = rarity switch
        {
            ArmorRarity.Common => 10f,
            ArmorRarity.Rare => 14f,
            ArmorRarity.Epic => 19f,
            ArmorRarity.Legendary => 25f,
            _ => 33f
        };

        var name = rarity switch
        {
            ArmorRarity.Common => "Scrap Vest",
            ArmorRarity.Rare => "Titan Weave",
            ArmorRarity.Epic => "Aegis Fiber",
            ArmorRarity.Legendary => "Nova Bulwark",
            _ => "Crimson Bastion"
        };

        return new ItemStack(ItemType.Armor, name, "Armor. Drag into armor slot.", rarity, Palette.Rarity(rarity), null, WeaponPattern.Standard, null, baseDef + rng.NextSingle() * 4f, 0f, false);
    }

    public static ItemStack Weapon(WeaponClass kind, ArmorRarity rarity, Random rng)
    {
        var p = rarity switch
        {
            ArmorRarity.Common => 0f,
            ArmorRarity.Rare => 3f,
            ArmorRarity.Epic => 6f,
            ArmorRarity.Legendary => 10f,
            _ => 16f
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

        return new ItemStack(ItemType.Weapon, name, description, rarity, Palette.Rarity(rarity), kind, pattern, null, 0f, p, false);
    }

    public static ItemStack StartingPistol()
    {
        return new ItemStack(ItemType.Weapon, "Rail Pistol", "Base sidearm.", ArmorRarity.Common, Palette.Rarity(ArmorRarity.Common), WeaponClass.Ranged, WeaponPattern.Standard, null, 0f, 1.5f, true);
    }

    public static ItemStack StartingMelee()
    {
        return new ItemStack(ItemType.Weapon, "Plasma Blade", "Base melee weapon.", ArmorRarity.Common, Palette.Rarity(ArmorRarity.Common), WeaponClass.Melee, WeaponPattern.Standard, null, 0f, 1.2f, true);
    }

    public static ItemStack BossGrenadeLauncher()
    {
        return new ItemStack(
            ItemType.Weapon,
            "Destroyer Grenade Launcher",
            "Boss weapon. Explosive shell deals 250 blast damage and +60% on direct hit.",
            ArmorRarity.Red,
            Palette.Rarity(ArmorRarity.Red),
            WeaponClass.Ranged,
            WeaponPattern.GrenadeLauncher,
            null,
            0f,
            0f,
            false);
    }

    public static ItemStack Consumable(ConsumableType t)
    {
        return t == ConsumableType.Medkit
            ? new ItemStack(ItemType.Consumable, "Medkit", "Restore HP. Hotkey Q/R.", ArmorRarity.Common, Palette.C(130, 210, 120), null, WeaponPattern.Standard, t, 0f, 0f, false)
            : new ItemStack(ItemType.Consumable, "Stim", "Move speed boost. Hotkey Q/R.", ArmorRarity.Common, Palette.C(220, 220, 120), null, WeaponPattern.Standard, t, 0f, 0f, false);
    }
}

public enum SlotKind
{
    RangedWeapon,
    MeleeWeapon,
    Armor,
    Trash,
    Storage,
    Backpack,
    QuickSlotQ,
    QuickSlotR,
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

