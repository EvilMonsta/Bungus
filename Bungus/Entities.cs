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
    private const float MeleeDamageMultiplier = 7f;
    private const float MeleeSwingLife = 0.18f;
    private const float BladeRadius = 72f;
    private const float BladeHalfAngle = 0.6225f;
    private const float LegendaryBladeHalfAngleBonus = MathF.PI / 72f;
    private const float SpearStartDistance = 24f;
    private const float SpearEndDistance = 125f;
    private const float LegendarySpearLengthMultiplier = 1.2f;
    private const float TwinShotChance = 0.33f;
    private const float TwinShotSpread = 0.06f;
    private const float BaseDashCooldownDuration = 1.1f;
    private const float MedkitHealAmount = 36f;
    private const float ShieldDamageMultiplier = 1.25f;
    private const float ShieldRechargeDelay = 5f;
    private const float ShieldRechargeRatePerSecond = 0.0333f;
    private const float RegenTickInterval = 1f;
    private const float StickyBulletsDuration = 15f;
    private const float MovingRangedSpreadAngle = MathF.PI / 90f;
    private const float SniperDamageMultiplier = 5.55f;
    private const float EmpoweredSniperDamageMultiplier = 13.875f;
    private const float SniperCooldown = 1.75f;
    private const float SniperProjectileSpeed = 2100f;
    private const float SniperProjectileLifetime = 1.3f;
    private const float PulseProjectileLifetime = 1.15f;
    private const float SniperIdleRequirement = 1f;
    private const float LegendarySniperChargeDuration = 2.5f;

    private readonly float _globalMaxHealthBonus;
    private readonly float _globalDamageBonus;
    private float _attackCd;
    private float _dodgeCd;
    private float _stim;
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
    private float _sniperStillTimer;
    private float _legendarySniperChargeTimer;
    private bool _legendarySniperChargePrimed;
    private float _timeSinceLastDamage = float.MaxValue;
    private float _regenTickTimer;
    private float _shield;
    private float _lastShieldMax = -1f;
    private float _stickyBulletsTimer;
    private float _poisonDurationMax;

    public Vector2 Position { get; private set; }
    public float Health { get; private set; }
    public float MaxHealth => BaseMaxHealthValue + _globalMaxHealthBonus + Str * 5f;
    public float SpeedMultiplier => 1f + Spd * 0.04f + (Armor?.SpeedBonusPercent ?? 0f);
    public float DashCooldownProgress => 1f - Math.Clamp(_dodgeCd / GetDashCooldownDuration(), 0f, 1f);
    public bool DashReady => _dodgeCd <= 0f;
    public float Shield => _shield;
    public float ShieldCapacity => Armor?.ShieldMax ?? 0f;
    public bool StickyBulletsActive => _stickyBulletsTimer > 0f;
    public bool StimActive => _stim > 0f;
    public bool Poisoned => _poison > 0f;
    public float StimEffectProgress => Math.Clamp(_stim / 6f, 0f, 1f);
    public float StickyBulletsEffectProgress => Math.Clamp(_stickyBulletsTimer / StickyBulletsDuration, 0f, 1f);
    public float PoisonEffectProgress => Math.Clamp(_poison / MathF.Max(_poisonDurationMax, 0.001f), 0f, 1f);
    public bool IsMoving { get; private set; }
    public bool IsSniperEquipped => ActiveWeaponClass == WeaponClass.Ranged && RangedWeapon?.Pattern == WeaponPattern.SniperRifle;
    public bool IsLegendarySniperEquipped => IsSniperEquipped && RangedWeapon?.Rarity == ArmorRarity.Legendary;
    public bool SniperChargeVisible => IsLegendarySniperEquipped && _legendarySniperChargePrimed;
    public float SniperChargeProgress => !SniperChargeVisible ? 0f : Math.Clamp(_legendarySniperChargeTimer / LegendarySniperChargeDuration, 0f, 1f);
    public bool SniperChargeReady => IsLegendarySniperEquipped && _legendarySniperChargePrimed && _legendarySniperChargeTimer >= LegendarySniperChargeDuration;

    public bool InventoryOpen { get; set; }

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
    public ItemStack? MeleeWeapon { get; set; }
    public ItemStack? Armor { get; set; }

    public WeaponClass ActiveWeaponClass { get; private set; } = WeaponClass.Ranged;

    private Player(Vector2 p, float globalMaxHealthBonus, float globalDamageBonus, int baseStrength, int baseDexterity, int baseSpeed, int baseGuns, ItemStack? rangedWeapon, ItemStack? meleeWeapon, ItemStack? armor, ItemStack? quickSlotQ, ItemStack? quickSlotR)
    {
        Position = p;
        _globalMaxHealthBonus = globalMaxHealthBonus;
        _globalDamageBonus = globalDamageBonus;
        Str = Math.Max(0, baseStrength);
        Dex = Math.Max(0, baseDexterity);
        Spd = Math.Max(0, baseSpeed);
        Guns = Math.Max(0, baseGuns);

        RangedWeapon = rangedWeapon ?? ItemStack.StartingPistol();
        MeleeWeapon = meleeWeapon ?? ItemStack.StartingMelee();
        Armor = armor ?? ItemStack.StartingArmor();
        Inventory.QuickSlotQ = quickSlotQ;
        Inventory.QuickSlotR = quickSlotR;
        Health = MaxHealth;
        SyncArmorState();
    }

    public static Player Create(Vector2 p, float globalMaxHealthBonus, float globalDamageBonus, int baseStrength, int baseDexterity, int baseSpeed, int baseGuns, ItemStack? rangedWeapon, ItemStack? meleeWeapon, ItemStack? armor, ItemStack? quickSlotQ, ItemStack? quickSlotR)
        => new(p, globalMaxHealthBonus, globalDamageBonus, baseStrength, baseDexterity, baseSpeed, baseGuns, rangedWeapon, meleeWeapon, armor, quickSlotQ, quickSlotR);

    public void PlaceAt(Vector2 position) => Position = position;

    public void Update(float dt, List<Obstacle> obstacles, int worldSize, List<DashAfterImage> afterImages)
    {
        SyncArmorState();
        _attackCd -= dt;
        _dodgeCd -= dt;
        _stim -= dt;
        _stickyBulletsTimer = MathF.Max(0f, _stickyBulletsTimer - dt);
        _timeSinceLastDamage += dt;

        if (_bleed > 0)
        {
            _bleed -= dt;
            _timeSinceLastDamage = 0f;
            _regenTickTimer = 0f;
            Health = MathF.Max(0f, Health - 2.4f * dt);
        }

        if (_poison > 0)
        {
            _poison -= dt;
            _timeSinceLastDamage = 0f;
            _regenTickTimer = 0f;
            Health = MathF.Max(0f, Health - MaxHealth * 0.03f * dt);
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
            var dist = BaseDashDistance * SpeedMultiplier;
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
            if (_stim > 0) speed *= 1.25f;
            var delta = Vector2.Normalize(d) * speed * dt;
            Position = MovementUtils.MoveWithCollisions(Position, delta, 16f, obstacles, worldSize);
        }

        UpdateShieldRecharge(dt);
        UpdateHealthRegen(dt);
        UpdateLegendarySniperCharge(dt);
        UpdateDashEcho(dt, afterImages);
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
        if (_attackCd > 0f) return false;

        var weapon = ActiveWeaponClass == WeaponClass.Ranged ? RangedWeapon : MeleeWeapon;
        if (weapon is null) return false;

        var dir = target - Position;
        if (dir == Vector2.Zero) dir = new Vector2(1f, 0f);
        dir = Vector2.Normalize(dir);

        if (ActiveWeaponClass == WeaponClass.Ranged)
        {
            var damage = GetWeaponDamage(weapon);
            if (weapon.Pattern == WeaponPattern.GrenadeLauncher)
            {
                dir = ApplyMovementSpread(dir);
                projectiles.Add(new Projectile(
                    Position + dir * 20f,
                    dir,
                    340f,
                    0.72f,
                    weapon.Color,
                    false,
                    damage + 200f,
                    ProjectileKind.Grenade,
                    120f,
                    damage,
                    7f,
                    false,
                    Position));
                _attackCd = 1f;
                return true;
            }
            else if (weapon.Pattern is WeaponPattern.PulseRifle or WeaponPattern.Toxikus)
            {
                var pulseShotDamage = GetPulseShotDamage(weapon);
                var poisonDps = weapon.Pattern == WeaponPattern.Toxikus ? 40f : 0f;
                var poisonDuration = weapon.Pattern == WeaponPattern.Toxikus ? 3f : 0f;
                FirePulseShot(projectiles, dir, weapon.Color, pulseShotDamage, poisonDps, poisonDuration);
                _pulseQueuedShots = GetPulseBurstShotCount(weapon) - 1;
                _pulseShotCd = weapon.Pattern == WeaponPattern.Toxikus ? 0.083f : 0.064f;
                _pulseDir = dir;
                _pulseColor = weapon.Color;
                _pulseDamage = pulseShotDamage;
                _pulsePoisonDamagePerSecond = poisonDps;
                _pulsePoisonDuration = poisonDuration;
                _attackCd = weapon.Pattern == WeaponPattern.Toxikus ? 0.486f : 0.374f;
                return true;
            }
            else if (weapon.Pattern == WeaponPattern.SniperRifle)
            {
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
            FirePulseShot(projectiles, _pulseDir, _pulseColor, _pulseDamage, _pulsePoisonDamagePerSecond, _pulsePoisonDuration);
            _pulseQueuedShots--;
            _pulseShotCd += 0.064f;
        }
    }

    private void FirePulseShot(List<Projectile> projectiles, Vector2 dir, Color color, float damage, float poisonDamagePerSecond = 0f, float poisonDuration = 0f)
    {
        dir = ApplyMovementSpread(dir);
        projectiles.Add(new Projectile(Position + dir * 18f, dir, 560f, PulseProjectileLifetime, color, false, damage, sourcePosition: Position, poisonDamagePerSecond: poisonDamagePerSecond, poisonDuration: poisonDuration));
    }

    private void FireStandardShot(List<Projectile> projectiles, Vector2 dir, Color color, float damage, float angleOffset = 0f)
    {
        var shotDir = angleOffset == 0f ? dir : VisibilityUtils.Rotate(dir, angleOffset);
        shotDir = ApplyMovementSpread(shotDir);
        projectiles.Add(new Projectile(Position + shotDir * 18f, shotDir, 520f, 1.15f, color, false, damage, sourcePosition: Position));
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
        if (weapon.Pattern == WeaponPattern.GrenadeLauncher && weapon.BaseDamage <= 0f) return 150f;
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

    public float GetMeleeDamageMultiplier() => Str * 0.0025f + Dex * 0.01f;
    public float GetRangedDamageMultiplier() => Guns * 0.01f;

    public float GetMeleeFlatDamageBonus() => Str;
    public float GetRangedFlatDamageBonus() => Guns * 0.3f;

    public float GetMeleeCooldown(float baseCooldown)
    {
        var attackSpeedBonus = Dex * 0.02f;
        return baseCooldown / (1f + attackSpeedBonus);
    }

    public float GetStatusEffectChance(float baseChance)
    {
        return baseChance;
    }

    private float GetDashCooldownDuration()
    {
        var recovery = Armor?.DashRecoveryPercent ?? 0f;
        return MathF.Max(0.1f, BaseDashCooldownDuration * (1f - recovery));
    }

    private Vector2 ApplyMovementSpread(Vector2 dir)
    {
        if (!IsMoving) return dir;
        var spread = (Random.Shared.NextSingle() * 2f - 1f) * MovingRangedSpreadAngle;
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

    public void SwitchActiveWeapon() => ActiveWeaponClass = ActiveWeaponClass == WeaponClass.Ranged ? WeaponClass.Melee : WeaponClass.Ranged;

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

        if (slot.ConsumableKind == ConsumableType.Medkit)
        {
            if (Health >= MaxHealth) return null;
            ApplyHealing(MedkitHealAmount);
            return slot.ConsumableKind;
        }

        if (slot.ConsumableKind == ConsumableType.Stim)
        {
            _stim = 6f;
            return slot.ConsumableKind;
        }

        if (slot.ConsumableKind == ConsumableType.StickyBullets)
        {
            _stickyBulletsTimer = StickyBulletsDuration;
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
    public void TickEffects(float dt) { }

    public void TakeDamage(float value, bool isExplosion = false)
    {
        if (value <= 0f) return;

        SyncArmorState();
        _timeSinceLastDamage = 0f;
        _regenTickTimer = 0f;

        var remainingDamage = ApplyShieldDamage(value);
        if (remainingDamage <= 0f) return;

        var resilience = Armor?.ResiliencePercent ?? 0f;
        var explosionResistance = isExplosion ? Armor?.ExplosionResistancePercent ?? 0f : 0f;
        var armor = Armor?.Defense ?? 0f;
        var reduced = remainingDamage * (1f - resilience) * (1f - explosionResistance) - armor;
        reduced = MathF.Max(1f, reduced);
        Health = MathF.Max(0f, Health - reduced);
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
        if (shieldMax <= 0f || _shield >= shieldMax || _timeSinceLastDamage < ShieldRechargeDelay) return;
        _shield = MathF.Min(shieldMax, _shield + shieldMax * ShieldRechargeRatePerSecond * dt);
    }

    private void UpdateHealthRegen(float dt)
    {
        var regenPerSecond = Armor?.RegenPercentPerSecond ?? 0f;
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

public sealed class Enemy
{
    public Vector2 Position;
    public float MaxHealth;
    public float Health;
    public int ZoneId = -1;
    public bool IsStrong;
    public bool IsPatrol;
    public bool IsEnhanced;
    public bool Alive => Health > 0f;

    public bool KillAwarded;
    public bool JustHitByPlayer;

    private Vector2 _facing;
    private Vector2 _baseFacing;
    private float _attackCd;

    private Vector2 _patrolA;
    private Vector2 _patrolB;
    private bool _toB = true;

    private bool _alert;
    private Vector2 _target;
    private bool _investigating;
    private bool _returningFromInvestigation;
    private Vector2 _investigateTarget;
    private Vector2 _investigateReturnPoint;
    private float _investigateWait;

    private float _sweepPhase;
    private float _sweepDir = 1f;

    private float _burstCd;
    private float _patrolTurnTimer;
    private bool _patrolTurning;
    private int _burstShotsLeft;
    private float _burstShotCd;

    private float _deathAnim = 0.45f;
    private float _slowTimer;
    private float _poisonTimer;
    private float _poisonDamagePerSecond;

    private const float BaseView = 435f;
    private const float StrongView = 540f;
    private const float AlertViewMultiplier = 1.25f;
    private const float FovHalf = MathF.PI / 3f; // 120 total

    private Enemy(Vector2 pos)
    {
        Position = pos;
        _facing = new Vector2(1f, 0f);
        _baseFacing = _facing;
    }

    public static Enemy CreatePatrol(Vector2 a, Vector2 b, bool outpost, int zoneId = -1, bool enhanced = false)
    {
        var maxHealth = enhanced ? 125f : 100f;
        var e = new Enemy(a)
        {
            ZoneId = zoneId,
            IsPatrol = true,
            IsEnhanced = enhanced,
            _patrolA = a,
            _patrolB = b,
            MaxHealth = maxHealth,
            Health = maxHealth
        };
        return e;
    }

    public static Enemy CreateStrong(Vector2 pos, int zoneId = -1, bool enhanced = false)
    {
        var maxHealth = enhanced ? 375f : 300f;
        var e = new Enemy(pos)
        {
            ZoneId = zoneId,
            IsStrong = true,
            IsEnhanced = enhanced,
            MaxHealth = maxHealth,
            Health = maxHealth
        };
        return e;
    }

    public void UpdateVisionSweep(float dt)
    {
        if (!Alive) { _deathAnim -= dt; return; }

        _sweepPhase += dt * 0.70f * _sweepDir;
        if (_sweepPhase > 1f) { _sweepPhase = 1f; _sweepDir = -1f; }
        if (_sweepPhase < -1f) { _sweepPhase = -1f; _sweepDir = 1f; }

        var baseAngle = MathF.Atan2(_baseFacing.Y, _baseFacing.X);
        var sweepOffset = _sweepPhase * (MathF.PI * 0.07f);
        var a = baseAngle + sweepOffset;
        _facing = Vector2.Normalize(new Vector2(MathF.Cos(a), MathF.Sin(a)));
    }

    public void UpdateAwareness(Vector2 playerPos, float dt, List<Obstacle> obstacles)
    {
        if (!Alive) return;

        if (_alert)
        {
            if (Vector2.Distance(Position, playerPos) <= GetAlertViewDistance())
            {
                _target = playerPos;
                return;
            }

            _alert = false;
            return;
        }

        if (CanSeePoint(playerPos, obstacles))
        {
            ForceAggro(playerPos);
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

    public bool CanNoticeCombatPoint(Vector2 point, List<Obstacle> obstacles)
    {
        var to = point - Position;
        if (to.LengthSquared() < 0.01f) return false;

        var dir = Vector2.Normalize(to);
        var angle = MathF.Acos(Math.Clamp(Vector2.Dot(_facing, dir), -1f, 1f));
        return angle <= FovHalf && VisibilityUtils.HasLineOfSight(Position, point, obstacles);
    }

    public void ForceAggro(Vector2 target)
    {
        _alert = true;
        _investigating = false;
        _returningFromInvestigation = false;
        _target = target;
    }

    public bool ReactToShot(Vector2 shotSource, List<Obstacle> obstacles)
    {
        if (CanSeePoint(shotSource, obstacles))
        {
            ForceAggro(shotSource);
            return true;
        }

        StartInvestigation(shotSource);
        return false;
    }

    private void StartInvestigation(Vector2 target)
    {
        if (_alert) return;

        if (!_investigating) _investigateReturnPoint = Position;
        _investigating = true;
        _returningFromInvestigation = false;
        _investigateTarget = target;
        _investigateWait = 0f;
    }

    public void UpdateMovement(float dt, Vector2 playerPos, List<Obstacle> obstacles, int worldSize)
    {
        _attackCd -= dt;
        _slowTimer = MathF.Max(0f, _slowTimer - dt);
        TickPoison(dt);

        if (!Alive) return;

        if (_alert)
        {
            var to = _target - Position;
            if (to.LengthSquared() > 16f)
            {
                var dir = Vector2.Normalize(to);
                _facing = dir;
                _baseFacing = dir;
                Position = MovementUtils.MoveWithCollisions(Position, dir * (IsStrong ? 118.75f : 147.5f) * GetMovementSpeedMultiplier() * dt, 14f, obstacles, worldSize);
            }

            _burstCd -= dt;
            if (IsStrong) _burstShotCd -= dt;

            return;
        }

        if (_investigating)
        {
            UpdateInvestigation(dt, obstacles, worldSize);
            return;
        }

        if (IsPatrol)
        {
            if (_patrolTurning)
            {
                _patrolTurnTimer -= dt;
                var turned = VisibilityUtils.Rotate(_facing, MathF.PI * dt / 2f);
                if (turned != Vector2.Zero)
                {
                    _facing = Vector2.Normalize(turned);
                    _baseFacing = _facing;
                }
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
                _baseFacing = dir;
                Position = MovementUtils.MoveWithCollisions(Position, dir * 107.5f * GetMovementSpeedMultiplier() * dt, 14f, obstacles, worldSize);
            }
        }
    }

    private void UpdateInvestigation(float dt, List<Obstacle> obstacles, int worldSize)
    {
        if (!_returningFromInvestigation && _investigateWait > 0f)
        {
            _investigateWait -= dt;
            if (_investigateWait <= 0f) _returningFromInvestigation = true;
            return;
        }

        var target = _returningFromInvestigation ? _investigateReturnPoint : _investigateTarget;
        var to = target - Position;
        if (to.Length() < 10f)
        {
            if (_returningFromInvestigation)
            {
                _investigating = false;
                return;
            }

            _investigateWait = 3f;
            return;
        }

        var dir = Vector2.Normalize(to);
        _baseFacing = dir;
        Position = MovementUtils.MoveWithCollisions(Position, dir * (IsStrong ? 102.5f : 120f) * GetMovementSpeedMultiplier() * dt, 14f, obstacles, worldSize);
    }

    public void ApplyStickySlow(float duration = 1f)
    {
        _slowTimer = MathF.Max(_slowTimer, duration);
    }

    public void ApplyPoison(float damagePerSecond, float duration)
    {
        if (!Alive || damagePerSecond <= 0f || duration <= 0f) return;
        _poisonDamagePerSecond = MathF.Max(_poisonDamagePerSecond, damagePerSecond);
        _poisonTimer = MathF.Max(_poisonTimer, duration);
    }

    private void TickPoison(float dt)
    {
        if (_poisonTimer <= 0f || !Alive) return;
        _poisonTimer = MathF.Max(0f, _poisonTimer - dt);
        Health = MathF.Max(0f, Health - _poisonDamagePerSecond * dt);
    }

    private float GetMovementSpeedMultiplier() => _slowTimer > 0f ? 0.7f : 1f;

    public void TryShootBurst(Vector2 playerPos, List<Projectile> projectiles)
    {
        if (!Alive || !_alert) return;

        if (!IsStrong)
        {
            if (_burstCd > 0f) return;

            var dir = playerPos - Position;
            if (dir != Vector2.Zero) dir = Vector2.Normalize(dir);
            projectiles.Add(new Projectile(Position + dir * 16f, dir, 420f, 1.68f, Palette.C(255, 120, 120), true, ScaleDamage(8f)));
            _burstCd = 2.8f;
            return;
        }

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
            projectiles.Add(new Projectile(Position + dir * 16f, dir, 420f, 1.68f, Palette.C(255, 120, 120), true, ScaleDamage(10f)));
            _burstShotsLeft--;
            _burstShotCd = 0.13f;
        }
    }

    public bool TryMeleeHit(Player player)
    {
        if (!Alive || _attackCd > 0f || Vector2.Distance(Position, player.Position) > 24f) return false;
        _attackCd = IsStrong ? 1.3f : 0.9f;
        player.TakeDamage(ScaleDamage(IsStrong ? 18f : 10f));
        return true;
    }

    private float ScaleDamage(float damage) => IsEnhanced ? damage * 1.1f : damage;

    public float GetViewDistance() => IsStrong ? StrongView : BaseView;

    private float GetAlertViewDistance() => GetViewDistance() * AlertViewMultiplier;

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
                if (IsEnhanced)
                {
                    var inner1 = Position + new Vector2(0, -8);
                    var inner2 = Position + new Vector2(-7, 7);
                    var inner3 = Position + new Vector2(7, 7);
                    Raylib.DrawTriangle(inner1, inner2, inner3, Color.White);
                    Raylib.DrawTriangleLines(inner1, inner2, inner3, Color.Black);
                }
            }
            else
            {
                Raylib.DrawCircleV(Position, 14f, theme.Enemy);
                if (IsEnhanced) Raylib.DrawCircleV(Position, 7f, Color.White);
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

        var left = VisibilityUtils.Rotate(_facing, -FovHalf);
        var right = VisibilityUtils.Rotate(_facing, FovHalf);
        var sightLineLength = GetViewDistance() * 0.75f;
        VisibilityUtils.DrawDashedLine(Position, Position + left * sightLineLength, 22, c);
        VisibilityUtils.DrawDashedLine(Position, Position + right * sightLineLength, 22, c);
    }
}

public sealed class HexEnemy
{
    public Vector2 Position;
    public float MaxHealth = 200f;
    public float Health = 200f;
    public bool Alive => Health > 0f;
    public bool KillAwarded;

    private Vector2 _facing = new(1f, 0f);
    private float _strafeSwitch;
    private float _fireCd;
    private float _burstCd;
    private int _burstLeft;
    private float _burstShotCd;
    private readonly bool _burstMode;
    private float _slowTimer;
    private float _poisonTimer;
    private float _poisonDamagePerSecond;

    private const float DesiredDistance = 290f;

    private HexEnemy(Vector2 pos, bool burstMode)
    {
        Position = pos;
        _burstMode = burstMode;
    }

    public static HexEnemy Create(Vector2 pos, Random rng) => new(pos, rng.NextSingle() < 0.5f);

    public void Update(float dt, Vector2 playerPos, List<Projectile> projectiles, List<Obstacle> obstacles, int worldSize)
    {
        if (!Alive) return;
        _slowTimer = MathF.Max(0f, _slowTimer - dt);
        TickPoison(dt);
        if (!Alive) return;

        var toPlayer = playerPos - Position;
        if (toPlayer == Vector2.Zero) toPlayer = new Vector2(1f, 0f);
        var dist = toPlayer.Length();
        var dir = Vector2.Normalize(toPlayer);
        _facing = dir;

        var radial = 0f;
        if (dist > DesiredDistance + 20f) radial = 175f;
        else if (dist < DesiredDistance - 20f) radial = -137.5f;

        _strafeSwitch -= dt;
        if (_strafeSwitch <= 0f) _strafeSwitch = 0.25f + Random.Shared.NextSingle() * 0.65f;
        var strafeSign = MathF.Sin(_strafeSwitch * 8f + Position.X * 0.01f) > 0f ? 1f : -1f;
        var strafeDir = new Vector2(-dir.Y, dir.X) * strafeSign;
        var move = dir * radial + strafeDir * 100f;
        Position = MovementUtils.MoveWithCollisions(Position, move * GetMovementSpeedMultiplier() * dt, 16f, obstacles, worldSize);

        if (_burstMode)
        {
            _burstCd -= dt;
            if (_burstCd <= 0f && _burstLeft <= 0)
            {
                _burstLeft = 5;
                _burstShotCd = 0f;
                _burstCd = 1f;
            }

            _burstShotCd -= dt;
            while (_burstLeft > 0 && _burstShotCd <= 0f)
            {
                projectiles.Add(new Projectile(Position + dir * 18f, dir, 560f, 1.44f, Palette.C(255, 110, 180), true, 4f));
                _burstLeft--;
                _burstShotCd += 0.06f;
            }
        }
        else
        {
            _fireCd -= dt;
            if (_fireCd <= 0f)
            {
                projectiles.Add(new Projectile(Position + dir * 18f, dir, 560f, 1.44f, Palette.C(255, 110, 180), true, 10f));
                _fireCd = 0.5f;
            }
        }
    }

    public void Damage(float amount)
    {
        if (!Alive) return;
        Health = MathF.Max(0f, Health - amount);
    }

    public void ApplyPoison(float damagePerSecond, float duration)
    {
        if (!Alive || damagePerSecond <= 0f || duration <= 0f) return;
        _poisonDamagePerSecond = MathF.Max(_poisonDamagePerSecond, damagePerSecond);
        _poisonTimer = MathF.Max(_poisonTimer, duration);
    }

    private void TickPoison(float dt)
    {
        if (_poisonTimer <= 0f || !Alive) return;
        _poisonTimer = MathF.Max(0f, _poisonTimer - dt);
        Health = MathF.Max(0f, Health - _poisonDamagePerSecond * dt);
    }

    public void ApplyStickySlow(float duration = 1f)
    {
        _slowTimer = MathF.Max(_slowTimer, duration);
    }

    private float GetMovementSpeedMultiplier() => _slowTimer > 0f ? 0.7f : 1f;

    public void DrawSight()
    {
    }

    public void Draw()
    {
        if (!Alive) return;

        Span<Vector2> points = stackalloc Vector2[6];
        for (var i = 0; i < 6; i++)
        {
            var a = i / 6f * MathF.Tau;
            points[i] = Position + new Vector2(MathF.Cos(a), MathF.Sin(a)) * 15f;
        }

        for (var i = 1; i < 5; i++) Raylib.DrawTriangle(points[0], points[i], points[i + 1], Palette.C(224, 84, 170));
        for (var i = 0; i < 6; i++) Raylib.DrawLineV(points[i], points[(i + 1) % 6], Color.Maroon);

        var hp = Health / MaxHealth;
        Raylib.DrawRectangle((int)Position.X - 22, (int)Position.Y - 28, 44, 5, Palette.C(20, 20, 20, 220));
        Raylib.DrawRectangle((int)Position.X - 22, (int)Position.Y - 28, (int)(44 * hp), 5, Color.Green);
    }
}

public sealed class GeneratorGuardianEnemy
{
    public Vector2 Position;
    public float MaxHealth = 1000f;
    public float Health = 1000f;
    public int ZoneId = -1;
    public bool Alive => Health > 0f;
    public bool KillAwarded;

    private readonly Vector2 _spawn;
    private bool _alert;
    private float _attackCd;
    private float _sideDashCd;
    private float _playerDashCd;
    private float _slowTimer;
    private float _poisonTimer;
    private float _poisonDamagePerSecond;
    private float _spearVisualTimer;
    private Vector2 _spearStart;
    private Vector2 _spearEnd;
    private Vector2 _facing = new(1f, 0f);

    private const float SpearStartDistance = 24f;
    private const float GoldenSpearEndDistance = 145f;
    private const float SpearHitRadius = 18f;
    private const float SpearVisualDuration = 0.18f;

    public GeneratorGuardianEnemy(Vector2 position, int zoneId)
    {
        Position = position;
        _spawn = position;
        ZoneId = zoneId;
        _sideDashCd = NextSideDashCooldown();
        _playerDashCd = NextPlayerDashCooldown();
    }

    public void Update(float dt, Vector2 playerPos, Player player, List<Obstacle> obstacles, int worldSize, List<DashAfterImage> afterImages)
    {
        _slowTimer = MathF.Max(0f, _slowTimer - dt);
        _spearVisualTimer = MathF.Max(0f, _spearVisualTimer - dt);
        TickPoison(dt);
        if (!Alive) return;

        var toPlayer = playerPos - Position;
        var playerDistance = toPlayer.Length();
        if (_alert && playerDistance > 980f)
        {
            ReturnToSpawn(dt, obstacles, worldSize);
            return;
        }

        if (!_alert)
        {
            _facing = Vector2.Normalize(_spawn - Position == Vector2.Zero ? _facing : _spawn - Position);
            return;
        }

        var dir = playerDistance <= 0.001f ? _facing : Vector2.Normalize(toPlayer);
        _facing = dir;
        Position = MovementUtils.MoveWithCollisions(Position, dir * 150f * GetMovementSpeedMultiplier() * dt, 16f, obstacles, worldSize);

        _sideDashCd -= dt;
        _playerDashCd -= dt;
        if (_sideDashCd <= 0f)
        {
            var side = VisibilityUtils.Rotate(dir, Random.Shared.NextSingle() < 0.5f ? MathF.PI * 0.5f : -MathF.PI * 0.5f);
            Position = MovementUtils.MoveWithCollisions(Position, side * 110f * GetMovementSpeedMultiplier(), 16f, obstacles, worldSize);
            DashAfterImage.Spawn(afterImages, Position, side, 110f, Palette.C(80, 220, 255), false);
            _sideDashCd = NextSideDashCooldown();
        }

        if (_playerDashCd <= 0f)
        {
            Position = MovementUtils.MoveWithCollisions(Position, dir * 170f * GetMovementSpeedMultiplier(), 16f, obstacles, worldSize);
            DashAfterImage.Spawn(afterImages, Position, dir, 170f, Palette.C(120, 230, 255), false);
            _playerDashCd = NextPlayerDashCooldown();
        }

        _attackCd -= dt;
        if (_attackCd <= 0f && playerDistance <= GoldenSpearEndDistance + 16f)
        {
            _spearStart = Position + dir * SpearStartDistance;
            _spearEnd = Position + dir * GoldenSpearEndDistance;
            _spearVisualTimer = SpearVisualDuration;
            if (DistanceToSegment(player.Position, _spearStart, _spearEnd) <= SpearHitRadius)
            {
                player.TakeDamage(30f);
            }
            _attackCd = 0.8f;
        }
    }

    private void ReturnToSpawn(float dt, List<Obstacle> obstacles, int worldSize)
    {
        var toSpawn = _spawn - Position;
        if (toSpawn.LengthSquared() <= 36f)
        {
            _alert = false;
            Health = MathF.Min(MaxHealth, Health + MaxHealth * 0.30f * dt);
            return;
        }

        var dir = Vector2.Normalize(toSpawn);
        _facing = dir;
        Position = MovementUtils.MoveWithCollisions(Position, dir * 150f * dt, 16f, obstacles, worldSize);
        Health = MathF.Min(MaxHealth, Health + MaxHealth * 0.30f * dt);
    }

    public bool CanSeePoint(Vector2 point, List<Obstacle> obstacles)
    {
        var to = point - Position;
        if (to.Length() > 702f || to.LengthSquared() < 0.01f) return false;
        var dir = Vector2.Normalize(to);
        var angle = MathF.Acos(Math.Clamp(Vector2.Dot(_facing, dir), -1f, 1f));
        return angle <= MathF.PI / 3f && VisibilityUtils.HasLineOfSight(Position, point, obstacles);
    }

    public void ForceAggro(Vector2 target)
    {
        _alert = true;
        if (target != Position) _facing = Vector2.Normalize(target - Position);
    }

    public void Damage(float amount)
    {
        if (!Alive) return;
        Health = MathF.Max(0f, Health - amount);
    }

    public void ApplyStickySlow(float duration = 1f) => _slowTimer = MathF.Max(_slowTimer, duration);

    public void ApplyPoison(float damagePerSecond, float duration)
    {
        if (!Alive || damagePerSecond <= 0f || duration <= 0f) return;
        _poisonDamagePerSecond = MathF.Max(_poisonDamagePerSecond, damagePerSecond);
        _poisonTimer = MathF.Max(_poisonTimer, duration);
    }

    private void TickPoison(float dt)
    {
        if (_poisonTimer <= 0f || !Alive) return;
        _poisonTimer = MathF.Max(0f, _poisonTimer - dt);
        Health = MathF.Max(0f, Health - _poisonDamagePerSecond * dt);
    }

    private float GetMovementSpeedMultiplier() => _slowTimer > 0f ? 0.7f : 1f;
    private static float NextSideDashCooldown() => 2f + Random.Shared.NextSingle() * 2f;
    private static float NextPlayerDashCooldown() => 1f + Random.Shared.NextSingle() * 2f;

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var denom = ab.LengthSquared();
        if (denom <= 0.0001f) return Vector2.Distance(p, a);
        var t = Math.Clamp(Vector2.Dot(p - a, ab) / denom, 0f, 1f);
        return Vector2.Distance(p, a + ab * t);
    }

    public void DrawSight()
    {
        if (!Alive) return;
        var left = VisibilityUtils.Rotate(_facing, -MathF.PI / 3f);
        var right = VisibilityUtils.Rotate(_facing, MathF.PI / 3f);
        var c = Palette.C(130, 230, 255, 90);
        VisibilityUtils.DrawDashedLine(Position, Position + left * 526f, 22, c);
        VisibilityUtils.DrawDashedLine(Position, Position + right * 526f, 22, c);
    }

    public void Draw()
    {
        if (!Alive) return;
        Raylib.DrawPoly(Position, 3, 20f, 30f, Palette.C(120, 225, 255));
        Raylib.DrawPoly(Position, 3, 20f, 210f, Palette.C(120, 225, 255));
        Raylib.DrawCircleLines((int)Position.X, (int)Position.Y, 22f, Color.White);
        DrawSpearVisual();
        DrawHealthBar(Position, Health, MaxHealth, 52);
    }

    private void DrawSpearVisual()
    {
        if (_spearVisualTimer <= 0f) return;

        var alpha = Math.Clamp(_spearVisualTimer / SpearVisualDuration, 0f, 1f);
        var color = new Color((byte)120, (byte)225, (byte)255, (byte)(255 * alpha));
        var dir = _spearEnd - _spearStart;
        if (dir.LengthSquared() <= 0.001f) return;
        dir = Vector2.Normalize(dir);
        var angle = MathF.Atan2(dir.Y, dir.X) * 180f / MathF.PI;
        var length = Vector2.Distance(_spearStart, _spearEnd);
        var center = (_spearStart + _spearEnd) * 0.5f;

        Raylib.DrawLineEx(_spearStart, _spearEnd, 5f, color);
        Raylib.DrawRectanglePro(
            new Rectangle(center.X, center.Y, length, 8f),
            new Vector2(length * 0.5f, 4f),
            angle,
            color);
        Raylib.DrawCircleV(_spearEnd, 7f, Color.White);
    }

    private static void DrawHealthBar(Vector2 position, float health, float maxHealth, int width)
    {
        var ratio = Math.Clamp(health / MathF.Max(maxHealth, 0.001f), 0f, 1f);
        Raylib.DrawRectangle((int)position.X - width / 2, (int)position.Y - 34, width, 5, Palette.C(20, 20, 20, 220));
        Raylib.DrawRectangle((int)position.X - width / 2, (int)position.Y - 34, (int)(width * ratio), 5, Color.Green);
    }
}

public sealed class ToxicTriangleEnemy
{
    public Vector2 Position;
    public float MaxHealth = 300f;
    public float Health = 300f;
    public int ZoneId = -1;
    public bool Alive => Health > 0f;
    public bool KillAwarded;

    private bool _alert;
    private float _fireCd;
    private int _burstLeft;
    private float _burstShotCd;
    private float _slowTimer;
    private float _poisonTimer;
    private float _poisonDamagePerSecond;
    private Vector2 _facing = new(1f, 0f);

    private const float ViewDistance = 375f;
    private const float FovHalf = MathF.PI / 3f;

    public ToxicTriangleEnemy(Vector2 position, int zoneId)
    {
        Position = position;
        ZoneId = zoneId;
    }

    public void Update(float dt, Vector2 playerPos, List<Projectile> projectiles, List<Obstacle> obstacles, int worldSize)
    {
        _slowTimer = MathF.Max(0f, _slowTimer - dt);
        TickPoison(dt);
        if (!Alive) return;

        var toPlayer = playerPos - Position;
        if (toPlayer == Vector2.Zero) toPlayer = new Vector2(1f, 0f);
        var dist = toPlayer.Length();
        var dir = Vector2.Normalize(toPlayer);

        if (!_alert)
        {
            if (CanSeePoint(playerPos, obstacles)) _alert = true;
            else return;
        }

        _facing = dir;

        var desiredDistance = 193f;
        var radial = dist > desiredDistance + 16f ? 182f : dist < desiredDistance - 16f ? -143f : 0f;
        var strafeDir = new Vector2(-dir.Y, dir.X) * (MathF.Sin((float)Raylib.GetTime() * 7f + Position.X * 0.01f) > 0f ? 1f : -1f);
        Position = MovementUtils.MoveWithCollisions(Position, (dir * radial + strafeDir * 104f) * GetMovementSpeedMultiplier() * dt, 16f, obstacles, worldSize);

        _fireCd -= dt;
        if (_fireCd <= 0f && _burstLeft <= 0)
        {
            _burstLeft = Random.Shared.Next(2, 10);
            _burstShotCd = 0f;
            _fireCd = 1.15f;
        }

        _burstShotCd -= dt;
        while (_burstLeft > 0 && _burstShotCd <= 0f)
        {
            var spread = ((Random.Shared.NextSingle() * 50f) - 25f) * MathF.PI / 180f;
            var shotDir = VisibilityUtils.Rotate(dir, spread);
            projectiles.Add(new Projectile(Position + shotDir * 18f, shotDir, 560f, 1.44f, Palette.C(80, 210, 70), true, 5f, playerPoisonDuration: 2f));
            _burstLeft--;
            _burstShotCd += 0.07f;
        }
    }

    public void Damage(float amount)
    {
        if (!Alive) return;
        Health = MathF.Max(0f, Health - amount);
        _alert = true;
    }

    public bool CanSeePoint(Vector2 point, List<Obstacle> obstacles)
    {
        var to = point - Position;
        var dist = to.Length();
        if (dist > ViewDistance || dist < 0.01f) return false;

        var dir = Vector2.Normalize(to);
        var angle = MathF.Acos(Math.Clamp(Vector2.Dot(_facing, dir), -1f, 1f));
        return angle <= FovHalf && VisibilityUtils.HasLineOfSight(Position, point, obstacles);
    }

    public bool CanNoticeCombatPoint(Vector2 point, List<Obstacle> obstacles)
    {
        var to = point - Position;
        var dist = to.Length();
        if (dist > ViewDistance || dist < 0.01f) return false;

        var dir = Vector2.Normalize(to);
        var angle = MathF.Acos(Math.Clamp(Vector2.Dot(_facing, dir), -1f, 1f));
        return angle <= FovHalf && VisibilityUtils.HasLineOfSight(Position, point, obstacles);
    }

    public bool ReactToShot(Vector2 shotSource, List<Obstacle> obstacles)
    {
        if (!CanSeePoint(shotSource, obstacles)) return false;

        ForceAggro(shotSource);
        return true;
    }

    public void ForceAggro(Vector2 target)
    {
        _alert = true;
        var dir = target - Position;
        if (dir != Vector2.Zero) _facing = Vector2.Normalize(dir);
    }

    public void ApplyStickySlow(float duration = 1f) => _slowTimer = MathF.Max(_slowTimer, duration);

    public void ApplyPoison(float damagePerSecond, float duration)
    {
        if (!Alive || damagePerSecond <= 0f || duration <= 0f) return;
        _poisonDamagePerSecond = MathF.Max(_poisonDamagePerSecond, damagePerSecond);
        _poisonTimer = MathF.Max(_poisonTimer, duration);
    }

    private void TickPoison(float dt)
    {
        if (_poisonTimer <= 0f || !Alive) return;
        _poisonTimer = MathF.Max(0f, _poisonTimer - dt);
        Health = MathF.Max(0f, Health - _poisonDamagePerSecond * dt);
    }

    private float GetMovementSpeedMultiplier() => _slowTimer > 0f ? 0.7f : 1f;
    public void DrawSight()
    {
        if (!Alive || _alert) return;
        var c = Palette.C(60, 180, 70, 28);
        var left = VisibilityUtils.Rotate(_facing, -FovHalf);
        var right = VisibilityUtils.Rotate(_facing, FovHalf);
        var sightLineLength = ViewDistance * 0.75f;
        VisibilityUtils.DrawDashedLine(Position, Position + left * sightLineLength, 22, c);
        VisibilityUtils.DrawDashedLine(Position, Position + right * sightLineLength, 22, c);
    }

    public void Draw()
    {
        if (!Alive) return;
        Raylib.DrawPoly(Position, 3, 18f, MathF.Atan2(_facing.Y, _facing.X) * 180f / MathF.PI + 90f, Palette.C(220, 110, 100));
        Raylib.DrawCircleLines((int)Position.X, (int)Position.Y, 20f, Palette.C(30, 120, 45));
        var hp = Health / MaxHealth;
        Raylib.DrawRectangle((int)Position.X - 24, (int)Position.Y - 30, 48, 5, Palette.C(20, 20, 20, 220));
        Raylib.DrawRectangle((int)Position.X - 24, (int)Position.Y - 30, (int)(48 * hp), 5, Color.Green);
    }
}

public sealed class TurretEnemy
{
    public Vector2 Position;
    public float MaxHealth = 260f;
    public float Health = 260f;
    public int ZoneId = -1;
    public bool Alive => Health > 0f;
    public bool KillAwarded;

    private Vector2 _facing;
    private readonly float _baseFacingAngle;
    private float _scanOffset;
    private float _scanTargetOffset = TurretScanHalfAngle;
    private float _scanWaitLeft;
    private float _shootCd;
    private bool _alert;
    private float _longRangeAlertTimer;
    private Vector2 _lastSeenPlayerPos;
    private bool _hasAim;
    private Vector2 _aimAt;
    private float _poisonTimer;
    private float _poisonDamagePerSecond;

    private const float ViewDistance = 735f;
    private const float FovHalf = MathF.PI / 3f;
    private const float TurretScanHalfAngle = MathF.PI / 3f;
    private const float TurretScanSpeed = MathF.PI / 9f;

    public TurretEnemy(Vector2 pos, float angle, int zoneId = -1)
    {
        Position = pos;
        ZoneId = zoneId;
        _baseFacingAngle = angle;
        _facing = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
    }

    public void Update(float dt, Vector2 playerPos, List<Projectile> projectiles, List<Obstacle> obstacles)
    {
        if (!Alive) return;
        TickPoison(dt);
        if (!Alive) return;

        var toPlayer = playerPos - Position;
        var distToPlayer = toPlayer.Length();
        _shootCd -= dt;
        _longRangeAlertTimer = MathF.Max(0f, _longRangeAlertTimer - dt);
        if (!_alert)
        {
            UpdateScanRotation(dt);
        }

        if (_alert && distToPlayer > ViewDistance && _longRangeAlertTimer <= 0f)
        {
            _alert = false;
            var toLast = _lastSeenPlayerPos - Position;
            if (toLast != Vector2.Zero) _facing = Vector2.Normalize(toLast);
        }

        if (CanSee(playerPos, obstacles, _longRangeAlertTimer > 0f))
        {
            if (!_alert) _shootCd = 3f;
            _alert = true;
            _lastSeenPlayerPos = playerPos;
            _hasAim = true;
            _aimAt = playerPos;
            var aimDir = playerPos - Position;
            if (aimDir != Vector2.Zero) _facing = Vector2.Normalize(aimDir);
            if (_shootCd <= 0f)
            {
                var dir = Vector2.Normalize(playerPos - Position);
                projectiles.Add(new Projectile(Position + dir * 20f, dir, 1785f, 1.84f, Palette.C(255, 40, 40), true, 56f));
                _shootCd = 3f;
            }
        }
        else
        {
            _hasAim = false;
        }
    }

    private void UpdateScanRotation(float dt)
    {
        if (_scanWaitLeft > 0f)
        {
            _scanWaitLeft -= dt;
            return;
        }

        var delta = _scanTargetOffset - _scanOffset;
        var step = MathF.Sign(delta) * MathF.Min(MathF.Abs(delta), TurretScanSpeed * dt);
        _scanOffset += step;
        SetFacingFromScan();

        if (MathF.Abs(_scanTargetOffset - _scanOffset) > 0.001f) return;

        _scanTargetOffset = _scanTargetOffset > 0f ? -TurretScanHalfAngle : TurretScanHalfAngle;
        _scanWaitLeft = 1f;
    }

    private void SetFacingFromScan()
    {
        var angle = _baseFacingAngle + _scanOffset;
        _facing = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
    }

    private bool CanSee(Vector2 point, List<Obstacle> obstacles, bool allowLongRange)
    {
        var to = point - Position;
        var dist = to.Length();
        if (dist < 110f || (!allowLongRange && dist > ViewDistance)) return false;

        if (!VisibilityUtils.HasLineOfSight(Position, point, obstacles)) return false;

        var dir = Vector2.Normalize(to);
        var angle = MathF.Acos(Math.Clamp(Vector2.Dot(_facing, dir), -1f, 1f));
        return angle <= FovHalf;
    }

    public bool CanSeePoint(Vector2 point, List<Obstacle> obstacles) => CanSee(point, obstacles, _longRangeAlertTimer > 0f);

    public bool CanNoticeCombatPoint(Vector2 point, List<Obstacle> obstacles)
    {
        var to = point - Position;
        if (to.LengthSquared() < 0.01f) return false;
        if (!VisibilityUtils.HasLineOfSight(Position, point, obstacles)) return false;

        var dir = Vector2.Normalize(to);
        var angle = MathF.Acos(Math.Clamp(Vector2.Dot(_facing, dir), -1f, 1f));
        return angle <= FovHalf;
    }

    public void ForceAggro(Vector2 target)
    {
        if (!_alert) _shootCd = 3f;
        _alert = true;
        _lastSeenPlayerPos = target;
        _hasAim = false;
        _aimAt = target;
        var aimDir = target - Position;
        if (aimDir != Vector2.Zero) _facing = Vector2.Normalize(aimDir);
    }

    public bool ReactToShot(Vector2 shotSource, Vector2 playerPos, List<Obstacle> obstacles)
    {
        if (!CanSee(shotSource, obstacles, true)) return false;

        ForceAggro(playerPos);
        if (Vector2.Distance(Position, shotSource) > ViewDistance) _longRangeAlertTimer = 5f;
        return true;
    }

    public void Damage(float amount)
    {
        if (!Alive) return;
        Health = MathF.Max(0f, Health - amount);
    }

    public void ApplyPoison(float damagePerSecond, float duration)
    {
        if (!Alive || damagePerSecond <= 0f || duration <= 0f) return;
        _poisonDamagePerSecond = MathF.Max(_poisonDamagePerSecond, damagePerSecond);
        _poisonTimer = MathF.Max(_poisonTimer, duration);
    }

    private void TickPoison(float dt)
    {
        if (_poisonTimer <= 0f || !Alive) return;
        _poisonTimer = MathF.Max(0f, _poisonTimer - dt);
        Health = MathF.Max(0f, Health - _poisonDamagePerSecond * dt);
    }

    public void ApplyStickySlow(float duration = 1f) { }

    public void DrawSight()
    {
        if (!Alive) return;
        if (_alert) return;

        var left = VisibilityUtils.Rotate(_facing, -FovHalf);
        var right = VisibilityUtils.Rotate(_facing, FovHalf);
        var sightLineLength = ViewDistance * 0.75f;
        VisibilityUtils.DrawDashedLine(Position, Position + left * sightLineLength, 24, Palette.C(250, 80, 80, 24));
        VisibilityUtils.DrawDashedLine(Position, Position + right * sightLineLength, 24, Palette.C(250, 80, 80, 24));
    }

    public void DrawAimLine()
    {
        if (!Alive || !_hasAim) return;
        Raylib.DrawLineEx(Position, _aimAt, 1.5f, Palette.C(255, 40, 40, 190));
    }

    public void Draw()
    {
        if (!Alive) return;

        var mainTip = Position + _facing * 18f;
        var mainLeft = Position + VisibilityUtils.Rotate(_facing, MathF.PI * 0.8f) * 14f;
        var mainRight = Position + VisibilityUtils.Rotate(_facing, -MathF.PI * 0.8f) * 14f;

        Raylib.DrawTriangle(mainTip, mainLeft, mainRight, Palette.C(240, 170, 90));
        Raylib.DrawTriangleLines(mainTip, mainLeft, mainRight, Color.Maroon);

        DrawMiniTriangle(mainTip, _facing);
        DrawMiniTriangle(mainLeft, Vector2.Normalize(mainLeft - Position));
        DrawMiniTriangle(mainRight, Vector2.Normalize(mainRight - Position));

        var hp = Health / MaxHealth;
        Raylib.DrawRectangle((int)Position.X - 24, (int)Position.Y - 30, 48, 5, Palette.C(20, 20, 20, 220));
        Raylib.DrawRectangle((int)Position.X - 24, (int)Position.Y - 30, (int)(48f * hp), 5, Color.Green);
    }

    private static void DrawMiniTriangle(Vector2 center, Vector2 dir)
    {
        var tip = center + dir * 6f;
        var left = center + VisibilityUtils.Rotate(dir, MathF.PI * 0.75f) * 4f;
        var right = center + VisibilityUtils.Rotate(dir, -MathF.PI * 0.75f) * 4f;
        Raylib.DrawTriangle(tip, left, right, Palette.C(220, 120, 70));
        Raylib.DrawTriangleLines(tip, left, right, Color.Brown);
    }
}

public sealed class MiniBossEnemySquare
{
    public Vector2 Position;
    public float MaxHealth = 1750f;
    public float Health = 1750f;
    public int ZoneId = -1;
    public bool Alive => Health > 0;
    public bool KillAwarded;

    private float _ramCd = 4f;
    private float _shootCd = 1.2f;
    private int _burstShotsLeft;
    private float _burstShotCd;
    private float _slamCd = 3.5f;
    private float _slamVisual;
    private bool _alert;
    private bool _investigating;
    private bool _returningFromInvestigation;
    private Vector2 _investigateTarget;
    private Vector2 _investigateReturnPoint;
    private float _investigateWait;
    private Vector2 _facing = new(1f, 0f);
    private float _slowTimer;
    private float _poisonTimer;
    private float _poisonDamagePerSecond;

    private const float ViewDistance = 650f;
    private const float AlertViewMultiplier = 1.25f;
    private const float FovHalf = MathF.PI / 3f;

    public MiniBossEnemySquare(Vector2 pos, int zoneId = -1) { Position = pos; ZoneId = zoneId; }

    public void Update(float dt, Vector2 playerPos, List<Projectile> projectiles, Player player, List<Obstacle> obstacles, int worldSize, List<DashAfterImage> afterImages)
    {
        if (!Alive) return;

        _slowTimer = MathF.Max(0f, _slowTimer - dt);
        TickPoison(dt);
        if (!Alive) return;
        _ramCd -= dt;
        _shootCd -= dt;
        _slamCd -= dt;
        _slamVisual -= dt;

        var toPlayer = playerPos - Position;
        var distanceToPlayer = toPlayer.Length();

        if (_alert)
        {
            if (distanceToPlayer > GetAlertViewDistance()) _alert = false;
        }
        else if (CanSeePoint(playerPos, obstacles))
        {
            ForceAggro(playerPos);
        }

        if (!_alert)
        {
            if (_investigating) UpdateInvestigation(dt, obstacles, worldSize);
            return;
        }

        if (toPlayer == Vector2.Zero) return;

        var dir = Vector2.Normalize(toPlayer);
        _facing = dir;
        Position = MovementUtils.MoveWithCollisions(Position, dir * 52.5f * GetMovementSpeedMultiplier() * dt, 28f, obstacles, worldSize);

        if (_ramCd <= 0f)
        {
            Position = MovementUtils.MoveWithCollisions(Position, dir * 150f * GetMovementSpeedMultiplier(), 28f, obstacles, worldSize);
            DashAfterImage.Spawn(afterImages, Position, dir, 150f, Palette.C(230, 100, 100), true);
            _ramCd = 4f;
            if (Vector2.Distance(Position, playerPos) < 56f) player.TakeDamage(24f);
        }

        if (_shootCd <= 0f && _burstShotsLeft <= 0)
        {
            _burstShotsLeft = 6;
            _burstShotCd = 0f;
            _shootCd = 1.9f;
        }

        if (_burstShotsLeft > 0)
        {
            _burstShotCd -= dt;
            while (_burstShotsLeft > 0 && _burstShotCd <= 0f)
            {
                var spread = ((Random.Shared.NextSingle() * 4f) - 2f) * (MathF.PI / 180f);
                var shotDir = VisibilityUtils.Rotate(dir, spread);
                projectiles.Add(new Projectile(Position + shotDir * 28f, shotDir, 560f, 1.62f, Palette.C(255, 150, 120), true, 13f));
                _burstShotsLeft--;
                _burstShotCd += 0.08f;
            }
        }

        if (_slamCd <= 0f)
        {
            _slamVisual = 0.7f;
            _slamCd = 3.6f;
            if (Vector2.Distance(Position, playerPos) < 120f) player.TakeDamage(20f);
        }
    }

    public bool CanSeePoint(Vector2 point, List<Obstacle> obstacles)
    {
        var to = point - Position;
        var dist = to.Length();
        if (dist > ViewDistance || dist < 0.01f) return false;

        var dir = Vector2.Normalize(to);
        var angle = MathF.Acos(Math.Clamp(Vector2.Dot(_facing, dir), -1f, 1f));
        return angle <= FovHalf && VisibilityUtils.HasLineOfSight(Position, point, obstacles);
    }

    public bool CanNoticeCombatPoint(Vector2 point, List<Obstacle> obstacles)
    {
        var to = point - Position;
        if (to.LengthSquared() < 0.01f) return false;

        var dir = Vector2.Normalize(to);
        var angle = MathF.Acos(Math.Clamp(Vector2.Dot(_facing, dir), -1f, 1f));
        return angle <= FovHalf && VisibilityUtils.HasLineOfSight(Position, point, obstacles);
    }

    public void ForceAggro(Vector2 target)
    {
        _alert = true;
        _investigating = false;
        _returningFromInvestigation = false;
        var dir = target - Position;
        if (dir != Vector2.Zero) _facing = Vector2.Normalize(dir);
    }

    public bool ReactToShot(Vector2 shotSource, List<Obstacle> obstacles)
    {
        if (CanSeePoint(shotSource, obstacles))
        {
            ForceAggro(shotSource);
            return true;
        }

        StartInvestigation(shotSource);
        return false;
    }

    private void StartInvestigation(Vector2 target)
    {
        if (_alert) return;

        if (!_investigating) _investigateReturnPoint = Position;
        _investigating = true;
        _returningFromInvestigation = false;
        _investigateTarget = target;
        _investigateWait = 0f;
    }

    private void UpdateInvestigation(float dt, List<Obstacle> obstacles, int worldSize)
    {
        if (!_returningFromInvestigation && _investigateWait > 0f)
        {
            _investigateWait -= dt;
            if (_investigateWait <= 0f) _returningFromInvestigation = true;
            return;
        }

        var target = _returningFromInvestigation ? _investigateReturnPoint : _investigateTarget;
        var to = target - Position;
        if (to.Length() < 14f)
        {
            if (_returningFromInvestigation)
            {
                _investigating = false;
                return;
            }

            _investigateWait = 3f;
            return;
        }

        var dir = Vector2.Normalize(to);
        _facing = dir;
        Position = MovementUtils.MoveWithCollisions(Position, dir * 47.5f * GetMovementSpeedMultiplier() * dt, 28f, obstacles, worldSize);
    }

    public void Damage(float amount)
    {
        if (!Alive) return;
        Health = MathF.Max(0f, Health - amount);
    }

    public void ApplyPoison(float damagePerSecond, float duration)
    {
        if (!Alive || damagePerSecond <= 0f || duration <= 0f) return;
        _poisonDamagePerSecond = MathF.Max(_poisonDamagePerSecond, damagePerSecond);
        _poisonTimer = MathF.Max(_poisonTimer, duration);
    }

    private void TickPoison(float dt)
    {
        if (_poisonTimer <= 0f || !Alive) return;
        _poisonTimer = MathF.Max(0f, _poisonTimer - dt);
        Health = MathF.Max(0f, Health - _poisonDamagePerSecond * dt);
    }

    public void ApplyStickySlow(float duration = 1f)
    {
        _slowTimer = MathF.Max(_slowTimer, duration);
    }

    private float GetMovementSpeedMultiplier() => _slowTimer > 0f ? 0.85f : 1f;

    private static float GetAlertViewDistance() => ViewDistance * AlertViewMultiplier;

    public void DrawSight()
    {
        if (!Alive) return;

        var c = Palette.C(255, 130, 110, 24);
        var sightLineLength = ViewDistance * 0.75f;
        VisibilityUtils.DrawDashedLine(Position, Position + VisibilityUtils.Rotate(_facing, -FovHalf) * sightLineLength, 24, c);
        VisibilityUtils.DrawDashedLine(Position, Position + VisibilityUtils.Rotate(_facing, FovHalf) * sightLineLength, 24, c);
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

public sealed class StationBossEnemy
{
    public Vector2 Position;
    public float MaxHealth = 4000f;
    public float Health = 4000f;
    public bool Alive => Health > 0f;
    public bool KillAwarded;
    public bool Active { get; private set; }
    public bool PhaseTwo { get; private set; }

    private readonly Rectangle _arena;
    private Vector2 _dashDir;
    private float _dashWindup;
    private bool _dashing;
    private float _stunTimer;
    private float _fireCd;
    private int _burstShotsLeft;
    private float _burstShotCd;
    private float _grenadeCd = 3f;
    private int _grenadesLeft;
    private float _grenadeShotCd;
    private bool _grayHealUsed;
    private float _grayHealTimer;
    private float _slowTimer;
    private float _poisonTimer;
    private float _poisonDamagePerSecond;

    public StationBossEnemy(Vector2 position, Rectangle arena)
    {
        Position = position;
        _arena = arena;
    }

    public void Activate() => Active = true;

    public void Update(float dt, Vector2 playerPos, List<Projectile> projectiles, Player player, List<Obstacle> obstacles, int worldSize)
    {
        _slowTimer = MathF.Max(0f, _slowTimer - dt);
        TickPoison(dt);
        if (!Alive || !Active) return;

        var toPlayer = playerPos - Position;
        var dir = toPlayer == Vector2.Zero ? new Vector2(1f, 0f) : Vector2.Normalize(toPlayer);

        if (_grayHealTimer > 0f)
        {
            _grayHealTimer -= dt;
            Health = MathF.Min(MaxHealth, Health + MaxHealth * 0.03f * dt);
            if (_grayHealTimer <= 0f && !PhaseTwo) EnterPhaseTwo(dir);
            return;
        }

        if (!_grayHealUsed && Health <= MaxHealth * 0.2f)
        {
            _grayHealUsed = true;
            _grayHealTimer = 5f;
            return;
        }

        if (_stunTimer > 0f)
        {
            _stunTimer -= dt;
            if (PhaseTwo && _stunTimer <= 0f) StartDash(dir, 0.15f);
            return;
        }

        if (_dashWindup > 0f)
        {
            _dashWindup -= dt;
            if (_dashWindup <= 0f)
            {
                _dashing = true;
            }
            return;
        }

        if (_dashing)
        {
            var dashSpeed = PhaseTwo ? 1250f : 1000f;
            var (next, hitWall) = MoveDashUntilCollision(Position, _dashDir * dashSpeed * dt, 32f, obstacles, worldSize);
            Position = next;
            if (Vector2.Distance(Position, player.Position) <= 96f) player.TakeDamage(100f);
            if (hitWall)
            {
                _dashing = false;
                FireRadialBurst(projectiles);
                _stunTimer = PhaseTwo ? 0.75f : 4f;
            }
            return;
        }

        Position = MovementUtils.MoveWithCollisions(Position, dir * 200f * GetMovementSpeedMultiplier() * dt, 32f, obstacles, worldSize);
        Position = Vector2.Clamp(Position, new Vector2(_arena.X + 36f, _arena.Y + 36f), new Vector2(_arena.X + _arena.Width - 36f, _arena.Y + _arena.Height - 36f));

        if (PhaseTwo)
        {
            if (_dashWindup <= 0f && !_dashing) StartDash(dir, 0.15f);
            return;
        }

        _fireCd -= dt;
        if (_fireCd <= 0f && _burstShotsLeft <= 0)
        {
            _burstShotsLeft = 4;
            _burstShotCd = 0f;
            _fireCd = 0.95f;
        }

        _burstShotCd -= dt;
        while (_burstShotsLeft > 0 && _burstShotCd <= 0f)
        {
            projectiles.Add(new Projectile(Position + dir * 28f, dir, 620f, 1.5f, Palette.C(255, 120, 120), true, 20f));
            _burstShotsLeft--;
            _burstShotCd += _burstShotsLeft == 2 ? 0.18f : 0.06f;
        }

        _grenadeCd -= dt;
        if (_grenadeCd <= 0f && _grenadesLeft <= 0)
        {
            _grenadesLeft = 3;
            _grenadeShotCd = 0f;
            _grenadeCd = 2.67f;
        }

        _grenadeShotCd -= dt;
        while (_grenadesLeft > 0 && _grenadeShotCd <= 0f)
        {
            var spread = ((Random.Shared.NextSingle() * 60f) - 30f) * MathF.PI / 180f;
            var grenadeDir = VisibilityUtils.Rotate(dir, spread);
            projectiles.Add(new Projectile(Position + grenadeDir * 32f, grenadeDir, 300f, 1.125f, Palette.C(255, 155, 90), true, 0f, ProjectileKind.Grenade, 40f, 25f, 5f));
            _grenadesLeft--;
            _grenadeShotCd += 0.107f;
        }

        if (Random.Shared.NextSingle() < dt * 0.22f)
        {
            StartDash(dir, 1f);
        }
    }

    private void EnterPhaseTwo(Vector2 dir)
    {
        PhaseTwo = true;
        _burstShotsLeft = 0;
        _grenadesLeft = 0;
        _fireCd = 999f;
        _grenadeCd = 999f;
        _stunTimer = 0.75f;
        _dashDir = dir;
    }

    private void StartDash(Vector2 dir, float windup)
    {
        _dashDir = dir == Vector2.Zero ? new Vector2(1f, 0f) : Vector2.Normalize(dir);
        _dashWindup = windup;
    }

    private (Vector2 Position, bool HitWall) MoveDashUntilCollision(Vector2 position, Vector2 delta, float radius, List<Obstacle> obstacles, int worldSize)
    {
        var steps = Math.Max(1, (int)MathF.Ceiling(delta.Length() / MathF.Max(4f, radius * 0.5f)));
        var step = delta / steps;
        var next = position;
        var min = new Vector2(_arena.X + radius, _arena.Y + radius);
        var max = new Vector2(_arena.X + _arena.Width - radius, _arena.Y + _arena.Height - radius);

        for (var i = 0; i < steps; i++)
        {
            var candidate = next + step;
            var clamped = Vector2.Clamp(candidate, min, max);
            if (clamped != candidate) return (next, true);
            if (MovementUtils.CircleHitsObstacle(candidate, radius, obstacles)) return (next, true);
            if (candidate.X < radius || candidate.Y < radius || candidate.X > worldSize - radius || candidate.Y > worldSize - radius) return (next, true);
            next = candidate;
        }

        return (next, false);
    }

    public void Damage(float amount)
    {
        if (!Alive) return;
        if (_grayHealTimer > 0f)
        {
            Health = MathF.Min(MaxHealth, Health + amount);
            return;
        }

        Health = MathF.Max(0f, Health - amount);
    }

    public bool TryApplySegmentDamage(Vector2 from, Vector2 to, float radius, float damage)
    {
        if (!Alive || !Active) return false;
        if (DistanceToSegment(Position, from, to) > radius + 34f) return false;
        Damage(damage);
        return true;
    }

    public bool IntersectsAnyHitZone(Vector2 position, float radius)
        => Alive && Active && Vector2.Distance(Position, position) <= radius + 34f;

    public void ApplyExplosionDamage(Vector2 position, float radius, float damage)
    {
        if (IntersectsAnyHitZone(position, radius)) Damage(damage);
    }

    public void ApplyStickySlow(float duration = 1f) => _slowTimer = MathF.Max(_slowTimer, duration);

    public void ApplyPoison(float damagePerSecond, float duration)
    {
        if (!Alive || damagePerSecond <= 0f || duration <= 0f) return;
        _poisonDamagePerSecond = MathF.Max(_poisonDamagePerSecond, damagePerSecond);
        _poisonTimer = MathF.Max(_poisonTimer, duration);
    }

    private void TickPoison(float dt)
    {
        if (_poisonTimer <= 0f || !Alive) return;
        _poisonTimer = MathF.Max(0f, _poisonTimer - dt);
        Damage(_poisonDamagePerSecond * dt);
    }

    private float GetMovementSpeedMultiplier() => _slowTimer > 0f ? 0.95f : 1f;

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var denom = ab.LengthSquared();
        if (denom <= 0.0001f) return Vector2.Distance(p, a);
        var t = Math.Clamp(Vector2.Dot(p - a, ab) / denom, 0f, 1f);
        return Vector2.Distance(p, a + ab * t);
    }

    private void FireRadialBurst(List<Projectile> projectiles)
    {
        const int count = 18;
        for (var i = 0; i < count; i++)
        {
            var angle = i / (float)count * MathF.Tau;
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            projectiles.Add(new Projectile(Position + dir * 34f, dir, 620f, 1.5f, Palette.C(255, 120, 120), true, 16f));
        }
    }

    public void DrawSight() { }

    public void Draw()
    {
        if (!Alive) return;
        var fill = _grayHealTimer > 0f ? Palette.C(130, 130, 130) : PhaseTwo ? Palette.C(120, 30, 32) : Palette.C(210, 40, 44);
        Raylib.DrawCircleV(Position, 34f, fill);
        if (_dashing) Raylib.DrawCircleV(Position, 68f, Palette.C(255, 70, 70, 60));
        var tri = _dashWindup > 0f ? Color.Yellow : Color.White;
        Raylib.DrawPoly(Position + new Vector2(-8f, 0f), 3, 13f, 90f, tri);
        Raylib.DrawPoly(Position + new Vector2(10f, 0f), 3, 13f, -90f, Color.Black);
        if (PhaseTwo)
        {
            Raylib.DrawPoly(Position + new Vector2(0f, -13f), 3, 11f, 180f, Color.Black);
            Raylib.DrawPoly(Position + new Vector2(0f, 13f), 3, 11f, 0f, Color.Black);
        }
        Raylib.DrawCircleLines((int)Position.X, (int)Position.Y, 36f, Color.White);
        var hp = Health / MaxHealth;
        Raylib.DrawRectangle((int)Position.X - 58, (int)Position.Y - 54, 116, 7, Palette.C(20, 20, 20, 230));
        Raylib.DrawRectangle((int)Position.X - 58, (int)Position.Y - 54, (int)(116 * hp), 7, Color.Green);
    }
}

public sealed class BossEnemyDestroyer
{
    public Vector2 Position;
    public float MaxHealth = 4500f;
    public float Health = 4500f;
    public bool Alive => Health > 0f;
    public bool KillAwarded;
    public bool PhaseTwo => Health <= MaxHealth * 0.5f;

    private const float ShieldNodeMaxHealth = 175f;
    private const float ShieldNodeSize = 28f;
    private const float DestroyedShieldNodeSize = ShieldNodeSize * 0.5f;

    private Vector2 _facing = new(1f, 0f);
    private float _forwardDashCd = 1.5f;
    private float _sideDashCd = 2.3f;
    private float _shootCd = 1.2f;
    private float _grenadeCd = 4.6f;
    private float _radialShotCd = 3.3f;
    private float _strafeSwitch;
    private int _burstShotsLeft;
    private float _burstShotCd;
    private bool _alert;
    private bool _phaseTwoShieldReset;
    private float _slowTimer;
    private float _poisonTimer;
    private float _poisonDamagePerSecond;

    private const float ViewDistance = 825f;
    private const float AlertViewMultiplier = 1.25f;
    private const float PhaseOneSpeed = 72.5f;
    private const float PhaseTwoSpeed = 190.625f;
    private const float DesiredDistance = 270f;
    private const float DashDistance = 152.5f;
    private const float SideDashDistance = DashDistance * 0.5f;
    private const float CollisionRadius = 52f;
    private const float BulletSpeed = 520f;
    private const float BulletDamage = 16f;
    private const float BulletLifetime = 1.25f;
    private const float PhaseTwoRangeMultiplier = 1.25f;
    private readonly float[] _shieldNodeHealth = [ShieldNodeMaxHealth, ShieldNodeMaxHealth, ShieldNodeMaxHealth, ShieldNodeMaxHealth];

    public BossEnemyDestroyer(Vector2 pos)
    {
        Position = pos;
    }

    public void Update(float dt, Vector2 playerPos, List<Projectile> projectiles, Player player, List<Obstacle> obstacles, int worldSize, List<DashAfterImage> afterImages)
    {
        if (!Alive) return;

        _slowTimer = MathF.Max(0f, _slowTimer - dt);
        TickPoison(dt);
        if (!Alive) return;
        _forwardDashCd -= dt;
        _sideDashCd -= dt;
        _shootCd -= dt;
        _grenadeCd -= dt;
        if (PhaseTwo) _radialShotCd -= dt;
        else _radialShotCd = 3f;

        var toPlayer = playerPos - Position;
        if (toPlayer == Vector2.Zero) toPlayer = new Vector2(1f, 0f);
        var distance = toPlayer.Length();
        var dir = Vector2.Normalize(toPlayer);
        _facing = dir;

        if (PhaseTwo && !_phaseTwoShieldReset)
        {
            RestoreShieldNodes();
            _phaseTwoShieldReset = true;
        }

        if (_alert)
        {
            if (distance > GetAlertViewDistance()) _alert = false;
        }
        else if (VisibilityUtils.HasLineOfSight(Position, playerPos, obstacles) && distance <= ViewDistance)
        {
            _alert = true;
        }

        if (!_alert) return;

        if (PhaseTwo)
        {
            UpdatePhaseTwoMovement(dt, dir, distance, obstacles, worldSize);
        }
        else
        {
            Position = MovementUtils.MoveWithCollisions(Position, dir * PhaseOneSpeed * GetMovementSpeedMultiplier() * dt, CollisionRadius, obstacles, worldSize);
        }

        if (_forwardDashCd <= 0f)
        {
            ExecuteDash(player, dir, DashDistance, 34f, afterImages, obstacles, worldSize);
            _forwardDashCd = 1f + Random.Shared.NextSingle() * 2f;
        }

        if (_sideDashCd <= 0f)
        {
            var sideDir = VisibilityUtils.Rotate(dir, Random.Shared.NextSingle() < 0.5f ? MathF.PI / 2f : -MathF.PI / 2f);
            ExecuteDash(player, sideDir, SideDashDistance, 22f, afterImages, obstacles, worldSize);
            _sideDashCd = 1f + Random.Shared.NextSingle() * 3f;
        }

        if (_shootCd <= 0f && _burstShotsLeft <= 0)
        {
            _burstShotsLeft = PhaseTwo ? 8 : 6;
            _burstShotCd = 0f;
            _shootCd = PhaseTwo ? 1.5f : 2f;
        }

        _burstShotCd -= dt;
        while (_burstShotsLeft > 0 && _burstShotCd <= 0f)
        {
            FireBurst(projectiles, dir);
            _burstShotsLeft--;
            _burstShotCd += 0.08f;
        }

        if (!PhaseTwo && _grenadeCd <= 0f)
        {
            projectiles.Add(new Projectile(
                Position + dir * 42f,
                dir,
                340f,
                0.68f,
                Palette.C(255, 90, 40),
                true,
                0f,
                ProjectileKind.Grenade,
                120f,
                80f,
                8f));
            _grenadeCd = 3f + Random.Shared.NextSingle() * 4f;
        }

        if (PhaseTwo && _radialShotCd <= 0f)
        {
            FireRadialBurst(projectiles);
            _radialShotCd = 3f;
        }
    }

    private void UpdatePhaseTwoMovement(float dt, Vector2 dir, float distance, List<Obstacle> obstacles, int worldSize)
    {
        var radial = 0f;
        if (distance > DesiredDistance + 25f) radial = PhaseTwoSpeed;
        else if (distance < DesiredDistance - 20f) radial = -PhaseTwoSpeed * 0.75f;

        _strafeSwitch -= dt;
        if (_strafeSwitch <= 0f) _strafeSwitch = 0.22f + Random.Shared.NextSingle() * 0.55f;
        var strafeSign = MathF.Sin(_strafeSwitch * 8f + Position.X * 0.015f) > 0f ? 1f : -1f;
        var strafeDir = new Vector2(-dir.Y, dir.X) * strafeSign;
        var move = dir * radial + strafeDir * (PhaseTwoSpeed * 0.75f);
        Position = MovementUtils.MoveWithCollisions(Position, move * GetMovementSpeedMultiplier() * dt, CollisionRadius, obstacles, worldSize);
    }

    private void ExecuteDash(Player player, Vector2 dashDir, float distance, float damage, List<DashAfterImage> afterImages, List<Obstacle> obstacles, int worldSize)
    {
        Position = MovementUtils.MoveWithCollisions(Position, dashDir * distance, CollisionRadius, obstacles, worldSize);
        DashAfterImage.Spawn(afterImages, Position, dashDir, distance, Palette.C(255, 85, 85), true);
        if (Vector2.Distance(Position, player.Position) < 76f) player.TakeDamage(damage);
    }

    private void FireBurst(List<Projectile> projectiles, Vector2 dir)
    {
        var burstAngles = PhaseTwo
            ? new[] { -0.2f, -0.1f, 0f, 0.1f, 0.2f }
            : new[] { -0.11f, 0.11f };

        foreach (var offset in burstAngles)
        {
            var spread = ((Random.Shared.NextSingle() * 3f) - 1.5f) * (MathF.PI / 180f);
            var shotDir = VisibilityUtils.Rotate(dir, offset + spread);
            projectiles.Add(CreateBullet(shotDir));
        }
    }

    private void FireRadialBurst(List<Projectile> projectiles)
    {
        for (var i = 0; i < 20; i++)
        {
            var angle = i / 20f * MathF.Tau;
            var shotDir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            projectiles.Add(CreateBullet(shotDir));
        }
    }

    private Projectile CreateBullet(Vector2 dir)
    {
        var lifetime = BulletLifetime * (PhaseTwo ? PhaseTwoRangeMultiplier : 1f);
        return new Projectile(Position + dir * 40f, dir, BulletSpeed, lifetime, Palette.C(255, 140, 110), true, BulletDamage);
    }

    private float GetMovementSpeedMultiplier() => _slowTimer > 0f ? 0.95f : 1f;

    private static float GetAlertViewDistance() => ViewDistance * AlertViewMultiplier;

    public bool CanSeePoint(Vector2 point, List<Obstacle> obstacles)
    {
        var to = point - Position;
        var dist = to.Length();
        return dist <= ViewDistance && VisibilityUtils.HasLineOfSight(Position, point, obstacles);
    }

    public void ForceAggro(Vector2 target)
    {
        _alert = true;
        var dir = target - Position;
        if (dir != Vector2.Zero) _facing = Vector2.Normalize(dir);
    }

    public void ApplyStickySlow(float duration = 1f)
    {
        _slowTimer = MathF.Max(_slowTimer, duration);
    }

    public void ApplyPoison(float damagePerSecond, float duration)
    {
        if (!Alive || damagePerSecond <= 0f || duration <= 0f) return;
        _poisonDamagePerSecond = MathF.Max(_poisonDamagePerSecond, damagePerSecond);
        _poisonTimer = MathF.Max(_poisonTimer, duration);
    }

    private void TickPoison(float dt)
    {
        if (_poisonTimer <= 0f || !Alive) return;
        _poisonTimer = MathF.Max(0f, _poisonTimer - dt);
        if (!ShieldActive) DamageCore(_poisonDamagePerSecond * dt);
    }

    public bool IntersectsAnyHitZone(Vector2 point, float radius)
    {
        if (!Alive) return false;

        for (var i = 0; i < _shieldNodeHealth.Length; i++)
        {
            if (!IsShieldNodeAlive(i)) continue;

            var limit = radius + GetShieldNodeHitRadius(i);
            if (Vector2.DistanceSquared(GetShieldNodePosition(i), point) <= limit * limit) return true;
        }

        var bodyLimit = radius + GetBodyHitRadius();
        return Vector2.DistanceSquared(Position, point) <= bodyLimit * bodyLimit;
    }

    public bool TryApplyPointDamage(Vector2 point, float radius, float amount)
    {
        if (!Alive) return false;

        var shieldIndex = FindShieldNodeHit(point, radius);
        if (shieldIndex >= 0)
        {
            DamageShieldNode(shieldIndex, amount);
            return true;
        }

        var bodyLimit = radius + GetBodyHitRadius();
        if (Vector2.DistanceSquared(Position, point) > bodyLimit * bodyLimit) return false;

        if (!ShieldActive) DamageCore(amount);
        return true;
    }

    public bool TryApplySegmentDamage(Vector2 from, Vector2 to, float radius, float amount)
    {
        if (!Alive) return false;

        var shieldIndex = FindShieldNodeHit(from, to, radius);
        if (shieldIndex >= 0)
        {
            DamageShieldNode(shieldIndex, amount);
            return true;
        }

        var bodyLimit = radius + GetBodyHitRadius();
        if (DistanceToSegment(Position, from, to) > bodyLimit) return false;

        if (!ShieldActive) DamageCore(amount);
        return true;
    }

    public bool ApplyExplosionDamage(Vector2 center, float radius, float amount)
    {
        if (!Alive) return false;

        var hitAny = false;
        var shieldWasActive = ShieldActive;

        for (var i = 0; i < _shieldNodeHealth.Length; i++)
        {
            if (!IsShieldNodeAlive(i)) continue;

            var limit = radius + GetShieldNodeHitRadius(i);
            if (Vector2.DistanceSquared(GetShieldNodePosition(i), center) > limit * limit) continue;

            DamageShieldNode(i, amount);
            hitAny = true;
        }

        var bodyLimit = radius + GetBodyHitRadius();
        if (Vector2.DistanceSquared(Position, center) <= bodyLimit * bodyLimit)
        {
            if (!shieldWasActive) DamageCore(amount);
            hitAny = true;
        }

        return hitAny;
    }

    public void Damage(float amount)
    {
        if (!Alive || ShieldActive) return;
        DamageCore(amount);
    }

    public void DrawSight()
    {
        if (!Alive) return;
    }

    public void Draw()
    {
        if (!Alive) return;

        var mainSize = GetBodySize();
        if (PhaseTwo) DrawDiamond(Position, mainSize, Palette.C(165, 36, 36), Color.Maroon);
        else DrawSquare(Position, mainSize, Palette.C(120, 20, 20), Color.Maroon);

        if (!PhaseTwo || ShieldActive)
        {
            for (var i = 0; i < _shieldNodeHealth.Length; i++)
            {
                var hpRatio = Math.Clamp(_shieldNodeHealth[i] / ShieldNodeMaxHealth, 0f, 1f);
                var fill = BlendColor(Palette.C(220, 52, 52), Color.White, 1f - hpRatio);
                var line = IsShieldNodeAlive(i) ? Color.Black : Palette.C(180, 180, 180);
                DrawSquare(GetShieldNodePosition(i), GetShieldNodeSize(i), fill, line);
            }
        }

        var hp = Health / MaxHealth;
        var bar = new Rectangle(Position.X - 72, Position.Y - 76, 144, 10);

        if (ShieldActive)
        {
            var shieldFrame = new Rectangle(bar.X - 6f, bar.Y - 4f, bar.Width + 12f, bar.Height + 8f);
            Raylib.DrawRectangleRec(shieldFrame, Palette.C(48, 48, 48, 165));
            Raylib.DrawRectangleLinesEx(shieldFrame, 4f, Palette.C(165, 165, 165, 235));
        }

        Raylib.DrawRectangleRec(bar, Palette.C(20, 20, 20, 220));
        Raylib.DrawRectangle((int)bar.X, (int)bar.Y, (int)(bar.Width * hp), (int)bar.Height, PhaseTwo ? Color.Orange : Color.Red);
    }

    private bool ShieldActive
    {
        get
        {
            for (var i = 0; i < _shieldNodeHealth.Length; i++)
            {
                if (_shieldNodeHealth[i] > 0f) return true;
            }

            return false;
        }
    }

    private static Color BlendColor(Color from, Color to, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Color(
            (byte)(from.R + (to.R - from.R) * t),
            (byte)(from.G + (to.G - from.G) * t),
            (byte)(from.B + (to.B - from.B) * t),
            (byte)(from.A + (to.A - from.A) * t));
    }

    private float GetBodySize() => PhaseTwo ? 92f : 84f;

    private float GetBodyHitRadius() => PhaseTwo ? 46f : 42f;

    private Vector2 GetShieldNodePosition(int index)
    {
        var offset = GetBodySize() * 0.5f;
        return index switch
        {
            0 => Position + new Vector2(-offset, -offset),
            1 => Position + new Vector2(offset, -offset),
            2 => Position + new Vector2(offset, offset),
            _ => Position + new Vector2(-offset, offset)
        };
    }

    private float GetShieldNodeSize(int index) => IsShieldNodeAlive(index) ? ShieldNodeSize : DestroyedShieldNodeSize;

    private float GetShieldNodeHitRadius(int index) => GetShieldNodeSize(index) * 0.58f;

    private bool IsShieldNodeAlive(int index) => _shieldNodeHealth[index] > 0f;

    private int FindShieldNodeHit(Vector2 point, float radius)
    {
        var closestIndex = -1;
        var closestDistance = float.MaxValue;

        for (var i = 0; i < _shieldNodeHealth.Length; i++)
        {
            if (!IsShieldNodeAlive(i)) continue;

            var limit = radius + GetShieldNodeHitRadius(i);
            var distance = Vector2.DistanceSquared(GetShieldNodePosition(i), point);
            if (distance > limit * limit || distance >= closestDistance) continue;

            closestIndex = i;
            closestDistance = distance;
        }

        return closestIndex;
    }

    private int FindShieldNodeHit(Vector2 from, Vector2 to, float radius)
    {
        var closestIndex = -1;
        var closestDistance = float.MaxValue;

        for (var i = 0; i < _shieldNodeHealth.Length; i++)
        {
            if (!IsShieldNodeAlive(i)) continue;

            var limit = radius + GetShieldNodeHitRadius(i);
            var distance = DistanceToSegment(GetShieldNodePosition(i), from, to);
            if (distance > limit || distance >= closestDistance) continue;

            closestIndex = i;
            closestDistance = distance;
        }

        return closestIndex;
    }

    private void DamageShieldNode(int index, float amount)
    {
        if (!IsShieldNodeAlive(index) || amount <= 0f) return;
        _shieldNodeHealth[index] = MathF.Max(0f, _shieldNodeHealth[index] - amount);
    }

    private void RestoreShieldNodes()
    {
        for (var i = 0; i < _shieldNodeHealth.Length; i++)
        {
            _shieldNodeHealth[i] = ShieldNodeMaxHealth;
        }
    }

    private void DamageCore(float amount)
    {
        if (amount <= 0f) return;
        Health = MathF.Max(0f, Health - amount);
    }

    private static void DrawSquare(Vector2 center, float size, Color fill, Color line)
    {
        Raylib.DrawPoly(center, 4, size / MathF.Sqrt(2f), 45f, fill);
        Raylib.DrawPolyLinesEx(center, 4, size / MathF.Sqrt(2f), 45f, 2f, line);
    }

    private static void DrawDiamond(Vector2 center, float size, Color fill, Color line)
    {
        Raylib.DrawPoly(center, 4, size / MathF.Sqrt(2f), 0f, fill);
        Raylib.DrawPolyLinesEx(center, 4, size / MathF.Sqrt(2f), 0f, 2f, line);
    }

    private static float DistanceToSegment(Vector2 point, Vector2 from, Vector2 to)
    {
        var delta = to - from;
        var t = Vector2.Dot(point - from, delta) / MathF.Max(delta.LengthSquared(), 0.0001f);
        t = Math.Clamp(t, 0f, 1f);
        return Vector2.Distance(point, from + delta * t);
    }
}
