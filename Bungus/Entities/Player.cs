using System.Numerics;
using Raylib_cs;

namespace Bungus.Game;

public sealed class Player
{
    private const float BaseMaxHealthValue = 100f;
    private const float BaseMoveSpeed = 210f;
    private const float BaseDashDistance = 150f;
    private const float DashEchoDuration = 0.25f;
    private const float DashEchoSpawnInterval = 0.05f;
    private const float MeleeDamageMultiplier = 6.3f;
    private const float MeleeSwingLife = 0.18f;
    private const float BladeRadius = 75f;
    private const float BladeHalfAngle = 0.6225f;
    private const float LegendaryBladeHalfAngleBonus = MathF.PI / 72f;
    private const float SpearStartDistance = 24f;
    private const float SpearEndDistance = 125f;
    private const float LegendarySpearLengthMultiplier = 1.2475f;
    private const float TwinShotChance = 0.33f;
    private const float TwinShotSpread = 0.06f;
    private const float BaseDashCooldownDuration = 1.1f;
    private const float MedkitFlatHealAmount = 25f;
    private const float MedkitMaxHealthHealPercent = 0.10f;
    private const float ShieldDamageMultiplier = 1.25f;
    private const float ShieldRechargeDelay = 5f;
    private const float ShieldRechargeRatePerSecond = 0.0333f;
    private const float RegenTickInterval = 1f;
    private const float StickyBulletsDuration = 15f;
    private const float TeslaBulletsDuration = 15f;
    private const float StimDuration = 5f;
    private const float StimSpeedBonus = 0.30f;
    private const float MovingRangedSpreadAngle = MathF.PI / 90f;
    private const float SniperDamageMultiplier = 8.325f;
    private const float EmpoweredSniperDamageMultiplier = 20.8125f;
    private const float SniperCooldown = 1.75f;
    private const float SniperProjectileSpeed = 2100f;
    private const float SniperProjectileLifetime = 2000f / SniperProjectileSpeed;
    private const float PulseProjectileSpeed = 600f;
    private const float PulseProjectileLifetime = 650f / PulseProjectileSpeed;
    private const float AutoRifleDamageMultiplier = 0.53f;
    private const float AutoRifleCooldown = 60f / 500f;
    private const float AutoRifleProjectileSpeed = 600f;
    private const float AutoRifleProjectileLifetime = 620f / AutoRifleProjectileSpeed;
    private const float RocketPulseRifleFireRate = 400f / 60f;
    private const float RocketPulseRifleShotInterval = 0.064f;
    private const float RocketPulseRifleCooldown = 3f / RocketPulseRifleFireRate;
    private const float RocketPulseRifleBurstSpreadDegrees = 5f;
    private const float RocketPulseRifleNormalSpreadDegrees = 3f;
    private const float RocketPulseRifleRange = 600f;
    private const float RocketPulseRifleProjectileSpeed = 600f;
    private const float RocketPulseRifleProjectileLifetime = RocketPulseRifleRange / RocketPulseRifleProjectileSpeed;
    private const float ToxikusProjectileSpeed = 550f;
    private const float ToxikusProjectileLifetime = 625f / ToxikusProjectileSpeed;
    private const float TraceRifleCooldown = 60f / 1000f;
    private const float TraceRifleRange = 820f;
    private const float LinearRifleChargeDuration = 0.8f;
    private const float LegendaryLinearRifleChargeDuration = 0.7f;
    private const float LinearRifleCooldown = 0.45f;
    private const float LinearRifleChargeDecaySpeed = 3f;
    private const float LinearRifleProjectileSpeed = 3800f;
    private const float LinearRifleRange = 1000f;
    private const float RocketLauncherFireRate = 40f / 60f;
    private const float RocketProjectileSpeed = 475f;
    private const float RocketProjectileLifetime = 510f / RocketProjectileSpeed;
    private const float PulsarCooldown = 1f / 3f;
    private const float PulsarProjectileSpeed = 400f;
    private const float PulsarProjectileLifetime = 600f / PulsarProjectileSpeed;
    private const float SniperIdleRequirement = 1f;
    private const float LegendarySniperChargeDuration = 2.5f;

    private readonly float _globalMaxHealthBonus;
    private readonly float _globalDamageBonus;
    private readonly int _cradleSpeed;
    private readonly int _cradleMeleeSpeed;
    private readonly int _cradleDashRecovery;
    private readonly int _cradleStability;
    private readonly int _cradleGunsmith;
    private readonly int _cradleFighter;
    private readonly int _cradleArcane;
    private float _attackCd;
    private float _dodgeCd;
    private float _stim;
    private float _stimDurationMax;
    private float _bleed;
    private float _poison;
    private float _dashEchoTimer;
    private float _dashEchoSpawnTimer;
    private Vector2 _dashEchoDir;
    private int _pulseQueuedShots;
    private float _pulseShotCd;
    private Vector2 _pulseDir;
    private Color _pulseColor;
    private float _pulseDamage;
    private float _pulsePoisonDamagePerSecond;
    private float _pulsePoisonDuration;
    private float _pulseProjectileSpeed;
    private float _pulseProjectileLifetime;
    private float _pulseShotInterval = 0.064f;
    private bool _pulseQueuedExplosive;
    private float _pulseExplosionDamage;
    private float _pulseExplosionRadius;
    private float _pulseDrawRadius;
    private float _pulseSpreadRadians;
    private bool _rocketPulseBurstMode;
    private float _sniperStillTimer;
    private float _legendarySniperChargeTimer;
    private bool _legendarySniperChargePrimed;
    private float _linearRifleCharge;
    private float _timeSinceLastDamage = float.MaxValue;
    private float _regenTickTimer;
    private float _shield;
    private float _lastShieldMax = -1f;
    private float _stickyBulletsTimer;
    private float _teslaBulletsTimer;
    private float _poisonDurationMax;
    private float _radioactiveDecompositionTimer;
    private float _movementSlowTimer;
    private float _terrorSpin;

