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
    private const float MinZoneGap = 300f;
    private const float CenterNoZoneRadius = 850f;
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
    private List<GeneratorGuardianEnemy> _generatorGuards = [];
    private List<ToxicTriangleEnemy> _toxicEnemies = [];
    private StationBossEnemy? _stationBoss;
    private readonly List<StationBossEnemy> _pitStationBosses = [];
    private List<Projectile> _projectiles = [];
    private List<Explosion> _explosions = [];
    private List<SwingArc> _swings = [];
    private List<DashAfterImage> _dashAfterImages = [];
    private List<MotionAfterImage> _motionAfterImages = [];

    private List<LootZone> _buildings = [];
    private List<LootZone> _outposts = [];
    private List<LootZone> _generatorZones = [];
    private List<LootZone> _hangars = [];
    private LootZone? _stationZone;
    private List<Obstacle> _obstacles = [];
    private List<LootChest> _chests = [];
    private List<GroundConsumablePickup> _groundConsumables = [];
    private List<ProtectiveDome> _protectiveDomes = [];
    private List<GeneratorNode> _generators = [];
    private List<ToxicPool> _toxicPools = [];

    private DragPayload? _drag;
    private ItemStack? _hovered;
    private SlotKind _lastClickKind;
    private int _lastClickIndex = -1;
    private double _lastClickTime;
    private SlotKind _inventoryUseHoldKind;
    private int _inventoryUseHoldIndex = -1;
    private float _inventoryUseHoldTimer;
    private int _pendingStrengthPoints;
    private int _pendingDexterityPoints;
    private int _pendingSpeedPoints;
    private int _pendingGunsmithPoints;
    private int? _openedChestIndex;
    private bool _mapOpen;
    private Vector2? _mapMarker;
    private bool _requestExit;
    private readonly List<VisualTheme> _themes;
    private int _themeIndex;
    private DisplayMode _displayMode;
    private float _nextHexSpawnTimer;
    private readonly MetaProfile _meta = new();
    private readonly List<ExtractPortal> _extractPortals = [];
    private string _selectedMapName = "Baselands";
    private MapDefinition _currentMap = MapDefinition.Baselands;
    private int _worldSize = MapDefinition.Baselands.WorldSize;
    private DeploymentListMode _deploymentListMode = DeploymentListMode.Expeditions;
    private bool _challengeMode;
    private int _pitNextWave = 1;
    private float _pitWaveTimer;
    private readonly Dictionary<object, int> _pitEnemyWaves = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<int> _pitCompletedWaves = [];
    private readonly List<ItemStack> _pitRewardOffers = [];
    private readonly List<List<ItemStack>> _pitRouletteItems = [];
    private readonly bool[] _pitRewardClaimed = [false, false, false];
    private bool _pitRewardOpen;
    private float _pitRewardSpinElapsed;
    private readonly float[] _pitConsumableSpawnTimers = [0f, 0f];
    private readonly GroundConsumablePickup?[] _pitConsumablePickups = [null, null];
    private Vector2[] _pitConsumableSpawnPoints = [];
    private int _pitRunXpEarned;
    private int _pitRunCoinsEarned;
    private Rectangle? _stationEntranceDoor;
    private Rectangle? _stationBossDoor;
    private Rectangle? _stationBossArena;
    private bool _stationEntranceOpen;
    private bool _stationBossFightStarted;
    private bool _stationBossDoorSealed;
    private float _stationBossDoorSealTimer = -1f;
    private int _runScore;
    private float _portalUnlockTimer;
    private float _portalActiveTimer;
    private float _lastChanceTimer;
    private bool _lastChanceActive;
    private bool _lastChancePortalNotified;
    private string _noticeText = string.Empty;
    private float _noticeTimer;
    private string _deathHeader = "You Died";
    private string _deathBody = "All carried items were lost.";
    private readonly Dictionary<string, int> _promoCodeUses = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _sessionActiveCodes = new(StringComparer.OrdinalIgnoreCase);
    private bool _isFunnyNextRun;
    private bool _codesPopupOpen;
    private string _codeInput = string.Empty;
    private string _codeStatusText = string.Empty;
    private bool _codeStatusSuccess;

    private static readonly Rectangle TakeAllButtonRect = new(740, 266, 220, 34);
    private static readonly float[] PitRewardSpinDurations = [3f, 4f, 5f];
    private const float InventoryConsumableUseHoldDuration = 1f;
    private sealed record MapDefinition(
        string Name,
        int Difficulty,
        int WorldSize,
        int BuildingMin,
        int BuildingMaxExclusive,
        int OutpostMin,
        int OutpostMaxExclusive,
        float PortalUnlockDelay,
        float PortalLifetime,
        float LastChanceLifetime,
        int PortalCount,
        int LastChancePortalCount,
        bool IsDeadZone)
    {
        public static readonly MapDefinition Baselands = new("Baselands", 1, 6000, 14, 21, 7, 11, 120f, 330f, 60f, 2, 1, false);
        public static readonly MapDefinition DeadZone = new("Dead Zone", 2, 12000, 10, 16, 8, 16, 180f, 720f, 60f, 3, 2, true);
        public static readonly MapDefinition[] All = [Baselands, DeadZone];
    }

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
        _challengeMode = false;
        _pitRewardOpen = false;
        _pitRewardOffers.Clear();
        _pitRouletteItems.Clear();
        _pitEnemyWaves.Clear();
        _pitCompletedWaves.Clear();
        _pitRunXpEarned = 0;
        _pitRunCoinsEarned = 0;
        _currentMap = MapDefinition.All.FirstOrDefault(m => m.Name.Equals(mapName, StringComparison.OrdinalIgnoreCase)) ?? MapDefinition.Baselands;
        _selectedMapName = _currentMap.Name;
        _worldSize = _currentMap.WorldSize;
        (_buildings, _outposts) = GenerateZones(_rng.Next(_currentMap.BuildingMin, _currentMap.BuildingMaxExclusive), _rng.Next(_currentMap.OutpostMin, _currentMap.OutpostMaxExclusive));
        GenerateSpecialZones();
        _obstacles = GenerateObstacles();
        _chests = GenerateChestsInZones();
        _groundConsumables = [];
        _protectiveDomes = [];
        _generators = [];
        _toxicPools = [];
        _stationEntranceDoor = null;
        _stationBossDoor = null;
        _stationBossArena = null;
        _stationEntranceOpen = false;
        _stationBossFightStarted = false;
        _stationBossDoorSealed = false;
        _stationBossDoorSealTimer = -1f;
        GenerateDeadZoneSetPieces();
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
        ApplyIsFunnyNextRunBonus();
        _projectiles = [];
        _explosions = [];
        _swings = [];
        _enemies = GenerateEnemies();
        _hexEnemies = [];
        _turrets = GenerateTurrets();
        _miniBosses = GenerateMiniBosses();
        _destroyerBoss = _currentMap.IsDeadZone ? null : GenerateDestroyerBoss();
        _generatorGuards = GenerateGeneratorGuards();
        _toxicEnemies = GenerateToxicEnemies();
        _stationBoss = GenerateStationBoss();
        _pitStationBosses.Clear();
        _nextHexSpawnTimer = NextHexSpawnDelay();
        _extractPortals.Clear();
        _runScore = 0;
        _portalUnlockTimer = _currentMap.PortalUnlockDelay;
        _portalActiveTimer = _currentMap.PortalLifetime;
        _lastChanceTimer = 0f;
        _lastChanceActive = false;
        _lastChancePortalNotified = false;
        _player.InventoryOpen = false;
        _openedChestIndex = null;
        _mapOpen = false;
        _mapMarker = null;
        _drag = null;
        _hovered = null;
        ClearPendingLevelUpPoints();
        LoadMetaRunBackpackIntoPlayer();

        _camera.Offset = new Vector2(Raylib.GetScreenWidth() / 2f, Raylib.GetScreenHeight() / 2f);
        _camera.Target = _player.Position;
        SavePersistentState();
    }

    private void StartPitChallenge()
    {
        _challengeMode = true;
        _currentMap = MapDefinition.Baselands;
        _selectedMapName = "Pit";
        _worldSize = 2000;
        _buildings = [];
        _outposts = [];
        _generatorZones = [];
        _hangars = [];
        _stationZone = null;
        _obstacles = [];
        _chests = [];
        _groundConsumables = [];
        _protectiveDomes = [];
        _generators = [];
        _toxicPools = [];
        _stationEntranceDoor = null;
        _stationBossDoor = null;
        _stationBossArena = null;
        _stationEntranceOpen = false;
        _stationBossFightStarted = false;
        _stationBossDoorSealed = false;
        _stationBossDoorSealTimer = -1f;
        _player = Player.Create(
            new Vector2(_worldSize / 2f, _worldSize / 2f),
            GetCommonHealthBonus(),
            GetCommonDamageBonus(),
            _meta.BaseStrength,
            _meta.BaseDexterity,
            _meta.BaseSpeed,
            _meta.BaseGuns,
            ItemStack.StartingPistol(),
            ItemStack.StartingMelee(),
            ItemStack.StartingArmor(),
            null,
            null);
        ApplyIsFunnyNextRunBonus();
        _projectiles = [];
        _explosions = [];
        _swings = [];
        _dashAfterImages = [];
        _motionAfterImages = [];
        _enemies = [];
        _hexEnemies = [];
        _turrets = [];
        _miniBosses = [];
        _destroyerBoss = null;
        _generatorGuards = [];
        _toxicEnemies = [];
        _stationBoss = null;
        _pitStationBosses.Clear();
        _extractPortals.Clear();
        _runScore = 0;
        _lastChanceActive = false;
        _lastChanceTimer = 0f;
        _pitNextWave = 1;
        _pitWaveTimer = 0f;
        _pitEnemyWaves.Clear();
        _pitCompletedWaves.Clear();
        _pitRewardOffers.Clear();
        _pitRewardOpen = false;
        _pitRouletteItems.Clear();
        _pitRewardSpinElapsed = 0f;
        _pitRunXpEarned = 0;
        _pitRunCoinsEarned = 0;
        ResetPitConsumableSpawns();
        _player.InventoryOpen = false;
        _openedChestIndex = null;
        _mapOpen = false;
        _mapMarker = null;
        _drag = null;
        _hovered = null;
        ClearPendingLevelUpPoints();
        SpawnPitWave();

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
            case GameState.Armory: UpdateArmory(); break;
            case GameState.Character: UpdateCharacter(); break;
            case GameState.Settings: UpdateSettings(); break;
            case GameState.Playing: UpdatePlaying(dt); break;
            case GameState.Paused: UpdatePause(); break;
            case GameState.Death: UpdateDeath(); break;
        }

        UpdateCursorVisibility();

        if (_noticeTimer > 0f)
        {
            _noticeTimer -= dt;
            if (_noticeTimer <= 0f) _noticeText = string.Empty;
        }
    }

    private void UpdateCursorVisibility()
    {
        if (_state == GameState.Playing && !_player.InventoryOpen && !_mapOpen && !_pitRewardOpen)
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
        if (_codesPopupOpen)
        {
            UpdateCodesPopup();
            return;
        }

        if (Clicked(MainMenuButtonRect(0))) { ClearUiInteraction(); _state = GameState.MapSelect; }
        if (Clicked(MainMenuButtonRect(1))) { ClearUiInteraction(); _state = GameState.Storage; }
        if (Clicked(MainMenuButtonRect(2))) { ClearUiInteraction(); _state = GameState.Armory; }
        if (Clicked(MainMenuButtonRect(3))) { ClearUiInteraction(); _state = GameState.Character; }
        if (Clicked(MainMenuButtonRect(4))) { ClearUiInteraction(); _state = GameState.Settings; }
        if (Clicked(MainMenuCodesButtonRect())) OpenCodesPopup();
        if (Clicked(MainMenuButtonRect(5))) _requestExit = true;
    }

    private void UpdateMapSelect()
    {
        if (Clicked(DeploymentToggleRect()))
        {
            _deploymentListMode = _deploymentListMode == DeploymentListMode.Expeditions
                ? DeploymentListMode.Challenges
                : DeploymentListMode.Expeditions;
            ClearUiInteraction();
            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Clicked(new Rectangle(70, 620, 220, 52)))
        {
            ClearUiInteraction();
            _state = GameState.MainMenu;
            return;
        }

        if (_deploymentListMode == DeploymentListMode.Challenges)
        {
            if (Clicked(MapCardRect(0)))
            {
                ClearUiInteraction();
                StartPitChallenge();
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
        if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Clicked(new Rectangle(70, 620, 220, 52)))
        {
            ClearUiInteraction();
            _state = GameState.MainMenu;
            return;
        }

        UpdateStorageUi();
    }

    private void UpdateArmory()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Clicked(new Rectangle(70, 620, 220, 52)))
        {
            ClearUiInteraction();
            _state = GameState.MainMenu;
            return;
        }

        UpdateArmoryUi();
    }

    private void UpdateArmoryUi()
    {
        _hovered = null;
        var mouse = Raylib.GetMousePosition();

        for (var i = 0; i < _meta.ArmoryOffers.Count; i++)
        {
            var offer = _meta.ArmoryOffers[i];
            var rect = ArmoryOfferRect(i);
            if (!Raylib.CheckCollisionPointRec(mouse, rect)) continue;

            _hovered = offer.Item;
            if (Raylib.IsMouseButtonPressed(MouseButton.Left)) TryBuyArmoryOffer(i);
            return;
        }
    }

    private void TryBuyArmoryOffer(int index)
    {
        if (index < 0 || index >= _meta.ArmoryOffers.Count) return;

        var offer = _meta.ArmoryOffers[index];
        if (offer.Purchased)
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

        if (!_meta.HasFreeStorageSlot())
        {
            ShowNotice("Storage is full.");
            return;
        }

        _meta.SynthCoins -= price;
        offer.Purchased = true;
        _meta.AddToStorage(offer.Item);
        SavePersistentState();
        ShowNotice($"Bought {offer.Item.Name} for {price} SynthCoins.");
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
        if (Clicked(CenterRect(0, 226, 360, 56))) SetDisplayMode(DisplayMode.Windowed);
        if (Clicked(CenterRect(0, 290, 360, 56))) SetDisplayMode(DisplayMode.Fullscreen);

        for (var i = 0; i < _themes.Count; i++)
        {
            if (Clicked(CenterRect(0, 400 + i * 56, 390, 48)))
            {
                _themeIndex = i;
                SavePersistentState();
            }
        }

        if (Clicked(CenterRect(0, 720, 280, 56)) || Raylib.IsKeyPressed(KeyboardKey.Escape)) _state = GameState.MainMenu;
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
            if (_challengeMode) StartPitChallenge();
            else StartRun(_selectedMapName);
            _state = GameState.Playing;
        }
        if (Clicked(CenterRect(0, 400, 320, 62))) { ClearUiInteraction(); _state = GameState.MainMenu; }
    }

    private void UpdatePlaying(float dt)
    {
        if (_challengeMode && _pitRewardOpen)
        {
            UpdatePitRewardSelection(dt);
            UpdateCursorVisibility();
            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.M))
        {
            _mapOpen = !_mapOpen;
            _drag = null;
            ResetInventoryUseHold();
            return;
        }

        if (_mapOpen)
        {
            UpdateMapWindow();
            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            if (_player.InventoryOpen) CloseRunInventory();
            else _state = GameState.Paused;
            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Tab))
        {
            _player.InventoryOpen = !_player.InventoryOpen;
            if (!_player.InventoryOpen) CloseRunInventory();
            else
            {
                ResetInventoryUseHold();
            }
        }

        if (_challengeMode && _player.InventoryOpen)
        {
            UpdateInventoryUi();
            UpdateLevelUi();
            if (_drag is null) _player.Inventory.AutoFillConsumableSlots();
            return;
        }

        var enemyCollisionObstacles = BuildEnemyCollisionObstacles();
        var playerPreviousPosition = _player.Position;
        _player.Update(dt, _obstacles, _worldSize, _dashAfterImages);
        AddMotionTrail(playerPreviousPosition, _player.Position, Theme.Player, 15f, MotionTrailShape.Circle, 0.18f, 13f);
        _player.UpdateCombat(dt, _projectiles);
        if (Raylib.IsKeyPressed(KeyboardKey.Q)) HandleConsumedQuickSlot(_player.UseQuickSlotQ());
        if (Raylib.IsKeyPressed(KeyboardKey.R)) HandleConsumedQuickSlot(_player.UseQuickSlotR());
        if (Raylib.IsKeyPressed(KeyboardKey.E)) _player.SwitchActiveWeapon();

        var mouseWorld = Raylib.GetScreenToWorld2D(Raylib.GetMousePosition(), _camera);
        if (Raylib.IsMouseButtonDown(MouseButton.Left) && !_player.InventoryOpen)
        {
            _player.Attack(mouseWorld, _projectiles, _swings, _obstacles, _worldSize, _dashAfterImages);
        }

        UpdateEnemies(dt, enemyCollisionObstacles);
        UpdateHexEnemies(dt, enemyCollisionObstacles);
        UpdateTurrets(dt, enemyCollisionObstacles);
        UpdateMiniBosses(dt, enemyCollisionObstacles);
        UpdateDestroyerBoss(dt, enemyCollisionObstacles);
        UpdateDeadZoneEnemies(dt, enemyCollisionObstacles);
        UpdateDeadZoneHazards(dt);
        UpdateDeadZoneProgress(dt);
        if (_challengeMode) UpdatePitChallenge(dt);
        UpdateProjectiles(dt);
        UpdateSwings(dt);
        UpdateEffects(dt);
        UpdateProtectiveDomes(dt);
        UpdateChests();
        UpdateGroundConsumables();
        UpdateInventoryUi();
        UpdateLevelUi();
        if (_drag is null) _player.Inventory.AutoFillConsumableSlots();
        if (!_challengeMode) UpdateExtraction(dt);
        if (_state != GameState.Playing) return;

        var desiredCameraTarget = GetDesiredCameraTarget(mouseWorld);
        _camera.Target = Vector2.Lerp(_camera.Target, desiredCameraTarget, _player.IsSniperEquipped ? 0.035f : 0.2f);
        if (_player.Health <= 0) FailRun("You Died", "All carried items were lost.");
    }

    private void CloseRunInventory()
    {
        _player.InventoryOpen = false;
        _openedChestIndex = null;
        ClearPendingLevelUpPoints();
        ResetInventoryUseHold();
    }

    private Vector2 GetDesiredCameraTarget(Vector2 mouseWorld)
    {
        if (!_player.IsSniperEquipped || _player.InventoryOpen) return _player.Position;

        var toCursor = mouseWorld - _player.Position;
        if (toCursor.LengthSquared() <= 0.001f) return _player.Position;

        var dir = Vector2.Normalize(toCursor);
        var desiredOffset = toCursor * 0.5f;
        var maxOffset = GetMaxSniperCameraOffset(dir);
        if (desiredOffset.Length() > maxOffset) desiredOffset = dir * maxOffset;
        return _player.Position + desiredOffset;
    }

    private float GetMaxSniperCameraOffset(Vector2 dir)
    {
        var halfWidth = Raylib.GetScreenWidth() * 0.5f;
        var halfHeight = Raylib.GetScreenHeight() * 0.5f;
        var xLimit = MathF.Abs(dir.X) < 0.001f ? float.PositiveInfinity : halfWidth / MathF.Abs(dir.X);
        var yLimit = MathF.Abs(dir.Y) < 0.001f ? float.PositiveInfinity : halfHeight / MathF.Abs(dir.Y);
        var distanceFromCenterToEdge = MathF.Min(xLimit, yLimit);
        return distanceFromCenterToEdge * 0.5f / MathF.Max(_camera.Zoom, 0.001f);
    }

    private void UpdateMapWindow()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            _mapOpen = false;
            return;
        }

        var mapRect = GetMapRect();
        var mouse = Raylib.GetMousePosition();
        if (!Raylib.CheckCollisionPointRec(mouse, mapRect)) return;

        if (Raylib.IsMouseButtonPressed(MouseButton.Right) && _mapMarker is Vector2 marker)
        {
            var markerScreen = WorldToMap(marker, mapRect);
            if (Vector2.Distance(markerScreen, mouse) <= 22f) _mapMarker = null;
            return;
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            _mapMarker = MapToWorld(mouse, mapRect);
        }
    }

    private float NextHexSpawnDelay()
        => _lastChanceActive
            ? 5f + _rng.NextSingle() * 10f
            : 80f + _rng.NextSingle() * 160f;

    private void UpdateEnemies(float dt, List<Obstacle> enemyCollisionObstacles)
    {
        foreach (var e in _enemies)
        {
            var previousPosition = e.Position;
            e.UpdateVisionSweep(dt);
            if (_challengeMode) e.ForceAggro(_player.Position);
            else e.UpdateAwareness(_player.Position, dt, enemyCollisionObstacles);
            e.UpdateMovement(dt, _player.Position, enemyCollisionObstacles, _worldSize);
            AddMotionTrail(previousPosition, e.Position, e.IsStrong ? Theme.EnemyStrong : Theme.Enemy, e.IsStrong ? 11f : 12f, e.IsStrong ? MotionTrailShape.Triangle : MotionTrailShape.Circle);
            e.TryShootBurst(_player.Position, _projectiles);

            if (e.TryMeleeHit(_player) && _rng.NextSingle() <= _player.GetStatusEffectChance(0.05f))
            {
                _player.ApplyBleed(3f);
            }

            if (!e.Alive && !e.KillAwarded)
            {
                e.KillAwarded = true;
                if (_challengeMode)
                {
                    HandlePitEnemyDeath(e);
                    continue;
                }
                TryDropEnemyConsumable(e.Position);
                _player.RegisterKill(e.IsStrong ? 2 : 1);
                AddRunScore(e.IsStrong ? 20 : 10);
            }
        }

        if (_challengeMode) return;

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

    private void UpdateHexEnemies(float dt, List<Obstacle> enemyCollisionObstacles)
    {
        foreach (var h in _hexEnemies)
        {
            var previousPosition = h.Position;
            h.Update(dt, _player.Position, _projectiles, enemyCollisionObstacles, _worldSize);
            AddMotionTrail(previousPosition, h.Position, Palette.C(255, 110, 180), 17f, MotionTrailShape.Hex, 0.08f);
            if (!h.Alive && !h.KillAwarded)
            {
                h.KillAwarded = true;
                if (_challengeMode)
                {
                    HandlePitEnemyDeath(h);
                    continue;
                }
                TryDropEnemyConsumable(h.Position);
                _player.RegisterKill(2);
                AddRunScore(25);
            }
        }
    }

    private void UpdateTurrets(float dt, List<Obstacle> enemyCollisionObstacles)
    {
        foreach (var turret in _turrets)
        {
            var previousPosition = turret.Position;
            turret.Update(dt, _player.Position, _projectiles, enemyCollisionObstacles, _challengeMode);
            AddMotionTrail(previousPosition, turret.Position, Palette.C(230, 80, 80), 15f, MotionTrailShape.Square, rotateWithMovement: false);
            if (!turret.Alive && !turret.KillAwarded)
            {
                turret.KillAwarded = true;
                if (_challengeMode)
                {
                    HandlePitEnemyDeath(turret);
                    continue;
                }
                TryDropEnemyConsumable(turret.Position);
                _player.RegisterKill(2);
                AddRunScore(20);
            }
        }
    }

    private void UpdateMiniBosses(float dt, List<Obstacle> enemyCollisionObstacles)
    {
        foreach (var b in _miniBosses)
        {
            var previousPosition = b.Position;
            b.Update(dt, _player.Position, _projectiles, _player, enemyCollisionObstacles, _worldSize, _dashAfterImages, _challengeMode);
            AddMotionTrail(previousPosition, b.Position, Palette.C(230, 100, 100), 21f, MotionTrailShape.Square, 0.2f, rotateWithMovement: false);
            if (!b.Alive && !b.KillAwarded)
            {
                b.KillAwarded = true;
                if (_challengeMode)
                {
                    HandlePitEnemyDeath(b);
                    continue;
                }
                TryDropEnemyConsumable(b.Position);
                _chests.Add(new LootChest(b.Position, RollMiniBossLoot()));
                _player.RegisterKill(5);
                AddRunScore(100);
            }
        }
    }

    private void UpdateDestroyerBoss(float dt, List<Obstacle> enemyCollisionObstacles)
    {
        if (_destroyerBoss is null) return;

        var previousPosition = _destroyerBoss.Position;
        _destroyerBoss.Update(dt, _player.Position, _projectiles, _player, enemyCollisionObstacles, _worldSize, _dashAfterImages);
        AddMotionTrail(previousPosition, _destroyerBoss.Position, Theme.Boss, 24f, MotionTrailShape.Square, 0.2f, rotateWithMovement: false);
        if (!_destroyerBoss.Alive && !_destroyerBoss.KillAwarded)
        {
            _destroyerBoss.KillAwarded = true;
            if (_challengeMode)
            {
                HandlePitEnemyDeath(_destroyerBoss);
                return;
            }
            TryDropEnemyConsumable(_destroyerBoss.Position);
            _player.RegisterKill(25);
            AddRunScore(1000);
            _chests.Add(new LootChest(_destroyerBoss.Position, RollBossLoot()));
        }
    }

    private void UpdateDeadZoneEnemies(float dt, List<Obstacle> enemyCollisionObstacles)
    {
        foreach (var guard in _generatorGuards)
        {
            var previousPosition = guard.Position;
            guard.Update(dt, _player.Position, _player, enemyCollisionObstacles, _worldSize, _dashAfterImages, _challengeMode);
            AddMotionTrail(previousPosition, guard.Position, Palette.C(255, 170, 95), 17f, MotionTrailShape.Triangle, 0.2f);
            if (!guard.Alive && !guard.KillAwarded)
            {
                guard.KillAwarded = true;
                if (_challengeMode)
                {
                    HandlePitEnemyDeath(guard);
                    continue;
                }
                var generator = _generators.FirstOrDefault(g => g.ZoneId == guard.ZoneId);
                if (generator is not null) generator.GuardDefeated = true;
                TryDropStationKey(guard.Position);
                TryDropEnemyConsumable(guard.Position);
                _player.RegisterKill(4);
                AddRunScore(80);
            }
        }

        foreach (var toxic in _toxicEnemies)
        {
            var previousPosition = toxic.Position;
            toxic.Update(dt, _player.Position, _projectiles, enemyCollisionObstacles, _worldSize, _challengeMode);
            AddMotionTrail(previousPosition, toxic.Position, Palette.C(220, 110, 100), 12f, MotionTrailShape.Triangle);
            if (!toxic.Alive && !toxic.KillAwarded)
            {
                toxic.KillAwarded = true;
                if (_challengeMode)
                {
                    HandlePitEnemyDeath(toxic);
                    continue;
                }
                TryDropEnemyConsumable(toxic.Position);
                _player.RegisterKill(2);
                AddRunScore(25);
            }
        }

        if (_stationBoss is not null)
        {
            var previousPosition = _stationBoss.Position;
            _stationBoss.Update(dt, _player.Position, _projectiles, _player, enemyCollisionObstacles, _worldSize);
            AddMotionTrail(previousPosition, _stationBoss.Position, Palette.C(255, 40, 40), 30f, MotionTrailShape.Circle, 0.2f);
            if (!_stationBoss.Alive && !_stationBoss.KillAwarded)
            {
                _stationBoss.KillAwarded = true;
                if (_challengeMode)
                {
                    HandlePitEnemyDeath(_stationBoss);
                    return;
                }
                _player.RegisterKill(25);
                AddRunScore(1300);
                _chests.Add(new LootChest(_stationBoss.Position, RollStationBossLoot()));
                OpenStationBossDoor();
            }
        }

        foreach (var boss in _pitStationBosses)
        {
            var previousPosition = boss.Position;
            boss.Update(dt, _player.Position, _projectiles, _player, enemyCollisionObstacles, _worldSize);
            AddMotionTrail(previousPosition, boss.Position, Palette.C(255, 40, 40), 30f, MotionTrailShape.Circle, 0.2f);
            if (!boss.Alive && !boss.KillAwarded)
            {
                boss.KillAwarded = true;
                HandlePitEnemyDeath(boss);
            }
        }
    }

    private void UpdateDeadZoneHazards(float dt)
    {
        if (!_currentMap.IsDeadZone) return;

        if (_toxicPools.Any(pool => pool.Contains(_player.Position)))
        {
            _player.ApplyPoison(5f);
        }
    }

    private void UpdatePitChallenge(float dt)
    {
        _pitWaveTimer -= dt;
        if (_pitWaveTimer <= 0f) SpawnPitWave();

        for (var i = 0; i < _pitConsumableSpawnPoints.Length; i++)
        {
            if (_pitConsumablePickups[i] is not null) continue;
            _pitConsumableSpawnTimers[i] -= dt;
            if (_pitConsumableSpawnTimers[i] <= 0f) SpawnPitConsumable(i);
        }
    }

    private void ResetPitConsumableSpawns()
    {
        var center = _worldSize / 2f;
        _pitConsumableSpawnPoints =
        [
            new Vector2(center - 250f, center),
            new Vector2(center + 250f, center)
        ];

        for (var i = 0; i < _pitConsumableSpawnTimers.Length; i++)
        {
            _pitConsumableSpawnTimers[i] = 30f;
            _pitConsumablePickups[i] = null;
        }
    }

    private void SpawnPitConsumable(int index)
    {
        if (index < 0 || index >= _pitConsumableSpawnPoints.Length) return;
        var pickup = new GroundConsumablePickup(_pitConsumableSpawnPoints[index], RandomPitConsumable());
        _pitConsumablePickups[index] = pickup;
        _groundConsumables.Add(pickup);
    }

    private void SpawnPitWave()
    {
        var wave = _pitNextWave++;
        _pitWaveTimer = wave % 10 == 0 ? 60f : 30f;

        void AddEnemy(object enemy)
        {
            _pitEnemyWaves[enemy] = wave;
            switch (enemy)
            {
                case Enemy e: _enemies.Add(e); break;
                case HexEnemy h: _hexEnemies.Add(h); break;
                case MiniBossEnemySquare b: _miniBosses.Add(b); break;
                case GeneratorGuardianEnemy g: _generatorGuards.Add(g); break;
                case ToxicTriangleEnemy t: _toxicEnemies.Add(t); break;
                case StationBossEnemy s:
                    s.Activate();
                    _pitStationBosses.Add(s);
                    break;
            }
        }

        void AddCircles(int count, bool enhanced = false)
        {
            for (var i = 0; i < count; i++)
            {
                var point = RandomPitSpawnPoint(14f);
                AddEnemy(Enemy.CreatePatrol(point, point, false, enhanced: enhanced));
            }
        }

        void AddTriangles(int count, bool enhanced = false)
        {
            for (var i = 0; i < count; i++) AddEnemy(Enemy.CreateStrong(RandomPitSpawnPoint(14f), enhanced: enhanced));
        }

        void AddSquares(int count)
        {
            for (var i = 0; i < count; i++) AddEnemy(new MiniBossEnemySquare(RandomPitSpawnPoint(28f)));
        }

        void AddFastSquares(int count)
        {
            for (var i = 0; i < count; i++) AddEnemy(new MiniBossEnemySquare(RandomPitSpawnPoint(28f), isFast: true));
        }

        void AddHexes(int count)
        {
            for (var i = 0; i < count; i++) AddEnemy(HexEnemy.Create(RandomPitSpawnPoint(16f), _rng));
        }

        void AddToxic(int count)
        {
            for (var i = 0; i < count; i++) AddEnemy(new ToxicTriangleEnemy(RandomPitSpawnPoint(16f), -1));
        }

        void AddGuards(int count)
        {
            for (var i = 0; i < count; i++) AddEnemy(new GeneratorGuardianEnemy(RandomPitSpawnPoint(22f), -1));
        }

        void AddStationBosses(int count)
        {
            var arena = new Rectangle(0, 0, _worldSize, _worldSize);
            for (var i = 0; i < count; i++) AddEnemy(new StationBossEnemy(RandomPitSpawnPoint(36f), arena));
        }

        if (wave <= 5)
        {
            AddCircles(3 + wave);
        }
        else if (wave <= 9)
        {
            AddTriangles(wave - 4);
            AddCircles(3);
        }
        else if (wave == 10)
        {
            AddTriangles(6, true);
            AddCircles(3, true);
        }
        else if (wave <= 19)
        {
            AddTriangles(2, true);
            AddCircles(wave - 8, true);
        }
        else if (wave == 20)
        {
            AddHexes(10);
        }
        else if (wave <= 29)
        {
            var growth = wave - 21;
            AddTriangles(1 + (growth + 1) / 2, true);
            AddCircles(3 + growth / 2, true);
            AddHexes(1);
            AddToxic(1);
        }
        else if (wave == 30)
        {
            AddSquares(1);
            AddFastSquares(1);
        }
        else if (wave <= 39)
        {
            AddToxic(3);
            AddTriangles(wave - 30, true);
        }
        else if (wave == 40)
        {
            AddFastSquares(4);
        }
        else if (wave <= 49)
        {
            AddCircles(19 + wave - 40, true);
        }
        else if (wave == 50)
        {
            AddGuards(3);
            AddFastSquares(1);
        }
        else if (wave <= 51)
        {
            AddFastSquares(1);
            AddCircles(3);
        }
        else if (wave <= 55)
        {
            AddFastSquares(1);
            AddCircles(3);
            AddTriangles(wave - 51, true);
        }
        else if (wave == 56)
        {
            AddFastSquares(2);
        }
        else if (wave <= 59)
        {
            AddFastSquares(2);
            AddTriangles(wave - 56, true);
        }
        else if (wave == 60)
        {
            AddStationBosses(1);
            AddHexes(5);
        }
        else if (wave <= 69)
        {
            AddFastSquares(3);
        }
        else if (wave == 70)
        {
            AddStationBosses(2);
        }
        else if (wave <= 79)
        {
            AddFastSquares(4);
        }
        else if (wave == 80)
        {
            AddStationBosses(3);
        }
        else
        {
            AddFastSquares(5);
            AddGuards(2);
        }

        ShowNotice($"Wave {wave} started.");
    }

    private void HandlePitEnemyDeath(object enemy)
    {
        if (!_pitEnemyWaves.Remove(enemy, out var wave)) return;
        if (_pitCompletedWaves.Contains(wave)) return;
        if (_pitEnemyWaves.ContainsValue(wave)) return;

        _pitCompletedWaves.Add(wave);
        _player.GrantLevel();
        var rewardXp = wave % 10 == 0 ? 200 : 50;
        var rewardCoins = wave % 10 == 0 ? 50 : 20;
        AddMetaScore(rewardXp);
        _meta.SynthCoins += rewardCoins;
        _runScore += rewardXp;
        _pitRunXpEarned += rewardXp;
        _pitRunCoinsEarned += rewardCoins;
        if (!HasAnyPitEnemyAlive() && _pitWaveTimer > 3f) _pitWaveTimer = 3f;
        if (wave % 3 == 0) OpenPitRewardSelection(wave);
        SavePersistentState();
    }

    private bool HasAnyPitEnemyAlive()
        => _enemies.Any(e => e.Alive)
           || _hexEnemies.Any(h => h.Alive)
           || _turrets.Any(t => t.Alive)
           || _miniBosses.Any(b => b.Alive)
           || _generatorGuards.Any(g => g.Alive)
           || _toxicEnemies.Any(t => t.Alive)
           || (_destroyerBoss is not null && _destroyerBoss.Alive)
           || (_stationBoss is not null && _stationBoss.Alive)
           || _pitStationBosses.Any(b => b.Alive);

    private void OpenPitRewardSelection(int wave)
    {
        _pitRewardOffers.Clear();
        _pitRewardOffers.Add(RollPitReward(WeaponClass.Melee, wave));
        _pitRewardOffers.Add(RollPitReward(WeaponClass.Ranged, wave));
        _pitRewardOffers.Add(RollPitReward(null, wave));
        _pitRouletteItems.Clear();
        _pitRouletteItems.Add(BuildPitRouletteItems(WeaponClass.Melee, wave, _pitRewardOffers[0]));
        _pitRouletteItems.Add(BuildPitRouletteItems(WeaponClass.Ranged, wave, _pitRewardOffers[1]));
        _pitRouletteItems.Add(BuildPitRouletteItems(null, wave, _pitRewardOffers[2]));
        Array.Fill(_pitRewardClaimed, false);
        _pitRewardSpinElapsed = 0f;
        _pitRewardOpen = true;
    }

    private void UpdatePitRewardSelection(float dt)
    {
        _hovered = null;
        _pitRewardSpinElapsed += dt;
        if (Raylib.IsKeyPressed(KeyboardKey.Escape)) return;

        var ready = PitRewardReady;
        if (!ready) return;

        if (ready && Clicked(PitRewardSkipButtonRect()))
        {
            ClosePitRewardSelection();
            return;
        }

        for (var i = 0; i < _pitRewardOffers.Count; i++)
        {
            if (!_pitRewardClaimed[i] && Clicked(PitRewardTakeButtonRect(i)))
            {
                EquipPitReward(_pitRewardOffers[i]);
                _pitRewardClaimed[i] = true;
                if (_pitRewardClaimed.Take(_pitRewardOffers.Count).All(claimed => claimed))
                {
                    ClosePitRewardSelection();
                }
                return;
            }
        }
    }

    private bool PitRewardReady
        => _pitRewardOpen && _pitRewardSpinElapsed >= PitRewardSpinDurations[^1];

    private void ClosePitRewardSelection()
    {
        _pitRewardOpen = false;
        _pitRewardOffers.Clear();
        _pitRouletteItems.Clear();
        Array.Fill(_pitRewardClaimed, false);
        _pitRewardSpinElapsed = 0f;
    }

    private void EquipPitReward(ItemStack item)
    {
        if (item.Type == ItemType.Armor) _player.Armor = item;
        else if (item.WeaponKind == WeaponClass.Ranged) _player.RangedWeapon = item;
        else if (item.WeaponKind == WeaponClass.Melee) _player.MeleeWeapon = item;
        ShowNotice($"Equipped {item.Name}.");
    }

    private ItemStack RollPitReward(WeaponClass? weaponClass, int wave)
    {
        var rarity = RollPitRewardRarity(Math.Max(1, wave / 3));
        if (weaponClass is null && rarity == ArmorRarity.Red) rarity = ArmorRarity.Legendary;
        return weaponClass is null
            ? ItemStack.Armor(rarity, _rng)
            : ItemStack.Weapon(weaponClass.Value, rarity, _rng);
    }

    private List<ItemStack> BuildPitRouletteItems(WeaponClass? weaponClass, int wave, ItemStack finalItem)
    {
        var result = new List<ItemStack>();
        for (var i = 0; i < 18; i++) result.Add(RollPitReward(weaponClass, wave));
        result.Add(finalItem);
        return result;
    }

    private ArmorRarity RollPitRewardRarity(int roulette)
    {
        var roll = _rng.NextSingle();
        if (roulette <= 5)
        {
            var rareChance = 0.05f * roulette;
            return roll < rareChance ? ArmorRarity.Rare : ArmorRarity.Common;
        }

        if (roulette <= 10)
        {
            if (roll < 0.05f) return ArmorRarity.Epic;
            if (roll < 0.35f) return ArmorRarity.Rare;
            return ArmorRarity.Common;
        }

        if (roulette <= 15)
        {
            if (roll < 0.15f) return ArmorRarity.Epic;
            if (roll < 0.70f) return ArmorRarity.Rare;
            return ArmorRarity.Common;
        }

        if (roulette <= 20)
        {
            if (roll < 0.05f) return ArmorRarity.Legendary;
            if (roll < 0.50f) return ArmorRarity.Epic;
            return ArmorRarity.Rare;
        }

        if (roll < 0.01f) return ArmorRarity.Red;
        if (roll < 0.16f) return ArmorRarity.Legendary;
        if (roll < 0.91f) return ArmorRarity.Epic;
        return ArmorRarity.Rare;
    }

    private Vector2 RandomPitSpawnPoint(float radius)
    {
        var center = new Vector2(_worldSize / 2f, _worldSize / 2f);
        for (var i = 0; i < 120; i++)
        {
            var point = RandomMapPointSafe(radius);
            if (MathF.Abs(point.X - center.X) <= 250f && MathF.Abs(point.Y - center.Y) <= 250f) continue;
            return point;
        }

        return new Vector2(radius + 20f, radius + 20f);
    }

    private ItemStack RandomPitConsumable()
    {
        var values = new[] { ConsumableType.Medkit, ConsumableType.Stim, ConsumableType.ProtectiveDome, ConsumableType.StickyBullets };
        return ItemStack.Consumable(values[_rng.Next(values.Length)]);
    }

    private void UpdateDeadZoneProgress(float dt)
    {
        if (!_currentMap.IsDeadZone) return;

        if (!_stationEntranceOpen && _generators.Count(g => g.Destroyed) >= 3)
        {
            OpenStationEntrance("Station entrance unlocked.");
        }

        if (!_stationEntranceOpen
            && _stationEntranceDoor is Rectangle entranceDoor
            && CircleIntersectsRect(_player.Position, 70f, entranceDoor)
            && TryConsumeStationKey())
        {
            OpenStationEntrance("S.T.A.T.I.O.N key used.");
        }

        if (!_stationBossFightStarted
            && _stationBoss is not null
            && _stationBossArena is Rectangle arena
            && Raylib.CheckCollisionPointRec(_player.Position, arena))
        {
            _stationBossFightStarted = true;
            _stationBoss.Activate();
            _stationBossDoorSealTimer = 0.5f;
            ShowNotice("Station arena sealing.");
        }

        if (_stationBossDoorSealTimer > 0f)
        {
            _stationBossDoorSealTimer -= dt;
            if (_stationBossDoorSealTimer <= 0f) SealStationBossDoor();
        }
    }

    private void SealStationBossDoor()
    {
        if (_stationBossDoorSealed) return;
        _stationBossDoorSealTimer = -1f;
        _stationBossDoorSealed = true;

        if (_stationBossDoor is not Rectangle door) return;

        if (CircleIntersectsRect(_player.Position, 16f, door))
        {
            _player.PlaceAt(FindSafeArenaDoorPoint(door));
        }

        _obstacles.Add(new Obstacle(door));
        ShowNotice("Station arena sealed.");
    }

    private void OpenStationEntrance(string notice)
    {
        _stationEntranceOpen = true;
        RemoveObstacle(_stationEntranceDoor);
        ShowNotice(notice);
    }

    private bool TryConsumeStationKey()
    {
        if (_player.Inventory.QuickSlotQ?.IsStationKey == true)
        {
            _player.Inventory.QuickSlotQ = null;
            _player.Inventory.AutoFillConsumableSlots();
            return true;
        }

        if (_player.Inventory.QuickSlotR?.IsStationKey == true)
        {
            _player.Inventory.QuickSlotR = null;
            _player.Inventory.AutoFillConsumableSlots();
            return true;
        }

        for (var i = 0; i < _player.Inventory.BackpackSlots.Count; i++)
        {
            if (_player.Inventory.BackpackSlots[i]?.IsStationKey != true) continue;
            _player.Inventory.BackpackSlots[i] = null;
            return true;
        }

        return false;
    }

    private void OpenStationBossDoor()
    {
        _stationBossDoorSealTimer = -1f;
        _stationBossDoorSealed = false;
        RemoveObstacle(_stationBossDoor);
        ShowNotice("Station arena opened.");
    }

    private Vector2 FindSafeArenaDoorPoint(Rectangle door)
    {
        if (_stationBossArena is not Rectangle arena) return _player.Position;

        var basePoint = new Vector2(arena.X + 28f, Math.Clamp(_player.Position.Y, arena.Y + 24f, arena.Y + arena.Height - 24f));
        if (!MovementUtils.CircleHitsObstacle(basePoint, 16f, _obstacles)) return basePoint;

        for (var offset = 24f; offset <= 240f; offset += 24f)
        {
            var up = new Vector2(basePoint.X, Math.Clamp(basePoint.Y - offset, arena.Y + 24f, arena.Y + arena.Height - 24f));
            if (!MovementUtils.CircleHitsObstacle(up, 16f, _obstacles)) return up;

            var down = new Vector2(basePoint.X, Math.Clamp(basePoint.Y + offset, arena.Y + 24f, arena.Y + arena.Height - 24f));
            if (!MovementUtils.CircleHitsObstacle(down, 16f, _obstacles)) return down;
        }

        return new Vector2(door.X + door.Width + 28f, door.Y + door.Height * 0.5f);
    }

    private static bool CircleIntersectsRect(Vector2 center, float radius, Rectangle rect)
    {
        var nearest = new Vector2(
            Math.Clamp(center.X, rect.X, rect.X + rect.Width),
            Math.Clamp(center.Y, rect.Y, rect.Y + rect.Height));
        return Vector2.DistanceSquared(center, nearest) < radius * radius;
    }

    private void RemoveObstacle(Rectangle? rect)
    {
        if (rect is not Rectangle target) return;
        _obstacles.RemoveAll(o =>
            MathF.Abs(o.Rect.X - target.X) < 0.1f
            && MathF.Abs(o.Rect.Y - target.Y) < 0.1f
            && MathF.Abs(o.Rect.Width - target.Width) < 0.1f
            && MathF.Abs(o.Rect.Height - target.Height) < 0.1f);
    }

    private void UpdateGroundConsumables()
    {
        for (var i = _groundConsumables.Count - 1; i >= 0; i--)
        {
            var pickup = _groundConsumables[i];
            if (Vector2.Distance(pickup.Position, _player.Position) > 26f) continue;
            if (!Raylib.IsKeyPressed(KeyboardKey.F)) continue;
            if (!TryPickGroundItem(pickup.Item)) continue;

            MarkPitConsumablePicked(pickup);
            _groundConsumables.RemoveAt(i);
            break;
        }
    }

    private void MarkPitConsumablePicked(GroundConsumablePickup pickup)
    {
        if (!_challengeMode) return;

        for (var i = 0; i < _pitConsumablePickups.Length; i++)
        {
            if (!ReferenceEquals(_pitConsumablePickups[i], pickup)) continue;
            _pitConsumablePickups[i] = null;
            _pitConsumableSpawnTimers[i] = 30f;
            return;
        }
    }

    private void HandleConsumedQuickSlot(ConsumableType? consumableType)
    {
        if (consumableType != ConsumableType.ProtectiveDome) return;
        _protectiveDomes.Add(new ProtectiveDome(_player.Position));
    }

    private List<Obstacle> BuildEnemyCollisionObstacles()
    {
        var result = new List<Obstacle>(_obstacles.Count + _protectiveDomes.Count);
        result.AddRange(_obstacles);

        foreach (var dome in _protectiveDomes.Where(d => d.Alive))
        {
            result.Add(new Obstacle(new Rectangle(
                dome.Position.X - ProtectiveDome.Radius,
                dome.Position.Y - ProtectiveDome.Radius,
                ProtectiveDome.Radius * 2f,
                ProtectiveDome.Radius * 2f)));
        }

        return result;
    }

    private void UpdateProjectiles(float dt)
    {
        for (var i = _projectiles.Count - 1; i >= 0; i--)
        {
            var p = _projectiles[i];
            p.Update(dt);
            AddMotionTrail(
                p.PreviousPosition,
                p.Position,
                p.Color,
                p.Kind == ProjectileKind.Grenade ? MathF.Max(5f, p.DrawRadius) : MathF.Max(2.5f, p.DrawRadius),
                MotionTrailShape.Circle,
                p.Kind == ProjectileKind.Grenade ? 0.2f : 0.26f);

            var hitWorldBounds = p.Position.X < 0 || p.Position.Y < 0 || p.Position.X > _worldSize || p.Position.Y > _worldSize;
            var hitObstacle = MovementUtils.CircleHitsObstacle(p.Position, p.DrawRadius, _obstacles);
            var domeHit = p.OwnerEnemy ? FindHitDome(p.Position, p.DrawRadius) : null;

            if (p.Kind == ProjectileKind.Grenade)
            {
                var directHit = false;
                var hitTarget = false;

                if (p.OwnerEnemy)
                {
                    if (domeHit is not null)
                    {
                        domeHit.Damage(p.ExplosionDamage);
                        _explosions.Add(new Explosion(p.Position, 26f, p.Color));
                        _projectiles.RemoveAt(i);
                        continue;
                    }

                    hitTarget = Vector2.Distance(p.Position, _player.Position) < 16f;
                }
                else
                {
                    directHit = TryApplyPlayerSegmentDamage(p.PreviousPosition, p.Position, p.DrawRadius, p.Damage, p.SourcePosition, p.PoisonDamagePerSecond, p.PoisonDuration);
                    hitTarget = directHit || HasEnemyInRadius(p.Position, 22f);
                }

                if (hitWorldBounds || hitObstacle || hitTarget || !p.Alive)
                {
                    ExplodeProjectile(p);
                    _projectiles.RemoveAt(i);
                }

                continue;
            }

            if (domeHit is not null)
            {
                domeHit.Damage(p.Damage);
                _explosions.Add(new Explosion(p.Position, 26f, p.Color));
                _projectiles.RemoveAt(i);
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
                    if (p.PlayerPoisonDuration > 0f) _player.ApplyPoison(p.PlayerPoisonDuration);
                    _explosions.Add(new Explosion(p.Position, 26f, p.Color));
                    _projectiles.RemoveAt(i);
                }
                else if (!p.Alive) _projectiles.RemoveAt(i);
                continue;
            }

            if (TryApplyPlayerSegmentDamage(p.PreviousPosition, p.Position, p.DrawRadius, p.Damage, p.SourcePosition, p.PoisonDamagePerSecond, p.PoisonDuration))
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
        if (_generatorGuards.Any(g => g.Alive && Vector2.Distance(g.Position, position) < radius + 14f)) return true;
        if (_toxicEnemies.Any(e => e.Alive && Vector2.Distance(e.Position, position) < radius + 12f)) return true;
        if (_generators.Any(g => !g.Destroyed && Vector2.Distance(g.Position, position) < radius + 24f)) return true;
        if (_stationBoss is not null && _stationBoss.IntersectsAnyHitZone(position, radius)) return true;
        if (_pitStationBosses.Any(b => b.Alive && b.IntersectsAnyHitZone(position, radius))) return true;
        return _destroyerBoss is not null
            && _destroyerBoss.Alive
            && _destroyerBoss.IntersectsAnyHitZone(position, radius);
    }

    private bool TryApplyPlayerSegmentDamage(Vector2 from, Vector2 to, float radius, float damage, Vector2 shotSource, float poisonDamagePerSecond = 0f, float poisonDuration = 0f)
    {
        var enemyHit = _enemies
            .Where(e => e.Alive && DistanceToSegment(e.Position, from, to) <= radius + 11f)
            .OrderBy(e => DistanceToSegment(e.Position, from, to))
            .FirstOrDefault();
        if (enemyHit is not null)
        {
            enemyHit.Damage(damage);
            ApplyPlayerHitEffects(enemyHit, poisonDamagePerSecond, poisonDuration);
            var targetAggroed = enemyHit.ReactToShot(shotSource, _obstacles);
            AggroWitnesses(enemyHit.Position, targetAggroed);
            return true;
        }

        var hexHit = _hexEnemies
            .Where(h => h.Alive && DistanceToSegment(h.Position, from, to) <= radius + 15f)
            .OrderBy(h => DistanceToSegment(h.Position, from, to))
            .FirstOrDefault();
        if (hexHit is not null)
        {
            hexHit.Damage(damage);
            ApplyPlayerHitEffects(hexHit, poisonDamagePerSecond, poisonDuration);
            AggroWitnesses(hexHit.Position, true);
            return true;
        }

        var turretHit = _turrets
            .Where(t => t.Alive && DistanceToSegment(t.Position, from, to) <= radius + 18f)
            .OrderBy(t => DistanceToSegment(t.Position, from, to))
            .FirstOrDefault();
        if (turretHit is not null)
        {
            turretHit.Damage(damage);
            ApplyPlayerHitEffects(turretHit, poisonDamagePerSecond, poisonDuration);
            var targetAggroed = turretHit.ReactToShot(shotSource, _player.Position, _obstacles);
            AggroWitnesses(turretHit.Position, targetAggroed);
            return true;
        }

        var miniBossHit = _miniBosses
            .Where(b => b.Alive && DistanceToSegment(b.Position, from, to) <= radius + 26f)
            .OrderBy(b => DistanceToSegment(b.Position, from, to))
            .FirstOrDefault();
        if (miniBossHit is not null)
        {
            miniBossHit.Damage(damage);
            ApplyPlayerHitEffects(miniBossHit, poisonDamagePerSecond, poisonDuration);
            var targetAggroed = miniBossHit.ReactToShot(shotSource, _obstacles);
            AggroWitnesses(miniBossHit.Position, targetAggroed);
            return true;
        }

        var guardHit = _generatorGuards
            .Where(g => g.Alive && DistanceToSegment(g.Position, from, to) <= radius + 18f)
            .OrderBy(g => DistanceToSegment(g.Position, from, to))
            .FirstOrDefault();
        if (guardHit is not null)
        {
            guardHit.Damage(damage);
            ApplyPlayerHitEffects(guardHit, poisonDamagePerSecond, poisonDuration);
            var targetAggroed = guardHit.TryAggroFromPlayerHit(_player.Position);
            AggroWitnesses(guardHit.Position, targetAggroed);
            return true;
        }

        var toxicHit = _toxicEnemies
            .Where(e => e.Alive && DistanceToSegment(e.Position, from, to) <= radius + 16f)
            .OrderBy(e => DistanceToSegment(e.Position, from, to))
            .FirstOrDefault();
        if (toxicHit is not null)
        {
            toxicHit.Damage(damage);
            ApplyPlayerHitEffects(toxicHit, poisonDamagePerSecond, poisonDuration);
            var targetAggroed = toxicHit.ReactToShot(shotSource, _obstacles);
            AggroWitnesses(toxicHit.Position, targetAggroed);
            return true;
        }

        var generatorHit = _generators
            .Where(g => !g.Destroyed && DistanceToSegment(g.Position, from, to) <= radius + 28f)
            .OrderBy(g => DistanceToSegment(g.Position, from, to))
            .FirstOrDefault();
        if (generatorHit is not null)
        {
            generatorHit.Damage(damage);
            return true;
        }

        if (_stationBoss is not null && _stationBoss.TryApplySegmentDamage(from, to, radius, damage))
        {
            ApplyPlayerHitEffects(_stationBoss, poisonDamagePerSecond, poisonDuration);
            AggroWitnesses(_stationBoss.Position, true);
            return true;
        }

        var pitStationBossHit = _pitStationBosses.FirstOrDefault(b => b.Alive && b.TryApplySegmentDamage(from, to, radius, damage));
        if (pitStationBossHit is not null)
        {
            ApplyPlayerHitEffects(pitStationBossHit, poisonDamagePerSecond, poisonDuration);
            AggroWitnesses(pitStationBossHit.Position, true);
            return true;
        }

        if (_destroyerBoss is not null && _destroyerBoss.Alive && _destroyerBoss.TryApplySegmentDamage(from, to, radius, damage))
        {
            ApplyPlayerHitEffects(_destroyerBoss, poisonDamagePerSecond, poisonDuration);
            _destroyerBoss.ForceAggro(_player.Position);
            AggroWitnesses(_destroyerBoss.Position, true);
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
                _player.TakeDamage(projectile.ExplosionDamage, true);
            }

            return;
        }

        var aggroWitnesses = false;

        foreach (var enemy in _enemies.Where(e => e.Alive && Vector2.Distance(e.Position, projectile.Position) <= projectile.ExplosionRadius))
        {
            enemy.Damage(projectile.ExplosionDamage);
            ApplyPlayerHitEffects(enemy);
            aggroWitnesses |= enemy.ReactToShot(projectile.SourcePosition, _obstacles);
        }

        foreach (var hex in _hexEnemies.Where(h => h.Alive && Vector2.Distance(h.Position, projectile.Position) <= projectile.ExplosionRadius))
        {
            hex.Damage(projectile.ExplosionDamage);
            ApplyPlayerHitEffects(hex);
            aggroWitnesses = true;
        }

        foreach (var turret in _turrets.Where(t => t.Alive && Vector2.Distance(t.Position, projectile.Position) <= projectile.ExplosionRadius))
        {
            turret.Damage(projectile.ExplosionDamage);
            ApplyPlayerHitEffects(turret);
            aggroWitnesses |= turret.ReactToShot(projectile.SourcePosition, _player.Position, _obstacles);
        }

        foreach (var miniBoss in _miniBosses.Where(b => b.Alive && Vector2.Distance(b.Position, projectile.Position) <= projectile.ExplosionRadius))
        {
            miniBoss.Damage(projectile.ExplosionDamage);
            ApplyPlayerHitEffects(miniBoss);
            aggroWitnesses |= miniBoss.ReactToShot(projectile.SourcePosition, _obstacles);
        }

        foreach (var guard in _generatorGuards.Where(g => g.Alive && Vector2.Distance(g.Position, projectile.Position) <= projectile.ExplosionRadius + 14f))
        {
            guard.Damage(projectile.ExplosionDamage);
            ApplyPlayerHitEffects(guard);
            aggroWitnesses |= guard.TryAggroFromPlayerHit(_player.Position);
        }

        foreach (var toxic in _toxicEnemies.Where(e => e.Alive && Vector2.Distance(e.Position, projectile.Position) <= projectile.ExplosionRadius + 12f))
        {
            toxic.Damage(projectile.ExplosionDamage);
            ApplyPlayerHitEffects(toxic);
            aggroWitnesses |= toxic.ReactToShot(projectile.SourcePosition, _obstacles);
        }

        foreach (var generator in _generators.Where(g => !g.Destroyed && Vector2.Distance(g.Position, projectile.Position) <= projectile.ExplosionRadius + 24f))
        {
            generator.Damage(projectile.ExplosionDamage);
        }

        if (_stationBoss is not null)
        {
            _stationBoss.ApplyExplosionDamage(projectile.Position, projectile.ExplosionRadius, projectile.ExplosionDamage);
            if (_stationBoss.IntersectsAnyHitZone(projectile.Position, projectile.ExplosionRadius))
            {
                ApplyPlayerHitEffects(_stationBoss);
                aggroWitnesses = true;
            }
        }

        foreach (var boss in _pitStationBosses.Where(b => b.Alive))
        {
            boss.ApplyExplosionDamage(projectile.Position, projectile.ExplosionRadius, projectile.ExplosionDamage);
            if (boss.IntersectsAnyHitZone(projectile.Position, projectile.ExplosionRadius))
            {
                ApplyPlayerHitEffects(boss);
                aggroWitnesses = true;
            }
        }

        if (_destroyerBoss is not null && _destroyerBoss.Alive)
        {
            _destroyerBoss.ApplyExplosionDamage(projectile.Position, projectile.ExplosionRadius, projectile.ExplosionDamage);
            if (_destroyerBoss.IntersectsAnyHitZone(projectile.Position, projectile.ExplosionRadius))
            {
                ApplyPlayerHitEffects(_destroyerBoss);
                _destroyerBoss.ForceAggro(_player.Position);
                aggroWitnesses = true;
            }
        }

        AggroWitnesses(projectile.Position, aggroWitnesses);
    }

    private void ApplyPlayerHitEffects(Enemy enemy, float poisonDamagePerSecond = 0f, float poisonDuration = 0f)
    {
        if (_player.StickyBulletsActive) enemy.ApplyStickySlow();
        if (poisonDamagePerSecond > 0f) enemy.ApplyPoison(poisonDamagePerSecond, poisonDuration);
    }

    private void ApplyPlayerHitEffects(HexEnemy enemy, float poisonDamagePerSecond = 0f, float poisonDuration = 0f)
    {
        if (_player.StickyBulletsActive) enemy.ApplyStickySlow();
        if (poisonDamagePerSecond > 0f) enemy.ApplyPoison(poisonDamagePerSecond, poisonDuration);
    }

    private void ApplyPlayerHitEffects(TurretEnemy enemy, float poisonDamagePerSecond = 0f, float poisonDuration = 0f)
    {
        if (_player.StickyBulletsActive) enemy.ApplyStickySlow();
        if (poisonDamagePerSecond > 0f) enemy.ApplyPoison(poisonDamagePerSecond, poisonDuration);
    }

    private void ApplyPlayerHitEffects(MiniBossEnemySquare enemy, float poisonDamagePerSecond = 0f, float poisonDuration = 0f)
    {
        if (_player.StickyBulletsActive) enemy.ApplyStickySlow();
        if (poisonDamagePerSecond > 0f) enemy.ApplyPoison(poisonDamagePerSecond, poisonDuration);
    }

    private void ApplyPlayerHitEffects(BossEnemyDestroyer enemy, float poisonDamagePerSecond = 0f, float poisonDuration = 0f)
    {
        if (_player.StickyBulletsActive) enemy.ApplyStickySlow();
        if (poisonDamagePerSecond > 0f) enemy.ApplyPoison(poisonDamagePerSecond, poisonDuration);
    }

    private void ApplyPlayerHitEffects(GeneratorGuardianEnemy enemy, float poisonDamagePerSecond = 0f, float poisonDuration = 0f)
    {
        if (_player.StickyBulletsActive) enemy.ApplyStickySlow();
        if (poisonDamagePerSecond > 0f) enemy.ApplyPoison(poisonDamagePerSecond, poisonDuration);
    }

    private void ApplyPlayerHitEffects(ToxicTriangleEnemy enemy, float poisonDamagePerSecond = 0f, float poisonDuration = 0f)
    {
        if (_player.StickyBulletsActive) enemy.ApplyStickySlow();
        if (poisonDamagePerSecond > 0f) enemy.ApplyPoison(poisonDamagePerSecond, poisonDuration);
    }

    private void ApplyPlayerHitEffects(StationBossEnemy enemy, float poisonDamagePerSecond = 0f, float poisonDuration = 0f)
    {
        if (_player.StickyBulletsActive) enemy.ApplyStickySlow();
        if (poisonDamagePerSecond > 0f) enemy.ApplyPoison(poisonDamagePerSecond, poisonDuration);
    }

    private void UpdateProtectiveDomes(float dt)
    {
        for (var i = _protectiveDomes.Count - 1; i >= 0; i--)
        {
            var dome = _protectiveDomes[i];
            dome.Update(dt);

            foreach (var enemy in _enemies.Where(e => e.Alive && Vector2.Distance(e.Position, dome.Position) <= ProtectiveDome.Radius + 14f))
            {
                dome.TryApplyContactDamage(enemy, enemy.IsStrong ? 18f : 10f, enemy.IsStrong ? 1.3f : 0.9f);
            }

            foreach (var hex in _hexEnemies.Where(h => h.Alive && Vector2.Distance(h.Position, dome.Position) <= ProtectiveDome.Radius + 16f))
            {
                dome.TryApplyContactDamage(hex, 10f, 0.9f);
            }

            foreach (var boss in _miniBosses.Where(b => b.Alive && Vector2.Distance(b.Position, dome.Position) <= ProtectiveDome.Radius + 28f))
            {
                dome.TryApplyContactDamage(boss, 20f, 0.8f);
            }

            foreach (var guard in _generatorGuards.Where(g => g.Alive && Vector2.Distance(g.Position, dome.Position) <= ProtectiveDome.Radius + 18f))
            {
                dome.TryApplyContactDamage(guard, 18f, 0.8f);
            }

            foreach (var toxic in _toxicEnemies.Where(t => t.Alive && Vector2.Distance(t.Position, dome.Position) <= ProtectiveDome.Radius + 16f))
            {
                dome.TryApplyContactDamage(toxic, 10f, 0.9f);
            }

            if (_destroyerBoss is not null && _destroyerBoss.Alive && Vector2.Distance(_destroyerBoss.Position, dome.Position) <= ProtectiveDome.Radius + 52f)
            {
                dome.TryApplyContactDamage(_destroyerBoss, 22f, 0.8f);
            }

            if (_stationBoss is not null && _stationBoss.Alive && Vector2.Distance(_stationBoss.Position, dome.Position) <= ProtectiveDome.Radius + 34f)
            {
                dome.TryApplyContactDamage(_stationBoss, 22f, 0.8f);
            }

            if (dome.Alive) continue;
            _protectiveDomes.RemoveAt(i);
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
                ApplyPlayerHitEffects(e);
                e.ForceAggro(_player.Position);
                AggroWitnesses(e.Position, true);
            }

            foreach (var h in _hexEnemies.Where(x => x.Alive))
            {
                var hit = s.IsLine
                    ? DistanceToSegment(h.Position, s.LineStart, s.LineEnd) < 16f
                    : IsInArc(h.Position, s, 10f);
                if (hit && s.TryRegisterHit(h))
                {
                    h.Damage(_player.GetMeleeDamage());
                    ApplyPlayerHitEffects(h);
                    AggroWitnesses(h.Position, true);
                }
            }

            foreach (var t in _turrets.Where(x => x.Alive))
            {
                var hit = s.IsLine
                    ? DistanceToSegment(t.Position, s.LineStart, s.LineEnd) < 20f
                    : IsInArc(t.Position, s, 14f);
                if (hit && s.TryRegisterHit(t))
                {
                    t.Damage(_player.GetMeleeDamage());
                    ApplyPlayerHitEffects(t);
                    t.ForceAggro(_player.Position);
                    AggroWitnesses(t.Position, true);
                }
            }

            foreach (var b in _miniBosses.Where(x => x.Alive))
            {
                var hit = s.IsLine
                    ? DistanceToSegment(b.Position, s.LineStart, s.LineEnd) < 28f
                    : IsInArc(b.Position, s, 24f);
                if (hit && s.TryRegisterHit(b))
                {
                    b.Damage(_player.GetMeleeDamage() * 0.75f);
                    ApplyPlayerHitEffects(b);
                    b.ForceAggro(_player.Position);
                    AggroWitnesses(b.Position, true);
                }
            }

            foreach (var g in _generatorGuards.Where(x => x.Alive))
            {
                var hit = s.IsLine
                    ? DistanceToSegment(g.Position, s.LineStart, s.LineEnd) < 20f
                    : IsInArc(g.Position, s, 14f);
                if (hit && s.TryRegisterHit(g))
                {
                    g.Damage(_player.GetMeleeDamage());
                    ApplyPlayerHitEffects(g);
                    g.ForceAggro(_player.Position);
                    AggroWitnesses(g.Position, true);
                }
            }

            foreach (var toxic in _toxicEnemies.Where(x => x.Alive))
            {
                var hit = s.IsLine
                    ? DistanceToSegment(toxic.Position, s.LineStart, s.LineEnd) < 18f
                    : IsInArc(toxic.Position, s, 12f);
                if (hit && s.TryRegisterHit(toxic))
                {
                    toxic.Damage(_player.GetMeleeDamage());
                    ApplyPlayerHitEffects(toxic);
                    toxic.ForceAggro(_player.Position);
                    AggroWitnesses(toxic.Position, true);
                }
            }

            foreach (var generator in _generators.Where(x => !x.Destroyed))
            {
                var hit = s.IsLine
                    ? DistanceToSegment(generator.Position, s.LineStart, s.LineEnd) < 30f
                    : IsInArc(generator.Position, s, 24f);
                if (hit && s.TryRegisterHit(generator))
                {
                    generator.Damage(_player.GetMeleeDamage());
                }
            }

            if (_stationBoss is not null && _stationBoss.Alive)
            {
                var hit = s.IsLine
                    ? DistanceToSegment(_stationBoss.Position, s.LineStart, s.LineEnd) < 36f
                    : IsInArc(_stationBoss.Position, s, 30f);
                if (hit && s.TryRegisterHit(_stationBoss))
                {
                    _stationBoss.Damage(_player.GetMeleeDamage() * 0.75f);
                    ApplyPlayerHitEffects(_stationBoss);
                    AggroWitnesses(_stationBoss.Position, true);
                }
            }

            if (_destroyerBoss is not null && _destroyerBoss.Alive)
            {
                var hit = s.IsLine
                    ? DistanceToSegment(_destroyerBoss.Position, s.LineStart, s.LineEnd) < 54f
                    : IsInArc(_destroyerBoss.Position, s, 50f);
                if (hit && s.TryRegisterHit(_destroyerBoss))
                {
                    _destroyerBoss.Damage(_player.GetMeleeDamage() * 0.75f);
                    ApplyPlayerHitEffects(_destroyerBoss);
                    _destroyerBoss.ForceAggro(_player.Position);
                    AggroWitnesses(_destroyerBoss.Position, true);
                }
            }
        }
    }

    private void AggroWitnesses(Vector2 impactPosition, bool targetAggroed)
    {
        if (!targetAggroed) return;

        foreach (var enemy in _enemies.Where(x => x.Alive))
        {
            if (enemy.CanNoticeCombatPoint(impactPosition, _obstacles)) enemy.ForceAggro(_player.Position);
        }

        foreach (var turret in _turrets.Where(x => x.Alive))
        {
            if (turret.CanNoticeCombatPoint(impactPosition, _obstacles)) turret.ForceAggro(_player.Position);
        }

        foreach (var miniBoss in _miniBosses.Where(x => x.Alive))
        {
            if (miniBoss.CanNoticeCombatPoint(impactPosition, _obstacles)) miniBoss.ForceAggro(_player.Position);
        }

        foreach (var toxic in _toxicEnemies.Where(x => x.Alive))
        {
            if (toxic.CanNoticeCombatPoint(impactPosition, _obstacles)) toxic.ForceAggro(_player.Position);
        }

        if (_destroyerBoss is not null && _destroyerBoss.Alive && _destroyerBoss.CanSeePoint(impactPosition, _obstacles))
        {
            _destroyerBoss.ForceAggro(_player.Position);
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

        for (var i = _motionAfterImages.Count - 1; i >= 0; i--)
        {
            _motionAfterImages[i].Life -= dt * 7.5f;
            if (_motionAfterImages[i].Life <= 0f) _motionAfterImages.RemoveAt(i);
        }
    }

    private void AddMotionTrail(Vector2 previous, Vector2 current, Color color, float radius, MotionTrailShape shape, float alpha = 0.16f, float minRadius = -1f, bool rotateWithMovement = true)
    {
        var delta = current - previous;
        if (delta.LengthSquared() < 0.25f) return;

        var dir = Vector2.Normalize(delta);
        var trailPosition = current - dir * MathF.Min(radius * 0.75f, delta.Length() * 0.5f);
        var rotation = rotateWithMovement ? MathF.Atan2(dir.Y, dir.X) * 180f / MathF.PI : 0f;
        _motionAfterImages.Add(new MotionAfterImage(trailPosition, color, alpha, radius, shape, rotation, minRadius));

        const int maxMotionAfterImages = 700;
        if (_motionAfterImages.Count > maxMotionAfterImages)
        {
            _motionAfterImages.RemoveRange(0, _motionAfterImages.Count - maxMotionAfterImages);
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
            ResetInventoryUseHold();
            break;
        }

        if (_openedChestIndex is null) return;

        var openedChest = _chests[_openedChestIndex.Value];
        if (Vector2.Distance(openedChest.Position, _player.Position) > 120f)
        {
            _openedChestIndex = null;
            _player.InventoryOpen = false;
            ResetInventoryUseHold();
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
                ResetInventoryUseHold();
                return;
            }
        }

        UpdateInventoryConsumableUseHold(slots, m);

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
                    ResetInventoryUseHold();
                    return;
                }

                if (from.Item is null) return;
                _drag = new DragPayload(from.Kind, from.Index, from.Item!);
            }
        }

        if (_openedChestIndex is not null && (Clicked(TakeAllButtonRect) || (Raylib.IsKeyPressed(KeyboardKey.X) && _inventoryUseHoldIndex < 0)))
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

        if (slot.Item?.Type == ItemType.Consumable && !slot.Item.IsStationKey)
        {
            return MoveConsumableToQuickSlotQ(slot);
        }

        if (slot.Kind == SlotKind.Backpack)
        {
            return EquipFromBackpack(slot.Index);
        }

        return false;
    }

    private void UpdateInventoryConsumableUseHold(List<UiSlot> slots, Vector2 mouse)
    {
        if (_drag is not null || !Raylib.IsKeyDown(KeyboardKey.X))
        {
            ResetInventoryUseHold();
            return;
        }

        var slot = slots.FirstOrDefault(s => Raylib.CheckCollisionPointRec(mouse, s.Rect));
        if (slot?.Item?.Type != ItemType.Consumable || slot.Item.IsStationKey || slot.Kind == SlotKind.Trash || slot.Kind == SlotKind.Chest)
        {
            ResetInventoryUseHold();
            return;
        }

        if (_inventoryUseHoldKind != slot.Kind || _inventoryUseHoldIndex != slot.Index)
        {
            _inventoryUseHoldKind = slot.Kind;
            _inventoryUseHoldIndex = slot.Index;
            _inventoryUseHoldTimer = 0f;
        }

        _inventoryUseHoldTimer += Raylib.GetFrameTime();
        if (_inventoryUseHoldTimer < InventoryConsumableUseHoldDuration) return;

        var consumed = _player.UseConsumableItem(slot.Item);
        if (consumed is null)
        {
            ResetInventoryUseHold();
            return;
        }

        RemoveFromSource(new DragPayload(slot.Kind, slot.Index, slot.Item));
        HandleConsumedQuickSlot(consumed);
        _player.Inventory.AutoFillConsumableSlots();
        ResetInventoryUseHold();
    }

    private bool MoveConsumableToQuickSlotQ(UiSlot slot)
    {
        if (slot.Item?.Type != ItemType.Consumable) return false;
        if (slot.Item.IsStationKey) return false;
        if (slot.Kind == SlotKind.QuickSlotQ) return false;
        if (slot.Kind is not (SlotKind.Backpack or SlotKind.QuickSlotR)) return false;

        var target = _player.Inventory.QuickSlotQ;
        _player.Inventory.QuickSlotQ = slot.Item;

        if (slot.Kind == SlotKind.Backpack)
        {
            _player.Inventory.BackpackSlots[slot.Index] = target;
            return true;
        }

        _player.Inventory.QuickSlotR = target;
        return true;
    }

    private void ResetInventoryUseHold()
    {
        _inventoryUseHoldKind = default;
        _inventoryUseHoldIndex = -1;
        _inventoryUseHoldTimer = 0f;
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

        UpdateStorageSellHold(slots, mouse);

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
        if (slot.Kind != SlotKind.Storage && slot.Kind != SlotKind.RunBackpack && !IsMetaLoadoutSlot(slot.Kind)) return false;

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

        if (slot.Kind == SlotKind.RunBackpack)
        {
            return EquipFromMetaRunBackpack(slot.Index);
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

    private bool EquipFromMetaRunBackpack(int backpackIndex)
    {
        if (backpackIndex < 0 || backpackIndex >= _meta.RunBackpackSlots.Count) return false;

        var item = _meta.RunBackpackSlots[backpackIndex];
        if (item is null) return false;

        var target = GetPreferredLoadoutSlot(item);
        if (target is null) return false;

        var old = GetMetaLoadoutItem(target.Value);
        SetMetaLoadoutItem(target.Value, item);
        _meta.RunBackpackSlots[backpackIndex] = old;
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
            list.Add(new UiSlot(new Rectangle(742 + c * 48, 206 + r * 44, 42, 42), SlotKind.Storage, i, _meta.StorageSlots[i], i));
        }

        for (var i = 0; i < _meta.RunBackpackSlots.Count; i++)
        {
            var c = i % 6;
            var r = i / 6;
            list.Add(new UiSlot(new Rectangle(418 + c * 48, 228 + r * 44, 42, 42), SlotKind.RunBackpack, i, _meta.RunBackpackSlots[i], i));
        }

        list.AddRange(
        [
            new UiSlot(new Rectangle(238, 226, 58, 58), SlotKind.Armor, -1, _meta.Armor, -1),
            new UiSlot(new Rectangle(238, 294, 58, 58), SlotKind.RangedWeapon, -1, _meta.RangedWeapon, -1),
            new UiSlot(new Rectangle(238, 362, 58, 58), SlotKind.MeleeWeapon, -1, _meta.MeleeWeapon, -1),
            new UiSlot(new Rectangle(206, 454, 58, 58), SlotKind.QuickSlotQ, -1, _meta.QuickSlotQ, -1),
            new UiSlot(new Rectangle(272, 454, 58, 58), SlotKind.QuickSlotR, -1, _meta.QuickSlotR, -1)
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

        if (target.Kind == SlotKind.RunBackpack)
        {
            var old = _meta.RunBackpackSlots[target.Index];
            if (!CanReplaceStorageSource(drag, old)) return;

            _meta.RunBackpackSlots[target.Index] = drag.Item;
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
        if (drag.Kind is SlotKind.Storage or SlotKind.RunBackpack or SlotKind.Trash) return true;
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

        if (drag.Kind == SlotKind.RunBackpack)
        {
            _meta.RunBackpackSlots[drag.Index] = replacement;
            return;
        }

        if (drag.Kind == SlotKind.Trash)
        {
            _meta.Trash = replacement;
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

        if (GetPendingLevelUpPointCount() > 0 && Clicked(new Rectangle(54, 350, 120, 30)))
        {
            ApplyPendingLevelUpPoints();
        }

        if (GetPendingLevelUpPointCount() > 0 && Clicked(new Rectangle(184, 350, 120, 30)))
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

        if (target.Kind == SlotKind.QuickSlotQ && drag.Item.Type == ItemType.Consumable && !drag.Item.IsStationKey)
        {
            var old = _player.Inventory.QuickSlotQ;
            _player.Inventory.QuickSlotQ = drag.Item;
            RemoveFromSource(drag);
            if (old is not null) _player.Inventory.AddToBackpack(old);
            return;
        }

        if (target.Kind == SlotKind.QuickSlotR && drag.Item.Type == ItemType.Consumable && !drag.Item.IsStationKey)
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
        else if (drag.Kind == SlotKind.Trash)
        {
            _player.Inventory.Trash = null;
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

    private void ApplyIsFunnyNextRunBonus()
    {
        if (!_isFunnyNextRun) return;

        for (var i = 0; i < 250; i++) _player.GrantLevel();
        _isFunnyNextRun = false;
        ShowNotice("ISFUNNY activated. +250 run levels.");
    }

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
        if (_lastChanceActive)
        {
            _lastChanceTimer -= dt;
            if (_lastChanceTimer <= 0f)
            {
                FailRun("Extraction failed", "The last portal collapsed and all carried items were lost.");
                return;
            }

            if (!_lastChancePortalNotified && IsLastChancePortalOpen())
            {
                _lastChancePortalNotified = true;
                ShowNotice("Final portal is open.");
            }

            if (IsLastChancePortalOpen() && _extractPortals.Any(portal => Vector2.Distance(portal.Position, _player.Position) <= portal.InteractionRadius))
            {
                CompleteExtraction();
            }

            return;
        }

        if (_extractPortals.Count == 0)
        {
            _portalUnlockTimer -= dt;
            if (_portalUnlockTimer <= 0f)
            {
                SpawnExtractionPortals();
                _portalActiveTimer = _currentMap.PortalLifetime;
                ShowNotice("Portals are open.");
            }

            return;
        }

        _portalActiveTimer -= dt;
        if (_portalActiveTimer <= 0f)
        {
            ActivateLastChancePortal();
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
        var attempts = 0;
        while (_extractPortals.Count < _currentMap.PortalCount && attempts++ < 400)
        {
            var point = RandomExtractionPortalPoint(24f);
            if (_extractPortals.Any(portal => Vector2.Distance(portal.Position, point) < 2200f) && _extractPortals.Count > 0) continue;
            _extractPortals.Add(new ExtractPortal(point, _rng.NextSingle() * MathF.Tau));
        }

        while (_extractPortals.Count < _currentMap.PortalCount)
        {
            _extractPortals.Add(new ExtractPortal(RandomExtractionPortalPoint(24f), _rng.NextSingle() * MathF.Tau));
        }
    }

    private void ActivateLastChancePortal()
    {
        _extractPortals.Clear();
        _lastChanceActive = true;
        _lastChanceTimer = _currentMap.LastChanceLifetime;
        _lastChancePortalNotified = false;

        for (var i = 0; i < _currentMap.LastChancePortalCount; i++)
        {
            var portalPos = RandomExtractionPortalPoint(24f);
            _extractPortals.Add(new ExtractPortal(portalPos, _rng.NextSingle() * MathF.Tau));
            SpawnLastChanceEnemies(portalPos);
        }
        _nextHexSpawnTimer = MathF.Min(_nextHexSpawnTimer, NextHexSpawnDelay());
        ShowNotice("Final extraction chance started. Portal opens in the last 10 seconds.");
    }

    private void SpawnLastChanceEnemies(Vector2 portalPos)
    {
        for (var i = 0; i < _rng.Next(0, 5); i++)
        {
            _turrets.Add(new TurretEnemy(RandomPointNear(portalPos, 80f, 200f, 18f), _rng.NextSingle() * MathF.Tau));
        }

        for (var i = 0; i < _rng.Next(1, 3); i++)
        {
            _miniBosses.Add(new MiniBossEnemySquare(RandomPointNear(portalPos, 110f, 250f, 28f)));
        }

        for (var i = 0; i < _rng.Next(2, 6); i++)
        {
            var point = RandomPointNear(portalPos, 90f, 260f, 14f);
            _enemies.Add(Enemy.CreatePatrol(point, point, false, enhanced: _currentMap.IsDeadZone));
        }
    }

    private Vector2 RandomPointNear(Vector2 center, float minDistance, float maxDistance, float radius)
    {
        for (var i = 0; i < 80; i++)
        {
            var angle = _rng.NextSingle() * MathF.Tau;
            var distance = minDistance + _rng.NextSingle() * (maxDistance - minDistance);
            var point = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
            point = Vector2.Clamp(point, new Vector2(radius + 4f, radius + 4f), new Vector2(_worldSize - radius - 4f, _worldSize - radius - 4f));
            if (!MovementUtils.CircleHitsObstacle(point, radius, _obstacles)) return point;
        }

        return RandomOutdoorPoint(radius);
    }

    private bool IsLastChancePortalOpen() => _lastChanceActive && _lastChanceTimer <= 10f;

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
        RefreshArmoryOffers();
        SavePersistentState();
        ClearUiInteraction();
        _extractPortals.Clear();
        _lastChanceActive = false;
        _lastChanceTimer = 0f;
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
        _lastChanceActive = false;
        _lastChanceTimer = 0f;
        _pitRewardOpen = false;
        _pitRewardOffers.Clear();
        _pitRouletteItems.Clear();
        _pitRewardSpinElapsed = 0f;
        ClearUiInteraction();
        RefreshArmoryOffers();
        SavePersistentState();
        _deathHeader = header;
        _deathBody = _challengeMode
            ? $"{body}\nEarned: {_pitRunXpEarned} XP, {_pitRunCoinsEarned} SynthCoins."
            : body;
        _state = GameState.Death;
    }

    private void ClearUiInteraction()
    {
        _drag = null;
        _hovered = null;
        _openedChestIndex = null;
        ResetInventoryUseHold();
        ClearPendingLevelUpPoints();
    }

    private void ShowNotice(string text)
    {
        _noticeText = text;
        _noticeTimer = 5f;
    }

    private void OpenCodesPopup()
    {
        _codesPopupOpen = true;
        _codeInput = string.Empty;
        _codeStatusText = string.Empty;
        _codeStatusSuccess = false;
    }

    private void CloseCodesPopup()
    {
        _codesPopupOpen = false;
        _codeInput = string.Empty;
        _codeStatusText = string.Empty;
        _codeStatusSuccess = false;
    }

    private void UpdateCodesPopup()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Clicked(CodesPopupCloseRect()))
        {
            CloseCodesPopup();
            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Backspace) && _codeInput.Length > 0)
        {
            _codeInput = _codeInput[..^1];
        }

        while (true)
        {
            var key = Raylib.GetCharPressed();
            if (key == 0) break;

            if (_codeInput.Length >= 24) continue;
            var ch = (char)key;
            if (char.IsLetterOrDigit(ch) || ch == '_' || ch == '-')
            {
                _codeInput += char.ToUpperInvariant(ch);
            }
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Clicked(CodesPopupApplyRect()))
        {
            ApplyPromoCodeInput();
        }
    }

    private void ApplyPromoCodeInput()
    {
        var code = NormalizePromoCode(_codeInput);
        if (string.IsNullOrWhiteSpace(code))
        {
            SetCodeStatus(false, "Enter a code first.");
            return;
        }

        if (code == "RAMGUNGUS")
        {
            var result = ApplyRamGunGodCode();
            SetCodeStatus(result.Success, result.Message);
            if (result.Success) _codeInput = string.Empty;
            return;
        }

        if (code == "RAMARMORGUS")
        {
            var result = ApplyRamArmorGodCode();
            SetCodeStatus(result.Success, result.Message);
            if (result.Success) _codeInput = string.Empty;
            return;
        }

        if (code == "BLUEKING")
        {
            var result = ApplyBlueKingCode();
            SetCodeStatus(result.Success, result.Message);
            if (result.Success) _codeInput = string.Empty;
            return;
        }

        if (code == "SKEYFORRAM")
        {
            var result = ApplyStationKeyCode();
            SetCodeStatus(result.Success, result.Message);
            if (result.Success) _codeInput = string.Empty;
            return;
        }

        if (code == "LVL1RAM")
        {
            var result = ApplyLevelResetCode();
            SetCodeStatus(result.Success, result.Message);
            if (result.Success) _codeInput = string.Empty;
            return;
        }

        if (code == "ISFUNNY")
        {
            var result = ApplyIsFunnyCode();
            SetCodeStatus(result.Success, result.Message);
            if (result.Success) _codeInput = string.Empty;
            return;
        }

        SetCodeStatus(false, "No such code.");
    }

    private ProtectiveDome? FindHitDome(Vector2 point, float radius)
    {
        foreach (var dome in _protectiveDomes)
        {
            if (!dome.Alive) continue;
            var limit = ProtectiveDome.Radius + radius;
            if (Vector2.DistanceSquared(point, dome.Position) <= limit * limit) return dome;
        }

        return null;
    }

    private (bool Success, string Message) ApplyRamGunGodCode()
    {
        const string code = "RAMGUNGUS";
        if (!CanUsePromoCode(code, null, false, out var error))
        {
            return (false, error);
        }

        var rewards = new List<ItemStack>
        {
            ItemStack.PatternWeapon(WeaponClass.Ranged, WeaponPattern.Standard, ArmorRarity.Legendary, _rng),
            ItemStack.PatternWeapon(WeaponClass.Ranged, WeaponPattern.PulseRifle, ArmorRarity.Legendary, _rng),
            ItemStack.PatternWeapon(WeaponClass.Ranged, WeaponPattern.SniperRifle, ArmorRarity.Legendary, _rng),
            ItemStack.Toxikus(_rng),
            ItemStack.PatternWeapon(WeaponClass.Melee, WeaponPattern.Standard, ArmorRarity.Legendary, _rng),
            ItemStack.PatternWeapon(WeaponClass.Melee, WeaponPattern.EnergySpear, ArmorRarity.Legendary, _rng),
            ItemStack.Lancelot(_rng),
            ItemStack.BossGrenadeLauncher()
        };

        var stored = 0;
        foreach (var reward in rewards)
        {
            if (_meta.AddToStorage(reward)) stored++;
        }

        if (stored == 0)
        {
            return (false, "Storage is full.");
        }

        RegisterPromoCodeUse(code, false);
        SavePersistentState();
        var lost = rewards.Count - stored;
        return (true, lost > 0 ? $"Success: {stored} weapon(s) delivered, {lost} lost due to full storage." : "Success");
    }

    private (bool Success, string Message) ApplyRamArmorGodCode()
    {
        const string code = "RAMARMORGUS";
        if (!CanUsePromoCode(code, null, false, out var error))
        {
            return (false, error);
        }

        var rewards = new List<ItemStack>
        {
            ItemStack.Armor(ArmorRarity.Epic, _rng),
            ItemStack.Armor(ArmorRarity.Legendary, _rng),
            ItemStack.Armor(ArmorRarity.Red, _rng)
        };

        var stored = 0;
        foreach (var reward in rewards)
        {
            if (_meta.AddToStorage(reward)) stored++;
        }

        if (stored == 0)
        {
            return (false, "Storage is full.");
        }

        RegisterPromoCodeUse(code, false);
        SavePersistentState();
        var lost = rewards.Count - stored;
        return (true, lost > 0 ? $"Success: {stored} armor piece(s) delivered, {lost} lost due to full storage." : "Success");
    }

    private (bool Success, string Message) ApplyBlueKingCode()
    {
        const string code = "BLUEKING";
        if (!CanUsePromoCode(code, 1, false, out var error))
        {
            return (false, error);
        }

        var rewards = new List<ItemStack>
        {
            ItemStack.Weapon(WeaponClass.Ranged, ArmorRarity.Rare, _rng),
            ItemStack.Weapon(WeaponClass.Melee, ArmorRarity.Rare, _rng),
            ItemStack.Armor(ArmorRarity.Rare, _rng)
        };

        var stored = 0;
        foreach (var reward in rewards)
        {
            if (_meta.AddToStorage(reward)) stored++;
        }

        if (stored == 0)
        {
            return (false, "Storage is full.");
        }

        RegisterPromoCodeUse(code, false);
        SavePersistentState();
        var lost = rewards.Count - stored;
        return (true, lost > 0 ? $"Success: {stored} blue item(s) delivered, {lost} lost due to full storage." : "Success");
    }

    private (bool Success, string Message) ApplyStationKeyCode()
    {
        const string code = "SKEYFORRAM";
        if (!CanUsePromoCode(code, null, false, out var error))
        {
            return (false, error);
        }

        if (!_meta.AddToStorage(ItemStack.StationKey()))
        {
            return (false, "Storage is full.");
        }

        RegisterPromoCodeUse(code, false);
        SavePersistentState();
        return (true, "Success");
    }

    private (bool Success, string Message) ApplyLevelResetCode()
    {
        const string code = "LVL1RAM";
        if (!CanUsePromoCode(code, null, false, out var error))
        {
            return (false, error);
        }

        _meta.Level = 1;
        _meta.Score = 0;
        RegisterPromoCodeUse(code, false);
        SavePersistentState();
        return (true, "Success: account level reset to 1.");
    }

    private (bool Success, string Message) ApplyIsFunnyCode()
    {
        const string code = "ISFUNNY";
        if (!CanUsePromoCode(code, null, false, out var error))
        {
            return (false, error);
        }

        _isFunnyNextRun = true;
        RegisterPromoCodeUse(code, false);
        SavePersistentState();
        return (true, "Success: next run starts with +250 levels.");
    }

    private bool CanUsePromoCode(string code, int? maxUses, bool sessionOnly, out string error)
    {
        error = string.Empty;
        if (maxUses is int limit && GetPromoCodeUseCount(code) >= limit)
        {
            error = "This code can no longer be used.";
            return false;
        }

        if (sessionOnly && _sessionActiveCodes.Contains(code))
        {
            error = "This code is already active for this session.";
            return false;
        }

        return true;
    }

    private void RegisterPromoCodeUse(string code, bool sessionOnly)
    {
        _promoCodeUses[code] = GetPromoCodeUseCount(code) + 1;
        if (sessionOnly) _sessionActiveCodes.Add(code);
    }

    private int GetPromoCodeUseCount(string code)
        => _promoCodeUses.GetValueOrDefault(code, 0);

    private void SetCodeStatus(bool success, string message)
    {
        _codeStatusSuccess = success;
        _codeStatusText = message;
    }

    private static string NormalizePromoCode(string input)
        => (input ?? string.Empty).Trim().ToUpperInvariant();

    private static string FormatTime(float timeLeft)
    {
        var total = Math.Max(0, (int)MathF.Ceiling(timeLeft));
        return $"{total / 60:00}:{total % 60:00}";
    }

    private void DrawZoneArrows()
    {
        DrawScreenZoneArrow(_buildings, Palette.C(80, 170, 255), "T");
        DrawScreenZoneArrow(_outposts, Palette.C(245, 90, 90), "O");
        if (_mapMarker is Vector2 marker)
        {
            DrawScreenPointArrow(marker, Palette.C(255, 220, 80), "M");
        }

        if (_destroyerBoss is not null && _destroyerBoss.Alive)
        {
            DrawScreenPointArrow(_destroyerBoss.Position, Palette.C(230, 45, 45), "B");
        }
    }

    private static Rectangle GetMapRect()
    {
        var size = MathF.Min(Raylib.GetScreenWidth() - 140f, Raylib.GetScreenHeight() - 120f);
        size = MathF.Min(size, 620f);
        return new Rectangle(
            Raylib.GetScreenWidth() * 0.5f - size * 0.5f,
            Raylib.GetScreenHeight() * 0.5f - size * 0.5f + 20f,
            size,
            size);
    }

    private Vector2 WorldToMap(Vector2 worldPoint, Rectangle mapRect)
    {
        var scale = mapRect.Width / _worldSize;
        return new Vector2(mapRect.X + worldPoint.X * scale, mapRect.Y + worldPoint.Y * scale);
    }

    private Vector2 MapToWorld(Vector2 mapPoint, Rectangle mapRect)
    {
        var scale = _worldSize / mapRect.Width;
        var world = new Vector2((mapPoint.X - mapRect.X) * scale, (mapPoint.Y - mapRect.Y) * scale);
        return Vector2.Clamp(world, Vector2.Zero, new Vector2(_worldSize, _worldSize));
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
            (new Rectangle(54, 176, 220, 24), "STR", "+5 HP, +1 melee damage and +0.25% melee damage per point."),
            (new Rectangle(54, 206, 220, 24), "DEX", "+1% melee damage and +2% melee attack speed per point."),
            (new Rectangle(54, 236, 220, 24), "SPD", "+4% move speed multiplier per point."),
            (new Rectangle(54, 266, 220, 24), "GUN", "+0.3 flat and +1% ranged damage per point.")
        };

        var hit = hints.FirstOrDefault(h => Raylib.CheckCollisionPointRec(mouse, h.Rect));
        if (string.IsNullOrEmpty(hit.Header)) return;

        var x = (int)mouse.X + 20;
        var y = (int)mouse.Y + 14;
        const int width = 320;
        const int padding = 8;
        const int lineHeight = 19;
        var bodyLines = WrapText(hit.Body, 16, width - padding * 2);
        var height = 42 + bodyLines.Count * lineHeight + padding;
        Raylib.DrawRectangle(x, y, width, height, Palette.C(0, 0, 0, 225));
        Raylib.DrawRectangleLines(x, y, width, height, Color.SkyBlue);
        Raylib.DrawText(hit.Header, x + 8, y + 8, 18, Color.White);
        var lineY = y + 34;
        foreach (var line in bodyLines)
        {
            Raylib.DrawText(line, x + padding, lineY, 16, Color.LightGray);
            lineY += lineHeight;
        }
    }

    private int GetStoredItemCount() => _meta.StorageSlots.Count(item => item is not null);

    private static int GetSellValue(ItemStack item)
    {
        if (item.IsStationKey) return 900;

        if (item.Type == ItemType.Consumable)
        {
            return item.ConsumableKind switch
            {
                ConsumableType.Medkit => 20,
                ConsumableType.ProtectiveDome => 30,
                _ => 15
            };
        }

        return item.Rarity switch
        {
            ArmorRarity.Damaged => 25,
            ArmorRarity.Common => 25,
            ArmorRarity.Rare => 100,
            ArmorRarity.Epic => 500,
            ArmorRarity.Legendary => 2000,
            ArmorRarity.Red => 3000,
            _ => 25
        };
    }

    private static int GetArmoryPrice(ItemStack item)
    {
        var price = item.Rarity == ArmorRarity.Epic ? 2500 : 500;
        if (item.Type == ItemType.Armor)
        {
            price = (int)MathF.Ceiling(price * (1f + GetArmorModifierCount(item) * 0.2f));
        }

        return price;
    }

    private static int GetArmorModifierCount(ItemStack item)
    {
        if (item.Type != ItemType.Armor) return 0;

        var count = 0;
        if (item.SpeedBonusPercent > 0f) count++;
        if (item.ExplosionResistancePercent > 0f) count++;
        if (item.HealingBonusPercent > 0f) count++;
        if (item.DashRecoveryPercent > 0f) count++;
        if (item.ShieldMax > 0f) count++;
        if (item.RegenPercentPerSecond > 0f) count++;
        return count;
    }

    private void RefreshArmoryOffers()
    {
        _meta.ArmoryOffers.Clear();
        for (var i = 0; i < 7; i++) _meta.ArmoryOffers.Add(new ArmoryOffer { Item = RollArmoryEquipment(ArmorRarity.Rare) });
        for (var i = 0; i < 3; i++) _meta.ArmoryOffers.Add(new ArmoryOffer { Item = RollArmoryEquipment(ArmorRarity.Epic) });
    }

    private ItemStack RollArmoryEquipment(ArmorRarity rarity)
    {
        if (_rng.NextSingle() < 0.35f) return ItemStack.Armor(rarity, _rng);
        return ItemStack.Weapon(_rng.NextSingle() < 0.5f ? WeaponClass.Ranged : WeaponClass.Melee, rarity, _rng);
    }

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
            SlotKind.QuickSlotQ => item.Type == ItemType.Consumable && !item.IsStationKey,
            SlotKind.QuickSlotR => item.Type == ItemType.Consumable && !item.IsStationKey,
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

    private void LoadMetaRunBackpackIntoPlayer()
    {
        for (var i = 0; i < Inventory.BackpackCapacity; i++)
        {
            _player.Inventory.BackpackSlots[i] = _meta.RunBackpackSlots[i];
            _meta.RunBackpackSlots[i] = null;
        }
    }

    private SlotKind? GetPreferredLoadoutSlot(ItemStack item)
    {
        if (item.Type == ItemType.Armor) return SlotKind.Armor;
        if (item.Type == ItemType.Weapon && item.WeaponKind == WeaponClass.Ranged) return SlotKind.RangedWeapon;
        if (item.Type == ItemType.Weapon && item.WeaponKind == WeaponClass.Melee) return SlotKind.MeleeWeapon;
        if (item.Type == ItemType.Consumable && !item.IsStationKey) return _meta.QuickSlotQ is null ? SlotKind.QuickSlotQ : SlotKind.QuickSlotR;
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

        var (totalDamage, bonusDamage, dps, label) = GetDisplayedWeaponStats(player, weapon, kind);
        if (weapon.Pattern == WeaponPattern.Toxikus)
        {
            var basePoisonDamage = 30f + weapon.BaseDamage * 0.4f;
            var poisonDamage = 30f + player.GetWeaponDamage(weapon) * 0.4f;
            return $"{label} {totalDamage:0.0}(+{bonusDamage:0.0}) | Poison: {poisonDamage:0.0}(+{poisonDamage - basePoisonDamage:0.0}) | DPS: {dps:0.0}";
        }

        return $"{label} {totalDamage:0.0}(+{bonusDamage:0.0}) | DPS: {dps:0.0}";
    }

    private static (float TotalDamage, float BonusDamage, float Dps, string Label) GetDisplayedWeaponStats(Player player, ItemStack weapon, WeaponClass kind)
    {
        if (weapon.Pattern == WeaponPattern.GrenadeLauncher)
        {
            var baseDirect = weapon.BaseDamage + 150f;
            var totalDirect = player.GetWeaponDamage(weapon) + 150f;
            var directDps = totalDirect * 1.35f;
            return (totalDirect, totalDirect - baseDirect, directDps, "direct");
        }

        if (weapon.Pattern == WeaponPattern.SniperRifle)
        {
            var baseShot = weapon.BaseDamage * 5.55f;
            var shotDamage = player.GetSniperShotDamage(weapon);
            var sniperDps = shotDamage / 1.75f;
            return (shotDamage, shotDamage - baseShot, sniperDps, "shot");
        }

        if (weapon.Pattern is WeaponPattern.PulseRifle or WeaponPattern.Toxikus)
        {
            var basePerShot = weapon.BaseDamage * 0.525f;
            var perShot = player.GetPulseShotDamage(weapon);
            var shots = player.GetPulseBurstShotCount(weapon);
            var cooldown = weapon.Pattern == WeaponPattern.Toxikus ? 1f / 2.2f : 0.374f;
            var poisonDps = weapon.Pattern == WeaponPattern.Toxikus ? 30f + player.GetWeaponDamage(weapon) * 0.4f : 0f;
            var burstDps = perShot * shots / cooldown + poisonDps;
            return (perShot, perShot - basePerShot, burstDps, $"x{shots}");
        }

        if (kind == WeaponClass.Melee)
        {
            var baseHit = weapon.BaseDamage * 7f;
            var hitDamage = player.GetMeleeHitDamage(weapon);
            var cooldown = weapon.Pattern is WeaponPattern.EnergySpear or WeaponPattern.Lancelot
                ? player.GetMeleeCooldown(0.70f)
                : player.GetMeleeCooldown(0.64f);
            return (hitDamage, hitDamage - baseHit, hitDamage / cooldown, weapon.Pattern is WeaponPattern.EnergySpear or WeaponPattern.Lancelot ? "thrust" : "slash");
        }

        var total = player.GetWeaponDamage(weapon);
        var expectedShotsPerAttack = weapon.Rarity == ArmorRarity.Legendary ? 1.33f : 1f;
        var dps = total * expectedShotsPerAttack / 0.22f;
        return (total, total - weapon.BaseDamage, dps, "dmg");
    }

    private (List<LootZone> buildings, List<LootZone> outposts) GenerateZones(int buildingCount, int outpostCount)
    {
        var all = new List<LootZone>();

        PlaceZones(all, buildingCount, false);
        PlaceZones(all, outpostCount, true);

        return (all.Where(x => !x.IsOutpost).ToList(), all.Where(x => x.IsOutpost).ToList());
    }

    private IEnumerable<LootZone> AllZones()
    {
        foreach (var zone in _buildings) yield return zone;
        foreach (var zone in _outposts) yield return zone;
        foreach (var zone in _generatorZones) yield return zone;
        foreach (var zone in _hangars) yield return zone;
        if (_stationZone is not null) yield return _stationZone;
    }

    private void GenerateSpecialZones()
    {
        _generatorZones = [];
        _hangars = [];
        _stationZone = null;
        if (!_currentMap.IsDeadZone) return;

        var station = GetDeadZoneStationRect();
        _stationZone = new LootZone(10000, station, LootZoneKind.Station);

        var all = _buildings.Concat(_outposts).ToList();
        all.Add(_stationZone);
        PlaceSpecialZones(all, 5, LootZoneKind.Generator);
        PlaceSpecialZones(all, _rng.Next(2, 4), LootZoneKind.Hangar);
    }

    private void PlaceSpecialZones(List<LootZone> all, int count, LootZoneKind kind)
    {
        var created = 0;
        var attempts = 0;
        while (created < count && attempts < count * 240)
        {
            attempts++;
            var size = kind == LootZoneKind.Hangar
                ? new Vector2(_rng.Next(1400, 1650), _rng.Next(1400, 1650))
                : new Vector2(_rng.Next(360, 460), _rng.Next(360, 460));
            var pos = new Vector2(_rng.Next(80, _worldSize - (int)size.X - 80), _rng.Next(80, _worldSize - (int)size.Y - 80));
            var rect = new Rectangle(pos, size);
            if (!IsZonePlacementValid(rect, all)) continue;

            var zone = new LootZone(10000 + all.Count, rect, kind);
            all.Add(zone);
            if (kind == LootZoneKind.Hangar) _hangars.Add(zone);
            else _generatorZones.Add(zone);
            created++;
        }
    }

    private void PlaceZones(List<LootZone> all, int count, bool outpost)
    {
        var created = 0;
        var attempts = 0;
        while (created < count && attempts < count * 180)
        {
            attempts++;
            var scale = _currentMap.IsDeadZone ? MathF.Sqrt(1.4f) : 1f;
            var size = outpost
                ? new Vector2(_rng.Next(520, 780) * scale, _rng.Next(520, 780) * scale)
                : new Vector2(_rng.Next(420, 620) * scale, _rng.Next(420, 620) * scale);
            var pos = new Vector2(_rng.Next(80, _worldSize - (int)size.X - 80), _rng.Next(80, _worldSize - (int)size.Y - 80));
            var rect = new Rectangle(pos, size);
            if (!IsZonePlacementValid(rect, all)) continue;
            all.Add(new LootZone(all.Count, rect, outpost));
            created++;
        }
    }

    private bool IsZonePlacementValid(Rectangle rect, List<LootZone> existing)
    {
        var center = new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);
        var worldCenter = new Vector2(_worldSize / 2f, _worldSize / 2f);
        if (Vector2.Distance(center, worldCenter) < CenterNoZoneRadius + MathF.Max(rect.Width, rect.Height) * 0.5f)
        {
            return false;
        }

        if (_currentMap.IsDeadZone && RectDistance(rect, GetDeadZoneStationRect()) < MinZoneGap)
        {
            return false;
        }

        foreach (var zone in existing)
        {
            if (RectDistance(rect, zone.Rect) < MinZoneGap) return false;
        }

        return true;
    }

    private void UpdateStorageSellHold(List<UiSlot> slots, Vector2 mouse)
    {
        if (_drag is not null || !Raylib.IsKeyDown(KeyboardKey.X))
        {
            ResetInventoryUseHold();
            return;
        }

        var slot = slots.FirstOrDefault(s => Raylib.CheckCollisionPointRec(mouse, s.Rect));
        if (slot?.Item is null || slot.Kind == SlotKind.Trash)
        {
            ResetInventoryUseHold();
            return;
        }

        if (slot.Kind != SlotKind.Storage && slot.Kind != SlotKind.RunBackpack && !IsMetaLoadoutSlot(slot.Kind))
        {
            ResetInventoryUseHold();
            return;
        }

        if (_inventoryUseHoldKind != slot.Kind || _inventoryUseHoldIndex != slot.Index)
        {
            _inventoryUseHoldKind = slot.Kind;
            _inventoryUseHoldIndex = slot.Index;
            _inventoryUseHoldTimer = 0f;
        }

        _inventoryUseHoldTimer += Raylib.GetFrameTime();
        if (_inventoryUseHoldTimer < InventoryConsumableUseHoldDuration) return;

        SellStorageSlot(slot);
        ResetInventoryUseHold();
    }

    private void SellStorageSlot(UiSlot slot)
    {
        if (slot.Item is null) return;

        var value = GetSellValue(slot.Item);
        _meta.SynthCoins += value;
        ReplaceStorageSourceWith(new DragPayload(slot.Kind, slot.Index, slot.Item), null);
        SavePersistentState();
        ShowNotice($"Sold {slot.Item.Name} for {value} SynthCoins.");
    }

    private Rectangle GetDeadZoneStationRect()
        => new(_worldSize / 2f - 1500f, _worldSize / 2f - 1000f, 3000f, 2000f);

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

        foreach (var hangar in _hangars)
        {
            var chestCount = _rng.Next(2, 4);
            for (var i = 0; i < chestCount; i++)
            {
                var lootCount = _rng.Next(1, 4);
                var loot = new List<ItemStack>();
                for (var l = 0; l < lootCount; l++) loot.Add(RollHangarLoot());
                list.Add(new LootChest(RandomPointInZoneSafe(hangar.Rect, 20f), loot, hangar.Id, LootContainerKind.Chest));
            }
        }

        return list;
    }

    private ItemStack RollLoot(bool isOutpost)
    {
        var r = _rng.NextSingle();
        if (_currentMap.IsDeadZone)
        {
            if (isOutpost)
            {
                if (r < 0.20f) return ItemStack.Consumable(RollConsumableType());
                if (r < 0.60f) return RollEquipmentOfRarity(ArmorRarity.Common);
                if (r < 0.98f) return RollEquipmentOfRarity(ArmorRarity.Rare);
                return RollEquipmentOfRarity(ArmorRarity.Epic);
            }

            if (r < 0.40f) return ItemStack.Consumable(RollConsumableType());
            if (r < 0.90f) return RollEquipmentOfRarity(ArmorRarity.Common);
            return RollEquipmentOfRarity(ArmorRarity.Rare);
        }

        if (_selectedMapName.Equals("Baselands", StringComparison.OrdinalIgnoreCase))
        {
            if (isOutpost)
            {
                if (r < 0.25f) return ItemStack.Consumable(RollConsumableType());
                if (r < 0.75f) return RollEquipmentOfRarity(ArmorRarity.Common);
                if (r < 0.995f) return RollEquipmentOfRarity(ArmorRarity.Rare);
                return RollEquipmentOfRarity(ArmorRarity.Epic);
            }

            if (r < 0.395f) return ItemStack.Consumable(RollConsumableType());
            if (r < 0.97f) return RollEquipmentOfRarity(ArmorRarity.Common);
            return RollEquipmentOfRarity(ArmorRarity.Rare);
        }

        if (r < 0.35f) return ItemStack.Consumable(RollConsumableType());

        var rarity = RollRarity(isOutpost);
        return RollEquipmentOfRarity(rarity);
    }

    private ItemStack RollHangarLoot()
    {
        var r = _rng.NextSingle();
        if (r < 0.22f) return ItemStack.Consumable(RollConsumableType());
        if (r < 0.42f) return RollEquipmentOfRarity(ArmorRarity.Common);
        if (r < 0.92f) return RollEquipmentOfRarity(ArmorRarity.Rare);
        if (r < 0.96f) return RollEquipmentOfRarity(ArmorRarity.Epic);
        if (r < 0.98f) return ItemStack.Toxikus(_rng);
        return RollEquipmentOfRarity(ArmorRarity.Rare);
    }

    private ItemStack RollStationCrateLoot()
    {
        var r = _rng.NextSingle();
        if (r < 0.40f) return ItemStack.Consumable(RollConsumableType());
        if (r < 0.70f) return RollEquipmentOfRarity(ArmorRarity.Common);
        return RollEquipmentOfRarity(ArmorRarity.Rare);
    }

    private ArmorRarity RollRarity(bool isOutpost)
    {
        var r = _rng.NextSingle();

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
    {
        var roll = _rng.NextSingle();
        if (roll < 0.25f) return ConsumableType.Medkit;
        if (roll < 0.5f) return ConsumableType.Stim;
        if (roll < 0.75f) return ConsumableType.ProtectiveDome;
        return ConsumableType.StickyBullets;
    }

    private List<ItemStack> RollBossLoot()
    {
        var loot = new List<ItemStack> { RollEquipmentOfRarity(ArmorRarity.Epic) };
        if (_rng.NextSingle() < 0.01f) loot.Add(RollEquipmentOfRarity(ArmorRarity.Legendary));
        if (_rng.NextSingle() < 0.025f) loot.Add(ItemStack.BossGrenadeLauncher());
        return loot;
    }

    private List<ItemStack> RollStationBossLoot()
    {
        var secondRarity = _rng.NextSingle() < 0.05f ? ArmorRarity.Legendary : ArmorRarity.Epic;
        var loot = new List<ItemStack>
        {
            RollEquipmentOfRarity(ArmorRarity.Epic),
            RollEquipmentOfRarity(secondRarity)
        };
        if (_rng.NextSingle() < 0.20f) loot.Add(ItemStack.Lancelot(_rng));
        return loot;
    }

    private List<ItemStack> RollMiniBossLoot()
    {
        var loot = new List<ItemStack> { RollEquipmentOfRarity(_rng.NextSingle() < 0.5f ? ArmorRarity.Rare : ArmorRarity.Common) };
        if (_rng.NextSingle() < 0.25f) loot.Add(ItemStack.Consumable(RollConsumableType()));
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
        if (_generatorGuards.Any(guard => guard.Alive && guard.ZoneId == zoneId)) return false;
        if (_toxicEnemies.Any(enemy => enemy.Alive && enemy.ZoneId == zoneId)) return false;
        return true;
    }

    private void TryDropEnemyConsumable(Vector2 position)
    {
        if (_rng.NextSingle() >= 0.01f) return;
        _groundConsumables.Add(new GroundConsumablePickup(position, ItemStack.Consumable(RollConsumableType())));
    }

    private void TryDropStationKey(Vector2 position)
    {
        if (_rng.NextSingle() >= 0.05f) return;
        _groundConsumables.Add(new GroundConsumablePickup(position, ItemStack.StationKey()));
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
            var point = new Vector2(_rng.Next(50, _worldSize - 50), _rng.Next(50, _worldSize - 50));
            if (MovementUtils.CircleHitsObstacle(point, radius, _obstacles)) continue;
            return point;
        }

        return new Vector2(_worldSize / 2f, _worldSize / 2f);
    }

    private Vector2 RandomOutdoorPoint(float radius = 14f)
    {
        while (true)
        {
            var point = new Vector2(_rng.Next(100, _worldSize - 100), _rng.Next(100, _worldSize - 100));
            if (AllZones().Any(z => Raylib.CheckCollisionPointRec(point, z.Rect))) continue;
            if (MovementUtils.CircleHitsObstacle(point, radius, _obstacles)) continue;
            return point;
        }
    }

    private Vector2 RandomExtractionPortalPoint(float radius)
    {
        for (var i = 0; i < 400; i++)
        {
            var point = RandomOutdoorPoint(radius);
            if (IsPointInAnyZone(point, radius + 20f)) continue;
            if (!IsOutsideCurrentScreen(point, 80f)) continue;
            return point;
        }

        return RandomOutdoorPoint(radius);
    }

    private bool IsPointInAnyZone(Vector2 point, float margin)
        => AllZones().Any(zone => Raylib.CheckCollisionPointRec(point, ExpandRect(zone.Rect, margin)));

    private static Rectangle ExpandRect(Rectangle rect, float margin)
        => new(rect.X - margin, rect.Y - margin, rect.Width + margin * 2f, rect.Height + margin * 2f);

    private bool IsOutsideCurrentScreen(Vector2 point, float margin)
    {
        var screen = Raylib.GetWorldToScreen2D(point, _camera);
        return screen.X < -margin
            || screen.Y < -margin
            || screen.X > Raylib.GetScreenWidth() + margin
            || screen.Y > Raylib.GetScreenHeight() + margin;
    }

    private List<Obstacle> GenerateObstacles()
    {
        var list = new List<Obstacle>();

        foreach (var zone in _buildings.Concat(_outposts).Concat(_hangars))
        {
            var count = zone.Kind switch
            {
                LootZoneKind.Hangar => _rng.Next(14, 20),
                LootZoneKind.Outpost => _rng.Next(_currentMap.IsDeadZone ? 9 : 6, _currentMap.IsDeadZone ? 14 : 10),
                _ => _rng.Next(_currentMap.IsDeadZone ? 6 : 4, _currentMap.IsDeadZone ? 10 : 7)
            };
            for (var i = 0; i < count; i++)
            {
                var tries = 0;
                while (tries++ < 60)
                {
                    var w = zone.Kind == LootZoneKind.Hangar
                        ? _rng.Next(96, 180)
                        : zone.IsOutpost ? _rng.Next(70, 128) : _rng.Next(52, 96);
                    var h = zone.Kind == LootZoneKind.Hangar
                        ? _rng.Next(96, 180)
                        : zone.IsOutpost ? _rng.Next(70, 128) : _rng.Next(52, 96);
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

    private void GenerateDeadZoneSetPieces()
    {
        if (!_currentMap.IsDeadZone) return;

        foreach (var zone in _generatorZones)
        {
            _generators.Add(new GeneratorNode(zone.Center, zone.Id));
        }

        foreach (var hangar in _hangars)
        {
            AddHangarWalls(hangar.Rect);
            var clusterCenters = new List<Vector2>();
            var clusters = _rng.Next(8, 12);
            for (var i = 0; i < clusters; i++)
            {
                var center = RandomToxicClusterCenter(hangar.Rect, clusterCenters);
                clusterCenters.Add(center);
                var blobs = _rng.Next(9, 13);
                for (var b = 0; b < blobs; b++)
                {
                    var offset = new Vector2(_rng.Next(-46, 47), _rng.Next(-34, 35));
                    _toxicPools.Add(new ToxicPool(center + offset, _rng.Next(44, 86), _rng.Next(30, 74)));
                }
            }
        }

        if (_stationZone is not null)
        {
            AddStationLayout(_stationZone.Rect);
        }
    }

    private Vector2 RandomToxicClusterCenter(Rectangle rect, List<Vector2> existing)
    {
        const float minDistance = 260f;
        for (var i = 0; i < 80; i++)
        {
            var point = RandomPointInZoneSafe(rect, 130f);
            if (existing.All(other => Vector2.DistanceSquared(point, other) >= minDistance * minDistance)) return point;
        }

        return RandomPointInZoneSafe(rect, 130f);
    }

    private void AddHangarWalls(Rectangle rect)
    {
        const float wall = 28f;
        var gap = rect.Width * 0.22f;
        var gapX = rect.X + rect.Width * 0.5f - gap * 0.5f;

        _obstacles.Add(new Obstacle(new Rectangle(rect.X, rect.Y, wall, rect.Height)));
        _obstacles.Add(new Obstacle(new Rectangle(rect.X + rect.Width - wall, rect.Y, wall, rect.Height)));
        _obstacles.Add(new Obstacle(new Rectangle(rect.X, rect.Y, gapX - rect.X, wall)));
        _obstacles.Add(new Obstacle(new Rectangle(gapX + gap, rect.Y, rect.X + rect.Width - gapX - gap, wall)));
        _obstacles.Add(new Obstacle(new Rectangle(rect.X, rect.Y + rect.Height - wall, gapX - rect.X, wall)));
        _obstacles.Add(new Obstacle(new Rectangle(gapX + gap, rect.Y + rect.Height - wall, rect.X + rect.Width - gapX - gap, wall)));
    }

    private void AddStationLayout(Rectangle rect)
    {
        const float wall = 26f;
        var x = rect.X;
        var y = rect.Y;
        var w = rect.Width;
        var h = rect.Height;

        void AddWall(float wx, float wy, float ww, float wh)
            => _obstacles.Add(new Obstacle(new Rectangle(wx, wy, ww, wh)));

        var rawRows = new[]
        {
            "#############################################################################################",
            "# G                           #                #                                            #",
            "#                             #                #                                            #",
            "#                                                                                           #",
            "####   ######                                                                               #",
            "#           #                 #                #                                            #",
            "# G         #                 #                #                                            #",
            "##################################             #                                            #",
            "#          #          #       G  #             #                                            #",
            "#          #          #          #             #                                            #",
            "#                     #          #             #                                            #",
            "#          #          ###     #########   ######                                            #",
            "# G        #          #                        #                                            #",
            "###############     ###                        #                                            #",
            "#          #                     #######   #####                                            #",
            "#                                #             #                                            #",
            "#                                #             #                                            #",
            "# G        #                     #             #                  BOSS                      #",
            "###############    ###############   ###########                                            #",
            "#          #           # G       #             #                                            #",
            "#          #           #                       #                                            #",
            "#                      #                       #                                            #",
            "#                      #         #           G #                                            #",
            "###############    ###############   ###########                                            #",
            "#          #           #G        #             #                                            #",
            "#          #           #         #             #                                            #",
            "#                      #         #             #                                            #",
            "#                      #         #           G #                                            #",
            "#          #           ####   ##################                                            #",
            "#          #                     #             #                                            #",
            "#        G #                     #             #                                            #",
            "####   #####                                   #                                            #",
            "#          #                                   #                                            #",
            "#          #                     #             #                                            #",
            "# G        # G                   #             #                                            #",
            "###################################    ######################################################"
        };

        var cols = rawRows.Max(row => row.Length);
        var rows = rawRows
            .Select(row => (row.EndsWith('#') ? row[..^1] : row).PadRight(cols - 1) + "#")
            .ToArray();
        var cellW = w / (cols - 1);
        var cellH = h / (rows.Length - 1);
        var bossRow = Array.FindIndex(rows, row => row.Contains("BOSS", StringComparison.Ordinal));
        var bossTextCol = bossRow >= 0 ? rows[bossRow].IndexOf("BOSS", StringComparison.Ordinal) : -1;
        var bossWallCol = bossTextCol > 0 ? rows[bossRow].LastIndexOf('#', bossTextCol - 1) : -1;
        bossWallCol = Math.Clamp(bossWallCol > 1 ? bossWallCol : cols / 2, 1, cols - 2);

        float GridX(int col) => x + col * cellW;
        float GridY(int row) => y + row * cellH;
        float WallX(int col) => Math.Clamp(GridX(col) - wall * 0.5f, x, x + w - wall);
        float WallY(int row) => Math.Clamp(GridY(row) - wall * 0.5f, y, y + h - wall);

        void AddGridHorizontal(int row, int startCol, int endCol)
        {
            var left = GridX(startCol);
            var right = GridX(Math.Min(endCol + 1, cols - 1));
            AddWall(left, WallY(row), MathF.Max(wall, right - left), wall);
        }

        void AddGridVertical(int col, int startRow, int endRow)
        {
            var top = GridY(startRow);
            var bottom = GridY(Math.Min(endRow + 1, rows.Length - 1));
            AddWall(WallX(col), top, wall, MathF.Max(wall, bottom - top));
        }

        for (var row = 0; row < rows.Length; row++)
        {
            var start = -1;
            for (var col = 0; col < cols; col++)
            {
                var isWall = rows[row][col] == '#';
                if (isWall && start < 0) start = col;
                if ((!isWall || col == cols - 1) && start >= 0)
                {
                    var end = isWall && col == cols - 1 ? col : col - 1;
                    if (end - start >= 1) AddGridHorizontal(row, start, end);
                    start = -1;
                }
            }
        }

        for (var col = 0; col < cols; col++)
        {
            var start = -1;
            for (var row = 0; row < rows.Length; row++)
            {
                var isWall = rows[row][col] == '#';
                if (isWall && start < 0) start = row;
                if ((!isWall || row == rows.Length - 1) && start >= 0)
                {
                    var end = isWall && row == rows.Length - 1 ? row : row - 1;
                    if (end - start >= 1) AddGridVertical(col, start, end);
                    start = -1;
                }
            }
        }

        var bottomRow = rows[^1];
        var entranceStartCol = bottomRow.IndexOf(' ');
        var entranceEndCol = entranceStartCol;
        while (entranceEndCol < cols && bottomRow[entranceEndCol] == ' ') entranceEndCol++;

        var entranceX = GridX(entranceStartCol);
        var entranceWidth = GridX(entranceEndCol) - entranceX;
        _stationEntranceDoor = new Rectangle(entranceX, y + h - wall, entranceWidth, wall);
        _obstacles.Add(new Obstacle(_stationEntranceDoor.Value));

        var bossDoorRow = 3;
        var bossDoorHeightRows = 2.2f;
        var bossWallX = WallX(bossWallCol);
        var bossDoorY = GridY(bossDoorRow);
        var bossDoorHeight = cellH * bossDoorHeightRows;
        AddWall(bossWallX, y, wall, MathF.Max(wall, bossDoorY - y));
        AddWall(bossWallX, bossDoorY + bossDoorHeight, wall, MathF.Max(wall, y + h - bossDoorY - bossDoorHeight));
        _stationBossDoor = new Rectangle(bossWallX, bossDoorY, wall, bossDoorHeight);
        _stationBossArena = new Rectangle(bossWallX + wall, y + wall, x + w - bossWallX - wall * 2f, h - wall * 2f);

        var potentialCrates = new List<Vector2>();
        for (var row = 0; row < rows.Length; row++)
        {
            for (var col = 0; col < cols; col++)
            {
                if (rows[row][col] == 'G') potentialCrates.Add(new Vector2(GridX(col), GridY(row)));
            }
        }

        foreach (var pos in potentialCrates.OrderBy(_ => _rng.Next()).Take(_rng.Next(4, 8)))
        {
            _chests.Add(new LootChest(pos, new List<ItemStack> { RollStationCrateLoot() }, null, LootContainerKind.Crate));
        }
    }

    private List<Enemy> GenerateEnemies()
    {
        var list = new List<Enemy>();
        var enhanced = _currentMap.IsDeadZone;

        foreach (var b in _buildings)
        {
            var count = ScaleDeadZoneEnemyCount(_rng.Next(2, 4));
            for (var i = 0; i < count; i++)
            {
                var patrolA = RandomPointInZoneSafe(b.Rect, 14f);
                var patrolB = RandomPointInZoneSafe(b.Rect, 14f);
                list.Add(Enemy.CreatePatrol(patrolA, patrolB, false, b.Id, enhanced));
            }

            var strongCount = ScaleDeadZoneEnemyCount(_rng.Next(1, 3));
            for (var i = 0; i < strongCount; i++)
            {
                list.Add(Enemy.CreateStrong(RandomPointInZoneSafe(b.Rect, 14f), b.Id, enhanced));
            }
        }

        foreach (var o in _outposts)
        {
            var count = ScaleDeadZoneEnemyCount(_rng.Next(5, 8));
            for (var i = 0; i < count; i++)
            {
                var patrolA = RandomPointInZoneSafe(o.Rect, 14f);
                var patrolB = RandomPointInZoneSafe(o.Rect, 14f);
                list.Add(Enemy.CreatePatrol(patrolA, patrolB, true, o.Id, enhanced));
            }
            var strong = ScaleDeadZoneEnemyCount(_rng.Next(3, 5));
            for (var i = 0; i < strong; i++) list.Add(Enemy.CreateStrong(RandomPointInZoneSafe(o.Rect, 14f), o.Id, enhanced));
        }

        var outdoorPatrols = _rng.Next(12, 19);
        for (var i = 0; i < outdoorPatrols; i++)
        {
            var patrolA = RandomOutdoorPoint();
            var patrolB = patrolA + new Vector2(_rng.Next(-160, 161), _rng.Next(-160, 161));
            patrolB = Vector2.Clamp(patrolB, new Vector2(40f, 40f), new Vector2(_worldSize - 40f, _worldSize - 40f));
            if (MovementUtils.CircleHitsObstacle(patrolB, 14f, _obstacles)) patrolB = patrolA;
            list.Add(Enemy.CreatePatrol(patrolA, patrolB, false, enhanced: enhanced));
        }

        var outdoorStrong = _currentMap.IsDeadZone ? _rng.Next(18, 29) : _rng.Next(6, 11);
        for (var i = 0; i < outdoorStrong; i++) list.Add(Enemy.CreateStrong(RandomOutdoorPoint(), enhanced: enhanced));

        var outdoorGuards = _rng.Next(10, 17);
        for (var i = 0; i < outdoorGuards; i++)
        {
            var point = RandomOutdoorPoint();
            list.Add(Enemy.CreatePatrol(point, point, false, enhanced: enhanced));
        }

        return list;
    }

    private int ScaleDeadZoneEnemyCount(int count)
        => _currentMap.IsDeadZone ? Math.Max(1, (int)MathF.Ceiling(count * 1.5f)) : count;


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

    private List<GeneratorGuardianEnemy> GenerateGeneratorGuards()
    {
        var list = new List<GeneratorGuardianEnemy>();
        foreach (var zone in _generatorZones)
        {
            list.Add(new GeneratorGuardianEnemy(zone.Center + new Vector2(70f, 0f), zone.Id));
        }

        return list;
    }

    private List<ToxicTriangleEnemy> GenerateToxicEnemies()
    {
        var list = new List<ToxicTriangleEnemy>();
        foreach (var hangar in _hangars)
        {
            var count = _rng.Next(5, 11);
            for (var i = 0; i < count; i++)
            {
                list.Add(new ToxicTriangleEnemy(RandomPointInZoneSafe(hangar.Rect, 16f), hangar.Id));
            }
        }

        return list;
    }

    private StationBossEnemy? GenerateStationBoss()
    {
        if (!_currentMap.IsDeadZone || _stationBossArena is not Rectangle arena) return null;
        return new StationBossEnemy(new Vector2(arena.X + arena.Width * 0.5f, arena.Y + arena.Height * 0.5f), arena);
    }

    private Vector2 GeneratePlayerSpawnPoint()
    {
        for (var i = 0; i < 200; i++)
        {
            var point = RandomOutdoorPoint(16f);
            if (Vector2.Distance(point, new Vector2(_worldSize / 2f, _worldSize / 2f)) >= CenterNoZoneRadius + 250f)
            {
                return point;
            }
        }

        return new Vector2(_worldSize / 2f, CenterNoZoneRadius + 250f);
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
        => new(new Vector2(_worldSize / 2f, _worldSize / 2f));


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
