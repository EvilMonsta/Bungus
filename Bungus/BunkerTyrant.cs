using System.Numerics;
using Raylib_cs;

namespace Bungus.Game;

public enum TyrantMode
{
    MachineGun,
    Grenades,
    Scribs,
    Idle
}

public sealed class TyrantGrenadeWarning(Vector2 position)
{
    public Vector2 Position { get; } = position;
    public float Timer { get; set; } = 0.5f;
}

public sealed class BunkerTyrant
{
    public const float Radius = 50.4f;
    public Vector2 Position { get; }
    public float MaxHealth { get; } = 5000f;
    public float Health { get; private set; } = 5000f;
    public bool Alive => Health > 0f;
    public bool Active { get; private set; }
    public bool Invulnerable { get; private set; } = true;
    public bool KillAwarded { get; set; }
    public float DamageMultiplier { get; private set; } = 1f;
    public float WakeTimer { get; private set; } = 5f;
    public TyrantMode Mode { get; private set; }
    public float ModeTimer { get; private set; } = 7f;
    public float RestTimer { get; private set; }
    public bool Resting => RestTimer > 0f;
    public float VulnerableTimer { get; private set; }
    public bool ParasiteWaveActive { get; private set; }
    public int ParasitesSpawned { get; private set; }
    public bool ShockwaveReady => _shockwavePending && !_shockwaveTriggered;
    public List<TyrantGrenadeWarning> GrenadeWarnings { get; } = [];

    private float _machineGunTimer;
    private float _grenadeTimer;
    private float _scribTimer;
    private float _parasiteTimer;
    private bool _scribSpawnLeft = true;
    private bool _shockwaveTriggered;
    private bool _shockwavePending;
    private Vector2 _facing = new(1f, 0f);

    public BunkerTyrant(Vector2 position) => Position = position;

    public void Activate()
    {
        Active = true;
        WakeTimer = 5f;
        Mode = (TyrantMode)Random.Shared.Next(0, 3);
        ModeTimer = 7f;
        RestTimer = 0f;
        ResetModeCooldowns();
    }

    public void MakeVulnerable()
    {
        if (!Alive || WakeTimer > 0f) return;
        Invulnerable = false;
        VulnerableTimer = 20f;
        ParasiteWaveActive = false;
    }