    public Vector2 Position { get; private set; }
    public float Health { get; private set; }
    public float MaxHealth => BaseMaxHealthValue + _globalMaxHealthBonus + Str * 5f;
    public float SpeedMultiplier => 1f + Spd * 0.04f + _cradleSpeed * 0.028f + (Armor?.SpeedBonusPercent ?? 0f);
    public float DashCooldownProgress => 1f - Math.Clamp(_dodgeCd / GetDashCooldownDuration(), 0f, 1f);
    public bool DashReady => _dodgeCd <= 0f;
    public float Shield => _shield;
    public float ShieldCapacity => Armor?.ShieldMax ?? 0f;
    public bool StickyBulletsActive => _stickyBulletsTimer > 0f;
    public bool TeslaBulletsActive => _teslaBulletsTimer > 0f;
    public bool StimActive => _stim > 0f;
    public bool Poisoned => _poison > 0f;
    public bool RadioactiveDecompositionActive => _radioactiveDecompositionTimer > 0f;
    public bool MovementSlowed => _movementSlowTimer > 0f;
    public float RadioactiveDecompositionProgress => Math.Clamp(_radioactiveDecompositionTimer / 10f, 0f, 1f);
    public float MovementSlowProgress => Math.Clamp(_movementSlowTimer / 5f, 0f, 1f);
    public float StimEffectProgress => Math.Clamp(_stim / MathF.Max(_stimDurationMax, 0.001f), 0f, 1f);
    public float StickyBulletsEffectProgress => Math.Clamp(_stickyBulletsTimer / StickyBulletsDuration, 0f, 1f);
    public float TeslaBulletsEffectProgress => Math.Clamp(_teslaBulletsTimer / TeslaBulletsDuration, 0f, 1f);
    public float PoisonEffectProgress => Math.Clamp(_poison / MathF.Max(_poisonDurationMax, 0.001f), 0f, 1f);
    public bool IsMoving { get; private set; }
    public bool IsSniperEquipped => ActiveWeaponClass == WeaponClass.Ranged && ActiveWeapon?.Pattern == WeaponPattern.SniperRifle;
    public bool IsLegendarySniperEquipped => IsSniperEquipped && ActiveWeapon?.Rarity == ArmorRarity.Legendary;
    public bool IsLinearRifleEquipped => ActiveWeaponClass == WeaponClass.Ranged && ActiveWeapon?.Pattern == WeaponPattern.LinearRifle;
    public bool IsTerrorEquipped => ActiveWeaponClass == WeaponClass.Ranged && ActiveWeapon?.Pattern == WeaponPattern.Terror;
    public float TerrorSpinProgress => Math.Clamp(_terrorSpin / 6f, 0f, 1f);
    public bool IsLegendaryRocketPulseRifleEquipped => ActiveWeaponClass == WeaponClass.Ranged && ActiveWeapon?.Pattern == WeaponPattern.RocketPulseRifle && ActiveWeapon.Rarity == ArmorRarity.Legendary;
    public bool RocketPulseBurstMode => IsLegendaryRocketPulseRifleEquipped && _rocketPulseBurstMode;
    public float LinearRifleChargeProgress => Math.Clamp(_linearRifleCharge / GetLinearRifleChargeDuration(), 0f, 1f);
    public bool SniperChargeVisible => IsLegendarySniperEquipped && _legendarySniperChargePrimed;
    public float SniperChargeProgress => !SniperChargeVisible ? 0f : Math.Clamp(_legendarySniperChargeTimer / LegendarySniperChargeDuration, 0f, 1f);
    public bool SniperChargeReady => IsLegendarySniperEquipped && _legendarySniperChargePrimed && _legendarySniperChargeTimer >= LegendarySniperChargeDuration;

    public bool InventoryOpen { get; set; }
    public bool Invulnerable { get; set; }

    public int Str { get; private set; }
    public int Dex { get; private set; }
    public int Spd { get; private set; }
    public int Guns { get; private set; }

    public int Level { get; private set; } = 1;
    public int Kills { get; private set; }
    public int KillsTarget => 10 + 5 * ((Level - 1) * Level / 2);
    public int StatPoints { get; private set; }

    public Inventory Inventory { get; } = new();

    public ItemStack? RangedWeapon { get; set; }
    public ItemStack? HeavyWeapon { get; set; }
    public ItemStack? MeleeWeapon { get; set; }
    public ItemStack? Armor { get; set; }

    public WeaponSlot ActiveWeaponSlot { get; private set; } = WeaponSlot.PrimaryRanged;
    public WeaponClass ActiveWeaponClass => ActiveWeaponSlot == WeaponSlot.Melee ? WeaponClass.Melee : WeaponClass.Ranged;
    public ItemStack? ActiveWeapon => ActiveWeaponSlot switch
    {
        WeaponSlot.Melee => MeleeWeapon,
        WeaponSlot.HeavyRanged => HeavyWeapon,
        _ => RangedWeapon
    };

    private Player(Vector2 p, float globalMaxHealthBonus, float globalDamageBonus, int baseStrength, int baseDexterity, int baseSpeed, int baseGuns, int cradleSpeed, int cradleMeleeSpeed, int cradleDashRecovery, int cradleStability, int cradleGunsmith, int cradleFighter, int cradleArcane, ItemStack? rangedWeapon, ItemStack? heavyWeapon, ItemStack? meleeWeapon, ItemStack? armor, ItemStack? quickSlotQ, ItemStack? quickSlotR)
    {
        Position = p;
        _globalMaxHealthBonus = globalMaxHealthBonus;
        _globalDamageBonus = globalDamageBonus;
        _cradleSpeed = Math.Clamp(cradleSpeed, 0, 15);
        _cradleMeleeSpeed = Math.Clamp(cradleMeleeSpeed, 0, 15);
        _cradleDashRecovery = Math.Clamp(cradleDashRecovery, 0, 15);
        _cradleStability = Math.Clamp(cradleStability, 0, 15);
        _cradleGunsmith = Math.Clamp(cradleGunsmith, 0, 15);
        _cradleFighter = Math.Clamp(cradleFighter, 0, 15);
        _cradleArcane = Math.Clamp(cradleArcane, 0, 15);
        Str = Math.Max(0, baseStrength);
        Dex = Math.Max(0, baseDexterity);
        Spd = Math.Max(0, baseSpeed);
        Guns = Math.Max(0, baseGuns);

        RangedWeapon = rangedWeapon ?? ItemStack.StartingPistol();
        HeavyWeapon = heavyWeapon;
        MeleeWeapon = meleeWeapon ?? ItemStack.StartingMelee();
        Armor = armor ?? ItemStack.StartingArmor();
        Inventory.QuickSlotQ = quickSlotQ;
        Inventory.QuickSlotR = quickSlotR;
        Health = MaxHealth;
        SyncArmorState();
    }

