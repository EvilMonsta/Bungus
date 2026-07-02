namespace Bungus.Game;

public sealed partial class SciFiRogueGame
{
    private string T(string key)
    {
        if (_language == GameLanguage.Russian && Russian.TryGetValue(key, out var russian)) return russian;
        return English.TryGetValue(key, out var english) ? english : key;
    }

    private string SelectedText(string text, bool selected) => selected ? $"> {text} <" : text;

    private string LocalizedDisplayMode(DisplayMode mode)
        => mode == DisplayMode.Windowed ? T("settings.windowed") : T("settings.fullscreen");

    private string LocalizedEffects(VisualEffectsIntensity intensity)
        => intensity switch
        {
            VisualEffectsIntensity.Low => T("settings.low"),
            VisualEffectsIntensity.High => T("settings.high"),
            _ => T("settings.normal")
        };

    private string LocalizedThemeName(string name)
        => _language == GameLanguage.Russian
            ? name switch
            {
                "Neon Night" => "Неоновая ночь",
                "Amber Dusk" => "Янтарные сумерки",
                "Toxic Bloom" => "Токсичный рассвет",
                "Frostline" => "Ледяной рубеж",
                "Synthwave" => "Синтвейв",
                _ => name
            }
            : name;

    private string LocalizedItemName(ItemStack item)
    {
        if (_language != GameLanguage.Russian) return item.Name;
        if (item.IsHeavyAmmo) return "Heavy Ammo";
        if (item.IsStationKey) return "Ключ S.T.A.T.I.O.N";
        if (item.IsDeviceDataFragment) return "Фрагмент данных устройства";
        if (item.IsVexEye) return "Глаз Векса";
        if (item.IsInfectedExemplar) return "Образец зараженного";
        if (item.Type == ItemType.Armor) return LocalizedArmorName(item);
        if (item.Type == ItemType.Consumable) return LocalizedConsumableName(item.ConsumableKind);
        if (item.Type == ItemType.Weapon) return LocalizedWeaponName(item);
        return item.Name;
    }

    private string LocalizedItemDescription(ItemStack item)
    {
        if (_language != GameLanguage.Russian) return item.Description;
        if (item.IsHeavyAmmo) return "Боезапас для тяжелого оружия.";
        if (item.IsStationKey) return "Ключ доступа к станции.";
        if (item.IsDeviceDataFragment) return "Фрагмент данных устройства. Сохраняется между забегами.";
        if (item.IsVexEye) return "Редкий трофей из глубин.";
        if (item.IsInfectedExemplar) return "Образец зараженной ткани.";
        if (item.Type == ItemType.Armor) return "Броня с защитными модификаторами.";
        if (item.Type == ItemType.Consumable) return item.ConsumableKind switch
        {
            ConsumableType.Medkit => "Восстанавливает 25 HP + 10% максимального HP. Горячая клавиша Q/E.",
            ConsumableType.Stim => "Временно повышает скорость передвижения. Горячая клавиша Q/E.",
            ConsumableType.ProtectiveDome => "Создает купол, который блокирует выстрелы врагов и поглощает урон. Горячая клавиша Q/E.",
            ConsumableType.TeslaBullets => "На 15 секунд попадания дальним оружием проводят молнии к ближайшим врагам. Горячая клавиша Q/E.",
            ConsumableType.FreezeGrenade => "Бросает замораживающую гранату. Враги в зоне сначала замерзают, затем замедляются. Горячая клавиша Q/E.",
            ConsumableType.HeGrenade => "Бросает фугасную гранату. Горячая клавиша Q/E.",
            ConsumableType.MidaMiniTurret => "Ставит временную мини-турель, стреляющую по ближайшим врагам. Горячая клавиша Q/E.",
            _ => "На 15 секунд ваши атаки замедляют врагов. Горячая клавиша Q/E."
        };
        if (item.Type == ItemType.Weapon) return item.WeaponKind == WeaponClass.Melee ? "Оружие ближнего боя." : "Дальнобойное оружие.";
        return item.Description;
    }

