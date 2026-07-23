namespace Aegis.Scanners.Internal;

/// <summary>
/// Известные игровые процессы (для авто-включения игрового режима) и фоновые программы, которые на время
/// игры можно закрыть. Отдельно — список процессов, которые трогать НЕЛЬЗЯ никогда: закрытие любого из них
/// роняет систему или рабочий стол.
///
/// Это данные, а не логика: пополняются списком без изменения кода. Совпадение — по точному имени процесса
/// (не по подстроке), чтобы не закрыть чужую программу с похожим названием.
/// </summary>
public static class GameProcessCatalog
{
    /// <summary>Игры, по которым включается авто-режим (имена процессов в нижнем регистре).</summary>
    public static readonly IReadOnlySet<string> KnownGames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // Valve / Source
        "cs2.exe", "csgo.exe", "dota2.exe", "hl2.exe", "left4dead2.exe", "portal2.exe", "tf_win64.exe", "deadlock.exe",
        // Riot
        "valorant-win64-shipping.exe", "valorant.exe", "league of legends.exe", "leagueoflegends.exe", "lol.launcher.exe",
        // Blizzard / Activision
        "overwatch.exe", "wow.exe", "wowclassic.exe", "diablo iv.exe", "diablo4.exe", "hearthstone.exe",
        "starcraft ii.exe", "cod.exe", "modernwarfare.exe", "blackops6.exe",
        // Epic / Fortnite
        "fortniteclient-win64-shipping.exe", "rocketleague.exe",
        // EA
        "apex_legends.exe", "r5apex.exe", "bf2042.exe", "battlefield.exe", "fc25.exe", "fc24.exe", "thesims4.exe",
        // Ubisoft
        "rainbowsix.exe", "rainbowsix_vulkan.exe", "acodyssey.exe", "acvalhalla.exe", "acmirage.exe", "farcry6.exe",
        // Rockstar / Take-Two
        "gta5.exe", "gtav.exe", "gta5_enhanced.exe", "rdr2.exe", "playgtav.exe",
        // FromSoftware
        "eldenring.exe", "darksoulsiii.exe", "sekiro.exe", "armoredcore6.exe", "nightreign.exe",
        // CD Projekt Red
        "cyberpunk2077.exe", "witcher3.exe",
        // Прочие крупные
        "bg3.exe", "bg3_dx11.exe", "destiny2.exe", "warframe.x64.exe", "warframe.exe",
        "pathofexile_x64.exe", "pathofexile.exe", "pathofexile_x64steam.exe", "pathofexile2.exe",
        "escapefromtarkov.exe", "pubg-win64-shipping.exe", "tslgame.exe",
        "palworld-win64-shipping.exe", "helldivers2.exe", "hogwartslegacy.exe", "starfield.exe",
        "satisfactory.exe", "factorygame-win64-shipping.exe", "lethalcompany.exe", "phasmophobia.exe",
        "hades2.exe", "hades.exe", "hollow_knight.exe", "hollowknight.exe", "silksong.exe",
        "fallguys_client_game.exe", "terraria.exe", "stardewvalley.exe", "factorio.exe", "noita.exe",
        "deeprockgalactic-win64-shipping.exe", "minecraft.windows.exe", "javaw.exe",
        "theforest.exe", "sonsoftheforest.exe", "rust.exe", "dayz_x64.exe", "squad.exe",
        "world of tanks.exe", "worldoftanks.exe", "wot.exe", "worldofwarships.exe", "wt.exe", "aces.exe",
        "atomicheart.exe", "stalker2-win64-shipping.exe", "metroexodus.exe",
        "smitex64.exe", "warthunder.exe", "crsed.exe", "enlisted.exe",
        "forzahorizon5.exe", "forzahorizon4.exe", "forzamotorsport.exe", "f1_24.exe",
        "cities2.exe", "civ6.exe", "civilizationvii.exe", "totalwar.exe", "eu4.exe", "hoi4.exe", "stellaris.exe",
        "re4.exe", "re8.exe", "monsterhunterwilds.exe", "monsterhunterworld.exe", "streetfighter6.exe",
        "tekken8.exe", "mkl.exe", "spacemarine2.exe", "blackmythwukong.exe", "b1-win64-shipping.exe",
    };

    /// <summary>
    /// Фоновые программы, которые на время игры можно закрыть: они не нужны в игре, но едят память,
    /// процессор и сеть. Все запускаются заново сами (или пользователем) — данные при этом не теряются.
    /// </summary>
    public static readonly IReadOnlyList<BackgroundApp> BackgroundApps =
    [
        // Браузеры — обычно главный пожиратель памяти во время игры.
        new("chrome.exe", "Google Chrome", BackgroundAppKind.Browser),
        new("msedge.exe", "Microsoft Edge", BackgroundAppKind.Browser),
        new("firefox.exe", "Mozilla Firefox", BackgroundAppKind.Browser),
        new("opera.exe", "Opera", BackgroundAppKind.Browser),
        new("brave.exe", "Brave", BackgroundAppKind.Browser),
        new("vivaldi.exe", "Vivaldi", BackgroundAppKind.Browser),
        new("browser.exe", "Яндекс Браузер", BackgroundAppKind.Browser),

        // Мессенджеры и общение.
        new("Discord.exe", "Discord", BackgroundAppKind.Chat),
        new("Telegram.exe", "Telegram", BackgroundAppKind.Chat),
        new("WhatsApp.exe", "WhatsApp", BackgroundAppKind.Chat),
        new("Slack.exe", "Slack", BackgroundAppKind.Chat),
        new("Teams.exe", "Microsoft Teams", BackgroundAppKind.Chat),
        new("ms-teams.exe", "Microsoft Teams", BackgroundAppKind.Chat),
        new("Skype.exe", "Skype", BackgroundAppKind.Chat),
        new("Signal.exe", "Signal", BackgroundAppKind.Chat),
        new("Viber.exe", "Viber", BackgroundAppKind.Chat),

        // Обновляторы и «помощники» — в игре бесполезны, но любят просыпаться в самый неподходящий момент.
        new("GoogleUpdate.exe", "Обновление Google", BackgroundAppKind.Updater),
        new("MicrosoftEdgeUpdate.exe", "Обновление Edge", BackgroundAppKind.Updater),
        new("AdobeARM.exe", "Обновление Adobe", BackgroundAppKind.Updater),
        new("Adobe Desktop Service.exe", "Adobe Desktop Service", BackgroundAppKind.Updater),
        new("CCXProcess.exe", "Adobe Creative Cloud", BackgroundAppKind.Updater),
        new("jusched.exe", "Обновление Java", BackgroundAppKind.Updater),
        new("BraveUpdate.exe", "Обновление Brave", BackgroundAppKind.Updater),
        new("OperaUpdate.exe", "Обновление Opera", BackgroundAppKind.Updater),
        new("YandexUpdate.exe", "Обновление Яндекса", BackgroundAppKind.Updater),
        new("Dropbox.exe", "Dropbox (синхронизация)", BackgroundAppKind.Sync),
        new("OneDrive.exe", "OneDrive (синхронизация)", BackgroundAppKind.Sync),
        new("YandexDisk.exe", "Яндекс Диск (синхронизация)", BackgroundAppKind.Sync),
        new("googledrivesync.exe", "Google Диск (синхронизация)", BackgroundAppKind.Sync),
        new("Spotify.exe", "Spotify", BackgroundAppKind.Media),
        new("ZoomUpdateAgent.exe", "Обновление Zoom", BackgroundAppKind.Updater),
    ];

    /// <summary>
    /// Процессы, которые НИКОГДА не закрываем: это ядро Windows и рабочий стол. Закрытие любого из них —
    /// синий экран или пропавший интерфейс. Список проверяется перед каждым закрытием, даже если имя
    /// каким-то образом попало в список «фоновых».
    /// </summary>
    public static readonly IReadOnlySet<string> Protected = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "system", "registry", "memory compression", "idle",
        "csrss.exe", "smss.exe", "wininit.exe", "winlogon.exe", "services.exe", "lsass.exe", "lsaiso.exe",
        "svchost.exe", "dwm.exe", "explorer.exe", "fontdrvhost.exe", "sihost.exe", "taskhostw.exe",
        "ctfmon.exe", "spoolsv.exe", "audiodg.exe", "conhost.exe", "wudfhost.exe", "dllhost.exe",
        "runtimebroker.exe", "searchhost.exe", "startmenuexperiencehost.exe", "shellexperiencehost.exe",
        "aegis.exe", // сама программа — иначе режим некому будет выключить
    };

    /// <summary>
    /// Ищет среди запущенных процессов игру: сначала по своему списку, затем по добавленным пользователем.
    /// Возвращает имя найденного процесса или null. Сравнение точное по имени файла.
    /// </summary>
    public static string? FindRunningGame(IEnumerable<string> runningProcessNames, IReadOnlyList<string>? customGames = null)
    {
        ArgumentNullException.ThrowIfNull(runningProcessNames);

        // Ключ — приведённое имя, значение — как процесс называется на самом деле (его и показываем человеку).
        var running = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var process in runningProcessNames)
        {
            var name = Normalize(process);
            if (name.Length > 0)
            {
                running.TryAdd(name, name);
            }
        }

        foreach (var game in KnownGames)
        {
            if (running.TryGetValue(game, out var actual))
            {
                return actual;
            }
        }

        foreach (var custom in customGames ?? [])
        {
            var name = Normalize(custom);
            if (name.Length > 0 && running.TryGetValue(name, out var actual))
            {
                return actual;
            }
        }

        return null;
    }

    /// <summary>Можно ли закрывать процесс с таким именем на время игры.</summary>
    public static bool IsClosable(string processName)
    {
        var name = Normalize(processName);
        return name.Length > 0
               && !Protected.Contains(name)
               && BackgroundApps.Any(app => string.Equals(Normalize(app.ProcessName), name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Понятное имя фоновой программы по имени процесса (или само имя процесса, если неизвестна).</summary>
    public static string DisplayName(string processName)
    {
        var name = Normalize(processName);
        return BackgroundApps.FirstOrDefault(app =>
            string.Equals(Normalize(app.ProcessName), name, StringComparison.OrdinalIgnoreCase))?.DisplayName ?? processName;
    }

    /// <summary>Приводит имя процесса к сравнимому виду: без пути, с расширением .exe, без регистра.</summary>
    private static string Normalize(string value)
    {
        var name = (value ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            return string.Empty;
        }

        // Из «Диспетчера задач» имя приходит без расширения, из tasklist — с ним. Приводим к одному виду.
        name = Path.GetFileName(name);
        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name : name + ".exe";
    }
}

/// <summary>Фоновая программа, которую можно закрыть на время игры.</summary>
public sealed record BackgroundApp(string ProcessName, string DisplayName, BackgroundAppKind Kind);

/// <summary>Тип фоновой программы — для понятной группировки в интерфейсе.</summary>
public enum BackgroundAppKind
{
    /// <summary>Браузер.</summary>
    Browser,

    /// <summary>Мессенджер.</summary>
    Chat,

    /// <summary>Обновлятор/служба обновлений программы.</summary>
    Updater,

    /// <summary>Облачная синхронизация.</summary>
    Sync,

    /// <summary>Музыка/видео.</summary>
    Media,
}