    public static Player Create(Vector2 p, float globalMaxHealthBonus, float globalDamageBonus, int baseStrength, int baseDexterity, int baseSpeed, int baseGuns, int cradleSpeed, int cradleMeleeSpeed, int cradleDashRecovery, int cradleStability, int cradleGunsmith, int cradleFighter, int cradleArcane, ItemStack? rangedWeapon, ItemStack? heavyWeapon, ItemStack? meleeWeapon, ItemStack? armor, ItemStack? quickSlotQ, ItemStack? quickSlotR)
        => new(p, globalMaxHealthBonus, globalDamageBonus, baseStrength, baseDexterity, baseSpeed, baseGuns, cradleSpeed, cradleMeleeSpeed, cradleDashRecovery, cradleStability, cradleGunsmith, cradleFighter, cradleArcane, rangedWeapon, heavyWeapon, meleeWeapon, armor, quickSlotQ, quickSlotR);

    public void PlaceAt(Vector2 position) => Position = position;

    public void Update(float dt, List<Obstacle> obstacles, int worldSize, List<DashAfterImage> afterImages)
    {
        SyncArmorState();
        _attackCd -= dt;
        _dodgeCd -= dt;
        _stim -= dt;
        _stickyBulletsTimer = MathF.Max(0f, _stickyBulletsTimer - dt);
        _teslaBulletsTimer = MathF.Max(0f, _teslaBulletsTimer - dt);
        _radioactiveDecompositionTimer = MathF.Max(0f, _radioactiveDecompositionTimer - dt);
        _movementSlowTimer = MathF.Max(0f, _movementSlowTimer - dt);
        _timeSinceLastDamage += dt;
        UpdateLinearRifleCharge(dt);
        if (IsTerrorEquipped && !InventoryOpen && Raylib.IsMouseButtonDown(MouseButton.Left))
            _terrorSpin = MathF.Min(6f, _terrorSpin + dt);
        else
            _terrorSpin = MathF.Max(0f, _terrorSpin - dt);

        if (_bleed > 0 && !Invulnerable)
        {
            _bleed -= dt;
            _timeSinceLastDamage = 0f;
            _regenTickTimer = 0f;
            Health = MathF.Max(0f, Health - 2.4f * dt);
        }
        else if (_bleed > 0)
        {
            _bleed -= dt;
        }

        if (_poison > 0 && !Invulnerable)
        {
            _poison -= dt;
            _timeSinceLastDamage = 0f;
            _regenTickTimer = 0f;
            Health = MathF.Max(0f, Health - MaxHealth * 0.03f * dt);
            if (_poison <= 0f) _poisonDurationMax = 0f;
        }
        else if (_poison > 0)
        {
            _poison -= dt;
            if (_poison <= 0f) _poisonDurationMax = 0f;
        }

        var d = Vector2.Zero;
        if (Raylib.IsKeyDown(KeyboardKey.W)) d.Y -= 1;
        if (Raylib.IsKeyDown(KeyboardKey.S)) d.Y += 1;
        if (Raylib.IsKeyDown(KeyboardKey.A)) d.X -= 1;
        if (Raylib.IsKeyDown(KeyboardKey.D)) d.X += 1;

        if (Raylib.IsKeyPressed(KeyboardKey.Space) && _dodgeCd <= 0f)
        {
            var dir = d == Vector2.Zero ? new Vector2(1f, 0f) : Vector2.Normalize(d);
            var dashDistanceMultiplier = MathF.Max(0.1f, 1f + (Armor?.DashDistancePercent ?? 0f));
            var dist = BaseDashDistance * SpeedMultiplier * dashDistanceMultiplier;
            Position = MovementUtils.MoveWithCollisions(Position, dir * dist, 16f, obstacles, worldSize);
            DashAfterImage.Spawn(afterImages, Position, dir, dist, Palette.C(120, 200, 255), false);
            _dashEchoDir = dir;
            _dashEchoTimer = DashEchoDuration;
            _dashEchoSpawnTimer = 0f;
            _dodgeCd = GetDashCooldownDuration();
        }

        IsMoving = d != Vector2.Zero;
        if (d != Vector2.Zero)
        {
            var speed = BaseMoveSpeed * SpeedMultiplier;
            if (_movementSlowTimer > 0f) speed *= 0.5f;
            if (_stim > 0) speed *= 1f + StimSpeedBonus * GetArcaneEffectMultiplier();
            if (IsLinearRifleEquipped && Raylib.IsMouseButtonDown(MouseButton.Left) && _attackCd <= 0f && _linearRifleCharge > 0f) speed *= 0.6f;
            var delta = Vector2.Normalize(d) * speed * dt;
            Position = MovementUtils.MoveWithCollisions(Position, delta, 16f, obstacles, worldSize);
        }

        UpdateShieldRecharge(dt);
        UpdateHealthRegen(dt);
        UpdateLegendarySniperCharge(dt);
        UpdateDashEcho(dt, afterImages);
    }

    private void UpdateLinearRifleCharge(float dt)
    {
        if (!IsLinearRifleEquipped || InventoryOpen)
        {
            _linearRifleCharge = 0f;
            return;
        }

        if (_attackCd > 0f)
        {
            _linearRifleCharge = 0f;
            return;
        }

        if (ActiveWeapon?.IsHeavyWeapon == true && Inventory.GetHeavyAmmoShotCount(ActiveWeapon) <= 0)
        {
            _linearRifleCharge = 0f;
            return;
        }

        if (Raylib.IsMouseButtonDown(MouseButton.Left))
        {
            _linearRifleCharge = MathF.Min(GetLinearRifleChargeDuration(), _linearRifleCharge + dt);
            return;
        }

        if (_linearRifleCharge < GetLinearRifleChargeDuration())
        {
            _linearRifleCharge = MathF.Max(0f, _linearRifleCharge - dt * LinearRifleChargeDecaySpeed);
        }
    }