    public void Update(
        float dt,
        Vector2 playerPosition,
        List<Obstacle> obstacles,
        List<Projectile> projectiles,
        Action<Vector2> spawnScrib,
        Action<Vector2> spawnParasite,
        Vector2 leftSpawn,
        Vector2 rightSpawn,
        Action shieldRestored)
    {
        if (!Alive || !Active) return;

        var toPlayer = playerPosition - Position;
        if (toPlayer.LengthSquared() > 0.001f) _facing = Vector2.Normalize(toPlayer);

        if (WakeTimer > 0f)
        {
            WakeTimer = MathF.Max(0f, WakeTimer - dt);
            return;
        }

        if (!Invulnerable)
        {
            VulnerableTimer = MathF.Max(0f, VulnerableTimer - dt);
            if (VulnerableTimer <= 0f)
            {
                Invulnerable = true;
                ParasiteWaveActive = true;
                ParasitesSpawned = 0;
                _parasiteTimer = 0f;
                Mode = TyrantMode.Idle;
                ModeTimer = 7f;
                RestTimer = 0f;
                shieldRestored();
            }
        }

        if (ParasiteWaveActive)
        {
            _parasiteTimer -= dt;
            if (ParasitesSpawned < 15 && _parasiteTimer <= 0f)
            {
                var batchSize = Math.Min(Random.Shared.Next(2, 4), 15 - ParasitesSpawned);
                for (var i = 0; i < batchSize; i++)
                {
                    var areaCenter = (ParasitesSpawned + i) % 2 == 0 ? leftSpawn : rightSpawn;
                    spawnParasite(RandomPointInSpawnArea(areaCenter));
                }
                ParasitesSpawned += batchSize;
                _parasiteTimer = 1.1f;
            }
        }

        UpdateGrenadeWarnings(dt, projectiles);
        if (RestTimer > 0f)
        {
            RestTimer = MathF.Max(0f, RestTimer - dt);
            if (RestTimer <= 0f)
            {
                Mode = RollNextAttackMode(Mode);
                ModeTimer = 7f;
                ResetModeCooldowns();
            }
            return;
        }

        ModeTimer -= dt;
        if (ModeTimer <= 0f)
        {
            if (Mode == TyrantMode.Idle) ParasiteWaveActive = false;
            RestTimer = 5f;
            return;
        }

        var direction = _facing;

        switch (Mode)
        {
            case TyrantMode.MachineGun:
                _machineGunTimer -= dt;
                while (_machineGunTimer <= 0f)
                {
                    var spread = (Random.Shared.NextSingle() * 20f - 10f) * MathF.PI / 180f;
                    var shotDirection = VisibilityUtils.Rotate(direction, spread);
                    projectiles.Add(new Projectile(
                        Position + shotDirection * 46f,
                        shotDirection,
                        500f,
                        8f,
                        Palette.C(110, 238, 58),
                        true,
                        25f * DamageMultiplier,
                        playerDecompositionDuration: 10f));
                    _machineGunTimer += 1f / 9f;
                }
                break;

            case TyrantMode.Grenades:
                _grenadeTimer -= dt;
                if (_grenadeTimer <= 0f)
                {
                    GrenadeWarnings.Add(new TyrantGrenadeWarning(ClipTargetToObstacles(Position, playerPosition, obstacles)));
                    _grenadeTimer = 1.25f;
                }
                break;

            case TyrantMode.Scribs:
                _scribTimer -= dt;
                if (_scribTimer <= 0f)
                {
                    spawnScrib(_scribSpawnLeft ? leftSpawn : rightSpawn);
                    _scribSpawnLeft = !_scribSpawnLeft;
                    _scribTimer = 1f;
                }
                break;
        }
    }

    private void UpdateGrenadeWarnings(float dt, List<Projectile> projectiles)
    {
        for (var i = GrenadeWarnings.Count - 1; i >= 0; i--)
        {
            var warning = GrenadeWarnings[i];
            warning.Timer -= dt;
            if (warning.Timer > 0f) continue;

            var direction = warning.Position - Position;
            var distance = direction.Length();
            if (distance <= 0.001f) direction = new Vector2(0f, -1f);
            else direction /= distance;
            projectiles.Add(new Projectile(
                Position + direction * 46f,
                direction,
                1400f,
                MathF.Max(0.05f, distance / 1400f),
                Palette.C(190, 58, 72),
                true,
                0f,
                ProjectileKind.Grenade,
                150f,
                50f * DamageMultiplier,
                7f,
                playerPoisonDuration: 2f,
                playerDecompositionDuration: 10f));
            GrenadeWarnings.RemoveAt(i);
        }
    }

    public bool Damage(float amount)
    {
        if (!Alive || !Active || WakeTimer > 0f || Invulnerable) return false;
        var healthBeforeDamage = Health;
        Health = MathF.Max(0f, Health - MathF.Max(0f, amount));
        if (healthBeforeDamage > MaxHealth * 0.1f && Health <= MaxHealth * 0.1f) _shockwavePending = true;
        return true;
    }

    public void MarkShockwaveTriggered()
    {
        _shockwaveTriggered = true;
        _shockwavePending = false;
    }

    public void HealFromParasite()
    {
        if (!Alive) return;
        Health = MathF.Min(MaxHealth, Health + MaxHealth * 0.02f);
        DamageMultiplier += 0.01f;
    }

