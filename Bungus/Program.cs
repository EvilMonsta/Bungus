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

public sealed partial class SciFiRogueGame : IDisposable
{
    private const int W = 1280;
    private const int H = 720;
    private const int World = 6000;
    private const float MinZoneGap = 300f;
    private const float CenterNoZoneRadius = 850f;
    private const float PortalUnlockDelay = 120f;
    private const float PortalLifetime = 300f;
    private const int ProtectedSaveVersion = 2;
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
    private List<GroundConsumablePickup> _groundConsumables = [];

    private DragPayload? _drag;
    private ItemStack? _hovered;
    private SlotKind _lastClickKind;
    private int _lastClickIndex = -1;
    private double _lastClickTime;
    private int _pendingStrengthPoints;
    private int _pendingDexterityPoints;
    private int _pendingSpeedPoints;
    private int _pendingGunsmithPoints;
    private int? _openedChestIndex;
    private bool _requestExit;
    private readonly List<VisualTheme> _themes;
    private int _themeIndex;
    private DisplayMode _displayMode;
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
        _groundConsumables = [];
        _player = Player.Create(
            GeneratePlayerSpawnPoint(),
            GetCommonHealthBonus(),
            GetCommonDamageBonus(),
            _meta.BaseStrength,
            _meta.BaseDexterity,
            _meta.BaseSpeed,
            _meta.BaseGuns,
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
        ClearPendingLevelUpPoints();

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
        if (Clicked(CenterRect(0, 156, 360, 56))) SetDisplayMode(DisplayMode.Windowed);
        if (Clicked(CenterRect(0, 220, 360, 56))) SetDisplayMode(DisplayMode.Fullscreen);

        for (var i = 0; i < _themes.Count; i++)
        {
            if (Clicked(CenterRect(0, 330 + i * 56, 360, 48)))
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
            if (!_player.InventoryOpen)
            {
                _openedChestIndex = null;
                ClearPendingLevelUpPoints();
            }
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
        UpdateGroundConsumables();
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
                TryDropEnemyConsumable(e.Position);
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
                TryDropEnemyConsumable(h.Position);
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
                TryDropEnemyConsumable(turret.Position);
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
                TryDropEnemyConsumable(b.Position);
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
            TryDropEnemyConsumable(_destroyerBoss.Position);
            _player.RegisterKill();
            _player.RegisterKill();
            _player.RegisterKill();
            _player.RegisterKill();
            _player.RegisterKill();
            AddRunScore(1000);
            _chests.Add(new LootChest(_destroyerBoss.Position, RollBossLoot()));
        }
    }

    private void UpdateGroundConsumables()
    {
        for (var i = _groundConsumables.Count - 1; i >= 0; i--)
        {
            var pickup = _groundConsumables[i];
            if (Vector2.Distance(pickup.Position, _player.Position) > 26f) continue;
            if (!Raylib.IsKeyPressed(KeyboardKey.F)) continue;
            if (!TryPickGroundItem(pickup.Item)) continue;

            _groundConsumables.RemoveAt(i);
            break;
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
            s.UpdateAnchor(_player.Position);
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
                if (!hit || !s.TryRegisterHit(e)) continue;
                e.Damage(_player.GetMeleeDamage());
                e.ForceAggro(_player.Position);
                e.JustHitByPlayer = true;
            }

            foreach (var h in _hexEnemies.Where(x => x.Alive))
            {
                var hit = s.IsLine
                    ? DistanceToSegment(h.Position, s.LineStart, s.LineEnd) < 16f
                    : IsInArc(h.Position, s, 10f);
                if (hit && s.TryRegisterHit(h)) h.Damage(_player.GetMeleeDamage());
            }

            foreach (var t in _turrets.Where(x => x.Alive))
            {
                var hit = s.IsLine
                    ? DistanceToSegment(t.Position, s.LineStart, s.LineEnd) < 20f
                    : IsInArc(t.Position, s, 14f);
                if (hit && s.TryRegisterHit(t)) t.Damage(_player.GetMeleeDamage());
            }

            foreach (var b in _miniBosses.Where(x => x.Alive))
            {
                var hit = s.IsLine
                    ? DistanceToSegment(b.Position, s.LineStart, s.LineEnd) < 28f
                    : IsInArc(b.Position, s, 24f);
                if (hit && s.TryRegisterHit(b)) b.Damage(_player.GetMeleeDamage() * 0.75f);
            }

            if (_destroyerBoss is not null && _destroyerBoss.Alive)
            {
                var hit = s.IsLine
                    ? DistanceToSegment(_destroyerBoss.Position, s.LineStart, s.LineEnd) < 54f
                    : IsInArc(_destroyerBoss.Position, s, 50f);
                if (hit && s.TryRegisterHit(_destroyerBoss)) _destroyerBoss.Damage(_player.GetMeleeDamage() * 0.75f);
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
            _dashAfterImages[i].Life -= dt * 3.75f;
            if (_dashAfterImages[i].Life <= 0f) _dashAfterImages.RemoveAt(i);
        }
    }

