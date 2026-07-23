namespace Aegis.System.Internal;

/// <summary>
/// Выверенный каталог чистки приложений (подход Winapp2, но СВОИ безопасные правила — clean-room).
/// Для браузеров разбито на ВЫБОР: кэш (безопасно), cookie (выход из аккаунтов — с предупреждением),
/// история. Для остальных — только кэш. `Detect` = установлено ли. Группа `Cache` указывает на ПАПКИ,
/// `Cookies`/`History` — на конкретные ФАЙЛЫ. Удаление обратимое (в Корзину).
/// </summary>
internal static class AppCacheCatalog
{
    internal enum CleanKind
    {
        Cache,
        Cookies,
        History,
    }

    internal sealed record CacheGroup(CleanKind Kind, string[] Paths);

    internal sealed record AppCacheRule(string Name, string[] Detect, CacheGroup[] Groups);

    private static AppCacheRule CacheOnly(string name, string[] detect, string[] cache) =>
        new(name, detect, [new CacheGroup(CleanKind.Cache, cache)]);

    // Стандартные кэш-папки браузера на движке Chromium (профили — маской *).
    private static string[] ChromiumCache(string root) =>
    [
        root + @"\*\Cache",
        root + @"\*\Code Cache",
        root + @"\*\GPUCache",
        root + @"\*\Service Worker\CacheStorage",
        root + @"\*\Service Worker\ScriptCache",
        root + @"\GrShaderCache",
        root + @"\ShaderCache",
    ];

    // Браузер Chromium: кэш + cookie (файл) + история (файл) как отдельные группы-выбор.
    private static AppCacheRule ChromiumBrowser(string name, string root) =>
        new(name, [root],
        [
            new CacheGroup(CleanKind.Cache, ChromiumCache(root)),
            new CacheGroup(CleanKind.Cookies, [root + @"\*\Network\Cookies", root + @"\*\Cookies"]),
            new CacheGroup(CleanKind.History, [root + @"\*\History"]),
        ]);

