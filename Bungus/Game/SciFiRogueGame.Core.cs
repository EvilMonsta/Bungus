using System.Numerics;
using System.Text.Json;
using Raylib_cs;

namespace Bungus.Game;

public sealed partial class SciFiRogueGame : IDisposable
{
    private const int W = 1280;
    private const int H = 720;
    private const int WindowedDesignW = 1920;
    private const int WindowedDesignH = 1080;
    private const float MinZoneGap = 300f;
    private const float CenterNoZoneRadius = 850f;
    private const float PlayerSpawnMinEnemyDistance = 1000f;
    private const float RunIntroFadeInDuration = 1f;
    private const float RunIntroHoldDuration = 2f;
    private const float RunIntroFadeOutDuration = 1f;
    private const float RunIntroDuration = RunIntroFadeInDuration + RunIntroHoldDuration + RunIntroFadeOutDuration;
    private const int BunkerWorldSize = 4000;
    private const float BunkerWallThickness = 8f;
    private const float BunkerDoorLength = 100f;
    private static readonly Vector2 BunkerSpawnPosition = new(300f, 300f);
    private static readonly Vector2 BunkerEntranceHatchPosition = new(100f, 300f);
    private static readonly Vector2 BunkerExitHatchPosition = new(3700f, 3700f);
    private static readonly Vector2 BunkerSecondarySpawnPosition = new(3550f, 3700f);
    private const int ProtectedSaveVersion = 2;
    private const float UiSlotSize = 87f;
    private const float UiSlotStep = 88f;
    private const float UiIconPadding = 9f;
    private const int StashGridColumns = 5;
    private const int StashVisibleRows = 5;
    private const int StorageSortButtonCount = 8;
    private static readonly string SaveFilePath = Path.Combine(AppContext.BaseDirectory, "save", "profile.json");
    private static readonly JsonSerializerOptions SaveJsonOptions = new() { WriteIndented = true };
    private static readonly CradleTrack[] CradleTracks =
    [
        CradleTrack.Health,
        CradleTrack.Speed,
        CradleTrack.MeleeSpeed,
        CradleTrack.DashRecovery,
        CradleTrack.Stability,
        CradleTrack.Gunsmith,
        CradleTrack.Fighter,
        CradleTrack.Arcane
    ];

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
    private List<BeamEffect> _beamEffects = [];
    private List<LightningEffect> _lightningEffects = [];
    private List<SwingArc> _swings = [];
    private List<DashAfterImage> _dashAfterImages = [];
    private List<MotionAfterImage> _motionAfterImages = [];
    private readonly Dictionary<string, Texture2D> _iconTextures = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _missingIconTextures = new(StringComparer.OrdinalIgnoreCase);