    private void UpdateDashEcho(float dt, List<DashAfterImage> afterImages)
    {
        if (_dashEchoTimer <= 0f) return;

        _dashEchoTimer = MathF.Max(0f, _dashEchoTimer - dt);
        _dashEchoSpawnTimer -= dt;

        while (_dashEchoSpawnTimer <= 0f)
        {
            var strength = _dashEchoTimer / DashEchoDuration;
            var offset = 6f + (1f - strength) * 10f;
            var alpha = 0.18f + strength * 0.42f;
            afterImages.Add(new DashAfterImage(Position - _dashEchoDir * offset, Palette.C(120, 200, 255), alpha, false));
            _dashEchoSpawnTimer += DashEchoSpawnInterval;

            if (_dashEchoTimer <= 0f) break;
        }
    }

    public bool Attack(Vector2 target, List<Projectile> projectiles, List<SwingArc> swings, List<Obstacle> obstacles, int worldSize, List<DashAfterImage> afterImages)
    {
        var weapon = ActiveWeapon;
        if (weapon is null) return false;
        if (_attackCd > 0f && weapon.Pattern != WeaponPattern.RamBomber) return false;

        var dir = target - Position;
        if (dir == Vector2.Zero) dir = new Vector2(1f, 0f);
        dir = Vector2.Normalize(dir);

        if (ActiveWeaponClass == WeaponClass.Ranged)
        {
            var damage = GetWeaponDamage(weapon);
            if (weapon.Pattern == WeaponPattern.Terror)
            {
                if (!TryConsumeHeavyAmmo(weapon)) return false;
                var spread = (Random.Shared.NextSingle() * 5f - 2.5f) * MathF.PI / 180f;
                dir = VisibilityUtils.Rotate(dir, spread);
                const float projectileSpeed = 1000f;
                projectiles.Add(new Projectile(
                    Position + dir * 20f,
                    dir,
                    projectileSpeed,
                    800f / projectileSpeed,
                    Palette.C(110, 238, 58),
                    false,
                    damage,
                    drawRadius: 4.5f,
                    highlighted: true,
                    sourcePosition: Position,
                    enemyDecompositionDuration: 5f));
                var fireRate = 2f + TerrorSpinProgress * 13f;
                _attackCd = 1f / fireRate;
                return true;
            }
            if (weapon.Pattern == WeaponPattern.RamBomber)
            {
                var (blastDamage, blastColor) = RollRamBomberBlast();
                projectiles.Add(new Projectile(
                    Position,
                    Vector2.Zero,
                    0f,
                    0.02f,
                    blastColor,
                    false,
                    blastDamage,
                    ProjectileKind.RamBlast,
                    1000f,
                    blastDamage,
                    0f,
                    false,
                    Position));
                _attackCd = 0f;
                return true;
            }
            if (weapon.Pattern == WeaponPattern.TraceRifle)
            {
                if (!TryConsumeHeavyAmmo(weapon)) return false;
                dir = ApplyMovementSpread(dir, 0.7f);
                var beamLength = MathF.Min(Vector2.Distance(target, Position), TraceRifleRange);
                var end = ClipRayToObstacles(Position, dir, beamLength, obstacles, worldSize, 2f);
                projectiles.Add(new Projectile(end, Vector2.Zero, 0f, 0.02f, weapon.Color, false, damage, ProjectileKind.TraceBeam, drawRadius: 5f, sourcePosition: Position));
                _attackCd = TraceRifleCooldown;
                return true;
            }
            else if (weapon.Pattern == WeaponPattern.LinearRifle)
            {
                if (!Raylib.IsMouseButtonReleased(MouseButton.Left) || _linearRifleCharge < GetLinearRifleChargeDuration()) return false;
                if (!TryConsumeHeavyAmmo(weapon)) return false;

                var linearDamage = damage * 9f;
                var end = ClipRayToObstacles(Position, dir, LinearRifleRange, obstacles, worldSize, 3f);
                var shotStart = Position + dir * 20f;
                var distance = MathF.Max(1f, Vector2.Distance(shotStart, end));
                projectiles.Add(new Projectile(
                    shotStart,
                    dir,
                    LinearRifleProjectileSpeed,
                    distance / LinearRifleProjectileSpeed,
                    weapon.Color,
                    false,
                    linearDamage,
                    ProjectileKind.LinearShot,
                    drawRadius: 3.85f,
                    highlighted: true,
                    sourcePosition: Position));
                _linearRifleCharge = 0f;
                _attackCd = LinearRifleCooldown;
                return true;
            }
            else if (weapon.Pattern == WeaponPattern.RocketLauncher)
            {
                if (!TryConsumeHeavyAmmo(weapon)) return false;
                dir = ApplyMovementSpread(dir);
                projectiles.Add(new Projectile(
                    Position + dir * 20f,
                    dir,
                    RocketProjectileSpeed,
                    RocketProjectileLifetime,
                    weapon.Color,
                    false,
                    damage + 200f,
                    ProjectileKind.Grenade,
                    117f,
                    damage,
                    8f,
                    false,
                    Position));
                _attackCd = 1f / RocketLauncherFireRate;
                return true;
            }
            else if (weapon.Pattern == WeaponPattern.Pulsar)
            {
                dir = ApplyMovementSpread(dir);
                projectiles.Add(new Projectile(
                    Position + dir * 18f,
                    dir,
                    PulsarProjectileSpeed,
                    PulsarProjectileLifetime,
                    weapon.Color,
                    false,
                    damage,
                    ProjectileKind.PulsarBolt,
                    drawRadius: 5f,
                    highlighted: true,
                    sourcePosition: Position));
                _attackCd = PulsarCooldown;
                return true;
            }
            else if (weapon.Pattern == WeaponPattern.GrenadeLauncher)
            {
                if (!TryConsumeHeavyAmmo(weapon)) return false;
                dir = ApplyMovementSpread(dir);
                projectiles.Add(new Projectile(
                    Position + dir * 20f,
                    dir,
                    375f,
                    350f / 375f,
                    weapon.Color,
                    false,
                    damage + 135f,
                    ProjectileKind.Grenade,
                    90f,
                    damage,
                    7f,
                    false,
                    Position));
                _attackCd = 1f / 1.5f;
                return true;
            }
            else if (weapon.Pattern == WeaponPattern.AutoRifle)
            {
                dir = ApplyMovementSpread(dir);
                projectiles.Add(new Projectile(
                    Position + dir * 18f,
                    dir,
                    AutoRifleProjectileSpeed,
                    AutoRifleProjectileLifetime,
                    weapon.Color,
                    false,
                    damage * AutoRifleDamageMultiplier,
                    sourcePosition: Position,
                    ricochetRemaining: weapon.Rarity == ArmorRarity.Legendary ? 1 : 0));
                _attackCd = AutoRifleCooldown;
                return true;
            }
            else if (weapon.Pattern == WeaponPattern.RocketPulseRifle)
            {
                var ammoCost = ItemStack.GetHeavyAmmoCostPercent(weapon);
                if (Inventory.HeavyAmmoPercent + 0.0001f < ammoCost * 3f) return false;
                for (var i = 0; i < 3; i++)
                {
                    if (!TryConsumeHeavyAmmo(weapon)) return false;
                }

                var rocketDamage = damage * 0.9f;
                var explosionDamage = damage * 0.45f;
                var burstMode = IsLegendaryRocketPulseRifleEquipped && _rocketPulseBurstMode;
                var shotInterval = burstMode ? RocketPulseRifleShotInterval / 1.5f : RocketPulseRifleShotInterval;
                _pulseDir = dir;
                _pulseColor = weapon.Color;
                _pulseDamage = rocketDamage;
                _pulsePoisonDamagePerSecond = 0f;
                _pulsePoisonDuration = 0f;
                _pulseProjectileSpeed = RocketPulseRifleProjectileSpeed;
                _pulseProjectileLifetime = RocketPulseRifleProjectileLifetime;
                _pulseQueuedExplosive = true;
                _pulseExplosionDamage = explosionDamage;
                _pulseExplosionRadius = 35f;
                _pulseDrawRadius = 4.8f;
                _pulseSpreadRadians = (burstMode ? RocketPulseRifleBurstSpreadDegrees : RocketPulseRifleNormalSpreadDegrees) * MathF.PI / 180f;
                FireRocketPulseShot(projectiles, dir, weapon.Color, rocketDamage, explosionDamage);
                _pulseQueuedShots = 2;
                _pulseShotCd = shotInterval;
                _pulseShotInterval = shotInterval;
                _attackCd = burstMode ? RocketPulseRifleCooldown / (1.3f * 1.1f) : RocketPulseRifleCooldown;
                return true;
            }
            else if (weapon.Pattern is WeaponPattern.PulseRifle or WeaponPattern.Toxikus)
            {
                var pulseShotDamage = GetPulseShotDamage(weapon);
                var poisonDps = weapon.Pattern == WeaponPattern.Toxikus ? GetToxikusPoisonDamage(weapon) : 0f;
                var poisonDuration = weapon.Pattern == WeaponPattern.Toxikus ? 3f : 0f;
                var projectileSpeed = weapon.Pattern == WeaponPattern.Toxikus ? ToxikusProjectileSpeed : PulseProjectileSpeed;
                var projectileLifetime = weapon.Pattern == WeaponPattern.Toxikus ? ToxikusProjectileLifetime : PulseProjectileLifetime;
                FirePulseShot(projectiles, dir, weapon.Color, pulseShotDamage, projectileSpeed, projectileLifetime, poisonDps, poisonDuration);
                _pulseQueuedShots = GetPulseBurstShotCount(weapon) - 1;
                _pulseShotCd = weapon.Pattern == WeaponPattern.Toxikus ? 0.0664f : 0.064f;
                _pulseDir = dir;
                _pulseColor = weapon.Color;
                _pulseDamage = pulseShotDamage;
                _pulsePoisonDamagePerSecond = poisonDps;
                _pulsePoisonDuration = poisonDuration;
                _pulseProjectileSpeed = projectileSpeed;
                _pulseProjectileLifetime = projectileLifetime;
                _pulseShotInterval = _pulseShotCd;
                _pulseQueuedExplosive = false;
                _attackCd = weapon.Pattern == WeaponPattern.Toxikus ? 1f / 2.2f : 0.374f;
                return true;
            }
            else if (weapon.Pattern == WeaponPattern.SniperRifle)
            {
                if (!TryConsumeHeavyAmmo(weapon)) return false;
                var empowered = SniperChargeReady;
                var sniperDamage = GetSniperShotDamage(weapon, empowered);
                dir = ApplyMovementSpread(dir);
                projectiles.Add(new Projectile(
                    Position + dir * 20f,
                    dir,
                    SniperProjectileSpeed,
                    SniperProjectileLifetime,
                    empowered ? Palette.C(176, 92, 255) : Palette.C(255, 48, 48),
                    false,
                    sniperDamage,
                    ProjectileKind.Bullet,
                    0f,
                    0f,
                    empowered ? 5.5f : 4.5f,
                    empowered,
                    Position));
                _attackCd = SniperCooldown;
                ResetLegendarySniperChargeAfterShot();
                return true;
            }
            else
            {
                if (weapon.Rarity == ArmorRarity.Legendary && Random.Shared.NextSingle() < TwinShotChance)
                {
                    FireStandardShot(projectiles, dir, weapon.Color, damage, -TwinShotSpread);
                    FireStandardShot(projectiles, dir, weapon.Color, damage, TwinShotSpread);
                }
                else
                {
                    FireStandardShot(projectiles, dir, weapon.Color, damage);
                }

                _attackCd = 0.22f;
                return true;
            }
        }
        else
        {
            var angle = MathF.Atan2(dir.Y, dir.X);
            if (weapon.Pattern is WeaponPattern.EnergySpear or WeaponPattern.Lancelot)
            {
                var spearLength = SpearEndDistance - SpearStartDistance;
                if (weapon.Rarity == ArmorRarity.Legendary || weapon.Pattern == WeaponPattern.Lancelot) spearLength *= LegendarySpearLengthMultiplier;
                swings.Add(SwingArc.Line(Position, Position + dir * SpearStartDistance, Position + dir * (SpearStartDistance + spearLength), MeleeSwingLife, weapon.Color));
                if (weapon.Pattern == WeaponPattern.Lancelot)
                {
                    Position = MovementUtils.MoveWithCollisions(Position, dir * 64f, 16f, obstacles, worldSize);
                    DashAfterImage.Spawn(afterImages, Position, dir, 64f, weapon.Color, false);
                }
                _attackCd = GetMeleeCooldown(0.70f);
                return true;
            }
            else
            {
                var halfAngle = BladeHalfAngle + (weapon.Rarity == ArmorRarity.Legendary ? LegendaryBladeHalfAngleBonus : 0f);
                swings.Add(SwingArc.Arc(Position, Position, BladeRadius, angle - halfAngle, angle + halfAngle, MeleeSwingLife, weapon.Color, Random.Shared.NextSingle() < 0.5f));
                _attackCd = GetMeleeCooldown(0.64f);
                return true;
            }
        }
    }