    private string LocalizedConsumableName(ConsumableType? type)
        => type switch
        {
            ConsumableType.Medkit => "Аптечка",
            ConsumableType.Stim => "Стим",
            ConsumableType.ProtectiveDome => "Защитный купол",
            ConsumableType.StickyBullets => "Липкие пули",
            ConsumableType.TeslaBullets => "Tesla-пули",
            ConsumableType.FreezeGrenade => "Крио-граната",
            ConsumableType.HeGrenade => "HE-граната",
            ConsumableType.MidaMiniTurret => "MIDA мини-турель",
            ConsumableType.StationKey => "Ключ станции",
            _ => "Расходник"
        };

    private string LocalizedArmorName(ItemStack item)
        => item.ArmorKind switch
        {
            ArmorKind.Light => "Легкая броня",
            ArmorKind.Heavy => "Тяжелая броня",
            _ => "Броня"
        };

    private string LocalizedWeaponName(ItemStack item)
        => item.Pattern switch
        {
            WeaponPattern.Standard when item.WeaponKind == WeaponClass.Melee => "Клинок",
            WeaponPattern.Standard => "Рельсовый пистолет",
            WeaponPattern.PulseRifle => "Импульсная винтовка",
            WeaponPattern.EnergySpear => "Энергетическое копье",
            WeaponPattern.GrenadeLauncher => "Гранатомет",
            WeaponPattern.SniperRifle => "Снайперская винтовка",
            WeaponPattern.Toxikus => "Токсикус",
            WeaponPattern.Lancelot => "Ланселот",
            WeaponPattern.TraceRifle => "Трассирующая винтовка",
            WeaponPattern.LinearRifle => "Линейная винтовка",
            WeaponPattern.RocketLauncher => "Ракетница",
            WeaponPattern.Pulsar => "Пульсар",
            WeaponPattern.RamBomber => "Рамбомбер",
            WeaponPattern.AutoRifle => "Автовинтовка",
            WeaponPattern.RocketPulseRifle => "Ракетно-импульсная винтовка",
            WeaponPattern.Terror => "Террор",
            _ => item.Name
        };

    private string LocalizedRarity(ArmorRarity rarity)
        => _language == GameLanguage.Russian
            ? rarity switch
            {
                ArmorRarity.Common => "Обычный",
                ArmorRarity.Rare => "Редкий",
                ArmorRarity.Epic => "Эпический",
                ArmorRarity.Legendary => "Легендарный",
                ArmorRarity.Red => "Красный",
                ArmorRarity.Damaged => "Поврежденный",
                _ => rarity.ToString()
            }
            : rarity.ToString();