    private List<LootZone> _buildings = [];
    private List<LootZone> _outposts = [];
    private List<LootZone> _generatorZones = [];
    private List<LootZone> _hangars = [];
    private LootZone? _stationZone;
    private List<Obstacle> _obstacles = [];
    private List<LootChest> _chests = [];
    private List<GroundConsumablePickup> _groundConsumables = [];
    private List<ProtectiveDome> _protectiveDomes = [];
    private List<FreezeZone> _freezeZones = [];
    private List<MidaMiniTurret> _midaMiniTurrets = [];
    private List<ProtectiveDome> _bunkerProtectiveDomes = [];
    private List<FreezeZone> _bunkerFreezeZones = [];
    private List<MidaMiniTurret> _bunkerMidaMiniTurrets = [];
    private readonly Dictionary<object, float> _frozenTargets = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object, float> _chilledTargets = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object, float> _radioactiveDecompositionTargets = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object, float> _radioactiveDecompositionDamageMultipliers = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object, float> _poisonVisualTargets = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object, float> _slowVisualTargets = new(ReferenceEqualityComparer.Instance);
    private List<GeneratorNode> _generators = [];
    private List<ToxicPool> _toxicPools = [];
    private SecuredTerminalZone? _securedTerminalZone;
    private readonly List<TerminalNote> _terminalNotes = [];
    private readonly bool[] _terminalNotesRead = [false, false];
    private bool _terminalOpen;
    private int? _openTerminalNoteIndex;
    private string _terminalInput = string.Empty;
    private string _terminalScreenText = "ACCESS DENIED";

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
    private Vector2? _bunkerMapMarker;
    private int _storageScrollRow;
    private int _storageSortMode = -1;
    private readonly HashSet<(SlotKind Kind, int Index)> _selectedStorageSlots = [];
    private bool _requestExit;
    private readonly List<VisualTheme> _themes;
    private int _themeIndex;
    private DisplayMode _displayMode;
    private AntialiasingMode _antialiasingMode = AntialiasingMode.Msaa4x;
    private TextureFilteringMode _textureFilteringMode = TextureFilteringMode.Bilinear;
    private bool _vsyncEnabled;
    private int _targetFps = 60;
    private static DisplayMode s_activeDisplayMode = DisplayMode.Windowed;
    private float _nextHexSpawnTimer;
    private readonly MetaProfile _meta = new();
    private readonly List<ExtractPortal> _extractPortals = [];
    private string _selectedMapName = "Baselands";
    private MapDefinition _currentMap = MapDefinition.Baselands;
    private int _worldSize = MapDefinition.Baselands.WorldSize;
    private DeploymentListMode _deploymentListMode = DeploymentListMode.Expeditions;
    private bool _challengeMode;
    private ChallengeKind _challengeKind = ChallengeKind.None;
    private int _pitNextWave = 1;
    private float _pitWaveTimer;
    private readonly Dictionary<object, int> _pitEnemyWaves = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<int> _pitCompletedWaves = [];
    private readonly List<ItemStack> _pitRewardOffers = [];
    private readonly List<List<ItemStack>> _pitRouletteItems = [];
    private readonly bool[] _pitRewardClaimed = [false, false, false, false];
    private bool _pitRewardOpen;
    private float _pitRewardSpinElapsed;
    private readonly float[] _pitConsumableSpawnTimers = [0f, 0f];
    private readonly GroundConsumablePickup?[] _pitConsumablePickups = [null, null];
    private Vector2[] _pitConsumableSpawnPoints = [];
    private int _pitRunXpEarned;
    private int _pitRunCoinsEarned;
    private int _pitRunTokensEarned;
    private float _pitNightmareDamageBonusPercent;
    private float _pitNightmareHealthBonusPercent;
    private float _pitNightmareSpeedBonusPercent;
    private bool _pitNightmarePortalActive;
    private bool _pitNightmareInfoOpen;
    private bool _pitDifficultyOpen;
    private float _pitDifficultySpinElapsed;
    private PitDifficultyOffer _pitDifficultyOffer;
    private readonly List<PitDifficultyOffer> _pitDifficultyRouletteItems = [];
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
    private bool _aboutPopupOpen;
    private bool _changelogPopupOpen;
    private float _changelogScroll;
    private readonly List<(string Text, bool Version)> _changelogLines = [];
    private string _codeInput = string.Empty;
    private string _codeStatusText = string.Empty;
    private bool _codeStatusSuccess;
    private float _runIntroTimer;
    private bool _inBunker;
    private Vector2 _surfaceReturnPosition;
    private Vector2 _secondaryBunkerHatchPosition;
    private bool _secondaryBunkerHatchUnlocked;
    private bool _toBunkerNextRun;
    private List<BunkerRoom> _bunkerRooms = [];
    private List<BunkerDoor> _bunkerDoors = [];
    private List<Obstacle> _bunkerObstacles = [];
    private readonly HashSet<int> _revealedBunkerRooms = [];
    private BunkerTyrant? _bunkerTyrant;
    private readonly List<BunkerScrib> _bunkerScribs = [];
    private readonly List<BunkerParasite> _bunkerParasites = [];
    private readonly List<BunkerToxicCloud> _bunkerToxicClouds = [];
    private readonly List<BunkerSiegeEnemy> _bunkerSiegeEnemies = [];
    private readonly List<BunkerAssaultEnemy> _bunkerAssaultEnemies = [];
    private readonly List<BunkerInfectedEnemy> _bunkerInfectedEnemies = [];
    private readonly List<BunkerInfectedCloud> _bunkerInfectedClouds = [];
    private readonly HashSet<LootChest> _bunkerChests = [];
    private readonly bool[] _bunkerTyrantSwitches = new bool[4];
    private bool _bunkerTyrantFightStarted;
    private bool _bunkerTyrantRewardDropped;
    private float _bunkerTyrantDoorSealTimer = -1f;
    private bool _bunkerTyrantArenaObstaclesDestroyed;
    private ItemStack? _bunkerTyrantDrop;
    private static readonly Vector2[] BunkerTyrantSwitchPositions =
    [
        new(1100f, 2700f), new(3100f, 2700f), new(1100f, 3900f), new(3100f, 3900f)
    ];
    private static readonly Vector2 BunkerTyrantLeftSpawn = new(1100f, 3300f);
    private static readonly Vector2 BunkerTyrantRightSpawn = new(3100f, 3300f);