    public void Draw(Texture2D? texture = null)
    {
        if (texture is { Id: not 0 } activeTexture)
        {
            var size = Radius * 2f;
            var source = new Rectangle(0f, 0f, activeTexture.Width, activeTexture.Height);
            var destination = new Rectangle(Position.X, Position.Y, size, size);
            var rotation = MathF.Atan2(_facing.Y, _facing.X) * 180f / MathF.PI;
            Raylib.DrawTexturePro(activeTexture, source, destination, new Vector2(size * 0.5f), rotation, Color.White);
        }
        else
        {
        var body = Invulnerable ? Palette.C(52, 8, 48) : Palette.C(108, 18, 42);
        if (WakeTimer > 0f) body = Palette.C(34, 20, 34);
        Raylib.DrawCircleV(Position, Radius, body);
        Raylib.DrawCircleLines((int)Position.X, (int)Position.Y, Radius, Palette.C(190, 55, 95));
        Raylib.DrawCircleV(Position, 17f, Palette.C(12, 8, 16));
        }
        if (Invulnerable)
        {
            var shieldRadius = Radius + 10f;
            Raylib.DrawCircleV(Position, shieldRadius, Palette.C(235, 42, 54, 32));
            Raylib.DrawCircleLinesV(Position, shieldRadius, Palette.C(255, 82, 88, 120));
        }
        var bar = new Rectangle(Position.X - 90f, Position.Y - 66f, 180f, 9f);
        Raylib.DrawRectangleRec(bar, Palette.C(24, 16, 22));
        Raylib.DrawRectangle((int)bar.X, (int)bar.Y, (int)(bar.Width * Health / MaxHealth), (int)bar.Height, Palette.C(176, 32, 58));
    }

    private static TyrantMode RollNextAttackMode(TyrantMode previous)
    {
        TyrantMode next;
        do next = (TyrantMode)Random.Shared.Next(0, 3);
        while (next == previous);
        return next;
    }

    private void ResetModeCooldowns()
    {
        _machineGunTimer = 0f;
        _grenadeTimer = 0f;
        _scribTimer = 0f;
    }

    private static Vector2 RandomPointInSpawnArea(Vector2 center)
        => center + new Vector2(
            Random.Shared.NextSingle() * 170f - 85f,
            Random.Shared.NextSingle() * 170f - 85f);

    private static Vector2 ClipTargetToObstacles(Vector2 from, Vector2 target, List<Obstacle> obstacles)
    {
        var delta = target - from;
        var distance = delta.Length();
        if (distance <= 0.001f) return target;
        var direction = delta / distance;
        for (var traveled = 8f; traveled <= distance; traveled += 8f)
        {
            var point = from + direction * traveled;
            if (MovementUtils.CircleHitsObstacle(point, 4f, obstacles)) return from + direction * MathF.Max(0f, traveled - 12f);
        }
        return target;
    }
}

public sealed class BunkerScrib(Vector2 position, int roomId = 19, Rectangle? patrolRoom = null, bool startAggroed = true)
{
    public const float Radius = 13.2f;
    public int RoomId { get; } = roomId;
    public Vector2 Position { get; private set; } = position;
    public const float MaxHealth = 100f;
    public float Health { get; private set; } = MaxHealth;
    public bool Alive => Health > 0f && !_exploded;
    public bool Exploded => _exploded;
    private bool _armed;
    private bool _exploded;
    private float _fuse = 0.5f;
    private List<Vector2> _path = [];
    private int _pathIndex;
    private float _pathRefreshTimer;
    private Vector2 _lastNavigationPosition;
    private float _stuckTimer;
    private float _freezeChillTimer;
    private readonly BunkerAwareness _awareness = new(patrolRoom ?? new Rectangle(1000f, 2600f, 2200f, 1400f), startAggroed);