    private static readonly Dictionary<string, string> English = new(StringComparer.OrdinalIgnoreCase)
    {
        ["common.back"] = "Back",
        ["common.close"] = "Close",
        ["common.ok"] = "Ok",
        ["common.on"] = "On",
        ["common.off"] = "Off",
        ["common.none"] = "None",
        ["common.apply"] = "Apply",
        ["common.confirm"] = "Confirm",
        ["common.reset"] = "Reset",
        ["common.loading"] = "Loading...",
        ["menu.play"] = "Play",
        ["menu.storage"] = "Storage",
        ["menu.store"] = "Store",
        ["menu.cradle"] = "Cradle",
        ["menu.settings"] = "Settings",
        ["menu.exit"] = "Exit",
        ["menu.codes"] = "Codes",
        ["menu.changelog"] = "Changelog",
        ["menu.about"] = "About",
        ["settings.title"] = "Settings",
        ["settings.display"] = "Display",
        ["settings.windowed"] = "Windowed",
        ["settings.fullscreen"] = "Fullscreen",
        ["settings.texture_filter"] = "Texture filter",
        ["settings.damage_numbers"] = "Damage numbers",
        ["settings.effects"] = "Effects",
        ["settings.low"] = "Low",
        ["settings.normal"] = "Normal",
        ["settings.high"] = "High",
        ["settings.choose_theme"] = "Choose theme",
        ["settings.antialiasing"] = "Antialiasing",
        ["settings.restart_graphics"] = "Restart applies antialiasing and VSync",
        ["settings.screen_shake"] = "Screen shake",
        ["settings.language"] = "Language",
        ["settings.english"] = "English",
        ["settings.russian"] = "Russian",
        ["pause.title"] = "Paused",
        ["pause.resume"] = "Resume",
        ["pause.abandon"] = "Abandon run",
        ["death.default_title"] = "You Died",
        ["death.default_body"] = "All carried items were lost.",
        ["death.abandoned_title"] = "Run abandoned",
        ["death.earned"] = "Earned",
        ["death.retained_xp"] = "Retained XP",
        ["death.deploy_again"] = "Deploy again",
        ["death.main_menu"] = "Main menu",
        ["codes.title"] = "Codes",
        ["codes.enter"] = "Enter a promo code",
        ["codes.placeholder"] = "CODE",
        ["about.developer"] = "Developer:",
        ["about.tester"] = "Tester:",
        ["about.contributors"] = "Contributors:",
        ["about.thanks_1"] = "Special thanks to my nerve cells",
        ["about.thanks_2"] = "for letting this become more than just an idea.",
        ["about.future"] = "Bungus will keep getting better.",
        ["map.expeditions"] = "Expeditions",
        ["map.challenges"] = "Challenges",
        ["map.choose_landing"] = "Choose your landing zone",
        ["map.choose_trial"] = "Choose a trial",
        ["map.pit"] = "Pit",
        ["map.pit_nightmare"] = "Pit (Nightmare)",
        ["map.wave_survival"] = "Wave survival trial",
        ["map.bring_gear"] = "Bring your own gear",
        ["map.click_deploy"] = "Click to deploy",
        ["map.nightmare_1"] = "Enter only with your own equipment",
        ["map.nightmare_2"] = "Equipment roulettes are disabled",
        ["map.nightmare_3"] = "Enemy speed +50%",
        ["map.nightmare_4"] = "Enemy health +25%",
        ["map.nightmare_5"] = "Every 3 waves: difficulty modifier",
        ["map.nightmare_6"] = "CryptoTokens for every 10 completed waves",
        ["hud.level"] = "Level",
        ["hud.pit_level"] = "Pit level",
        ["hud.wave"] = "Wave",
        ["hud.current"] = "Current",
        ["hud.consumables"] = "Consumables",
        ["hud.run_score"] = "Run score",
        ["hud.controls"] = "WASD move | LMB attack | 1 melee | 2 primary | 3 heavy | TAB inventory | ESC menu",
        ["hud.map"] = "Map",
        ["hud.map_help"] = "LMB: place/move marker | RMB near marker: remove | M/Esc: close",
        ["hud.level_up"] = "Level Up",
        ["hud.heavy_ammo"] = "Heavy Ammo",
        ["hud.access_code"] = "Access code",
        ["hud.shield"] = "SHIELD",
        ["extract.exit_portal_active"] = "Exit portal active",
        ["extract.exit_portal_inactive"] = "Exit portal inactive",
        ["extract.last_portal"] = "Last portal active",
        ["extract.last_chance"] = "Last chance",
        ["extract.portals_in"] = "Portals in",
        ["extract.portals_active"] = "Portals active",
        ["extract.challenge"] = "Challenge",
        ["extract.completed_waves"] = "Completed waves",
        ["extract.map"] = "Map",
        ["inventory.title"] = "Inventory",
        ["inventory.backpack"] = "Backpack",
        ["inventory.equipment"] = "Equipment",
        ["inventory.stats"] = "Stats",
        ["inventory.free_points"] = "Free points",
        ["inventory.total_points"] = "Total points",
        ["inventory.chest"] = "Chest",
        ["inventory.take_all"] = "Take all [X]",
        ["storage.title"] = "Storage",
        ["storage.description"] = "Equip items here before deployment. Extracted loot returns to this stash.",
        ["storage.capacity"] = "Capacity",
        ["storage.selection_help"] = "Shift+click selects items. Hold X on a selected item to sell selected.",
        ["storage.loadout"] = "Loadout",
        ["storage.armor"] = "Armor",
        ["storage.primary"] = "Primary",
        ["storage.heavy"] = "Heavy",
        ["storage.melee"] = "Melee",
        ["storage.consumables"] = "Consumables",
        ["storage.run_backpack"] = "Run Backpack",
        ["storage.stash"] = "Stash",
        ["armory.title"] = "Armory",
        ["armory.description"] = "Buy equipment. Stock refreshes after each run.",
        ["pit.enemy_damage"] = "Enemy damage",
        ["pit.enemy_health"] = "Enemy health",
        ["pit.enemy_speed"] = "Enemy speed",
        ["pit.nightmare_modifier"] = "Nightmare modifier",
        ["pit.next_modifier_active"] = "The next difficulty modifier is active.",
        ["pit.modifier"] = "Modifier",
        ["pit.damage"] = "Damage",
        ["pit.health"] = "Health",
        ["pit.speed"] = "Speed",
        ["pit.wave_reward"] = "Wave reward",
        ["pit.reward_hint"] = "Choose up to four items, or skip the offer.",
        ["pit.claim"] = "Claim",
        ["pit.claimed"] = "Claimed",
        ["pit.skip"] = "Skip",
        ["cradle.title"] = "Cradle",
        ["cradle.upgrades"] = "Account upgrades",
        ["cradle.general_level"] = "General level",
        ["cradle.next_level"] = "Next level",
        ["cradle.points"] = "Points",
        ["cradle.health"] = "Health",
        ["cradle.speed"] = "Speed",
        ["cradle.ranged_damage"] = "Ranged damage",
        ["cradle.melee_damage"] = "Melee damage",
        ["cradle.melee_attack_speed"] = "Melee attack speed",
        ["cradle.dash_recovery"] = "Dash recovery",
        ["cradle.stability"] = "Stability",
        ["cradle.gunsmith"] = "Gunsmith",
        ["cradle.fighter"] = "Fighter",
        ["cradle.arcane"] = "Arcane",
        ["cradle.track"] = "Track",
        ["cradle.health_desc"] = "Increases maximum health by 5 for each active cell.",
        ["cradle.speed_desc"] = "Increases movement speed by 2.8% for each active cell.",
        ["cradle.melee_speed_desc"] = "Increases melee attack speed by 1.6% for each active cell, reducing time between melee attacks.",
        ["cradle.dash_recovery_desc"] = "Reduces dash cooldown by 1% for each active cell.",
        ["cradle.stability_desc"] = "Reduces moving ranged spread by 1% for each active cell.",
        ["cradle.gunsmith_desc"] = "Increases ranged weapon damage by 0.4% for each active cell.",
        ["cradle.fighter_desc"] = "Increases melee weapon damage by 0.4% for each active cell.",
        ["cradle.arcane_desc"] = "Each active cell increases burn and poison damage, stim duration, healing bonus, dome absorption, slow and Tesla effects by 1%.",
        ["notice.save_reset"] = "Protected save could not be loaded. New profile created.",
        ["notice.restart_antialiasing"] = "Restart the game to apply antialiasing.",
        ["notice.restart_vsync"] = "Restart the game to apply VSync.",
        ["notice.language_changed"] = "Language changed.",
        ["notice.sold"] = "This item has already been sold.",
        ["notice.not_enough_coins"] = "Not enough SynthCoins.",
        ["notice.not_enough_tokens"] = "Not enough CryptoTokens.",
        ["notice.storage_full"] = "Storage is full.",
        ["notice.not_enough_storage_ammo"] = "Not enough stored Heavy Ammo.",
        ["notice.bought"] = "Bought {0} for {1} {2}."
    };