    private static readonly Rectangle TakeAllButtonRect = new(740, 318, 220, 34);
    private static readonly float[] PitRewardSpinDurations = [3f, 4f, 5f, 6f];
    private const float PitDifficultySpinDuration = 3f;
    private const float InventoryConsumableUseHoldDuration = 1f;
    private const int ArmoryOfferCount = 18;
    private const int ArmoryConsumableRowCount = 6;
    private readonly record struct PitDifficultyOffer(char Kind, float Percent);
    private readonly record struct EnemyTarget(object Target, Vector2 Position, float Radius);
    private readonly record struct BunkerRoom(int Id, Rectangle Rect);
    private sealed class BunkerDoor(int roomA, int roomB, Rectangle rect)
    {
        public int RoomA { get; } = roomA;
        public int RoomB { get; } = roomB;
        public Rectangle Rect { get; } = rect;
        public bool Open { get; set; }
        public Vector2 Center => new(Rect.X + Rect.Width * 0.5f, Rect.Y + Rect.Height * 0.5f);
    }
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
        _antialiasingMode = LoadStartupAntialiasingMode();
        _vsyncEnabled = LoadStartupVSyncEnabled();
        if (_antialiasingMode == AntialiasingMode.Msaa4x && _vsyncEnabled) Raylib.SetConfigFlags(ConfigFlags.Msaa4xHint | ConfigFlags.VSyncHint);
        else if (_antialiasingMode == AntialiasingMode.Msaa4x) Raylib.SetConfigFlags(ConfigFlags.Msaa4xHint);
        else if (_vsyncEnabled) Raylib.SetConfigFlags(ConfigFlags.VSyncHint);
        Raylib.InitWindow(W, H, "Bungus");
        _targetFps = LoadStartupTargetFps();
        Raylib.SetTargetFPS(_targetFps);
        Raylib.SetExitKey(KeyboardKey.Null);