    public bool Update(float dt, Vector2 playerPosition, List<Obstacle> obstacles)
    {
        if (!Alive) return false;
        _freezeChillTimer = MathF.Max(0f, _freezeChillTimer - dt);
        var moveMultiplier = _freezeChillTimer > 0f ? 0.75f : 1f;
        _awareness.Update(Position, playerPosition, obstacles, dt);
        if (!_awareness.Aggroed)
        {
            var before = Position;
            MoveNavigated(dt, _awareness.GetPatrolTarget(Position), 70f * moveMultiplier, obstacles);
            _awareness.ObserveMovement(before, Position);
            return false;
        }

        var distance = Vector2.Distance(Position, playerPosition);
        if (distance <= 30f) _armed = true;
        if (_armed)
        {
            _fuse -= dt;
            if (_fuse <= 0f)
            {
                _exploded = true;
                return true;
            }
            return false;
        }

        var speed = (distance <= 100f ? 660f : 220f) * moveMultiplier;
        MoveNavigated(dt, playerPosition, speed, obstacles);
        return false;
    }

    private void MoveNavigated(float dt, Vector2 target, float speed, List<Obstacle> obstacles)
    {
        UpdateStuckState(dt);
        _pathRefreshTimer -= dt;
        var clearPath = PathfindingUtils.HasClearPath(Position, target, Radius, obstacles, 4000);
        if (clearPath)
        {
            _path.Clear();
            _pathIndex = 0;
        }
        else if (_pathRefreshTimer <= 0f || _pathIndex >= _path.Count || _stuckTimer >= 0.25f)
        {
            _pathRefreshTimer = 0.3f;
            _stuckTimer = 0f;
            if (PathfindingUtils.TryFindPath(Position, target, Radius, obstacles, 4000, out var path, allowDirectShortcut: false))
            {
                _path = path;
                _pathIndex = 0;
            }
            else
            {
                _path.Clear();
                _pathIndex = 0;
                return;
            }
        }

        while (_pathIndex < _path.Count && Vector2.DistanceSquared(Position, _path[_pathIndex]) <= 20f * 20f) _pathIndex++;
        var waypoint = clearPath ? target : _pathIndex < _path.Count ? _path[_pathIndex] : Position;
        var direction = waypoint - Position;
        if (direction.LengthSquared() <= 0.001f) return;
        Position = MovementUtils.MoveWithCollisions(Position, Vector2.Normalize(direction) * speed * dt, Radius, obstacles, 4000);
    }

    private void UpdateStuckState(float dt)
    {
        if (Vector2.DistanceSquared(Position, _lastNavigationPosition) < 2f * 2f) _stuckTimer += dt;
        else
        {
            _lastNavigationPosition = Position;
            _stuckTimer = 0f;
        }
    }

    public bool Damage(float amount)
    {
        if (!Alive) return false;
        Health = MathF.Max(0f, Health - amount);
        if (Health > 0f) return false;
        _exploded = true;
        return true;
    }

    public void ForceAggro(Vector2 playerPosition) => _awareness.ForceAggro(Position, playerPosition);
    public void ApplyFreezeChill(float duration) => _freezeChillTimer = MathF.Max(_freezeChillTimer, duration);

    public void Draw()
    {
        Raylib.DrawCircleV(Position, Radius, _armed ? Palette.C(230, 50, 70) : Palette.C(116, 30, 64));
        Raylib.DrawCircleLines((int)Position.X, (int)Position.Y, Radius, Palette.C(245, 105, 120));
        DrawHealthBar(Position, Health / MaxHealth, 29f, 20f);
    }

    private static void DrawHealthBar(Vector2 position, float ratio, float width, float yOffset)
    {
        var bar = new Rectangle(position.X - width * 0.5f, position.Y - yOffset, width, 4f);
        Raylib.DrawRectangleRec(bar, Palette.C(24, 16, 22, 230));
        Raylib.DrawRectangle((int)bar.X, (int)bar.Y, (int)(bar.Width * Math.Clamp(ratio, 0f, 1f)), (int)bar.Height, Palette.C(210, 54, 78));
    }
}

public sealed class BunkerParasite(Vector2 position)
{
    public Vector2 Position { get; private set; } = position;
    public const float MaxHealth = 25f;
    public float Health { get; private set; } = MaxHealth;
    public bool Alive => Health > 0f;
    private List<Vector2> _path = [];
    private int _pathIndex;
    private float _pathRefreshTimer;
    private Vector2 _lastNavigationPosition;
    private float _stuckTimer;
    private float _freezeChillTimer;