    private static readonly Dictionary<string, string> Russian = new(StringComparer.OrdinalIgnoreCase)
    {
        ["common.back"] = "Назад",
        ["common.close"] = "Закрыть",
        ["common.ok"] = "Ок",
        ["common.on"] = "Вкл",
        ["common.off"] = "Выкл",
        ["common.none"] = "Нет",
        ["common.apply"] = "Применить",
        ["common.confirm"] = "Подтвердить",
        ["common.reset"] = "Сброс",
        ["common.loading"] = "Загрузка...",
        ["menu.play"] = "Играть",
        ["menu.storage"] = "Хранилище",
        ["menu.store"] = "Магазин",
        ["menu.cradle"] = "Колыбель",
        ["menu.settings"] = "Настройки",
        ["menu.exit"] = "Выход",
        ["menu.codes"] = "Коды",
        ["menu.changelog"] = "Список изменений",
        ["menu.about"] = "Об игре",
        ["settings.title"] = "Настройки",
        ["settings.display"] = "Экран",
        ["settings.windowed"] = "Окно",
        ["settings.fullscreen"] = "Полноэкр.",
        ["settings.texture_filter"] = "Фильтр",
        ["settings.damage_numbers"] = "Цифры урона",
        ["settings.effects"] = "Эффекты",
        ["settings.low"] = "Низкие",
        ["settings.normal"] = "Обычные",
        ["settings.high"] = "Высокие",
        ["settings.choose_theme"] = "Тема",
        ["settings.antialiasing"] = "Сглаж.",
        ["settings.restart_graphics"] = "Сглаж. и VSync после перезапуска",
        ["settings.screen_shake"] = "Тряска",
        ["settings.language"] = "Язык",
        ["settings.english"] = "English",
        ["settings.russian"] = "Русский",
        ["pause.title"] = "Пауза",
        ["pause.resume"] = "Продолжить",
        ["pause.abandon"] = "Покинуть забег",
        ["death.default_title"] = "Вы погибли",
        ["death.default_body"] = "Все переносимые предметы были потеряны.",
        ["death.abandoned_title"] = "Забег покинут",
        ["death.earned"] = "Получено",
        ["death.retained_xp"] = "Сохранено XP",
        ["death.deploy_again"] = "Еще забег",
        ["death.main_menu"] = "Главное меню",
        ["codes.title"] = "Коды",
        ["codes.enter"] = "Введите промокод",
        ["codes.placeholder"] = "КОД",
        ["about.developer"] = "Разработчик:",
        ["about.tester"] = "Тестер:",
        ["about.contributors"] = "Участники:",
        ["about.thanks_1"] = "Особая благодарность моим нервным клеткам",
        ["about.thanks_2"] = "за то, что дали этой идее стать игрой.",
        ["about.future"] = "Bungus продолжит становиться лучше.",
        ["map.expeditions"] = "Экспедиции",
        ["map.challenges"] = "Испытания",
        ["map.choose_landing"] = "Выбор зоны высадки",
        ["map.choose_trial"] = "Выбор испытания",
        ["map.pit"] = "Яма",
        ["map.pit_nightmare"] = "Яма (Кошмар)",
        ["map.wave_survival"] = "Выживание по волнам",
        ["map.bring_gear"] = "Со своим снаряжением",
        ["map.click_deploy"] = "Нажмите для высадки",
        ["map.nightmare_1"] = "Вход только со своим снаряжением",
        ["map.nightmare_2"] = "Рулетки снаряжения отключены",
        ["map.nightmare_3"] = "Скорость врагов +50%",
        ["map.nightmare_4"] = "Здоровье врагов +25%",
        ["map.nightmare_5"] = "Каждые 3 волны: модификатор сложности",
        ["map.nightmare_6"] = "CryptoTokens за каждые 10 завершенных волн",
        ["hud.level"] = "Уровень",
        ["hud.pit_level"] = "Ур. ямы",
        ["hud.wave"] = "Волна",
        ["hud.current"] = "Текущее",
        ["hud.consumables"] = "Расх.",
        ["hud.run_score"] = "Очки",
        ["hud.controls"] = "WASD ходьба | ЛКМ атака | 1 ближ. | 2 осн. | 3 тяж. | TAB инв. | ESC меню",
        ["hud.map"] = "Карта",
        ["hud.map_help"] = "ЛКМ: метка | ПКМ рядом: удалить | M/Esc: закрыть",
        ["hud.level_up"] = "Уровень +",
        ["hud.heavy_ammo"] = "Heavy Ammo",
        ["hud.access_code"] = "Код доступа",
        ["hud.shield"] = "ЩИТ",
        ["extract.exit_portal_active"] = "Выход активен",
        ["extract.exit_portal_inactive"] = "Выход неактивен",
        ["extract.last_portal"] = "Последний выход",
        ["extract.last_chance"] = "Последний шанс",
        ["extract.portals_in"] = "Порталы через",
        ["extract.portals_active"] = "Порталы активны",
        ["extract.challenge"] = "Испытание",
        ["extract.completed_waves"] = "Завершено волн",
        ["extract.map"] = "Карта",
        ["inventory.title"] = "Инвентарь",
        ["inventory.backpack"] = "Рюкзак",
        ["inventory.equipment"] = "Экипировка",
        ["inventory.stats"] = "Статы",
        ["inventory.free_points"] = "Своб. очки",
        ["inventory.total_points"] = "Всего",
        ["inventory.chest"] = "Сундук",
        ["inventory.take_all"] = "Взять все [X]",
        ["storage.title"] = "Хранилище",
        ["storage.description"] = "Снаряжайте предметы перед высадкой. Эвакуированная добыча вернется сюда.",
        ["storage.capacity"] = "Вместимость",
        ["storage.selection_help"] = "Shift+клик: выделить. Удерживайте X на выделенном, чтобы продать выбранное.",
        ["storage.loadout"] = "Комплект",
        ["storage.armor"] = "Броня",
        ["storage.primary"] = "Основное",
        ["storage.heavy"] = "Тяжелое",
        ["storage.melee"] = "Ближнее",
        ["storage.consumables"] = "Расходники",
        ["storage.run_backpack"] = "Рюкзак",
        ["storage.stash"] = "Склад",
        ["armory.title"] = "Арсенал",
        ["armory.description"] = "Покупайте снаряжение. Ассортимент обновляется после забега.",
        ["pit.enemy_damage"] = "Урон враг.",
        ["pit.enemy_health"] = "HP враг.",
        ["pit.enemy_speed"] = "Скор. враг.",
        ["pit.nightmare_modifier"] = "Мод. кошмара",
        ["pit.next_modifier_active"] = "Следующий модификатор активен.",
        ["pit.modifier"] = "Модификатор",
        ["pit.damage"] = "Урон",
        ["pit.health"] = "Здоровье",
        ["pit.speed"] = "Скорость",
        ["pit.wave_reward"] = "Награда волны",
        ["pit.reward_hint"] = "Выберите до 4 предметов или пропустите.",
        ["pit.claim"] = "Забрать",
        ["pit.claimed"] = "Забрано",
        ["pit.skip"] = "Пропустить",
        ["cradle.title"] = "Колыбель",
        ["cradle.upgrades"] = "Улучшения",
        ["cradle.general_level"] = "Общий ур.",
        ["cradle.next_level"] = "След. ур.",
        ["cradle.points"] = "Очки",
        ["cradle.health"] = "Здоровье",
        ["cradle.speed"] = "Скорость",
        ["cradle.ranged_damage"] = "Урон дальн. боя",
        ["cradle.melee_damage"] = "Урон ближ. боя",
        ["cradle.melee_attack_speed"] = "Скорость ближ. боя",
        ["cradle.dash_recovery"] = "Восст. рывка",
        ["cradle.stability"] = "Стабильность",
        ["cradle.gunsmith"] = "Оружейник",
        ["cradle.fighter"] = "Боец",
        ["cradle.arcane"] = "Аркана",
        ["cradle.track"] = "Ветка",
        ["cradle.health_desc"] = "+5 к максимальному здоровью за ячейку.",
        ["cradle.speed_desc"] = "+2.8% к скорости движения за ячейку.",
        ["cradle.melee_speed_desc"] = "+1.6% к скорости ближнего боя за ячейку.",
        ["cradle.dash_recovery_desc"] = "-1% к перезарядке рывка за ячейку.",
        ["cradle.stability_desc"] = "-1% к разбросу в движении за ячейку.",
        ["cradle.gunsmith_desc"] = "+0.4% к урону дальнего оружия за ячейку.",
        ["cradle.fighter_desc"] = "+0.4% к урону ближнего боя за ячейку.",
        ["cradle.arcane_desc"] = "+1% к огню, яду, стиму, лечению, куполу, замедлению и Tesla-эффектам за ячейку.",
        ["notice.save_reset"] = "Сохранение не загрузилось. Создан новый профиль.",
        ["notice.restart_antialiasing"] = "Перезапустите игру, чтобы применить сглаживание.",
        ["notice.restart_vsync"] = "Перезапустите игру, чтобы применить VSync.",
        ["notice.language_changed"] = "Язык изменен.",
        ["notice.sold"] = "Этот предмет уже продан.",
        ["notice.not_enough_coins"] = "Недостаточно SynthCoins.",
        ["notice.not_enough_tokens"] = "Недостаточно CryptoTokens.",
        ["notice.storage_full"] = "Хранилище заполнено.",
        ["notice.not_enough_storage_ammo"] = "Недостаточно Heavy Ammo.",
        ["notice.bought"] = "Куплено: {0} за {1} {2}."
    };
}