    public void UpdateCombat(float dt, List<Projectile> projectiles)
    {
        if (_pulseQueuedShots <= 0) return;

        _pulseShotCd -= dt;
        while (_pulseQueuedShots > 0 && _pulseShotCd <= 0f)
        {
            if (_pulseQueuedExplosive)
            {
                FireRocketPulseShot(projectiles, _pulseDir, _pulseColor, _pulseDamage, _pulseExplosionDamage);
            }
            else
            {
                FirePulseShot(projectiles, _pulseDir, _pulseColor, _pulseDamage, _pulseProjectileSpeed, _pulseProjectileLifetime, _pulsePoisonDamagePerSecond, _pulsePoisonDuration);
            }
            _pulseQueuedShots--;
            _pulseShotCd += _pulseShotInterval;
        }

        if (_pulseQueuedShots <= 0) _pulseQueuedExplosive = false;
    }

    private void FirePulseShot(List<Projectile> projectiles, Vector2 dir, Color color, float damage, float speed, float lifetime, float poisonDamagePerSecond = 0f, float poisonDuration = 0f)
    {
        dir = ApplyMovementSpread(dir);
        projectiles.Add(new Projectile(Position + dir * 18f, dir, speed, lifetime, color, false, damage, sourcePosition: Position, poisonDamagePerSecond: poisonDamagePerSecond, poisonDuration: poisonDuration));
    }