    public bool Update(float dt, Vector2 bossPosition, List<Obstacle> obstacles)
    {
        if (!Alive) return false;
        _freezeChillTimer = MathF.Max(0f, _freezeChillTimer - dt);
        var toBoss = bossPosition - Position;
        if (toBoss.Length() <= 15f) return true;
        MoveNavigated(dt, bossPosition, obstacles);
        return Vector2.Distance(Position, bossPosition) <= 15f;
    }

    private void MoveNavigated(float dt, Vector2 target, List<Obstacle> obstacles)
    {
        UpdateStuckState(dt);
        _pathRefreshTimer -= dt;
        var clearPath = PathfindingUtils.HasClearPath(Position, target, 8f, obstacles, 4000);
        if (clearPath)
        {
            _path.Clear();
            _pathIndex = 0;
        }
        else if (_pathRefreshTimer <= 0f || _pathIndex >= _path.Count || _stuckTimer >= 0.25f)
        {
            _pathRefreshTimer = 0.4f;
            _stuckTimer = 0f;
            if (PathfindingUtils.TryFindPath(Position, target, 8f, obstacles, 4000, out var path, allowDirectShortcut: false))
            {
                _path = path;
                _pathIndex = 0;
            }
            else
            {
                _path.Clear();
                _pathIndex = 0;
                return;
            }
        }

        while (_pathIndex < _path.Count && Vector2.DistanceSquared(Position, _path[_pathIndex]) <= 14f * 14f) _pathIndex++;
        var waypoint = clearPath ? target : _pathIndex < _path.Count ? _path[_pathIndex] : Position;
        var direction = waypoint - Position;
        if (direction.LengthSquared() <= 0.001f) return;
        var moveMultiplier = _freezeChillTimer > 0f ? 0.75f : 1f;
        Position = MovementUtils.MoveWithCollisions(Position, Vector2.Normalize(direction) * 156f * moveMultiplier * dt, 8f, obstacles, 4000);
    }

    private void UpdateStuckState(float dt)
    {
        if (Vector2.DistanceSquared(Position, _lastNavigationPosition) < 2f * 2f) _stuckTimer += dt;
        else
        {
            _lastNavigationPosition = Position;
            _stuckTimer = 0f;
        }
    }

    public void Damage(float amount) => Health = MathF.Max(0f, Health - amount);
    public void ApplyFreezeChill(float duration) => _freezeChillTimer = MathF.Max(_freezeChillTimer, duration);

    public void Draw()
    {
        Raylib.DrawCircleV(Position, 8f, Palette.C(150, 45, 105));
        Raylib.DrawCircleLines((int)Position.X, (int)Position.Y, 8f, Palette.C(235, 95, 170));
        var bar = new Rectangle(Position.X - 11f, Position.Y - 14f, 22f, 4f);
        Raylib.DrawRectangleRec(bar, Palette.C(24, 16, 22, 230));
        Raylib.DrawRectangle((int)bar.X, (int)bar.Y, (int)(bar.Width * Math.Clamp(Health / MaxHealth, 0f, 1f)), (int)bar.Height, Palette.C(210, 54, 128));
    }
}

public sealed class BunkerToxicCloud(Vector2 position, float lifetime)
{
    public Vector2 Position { get; } = position;
    public float Radius { get; } = 112.5f;
    public float Life { get; private set; } = lifetime;
    public bool Alive => Life > 0f;

    public void Update(float dt) => Life = MathF.Max(0f, Life - dt);

    public void Draw()
    {
        var alpha = Math.Clamp(Life / 1f, 0f, 1f) * 0.25f;
        Raylib.DrawCircleV(Position, Radius, new Color((byte)92, (byte)150, (byte)62, (byte)(255f * alpha)));
        Raylib.DrawCircleLines((int)Position.X, (int)Position.Y, Radius, Palette.C(126, 190, 82, 150));
    }
}