    public static readonly AppCacheRule[] Rules =
    [
        // ===== Браузеры (кэш / cookie / история — выбор) =====
        ChromiumBrowser("Google Chrome", @"%LocalAppData%\Google\Chrome\User Data"),
        ChromiumBrowser("Microsoft Edge", @"%LocalAppData%\Microsoft\Edge\User Data"),
        ChromiumBrowser("Brave", @"%LocalAppData%\BraveSoftware\Brave-Browser\User Data"),
        ChromiumBrowser("Yandex Browser", @"%LocalAppData%\Yandex\YandexBrowser\User Data"),
        ChromiumBrowser("Vivaldi", @"%LocalAppData%\Vivaldi\User Data"),
        new("Mozilla Firefox", [@"%LocalAppData%\Mozilla\Firefox\Profiles"],
        [
            new CacheGroup(CleanKind.Cache,
                [@"%LocalAppData%\Mozilla\Firefox\Profiles\*\cache2", @"%LocalAppData%\Mozilla\Firefox\Profiles\*\startupCache",
                 @"%LocalAppData%\Mozilla\Firefox\Profiles\*\shader-cache"]),
            new CacheGroup(CleanKind.Cookies, [@"%AppData%\Mozilla\Firefox\Profiles\*\cookies.sqlite"]),
            new CacheGroup(CleanKind.History, [@"%AppData%\Mozilla\Firefox\Profiles\*\places.sqlite"]),
        ]),
        CacheOnly("Opera", [@"%AppData%\Opera Software\Opera Stable"],
            [@"%AppData%\Opera Software\Opera Stable\Cache", @"%LocalAppData%\Opera Software\Opera Stable\Cache", @"%AppData%\Opera Software\Opera Stable\*\Cache"]),

        // ===== Чаты/общение (только кэш, НЕ данные аккаунта) =====
        CacheOnly("Discord", [@"%AppData%\discord"], [@"%AppData%\discord\Cache", @"%AppData%\discord\Code Cache", @"%AppData%\discord\GPUCache"]),
        CacheOnly("Slack", [@"%AppData%\Slack"], [@"%AppData%\Slack\Cache", @"%AppData%\Slack\Code Cache", @"%AppData%\Slack\GPUCache", @"%AppData%\Slack\Service Worker\CacheStorage"]),
        CacheOnly("Microsoft Teams", [@"%AppData%\Microsoft\Teams"], [@"%AppData%\Microsoft\Teams\Cache", @"%AppData%\Microsoft\Teams\Code Cache", @"%AppData%\Microsoft\Teams\GPUCache", @"%AppData%\Microsoft\Teams\Service Worker\CacheStorage"]),
        CacheOnly("Skype", [@"%AppData%\Microsoft\Skype for Desktop"], [@"%AppData%\Microsoft\Skype for Desktop\Cache", @"%AppData%\Microsoft\Skype for Desktop\GPUCache"]),
        CacheOnly("Zoom", [@"%AppData%\Zoom"], [@"%AppData%\Zoom\data\Cache"]),

        // ===== Игровые лаунчеры =====
        CacheOnly("Steam",
            [@"%ProgramFiles(x86)%\Steam", @"%LocalAppData%\Steam"],
            [@"%ProgramFiles(x86)%\Steam\appcache\httpcache", @"%LocalAppData%\Steam\htmlcache",
             @"%ProgramFiles(x86)%\Steam\logs", @"%ProgramFiles(x86)%\Steam\dumps"]),
        CacheOnly("Epic Games",
            [@"%LocalAppData%\EpicGamesLauncher"],
            [@"%LocalAppData%\EpicGamesLauncher\Saved\webcache", @"%LocalAppData%\EpicGamesLauncher\Saved\webcache_4147",
             @"%LocalAppData%\EpicGamesLauncher\Saved\webcache_4430", @"%LocalAppData%\EpicGamesLauncher\Saved\Logs",
             @"%LocalAppData%\EpicGamesLauncher\Intermediate",
             // Хранилище скачанных дополнений Unreal — часто десятки гигабайт; всё качается заново по требованию.
             @"%ProgramData%\Epic\EpicGamesLauncher\VaultCache"]),
        CacheOnly("EA App / Origin", [@"%LocalAppData%\Electronic Arts", @"%ProgramData%\Origin"], [@"%LocalAppData%\Electronic Arts\EA Desktop\cache", @"%ProgramData%\Origin\CefCache"]),
        CacheOnly("GOG Galaxy", [@"%LocalAppData%\GOG.com\Galaxy"],
            [@"%LocalAppData%\GOG.com\Galaxy\webcache", @"%ProgramData%\GOG.com\Galaxy\webcache", @"%ProgramData%\GOG.com\Galaxy\logs"]),
        CacheOnly("Battle.net", [@"%LocalAppData%\Battle.net"],
            [@"%LocalAppData%\Battle.net\Cache", @"%LocalAppData%\Battle.net\BrowserCache", @"%LocalAppData%\Blizzard Entertainment\Battle.net\Logs"]),
        CacheOnly("Ubisoft Connect", [@"%LocalAppData%\Ubisoft Game Launcher"],
            [@"%LocalAppData%\Ubisoft Game Launcher\logs", @"%LocalAppData%\Ubisoft Game Launcher\cache"]),
        CacheOnly("Riot (League of Legends, Valorant)", [@"%LocalAppData%\Riot Games"],
            [@"%LocalAppData%\Riot Games\Riot Client\Logs", @"%LocalAppData%\VALORANT\Saved\Logs", @"%LocalAppData%\VALORANT\Saved\Crashes"]),
        CacheOnly("Xbox (приложение)", [@"%LocalAppData%\Packages\Microsoft.GamingApp_8wekyb3d8bbwe"],
            [@"%LocalAppData%\Packages\Microsoft.GamingApp_8wekyb3d8bbwe\LocalCache",
             @"%LocalAppData%\Packages\Microsoft.XboxApp_8wekyb3d8bbwe\LocalCache"]),
        CacheOnly("Rockstar Games", [@"%LocalAppData%\Rockstar Games\Launcher"],
            [@"%LocalAppData%\Rockstar Games\Launcher\cache", @"%LocalAppData%\Rockstar Games\Launcher\logs"]),
        CacheOnly("Amazon Games", [@"%LocalAppData%\Amazon Games"],
            [@"%LocalAppData%\Amazon Games\Data\Logs", @"%LocalAppData%\Amazon Games\App\CEFCache"]),
        CacheOnly("itch.io", [@"%AppData%\itch"], [@"%AppData%\itch\Cache\Cache_Data", @"%AppData%\itch\logs"]),
        CacheOnly("Overwolf / CurseForge", [@"%LocalAppData%\Overwolf"],
            [@"%LocalAppData%\Overwolf\Log", @"%LocalAppData%\Overwolf\BrowserCache"]),

        // ===== Игры: логи и отчёты о сбоях (сами игры не трогаем — только их журналы) =====
        CacheOnly("Fortnite (логи)", [@"%LocalAppData%\FortniteGame"],
            [@"%LocalAppData%\FortniteGame\Saved\Logs", @"%LocalAppData%\FortniteGame\Saved\Crashes"]),
        CacheOnly("Roblox (логи)", [@"%LocalAppData%\Roblox\logs"], [@"%LocalAppData%\Roblox\logs"]),
        CacheOnly("Minecraft (логи)", [@"%AppData%\.minecraft"],
            [@"%AppData%\.minecraft\logs", @"%AppData%\.minecraft\crash-reports", @"%AppData%\.minecraft\webcache2"]),

        // ===== Медиа/прочее =====
        CacheOnly("Spotify", [@"%LocalAppData%\Spotify"], [@"%LocalAppData%\Spotify\Data", @"%LocalAppData%\Spotify\Browser\Cache"]),
        CacheOnly("WhatsApp", [@"%LocalAppData%\WhatsApp"], [@"%LocalAppData%\WhatsApp\Cache", @"%LocalAppData%\WhatsApp\GPUCache"]),

        // ===== Графика/шейдеры (видеокарта) — безопасно, пересоздаётся =====
        CacheOnly("Шейдеры NVIDIA",
            [@"%LocalAppData%\NVIDIA", @"%ProgramData%\NVIDIA Corporation\NV_Cache"],
            [@"%LocalAppData%\NVIDIA\DXCache", @"%LocalAppData%\NVIDIA\GLCache", @"%LocalAppData%\NVIDIA Corporation\NV_Cache",
             @"%ProgramData%\NVIDIA Corporation\NV_Cache"]),
        CacheOnly("Шейдеры DirectX", [@"%LocalAppData%\D3DSCache"], [@"%LocalAppData%\D3DSCache"]),
        CacheOnly("Шейдеры AMD", [@"%LocalAppData%\AMD\DxCache", @"%LocalAppData%\AMD\GLCache"], [@"%LocalAppData%\AMD\DxCache", @"%LocalAppData%\AMD\GLCache", @"%LocalAppData%\AMD\VkCache"]),
        CacheOnly("Шейдеры Intel", [@"%LocalAppData%\Intel\ShaderCache"], [@"%LocalAppData%\Intel\ShaderCache"]),

        // ===== Разработка/инструменты =====
        CacheOnly("Visual Studio Code", [@"%AppData%\Code"], [@"%AppData%\Code\Cache", @"%AppData%\Code\CachedData", @"%AppData%\Code\Code Cache", @"%AppData%\Code\GPUCache"]),
        CacheOnly("npm (Node.js)", [@"%LocalAppData%\npm-cache", @"%AppData%\npm-cache"], [@"%LocalAppData%\npm-cache\_cacache", @"%AppData%\npm-cache\_cacache"]),
        CacheOnly("pip (Python)", [@"%LocalAppData%\pip\Cache"], [@"%LocalAppData%\pip\Cache"]),
        CacheOnly("Java (кэш Web Start)", [@"%LocalAppData%\Sun\Java\Deployment\cache"], [@"%LocalAppData%\Sun\Java\Deployment\cache"]),
        CacheOnly("Unity", [@"%LocalAppData%\Unity\cache"], [@"%LocalAppData%\Unity\cache"]),
        CacheOnly("OBS Studio (логи)", [@"%AppData%\obs-studio\logs"], [@"%AppData%\obs-studio\logs"]),

        // ===== Adobe =====
        CacheOnly("Adobe (медиа-кэш)", [@"%AppData%\Adobe\Common"], [@"%AppData%\Adobe\Common\Media Cache Files", @"%AppData%\Adobe\Common\Media Cache"]),
    ];
}