    private void FireRocketPulseShot(List<Projectile> projectiles, Vector2 dir, Color color, float damage, float explosionDamage)
    {
        dir = VisibilityUtils.Rotate(dir, (Random.Shared.NextSingle() * 2f - 1f) * _pulseSpreadRadians);
        dir = ApplyMovementSpread(dir);
        projectiles.Add(new Projectile(
            Position + dir * 20f,
            dir,
            RocketPulseRifleProjectileSpeed,
            RocketPulseRifleProjectileLifetime,
            color,
            false,
            damage,
            ProjectileKind.Grenade,
            _pulseExplosionRadius,
            explosionDamage,
            _pulseDrawRadius,
            false,
            Position));
    }

    private void FireStandardShot(List<Projectile> projectiles, Vector2 dir, Color color, float damage, float angleOffset = 0f)
    {
        var shotDir = angleOffset == 0f ? dir : VisibilityUtils.Rotate(dir, angleOffset);
        shotDir = ApplyMovementSpread(shotDir);
        projectiles.Add(new Projectile(Position + shotDir * 18f, shotDir, 520f, 550f / 520f, color, false, damage, sourcePosition: Position));
    }

    private static (float Damage, Color Color) RollRamBomberBlast()
    {
        var roll = Random.Shared.NextSingle();
        if (roll < 0.95f) return (1f, Palette.C(255, 45, 45, 120));
        if (roll < 0.99f) return (1000f, Palette.C(90, 255, 120, 120));
        return (10000f, Palette.C(255, 255, 255, 145));
    }

    private bool TryConsumeHeavyAmmo(ItemStack weapon)
        => !weapon.IsHeavyWeapon || Inventory.TryConsumeHeavyAmmo(weapon);

    private static Vector2 ClipRayToObstacles(Vector2 start, Vector2 dir, float distance, List<Obstacle> obstacles, int worldSize, float radius)
    {
        var safeDistance = MathF.Max(0f, distance);
        var step = MathF.Max(6f, radius * 2f);
        var previous = start;

        for (var traveled = step; traveled <= safeDistance; traveled += step)
        {
            var point = start + dir * traveled;
            if (point.X < 0 || point.Y < 0 || point.X > worldSize || point.Y > worldSize) return previous;
            if (MovementUtils.CircleHitsObstacle(point, radius, obstacles)) return previous;
            previous = point;
        }

        var end = start + dir * safeDistance;
        end.X = Math.Clamp(end.X, 0f, worldSize);
        end.Y = Math.Clamp(end.Y, 0f, worldSize);
        if (MovementUtils.CircleHitsObstacle(end, radius, obstacles)) return previous;
        return end;
    }

    public float GetMeleeDamage()
    {
        return MeleeWeapon is null ? 0f : GetMeleeHitDamage(MeleeWeapon);
    }

    public float GetRangedDamage()
    {
        return RangedWeapon is null ? 0f : GetWeaponDamage(RangedWeapon);
    }

    public float GetSniperShotDamage(ItemStack weapon, bool empowered = false)
    {
        if (weapon.Type != ItemType.Weapon || weapon.Pattern != WeaponPattern.SniperRifle) return 0f;
        var multiplier = empowered ? EmpoweredSniperDamageMultiplier : SniperDamageMultiplier;
        return GetWeaponDamage(weapon) * multiplier;
    }

    public float GetWeaponBaseDamage(ItemStack weapon)
    {
        if (weapon.Type != ItemType.Weapon) return 0f;
        if (weapon.Pattern == WeaponPattern.GrenadeLauncher && weapon.BaseDamage <= 0f) return 100f;
        return MathF.Max(0f, weapon.BaseDamage);
    }