        _camera = new Camera2D { Zoom = 1.08f, Rotation = 0f };
        _themes = BuildThemes();
        LoadPersistentState();
    }

    private VisualTheme Theme => _themes[_themeIndex];

    private static bool IsUiScaledWindowed => s_activeDisplayMode == DisplayMode.Windowed;

    private static int GetUiScreenWidth() => IsUiScaledWindowed ? WindowedDesignW : Raylib.GetScreenWidth();

    private static int GetUiScreenHeight() => IsUiScaledWindowed ? WindowedDesignH : Raylib.GetScreenHeight();

    private static Vector2 GetUiScreenCenter() => new(GetUiScreenWidth() / 2f, GetUiScreenHeight() / 2f);

    private static float GetUiScale()
    {
        if (!IsUiScaledWindowed) return 1f;
        return MathF.Min(Raylib.GetScreenWidth() / (float)WindowedDesignW, Raylib.GetScreenHeight() / (float)WindowedDesignH);
    }

    private static Vector2 GetUiOffset()
    {
        var scale = GetUiScale();
        return new Vector2(
            (Raylib.GetScreenWidth() - GetUiScreenWidth() * scale) * 0.5f,
            (Raylib.GetScreenHeight() - GetUiScreenHeight() * scale) * 0.5f);
    }

    private static Vector2 GetUiMousePosition()
    {
        var scale = MathF.Max(0.001f, GetUiScale());
        return (Raylib.GetMousePosition() - GetUiOffset()) / scale;
    }

    private void StartRun(string mapName)
    {
        _challengeMode = false;
        _challengeKind = ChallengeKind.None;
        _pitRewardOpen = false;
        _pitRewardOffers.Clear();
        _pitRouletteItems.Clear();
        _pitEnemyWaves.Clear();
        _pitCompletedWaves.Clear();
        _pitRunXpEarned = 0;
        _pitRunCoinsEarned = 0;
        _pitRunTokensEarned = 0;
        ResetPitNightmareState();
        _currentMap = MapDefinition.All.FirstOrDefault(m => m.Name.Equals(mapName, StringComparison.OrdinalIgnoreCase)) ?? MapDefinition.Baselands;
        _selectedMapName = _currentMap.Name;
        _worldSize = _currentMap.WorldSize;
        (_buildings, _outposts) = GenerateZones(_rng.Next(_currentMap.BuildingMin, _currentMap.BuildingMaxExclusive), _rng.Next(_currentMap.OutpostMin, _currentMap.OutpostMaxExclusive));
        GenerateSpecialZones();
        _obstacles = GenerateObstacles();
        _chests = GenerateChestsInZones();
        _groundConsumables = [];
        _protectiveDomes = [];
        _freezeZones = [];
        _midaMiniTurrets = [];
        _bunkerProtectiveDomes = [];
        _bunkerFreezeZones = [];
        _bunkerMidaMiniTurrets = [];
        _frozenTargets.Clear();
        _chilledTargets.Clear();
        _radioactiveDecompositionTargets.Clear();
        _radioactiveDecompositionDamageMultipliers.Clear();
        _poisonVisualTargets.Clear();
        _slowVisualTargets.Clear();
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
        GenerateSecuredTerminalContent();
        MovementUtils.WarmObstacleIndex(_obstacles);
        _projectiles = [];
        _explosions = [];
        _beamEffects = [];
        _lightningEffects = [];
        _swings = [];
        _enemies = GenerateEnemies();
        _hexEnemies = [];
        _turrets = GenerateTurrets();
        _miniBosses = GenerateMiniBosses();
        _destroyerBoss = _currentMap.IsDeadZone ? null : GenerateDestroyerBoss();
        _generatorGuards = GenerateGeneratorGuards();
        _toxicEnemies = GenerateToxicEnemies();
        _stationBoss = GenerateStationBoss();
        _player = Player.Create(
            GeneratePlayerSpawnPoint(),
            GetCommonHealthBonus(),
            GetCommonDamageBonus(),
            0,
            0,
            0,
            0,
            _meta.CradleSpeed,
            _meta.CradleMeleeSpeed,
            _meta.CradleDashRecovery,
            _meta.CradleStability,
            _meta.CradleGunsmith,
            _meta.CradleFighter,
            _meta.CradleArcane,
            TakeMetaLoadoutItem(SlotKind.RangedWeapon),
            TakeMetaLoadoutItem(SlotKind.HeavyWeapon),
            TakeMetaLoadoutItem(SlotKind.MeleeWeapon),
            TakeMetaLoadoutItem(SlotKind.Armor),
            TakeMetaLoadoutItem(SlotKind.QuickSlotQ),
            TakeMetaLoadoutItem(SlotKind.QuickSlotR));
        ApplyIsFunnyNextRunBonus();
        PreloadGameplayTextures();
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
        _bunkerMapMarker = null;
        _drag = null;
        _hovered = null;
        _inBunker = false;
        _surfaceReturnPosition = Vector2.Zero;
        InitializeBunkerLayout();
        ClearPendingLevelUpPoints();
        LoadMetaRunBackpackIntoPlayer();
        ApplyToBunkerNextRunBonus();

        _camera.Offset = GetUiScreenCenter();
        _camera.Target = _player.Position;
        StartRunIntro();
        SavePersistentState();
    }

    private void StartPitChallenge(bool nightmare = false)
    {
        _challengeMode = true;
        _challengeKind = nightmare ? ChallengeKind.PitNightmare : ChallengeKind.Pit;
        _currentMap = MapDefinition.Baselands;
        _selectedMapName = nightmare ? "Pit (Nightmare)" : "Pit";
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
        _freezeZones = [];
        _midaMiniTurrets = [];
        _bunkerProtectiveDomes = [];
        _bunkerFreezeZones = [];
        _bunkerMidaMiniTurrets = [];
        ResetSecuredTerminalContent();
        _frozenTargets.Clear();
        _chilledTargets.Clear();
        _radioactiveDecompositionTargets.Clear();
        _radioactiveDecompositionDamageMultipliers.Clear();
        _poisonVisualTargets.Clear();
        _slowVisualTargets.Clear();
        _generators = [];
        _toxicPools = [];
        _stationEntranceDoor = null;
        _stationBossDoor = null;
        _stationBossArena = null;
        _stationEntranceOpen = false;
        _stationBossFightStarted = false;
        _stationBossDoorSealed = false;
        _stationBossDoorSealTimer = -1f;
        _player = nightmare
            ? CreateNightmarePlayer(new Vector2(_worldSize / 2f, _worldSize / 2f))
            : Player.Create(
                new Vector2(_worldSize / 2f, _worldSize / 2f),
                GetCommonHealthBonus(),
                GetCommonDamageBonus(),
                0,
                0,
                0,
                0,
                _meta.CradleSpeed,
                _meta.CradleMeleeSpeed,
                _meta.CradleDashRecovery,
                _meta.CradleStability,
                _meta.CradleGunsmith,
                _meta.CradleFighter,
                _meta.CradleArcane,
                ItemStack.StartingPistol(),
                null,
                ItemStack.StartingMelee(),
                ItemStack.StartingArmor(),
                null,
                null);
        ApplyIsFunnyNextRunBonus();
        _projectiles = [];
        _explosions = [];
        _beamEffects = [];
        _lightningEffects = [];
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
        PreloadGameplayTextures();
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
        _pitRunTokensEarned = 0;
        ResetPitNightmareState(resetBaseBonuses: !nightmare);
        if (nightmare)
        {
            _pitNightmareHealthBonusPercent = 25f;
            _pitNightmareSpeedBonusPercent = 50f;
            _extractPortals.Add(new ExtractPortal(new Vector2(_worldSize / 2f, _worldSize / 2f), _rng.NextSingle() * MathF.Tau));
        }
        ResetPitConsumableSpawns();
        _player.InventoryOpen = false;
        _openedChestIndex = null;
        _mapOpen = false;
        _mapMarker = null;
        _bunkerMapMarker = null;
        _drag = null;
        _hovered = null;
        _inBunker = false;
        _surfaceReturnPosition = Vector2.Zero;
        _secondaryBunkerHatchPosition = Vector2.Zero;
        _secondaryBunkerHatchUnlocked = false;
        _bunkerRooms = [];
        _bunkerDoors = [];
        _bunkerObstacles = [];
        _revealedBunkerRooms.Clear();
        ClearPendingLevelUpPoints();
        SpawnPitWave();

        _camera.Offset = GetUiScreenCenter();
        _camera.Target = _player.Position;
        StartRunIntro();
        SavePersistentState();
    }

    private Player CreateNightmarePlayer(Vector2 position)
    {
        var player = Player.Create(
            position,
            GetCommonHealthBonus(),
            GetCommonDamageBonus(),
            0,
            0,
            0,
            0,
            _meta.CradleSpeed,
            _meta.CradleMeleeSpeed,
            _meta.CradleDashRecovery,
            _meta.CradleStability,
            _meta.CradleGunsmith,
            _meta.CradleFighter,
            _meta.CradleArcane,
            TakeMetaLoadoutItem(SlotKind.RangedWeapon),
            TakeMetaLoadoutItem(SlotKind.HeavyWeapon),
            TakeMetaLoadoutItem(SlotKind.MeleeWeapon),
            TakeMetaLoadoutItem(SlotKind.Armor),
            TakeMetaLoadoutItem(SlotKind.QuickSlotQ),
            TakeMetaLoadoutItem(SlotKind.QuickSlotR));

        for (var i = 0; i < Inventory.BackpackCapacity; i++)
        {
            player.Inventory.BackpackSlots[i] = _meta.RunBackpackSlots[i];
            _meta.RunBackpackSlots[i] = null;
        }

        return player;
    }

    private void ResetPitNightmareState(bool resetBaseBonuses = true)
    {
        if (resetBaseBonuses)
        {
            _pitNightmareDamageBonusPercent = 0f;
            _pitNightmareHealthBonusPercent = 0f;
            _pitNightmareSpeedBonusPercent = 0f;
        }

        _pitNightmarePortalActive = false;
        _pitDifficultyOpen = false;
        _pitDifficultySpinElapsed = 0f;
        _pitDifficultyOffer = default;
        _pitDifficultyRouletteItems.Clear();
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
            case GameState.Cradle: UpdateCradle(); break;
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

}