    private void UpdateChests()
    {
        for (var i = 0; i < _chests.Count; i++)
        {
            var chest = _chests[i];
            if (chest.Items.Count == 0)
            {
                if (_openedChestIndex == i)
                {
                    _openedChestIndex = null;
                    _player.InventoryOpen = false;
                }

                continue;
            }

            if (Vector2.Distance(chest.Position, _player.Position) > 28f) continue;
            if (!Raylib.IsKeyPressed(KeyboardKey.F)) continue;

            if (chest.RequiresClear && chest.ZoneId is int zoneId && !IsZoneCleared(zoneId))
            {
                ShowNotice("Clear all enemies in this zone first.");
                continue;
            }

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

        if (Raylib.IsMouseButtonPressed(MouseButton.Right))
        {
            var from = slots.FirstOrDefault(s => Raylib.CheckCollisionPointRec(m, s.Rect));
            if (from is not null && TryMoveInventorySlotToTrash(from))
            {
                _drag = null;
                return;
            }
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

    private bool TryMoveInventorySlotToTrash(UiSlot slot)
    {
        if (slot.Item is null) return false;
        if (slot.Kind is SlotKind.Trash or SlotKind.Chest) return false;

        _player.Inventory.Trash = slot.Item;
        RemoveFromSource(new DragPayload(slot.Kind, slot.Index, slot.Item));
        return true;
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

        if (Raylib.IsMouseButtonPressed(MouseButton.Right))
        {
            var from = slots.FirstOrDefault(s => Raylib.CheckCollisionPointRec(mouse, s.Rect));
            if (from is not null && TryMoveStorageSlotToTrash(from))
            {
                _drag = null;
                return;
            }
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

    private bool TryMoveStorageSlotToTrash(UiSlot slot)
    {
        if (slot.Item is null) return false;
        if (slot.Kind == SlotKind.Trash) return false;
        if (slot.Kind != SlotKind.Storage && !IsMetaLoadoutSlot(slot.Kind)) return false;

        _meta.Trash = slot.Item;
        ReplaceStorageSourceWith(new DragPayload(slot.Kind, slot.Index, slot.Item), null);
        SavePersistentState();
        return true;
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
            new UiSlot(new Rectangle(1088, 252, 58, 58), SlotKind.Trash, -1, _meta.Trash, -1)
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
        if (!_player.InventoryOpen) return;

        if (Clicked(new Rectangle(252, 174, 22, 22))) QueuePendingLevelUpPoint(StatType.Strength);
        if (Clicked(new Rectangle(252, 204, 22, 22))) QueuePendingLevelUpPoint(StatType.Dexterity);
        if (Clicked(new Rectangle(252, 234, 22, 22))) QueuePendingLevelUpPoint(StatType.Speed);
        if (Clicked(new Rectangle(252, 264, 22, 22))) QueuePendingLevelUpPoint(StatType.Gunsmith);

        if (GetPendingLevelUpPointCount() > 0 && Clicked(new Rectangle(54, 326, 120, 30)))
        {
            ApplyPendingLevelUpPoints();
        }

        if (GetPendingLevelUpPointCount() > 0 && Clicked(new Rectangle(184, 326, 120, 30)))
        {
            ClearPendingLevelUpPoints();
        }
    }

    private void QueuePendingLevelUpPoint(StatType stat)
    {
        if (_player.StatPoints - GetPendingLevelUpPointCount() <= 0) return;

        if (stat == StatType.Strength) _pendingStrengthPoints++;
        if (stat == StatType.Dexterity) _pendingDexterityPoints++;
        if (stat == StatType.Speed) _pendingSpeedPoints++;
        if (stat == StatType.Gunsmith) _pendingGunsmithPoints++;
    }

    private int GetPendingLevelUpPointCount()
        => _pendingStrengthPoints + _pendingDexterityPoints + _pendingSpeedPoints + _pendingGunsmithPoints;

    private void ApplyPendingLevelUpPoints()
    {
        ApplyPendingStat(StatType.Strength, _pendingStrengthPoints);
        ApplyPendingStat(StatType.Dexterity, _pendingDexterityPoints);
        ApplyPendingStat(StatType.Speed, _pendingSpeedPoints);
        ApplyPendingStat(StatType.Gunsmith, _pendingGunsmithPoints);
        ClearPendingLevelUpPoints();
    }

    private void ApplyPendingStat(StatType stat, int count)
    {
        for (var i = 0; i < count; i++) _player.ApplyPoint(stat);
    }

    private void ClearPendingLevelUpPoints()
    {
        _pendingStrengthPoints = 0;
        _pendingDexterityPoints = 0;
        _pendingSpeedPoints = 0;
        _pendingGunsmithPoints = 0;
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

    private float GetCommonHealthBonus() => _meta.Level * 3f;

    private float GetCommonDamageBonus() => MathF.Floor(_meta.Level / 2f);

    private static int GetMetaScoreRequired(int level)
    {
        if (level <= 1) return 3000;
        if (level == 2) return 5000;
        return 5000 + (level - 2) * 1500;
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
        var fullscreen = Raylib.IsWindowFullscreen();
        if (_displayMode == DisplayMode.Fullscreen)
        {
            if (fullscreen) return;

            var monitor = Raylib.GetCurrentMonitor();
            Raylib.SetWindowSize(Raylib.GetMonitorWidth(monitor), Raylib.GetMonitorHeight(monitor));
            Raylib.ToggleFullscreen();
            return;
        }

        if (!fullscreen)
        {
            Raylib.SetWindowSize(W, H);
            CenterWindow();
            return;
        }

        Raylib.ToggleFullscreen();
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
        ClearPendingLevelUpPoints();
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
            (new Rectangle(54, 176, 220, 24), "STR", "+5 HP и +0.25% урона ближнего оружия за каждое очко."),
            (new Rectangle(54, 206, 220, 24), "DEX", "+1% урона ближнего оружия за каждое очко."),
            (new Rectangle(54, 236, 220, 24), "SPD", "+3% к множителю скорости за каждое очко."),
            (new Rectangle(54, 266, 220, 24), "GUN", "+1% урона дальнего оружия за каждое очко.")
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

    private Player CreateLandingPreviewPlayer()
        => Player.Create(
            Vector2.Zero,
            GetCommonHealthBonus(),
            GetCommonDamageBonus(),
            _meta.BaseStrength,
            _meta.BaseDexterity,
            _meta.BaseSpeed,
            _meta.BaseGuns,
            _meta.RangedWeapon,
            _meta.MeleeWeapon,
            _meta.Armor,
            _meta.QuickSlotQ,
            _meta.QuickSlotR);

    private static string BuildWeaponDamageText(Player player, ItemStack? weapon, WeaponClass kind)
    {
        if (weapon is null) return string.Empty;

        var total = player.GetWeaponDamage(weapon);
        var roundedTotal = MathF.Round(total, MidpointRounding.AwayFromZero);
        if (weapon.Pattern == WeaponPattern.GrenadeLauncher) return $"blast {roundedTotal:0} / direct {roundedTotal + 200f:0}";
        if (weapon.Pattern == WeaponPattern.PulseRifle)
        {
            var perShot = player.GetPulseShotDamage(weapon);
            var shots = player.GetPulseBurstShotCount(weapon);
            var roundedPerShot = MathF.Round(perShot, MidpointRounding.AwayFromZero);
            var roundedBurst = MathF.Round(perShot * shots, MidpointRounding.AwayFromZero);
            return $"burst {roundedPerShot:0}x{shots}={roundedBurst:0}";
        }

        if (kind == WeaponClass.Melee)
        {
            var hitDamage = player.GetMeleeHitDamage(weapon);
            var roundedHit = MathF.Round(hitDamage, MidpointRounding.AwayFromZero);
            return weapon.Pattern == WeaponPattern.EnergySpear
                ? $"thrust {roundedHit:0}"
                : $"slash {roundedHit:0}";
        }

        var bonus = player.GetWeaponModifierDamage(weapon);
        var roundedBonus = MathF.Round(bonus, MidpointRounding.AwayFromZero);
        return $"dmg {roundedTotal:0}(+{roundedBonus:0})";
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
            all.Add(new LootZone(all.Count, rect, outpost));
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
            for (var i = 0; i < chestCount; i++)
            {
                var pos = RandomPointInZoneSafe(zone.Rect, 20f);
                var lootCount = _rng.Next(1, 6);
                var loot = new List<ItemStack>();
                for (var l = 0; l < lootCount; l++) loot.Add(RollLoot(zone.IsOutpost));
                list.Add(new LootChest(pos, loot, zone.Id, LootContainerKind.Chest));
            }

            var crateSpawnChance = zone.IsOutpost ? 0.60f : 0.40f;
            var crateCount = 0;
            if (_rng.NextSingle() < crateSpawnChance)
            {
                var cratePos = RandomPointInZoneSafe(zone.Rect, 20f);
                list.Add(new LootChest(cratePos, RollCrateLoot(zone.IsOutpost), zone.Id, LootContainerKind.Crate));
                crateCount++;
            }

            if (crateCount > 0 && _rng.NextSingle() < 0.10f)
            {
                var cratePos = RandomPointInZoneSafe(zone.Rect, 20f);
                list.Add(new LootChest(cratePos, RollCrateLoot(zone.IsOutpost), zone.Id, LootContainerKind.Crate));
            }
        }

        return list;
    }

    private ItemStack RollLoot(bool isOutpost)
    {
        var r = _rng.NextSingle();
        if (r < 0.35f) return ItemStack.Consumable(RollConsumableType());

        var rarity = RollRarity(isOutpost);
        return RollEquipmentOfRarity(rarity);
    }

    private ArmorRarity RollRarity(bool isOutpost)
    {
        var r = _rng.NextSingle();

        if (_selectedMapName.Equals("Baselands", StringComparison.OrdinalIgnoreCase))
        {
            if (isOutpost)
            {
                if (r < 0.40f) return ArmorRarity.Common;
                if (r < 0.9923077f) return ArmorRarity.Rare;
                return ArmorRarity.Epic;
            }

            if (r < 0.90f) return ArmorRarity.Common;
            return ArmorRarity.Rare;
        }

        if (!isOutpost)
        {
            if (r < 0.55f) return ArmorRarity.Common;
            if (r < 0.84f) return ArmorRarity.Rare;
            if (r < 0.98f) return ArmorRarity.Epic;
            return ArmorRarity.Legendary;
        }

        if (r < 0.20f) return ArmorRarity.Rare;
        if (r < 0.75f) return ArmorRarity.Epic;
        return ArmorRarity.Legendary;
    }

    private ItemStack RollEquipmentOfRarity(ArmorRarity rarity)
    {
        if (_rng.NextSingle() < 0.35f) return ItemStack.Armor(rarity, _rng);
        return ItemStack.Weapon(_rng.NextSingle() < 0.5f ? WeaponClass.Ranged : WeaponClass.Melee, rarity, _rng);
    }

    private ConsumableType RollConsumableType()
        => _rng.NextSingle() < 0.5f ? ConsumableType.Medkit : ConsumableType.Stim;

    private List<ItemStack> RollBossLoot()
    {
        var loot = new List<ItemStack> { RollEquipmentOfRarity(ArmorRarity.Epic) };
        if (_rng.NextSingle() < 0.05f) loot.Add(RollEquipmentOfRarity(ArmorRarity.Legendary));
        if (_rng.NextSingle() < 0.02f) loot.Add(ItemStack.BossGrenadeLauncher());
        return loot;
    }

    private List<ItemStack> RollCrateLoot(bool isOutpost)
    {
        var loot = new List<ItemStack>();

        if (isOutpost)
        {
            var r = _rng.NextSingle();
            if (r < 0.01f) loot.Add(RollEquipmentOfRarity(ArmorRarity.Rare));
            else if (r < 0.76f) loot.Add(RollEquipmentOfRarity(ArmorRarity.Common));

            loot.Add(ItemStack.Consumable(RollConsumableType()));
            loot.Add(ItemStack.Consumable(RollConsumableType()));
            return loot;
        }

        if (_rng.NextSingle() < 0.20f) loot.Add(RollEquipmentOfRarity(ArmorRarity.Common));
        loot.Add(ItemStack.Consumable(RollConsumableType()));
        if (_rng.NextSingle() < 0.20f) loot.Add(ItemStack.Consumable(RollConsumableType()));
        return loot;
    }

    private bool IsZoneCleared(int zoneId)
    {
        if (_enemies.Any(enemy => enemy.Alive && enemy.ZoneId == zoneId)) return false;
        if (_turrets.Any(turret => turret.Alive && turret.ZoneId == zoneId)) return false;
        if (_miniBosses.Any(boss => boss.Alive && boss.ZoneId == zoneId)) return false;
        return true;
    }

    private void TryDropEnemyConsumable(Vector2 position)
    {
        if (_rng.NextSingle() >= 0.01f) return;
        _groundConsumables.Add(new GroundConsumablePickup(position, ItemStack.Consumable(RollConsumableType())));
    }

    private bool TryPickGroundItem(ItemStack item)
    {
        if (_player.Inventory.HasFreeBackpackSlot()) return _player.Inventory.AddToBackpack(item);

        if (item.Type == ItemType.Consumable && _player.Inventory.TryReceiveGroundConsumableWhenBackpackFull(item))
        {
            return true;
        }

        return false;
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

        var step = Math.Max(18f, radius);
        for (var y = zoneRect.Y + radius; y <= zoneRect.Y + zoneRect.Height - radius; y += step)
        {
            for (var x = zoneRect.X + radius; x <= zoneRect.X + zoneRect.Width - radius; x += step)
            {
                var point = new Vector2(x, y);
                if (!MovementUtils.CircleHitsObstacle(point, radius, _obstacles)) return point;
            }
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
                list.Add(Enemy.CreatePatrol(patrolA, patrolB, false, b.Id));
            }

            var strongCount = _rng.Next(1, 3);
            for (var i = 0; i < strongCount; i++)
            {
                list.Add(Enemy.CreateStrong(RandomPointInZoneSafe(b.Rect, 14f), b.Id));
            }
        }

        foreach (var o in _outposts)
        {
            var count = _rng.Next(5, 8);
            for (var i = 0; i < count; i++)
            {
                var patrolA = RandomPointInZoneSafe(o.Rect, 14f);
                var patrolB = RandomPointInZoneSafe(o.Rect, 14f);
                list.Add(Enemy.CreatePatrol(patrolA, patrolB, true, o.Id));
            }
            var strong = _rng.Next(3, 5);
            for (var i = 0; i < strong; i++) list.Add(Enemy.CreateStrong(RandomPointInZoneSafe(o.Rect, 14f), o.Id));
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
                list.Add(new TurretEnemy(RandomPointInZoneSafe(outpost.Rect, 18f), _rng.NextSingle() * MathF.Tau, outpost.Id));
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
            list.Add(new MiniBossEnemySquare(RandomPointInZoneSafe(o.Rect, 28f), o.Id));
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

}