    public float GetWeaponModifierDamage(ItemStack weapon)
    {
        if (weapon.Type != ItemType.Weapon) return 0f;

        var statMultiplier = weapon.WeaponKind == WeaponClass.Melee
            ? GetMeleeDamageMultiplier()
            : GetRangedDamageMultiplier();
        var flatBonus = weapon.WeaponKind == WeaponClass.Ranged ? GetRangedFlatDamageBonus() : 0f;
        return GetWeaponBaseDamage(weapon) * statMultiplier + _globalDamageBonus + flatBonus;
    }

    public float GetWeaponDamage(ItemStack weapon)
    {
        if (weapon.Type != ItemType.Weapon) return 0f;
        return GetWeaponBaseDamage(weapon) + GetWeaponModifierDamage(weapon);
    }

    public float GetMeleeHitDamage(ItemStack weapon)
    {
        if (weapon.Type != ItemType.Weapon || weapon.WeaponKind != WeaponClass.Melee) return 0f;
        return GetWeaponDamage(weapon) * MeleeDamageMultiplier + GetMeleeFlatDamageBonus();
    }

    public int GetPulseBurstShotCount(ItemStack weapon)
    {
        if (weapon.Type != ItemType.Weapon || weapon.Pattern is not (WeaponPattern.PulseRifle or WeaponPattern.Toxikus)) return 1;
        if (weapon.Pattern == WeaponPattern.Toxikus) return 2;
        return weapon.Rarity == ArmorRarity.Legendary ? 4 : 3;
    }

    public float GetPulseShotDamage(ItemStack weapon)
    {
        if (weapon.Type != ItemType.Weapon || weapon.Pattern is not (WeaponPattern.PulseRifle or WeaponPattern.Toxikus)) return 0f;
        return GetWeaponDamage(weapon) * 0.525f;
    }

    public float GetMeleeDamageMultiplier() => Str * 0.0025f + Dex * 0.01f + _cradleFighter * 0.004f;
    public float GetRangedDamageMultiplier() => Guns * 0.01f + _cradleGunsmith * 0.004f;

    public float GetMeleeFlatDamageBonus() => Str;
    public float GetRangedFlatDamageBonus() => Guns * 0.3f;

    public float GetArcaneEffectMultiplier() => 1f + _cradleArcane * 0.01f;

    public float GetToxikusPoisonDamage(ItemStack weapon)
        => (30f + GetWeaponDamage(weapon) * 0.4f) * GetArcaneEffectMultiplier();

    private float GetLinearRifleChargeDuration()
        => ActiveWeapon?.Pattern == WeaponPattern.LinearRifle && ActiveWeapon.Rarity == ArmorRarity.Legendary
            ? LegendaryLinearRifleChargeDuration
            : LinearRifleChargeDuration;

    public float GetMeleeCooldown(float baseCooldown)
    {
        var attackSpeedBonus = Dex * 0.02f + _cradleMeleeSpeed * 0.016f;
        return baseCooldown / (1f + attackSpeedBonus);
    }

    public float GetStatusEffectChance(float baseChance)
    {
        return Math.Clamp(baseChance * GetArcaneEffectMultiplier(), 0f, 1f);
    }

    private float GetDashCooldownDuration()
    {
        var recovery = (Armor?.DashRecoveryPercent ?? 0f) + _cradleDashRecovery * 0.01f;
        return MathF.Max(0.1f, BaseDashCooldownDuration * (1f - recovery));
    }

    private Vector2 ApplyMovementSpread(Vector2 dir, float multiplier = 1f)
    {
        if (!IsMoving) return dir;
        var stability = Math.Clamp(1f - _cradleStability * 0.01f, 0.1f, 1f);
        var armorSpread = MathF.Max(0.1f, 1f + (Armor?.MovementSpreadPercent ?? 0f));
        var spread = (Random.Shared.NextSingle() * 2f - 1f) * MovingRangedSpreadAngle * multiplier * stability * armorSpread;
        return Vector2.Normalize(VisibilityUtils.Rotate(dir, spread));
    }

    private void UpdateLegendarySniperCharge(float dt)
    {
        if (!IsLegendarySniperEquipped)
        {
            ResetLegendarySniperCharge();
            return;
        }

        if (IsMoving)
        {
            ResetLegendarySniperCharge();
            return;
        }

        if (!_legendarySniperChargePrimed)
        {
            _sniperStillTimer += dt;
            if (_sniperStillTimer >= SniperIdleRequirement)
            {
                _legendarySniperChargePrimed = true;
                _legendarySniperChargeTimer = 0f;
            }

            return;
        }

        if (_legendarySniperChargeTimer < LegendarySniperChargeDuration)
        {
            _legendarySniperChargeTimer = MathF.Min(LegendarySniperChargeDuration, _legendarySniperChargeTimer + dt);
        }
    }

    private void ResetLegendarySniperCharge()
    {
        _sniperStillTimer = 0f;
        _legendarySniperChargeTimer = 0f;
        _legendarySniperChargePrimed = false;
    }

    private void ResetLegendarySniperChargeAfterShot()
    {
        if (!IsLegendarySniperEquipped) return;
        _legendarySniperChargeTimer = 0f;
        _legendarySniperChargePrimed = true;
        _sniperStillTimer = SniperIdleRequirement;
    }

    public void SelectWeaponSlot(WeaponSlot slot) => ActiveWeaponSlot = slot;

    public bool ToggleRocketPulseMode()
    {
        if (!IsLegendaryRocketPulseRifleEquipped) return false;
        _rocketPulseBurstMode = !_rocketPulseBurstMode;
        return true;
    }

    public ConsumableType? UseQuickSlotQ()
    {
        var used = TryUseConsumable(Inventory.QuickSlotQ);
        if (used is null) return null;
        Inventory.QuickSlotQ = null;
        Inventory.AutoFillConsumableSlots();
        return used;
    }

    public ConsumableType? UseQuickSlotR()
    {
        var used = TryUseConsumable(Inventory.QuickSlotR);
        if (used is null) return null;
        Inventory.QuickSlotR = null;
        Inventory.AutoFillConsumableSlots();
        return used;
    }

    public ConsumableType? UseConsumableItem(ItemStack? item)
        => TryUseConsumable(item);

