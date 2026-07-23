namespace Aegis.Scanners.Internal;

/// <summary>
/// Где браузеры хранят свои внутренние базы (история, закладки, значки сайтов, данные форм) и какими
/// процессами эти базы заняты.
///
/// Зачем: когда браузер удаляет старые записи, файл базы НЕ уменьшается — внутри остаются пустоты. За годы
/// такой файл разрастается в разы. Сжатие («уплотнение») переупаковывает файл и выбрасывает пустоты.
/// Делать это можно ТОЛЬКО при закрытом браузере: он держит базу открытой и постоянно в неё пишет.
///
/// Это данные, а не логика: новый браузер добавляется строкой без изменения кода.
/// </summary>
public static class BrowserDatabaseCatalog
{
    /// <summary>Браузеры и их базы.</summary>
    public static readonly IReadOnlyList<BrowserDatabases> Browsers =
    [
        // Chromium-семейство: базы лежат в профилях (Default, Profile 1, …) — их перебирает пробник.
        Chromium("Google Chrome", "chrome.exe", @"%LocalAppData%\Google\Chrome\User Data"),
        Chromium("Microsoft Edge", "msedge.exe", @"%LocalAppData%\Microsoft\Edge\User Data"),
        Chromium("Brave", "brave.exe", @"%LocalAppData%\BraveSoftware\Brave-Browser\User Data"),
        Chromium("Яндекс Браузер", "browser.exe", @"%LocalAppData%\Yandex\YandexBrowser\User Data"),
        Chromium("Vivaldi", "vivaldi.exe", @"%LocalAppData%\Vivaldi\User Data"),
        Chromium("Opera", "opera.exe", @"%AppData%\Opera Software\Opera Stable"),

        // Firefox: свои имена файлов и своя структура профилей.
        new("Mozilla Firefox", ["firefox.exe"], @"%AppData%\Mozilla\Firefox\Profiles",
            ["places.sqlite", "favicons.sqlite", "cookies.sqlite", "formhistory.sqlite", "webappsstore.sqlite"]),
    ];

    /// <summary>Базы браузера на движке Chromium (имена файлов одинаковы у всех).</summary>
    private static BrowserDatabases Chromium(string name, string process, string root) =>
        new(name, [process], root,
            ["History", "Favicons", "Web Data", "Login Data", "Shortcuts", "Top Sites", "Network Action Predictor"]);

    /// <summary>
    /// Стоит ли вообще сжимать эту базу: у совсем маленьких файлов выигрыш неощутим, а операция не бесплатна.
    /// </summary>
    public const long MinimumFileSizeBytes = 1024 * 1024;

    /// <summary>
    /// Минимальный выигрыш, ради которого показываем предложение: меньше 5 МБ на всех базах — не тот случай,
    /// когда стоит просить человека закрыть браузер.
    /// </summary>
    public const long MinimumTotalGainBytes = 5 * 1024 * 1024;
}

/// <summary>Браузер: как называется, какими процессами занят и где его базы.</summary>
/// <param name="Name">Название для человека.</param>
/// <param name="Processes">Имена процессов — если хоть один запущен, базы трогать нельзя.</param>
/// <param name="ProfilesRoot">Корень с профилями пользователя (внутри — папки профилей).</param>
/// <param name="FileNames">Имена файлов баз внутри профиля.</param>
public sealed record BrowserDatabases(
    string Name,
    IReadOnlyList<string> Processes,
    string ProfilesRoot,
    IReadOnlyList<string> FileNames);
