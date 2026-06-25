using System.Numerics;
using Raylib_cs;

namespace Bungus.Game;

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
    private float _poisonTimer;
    private float _poisonDamagePerSecond;

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
        TickStatusEffects(dt);
        if (!Alive) return;

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

    public void ApplyStickySlow(float duration, float strengthMultiplier = 1f) { }

    public void ApplyPoison(float damagePerSecond, float duration)
    {
        _poisonDamagePerSecond = MathF.Max(_poisonDamagePerSecond, damagePerSecond);
        _poisonTimer = MathF.Max(_poisonTimer, duration);
    }

    public void Draw(Texture2D? texture = null)
    {
        var time = (float)Raylib.GetTime();
        var pulse = 0.5f + 0.5f * MathF.Sin(time * (Invulnerable ? 3.5f : 6.5f));
        Raylib.BeginBlendMode(BlendMode.Additive);
        var glowColor = Invulnerable ? Palette.C(255, 58, 96, 30) : Palette.C(255, 176, 84, 42);
        Raylib.DrawCircleGradient((int)Position.X, (int)Position.Y, Radius * (Invulnerable ? 2.25f : 1.75f) + pulse * 8f, glowColor, Palette.C(glowColor.R, glowColor.G, glowColor.B, 0));
        Raylib.EndBlendMode();

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
            Raylib.DrawCircleLines((int)Position.X, (int)Position.Y, Radius, Invulnerable ? Palette.C(210, 72, 118) : Palette.C(255, 166, 92));
            Raylib.DrawCircleV(Position, 17f, Palette.C(12, 8, 16));
        }
        if (Invulnerable)
        {
            var shieldRadius = Radius + 10f;
            Raylib.DrawCircleV(Position, shieldRadius, Palette.C(235, 42, 78, 24));
            Raylib.DrawCircleLinesV(Position, shieldRadius + pulse * 3f, Palette.C(255, 104, 132, 130));
        }
        else
        {
            Raylib.DrawCircleLinesV(Position, Radius + 13f + pulse * 5f, Palette.C(255, 190, 96, 165));
        }
        var bar = new Rectangle(Position.X - 90f, Position.Y - 66f, 180f, 9f);
        Raylib.DrawRectangleRec(bar, Palette.C(24, 16, 22));
        Raylib.DrawRectangle((int)bar.X, (int)bar.Y, (int)(bar.Width * Health / MaxHealth), (int)bar.Height, Invulnerable ? Palette.C(176, 32, 58) : Palette.C(232, 116, 58));
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

    private void TickStatusEffects(float dt)
    {
        if (_poisonTimer <= 0f) return;
        Damage(_poisonDamagePerSecond * dt);
        _poisonTimer = MathF.Max(0f, _poisonTimer - dt);
        if (_poisonTimer <= 0f) _poisonDamagePerSecond = 0f;
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