    private ConsumableType? TryUseConsumable(ItemStack? slot)
    {
        if (slot?.Type != ItemType.Consumable || slot.ConsumableKind is null) return null;
        if (slot.IsStationKey) return null;

        if (slot.ConsumableKind == ConsumableType.Medkit)
        {
            if (Health >= MaxHealth) return null;
            ApplyHealing(MedkitFlatHealAmount + MaxHealth * MedkitMaxHealthHealPercent);
            return slot.ConsumableKind;
        }

        if (slot.ConsumableKind == ConsumableType.Stim)
        {
            _stimDurationMax = StimDuration * GetArcaneEffectMultiplier();
            _stim = _stimDurationMax;
            return slot.ConsumableKind;
        }

        if (slot.ConsumableKind == ConsumableType.StickyBullets)
        {
            _stickyBulletsTimer = StickyBulletsDuration;
            return slot.ConsumableKind;
        }

        if (slot.ConsumableKind == ConsumableType.TeslaBullets)
        {
            _teslaBulletsTimer = TeslaBulletsDuration;
            return slot.ConsumableKind;
        }

        return slot.ConsumableKind;
    }

    public void ApplyBleed(float duration) => _bleed = MathF.Max(_bleed, duration);

    public void ApplyPoison(float duration)
    {
        if (duration >= _poison) _poisonDurationMax = duration;
        _poison = MathF.Max(_poison, duration);
    }
    public void ApplyRadioactiveDecomposition(float duration = 10f)
        => _radioactiveDecompositionTimer = MathF.Max(_radioactiveDecompositionTimer, duration);
    public void ApplyMovementSlow(float duration)
        => _movementSlowTimer = MathF.Max(_movementSlowTimer, duration);
    public void TickEffects(float dt) { }

    public void TakeDamage(float value, bool isExplosion = false, float armorPenetration = 0f)
    {
        if (Invulnerable) return;
        if (value <= 0f) return;

        SyncArmorState();
        _timeSinceLastDamage = 0f;
        _regenTickTimer = 0f;

        value *= RadioactiveDecompositionActive ? 1.25f : 1f;
        var remainingDamage = ApplyShieldDamage(value);
        if (remainingDamage <= 0f) return;

        var resilience = Armor?.ResiliencePercent ?? 0f;
        var explosionResistance = isExplosion ? Armor?.ExplosionResistancePercent ?? 0f : 0f;
        var armor = Armor?.Defense ?? 0f;
        var effectiveArmor = armor * (1f - Math.Clamp(armorPenetration, 0f, 1f));
        var reduced = armorPenetration > 0f
            ? (remainingDamage - effectiveArmor) * (1f - resilience) * (1f - explosionResistance)
            : remainingDamage * (1f - resilience) * (1f - explosionResistance) - effectiveArmor;
        reduced = MathF.Max(1f, reduced);
        Health = MathF.Max(0f, Health - reduced);
    }

    public bool ApplyKnockback(Vector2 direction, float distance, List<Obstacle> obstacles, int worldSize)
    {
        if (direction.LengthSquared() <= 0.001f) return false;
        direction = Vector2.Normalize(direction);
        var hitWall = false;
        for (var moved = 0f; moved < distance; moved += 4f)
        {
            var candidate = Position + direction * MathF.Min(4f, distance - moved);
            if (MovementUtils.CircleHitsObstacle(candidate, 16f, obstacles)
                || candidate.X < 16f || candidate.Y < 16f
                || candidate.X > worldSize - 16f || candidate.Y > worldSize - 16f)
            {
                hitWall = true;
                break;
            }
            Position = candidate;
        }
        return hitWall;
    }

    public void RegisterKill(int points = 1)
    {
        Kills += Math.Max(1, points);
        while (Kills >= KillsTarget)
        {
            Kills -= KillsTarget;
            Level++;
            StatPoints++;
            ApplyHealing(MaxHealth * 0.25f);
        }
    }

    public void GrantLevel()
    {
        Level++;
        StatPoints++;
        ApplyHealing(MaxHealth * 0.25f);
    }

    public void ApplyPoint(StatType stat)
    {
        if (StatPoints <= 0) return;
        StatPoints--;

        if (stat == StatType.Strength)
        {
            Str++;
            ApplyHealing(5f);
        }
        if (stat == StatType.Dexterity) Dex++;
        if (stat == StatType.Speed) Spd++;
        if (stat == StatType.Gunsmith) Guns++;
    }

    private void SyncArmorState()
    {
        var shieldMax = Armor?.ShieldMax ?? 0f;
        if (MathF.Abs(shieldMax - _lastShieldMax) <= 0.001f)
        {
            _shield = Math.Clamp(_shield, 0f, shieldMax);
            return;
        }

        _lastShieldMax = shieldMax;
        _shield = shieldMax;
    }

    private float ApplyShieldDamage(float incomingDamage)
    {
        if (_shield <= 0f) return incomingDamage;

        var blockedDamage = MathF.Min(incomingDamage, _shield / ShieldDamageMultiplier);
        _shield = MathF.Max(0f, _shield - incomingDamage * ShieldDamageMultiplier);
        return incomingDamage - blockedDamage;
    }

    private void UpdateShieldRecharge(float dt)
    {
        var shieldMax = ShieldCapacity;
        if (shieldMax <= 0f || _shield >= shieldMax || _timeSinceLastDamage < ShieldRechargeDelay / GetArcaneEffectMultiplier()) return;
        _shield = MathF.Min(shieldMax, _shield + shieldMax * ShieldRechargeRatePerSecond * GetArcaneEffectMultiplier() * dt);
    }

    private void UpdateHealthRegen(float dt)
    {
        var regenPerSecond = (Armor?.RegenPercentPerSecond ?? 0f) * GetArcaneEffectMultiplier();
        if (regenPerSecond <= 0f || Health <= 0f || Health >= MaxHealth) return;

        _regenTickTimer += dt;
        while (_regenTickTimer >= RegenTickInterval)
        {
            _regenTickTimer -= RegenTickInterval;
            ApplyHealing(MaxHealth * regenPerSecond);
            if (Health >= MaxHealth) break;
        }
    }

    private void ApplyHealing(float amount)
    {
        if (amount <= 0f || Health <= 0f) return;
        var healingBonus = Armor?.HealingBonusPercent ?? 0f;
        Health = MathF.Min(MaxHealth, Health + amount * (1f + healingBonus));
    }
}
