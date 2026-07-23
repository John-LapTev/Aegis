namespace Aegis.Scanners.Internal;

/// <summary>
/// Разбор пунктов контекстного меню проводника (то, что выпадает по правому клику). Каждый пункт — это либо
/// команда с путём к программе, либо обработчик-расширение (библиотека по идентификатору CLSID).
///
/// Зачем: пункты от давно удалённых программ остаются в меню навсегда. Они не только мешают — каждый такой
/// пункт проводник пытается загрузить при открытии меню, поэтому правый клик «думает» секундами.
/// Чистые функции: проверяются тестами на любой ОС.
/// </summary>
public static class ContextMenuAnalyzer
{
    /// <summary>
    /// Достаёт путь к программе из строки запуска пункта меню: <c>"C:\App\app.exe" "%1"</c> →
    /// <c>C:\App\app.exe</c>. Возвращает null, если пути в строке нет (например, команда через rundll32
    /// или ссылку на протокол).
    /// </summary>
    public static string? ExtractExecutablePath(string? command)
    {
        var value = command?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        // Путь в кавычках — берём как есть.
        if (value.StartsWith('"'))
        {
            var closing = value.IndexOf('"', 1);
            return closing > 1 ? value[1..closing] : null;
        }

        // Команду мог запустить системный «запускатель» (rundll32 и подобные). Проверять его бессмысленно —
        // он есть всегда; нас интересует то, ЧТО он загружает, поэтому пропускаем его и разбираем остаток.
        foreach (var launcher in Launchers)
        {
            if (value.StartsWith(launcher, StringComparison.OrdinalIgnoreCase))
            {
                return ExtractExecutablePath(value[launcher.Length..].TrimStart());
            }
        }

        // Без кавычек: путь идёт до первого аргумента. Ориентируемся на расширение исполняемого файла,
        // потому что в пути могут быть пробелы («C:\Program Files\...»).
        var best = -1;
        var bestLength = 0;
        foreach (var extension in Executables)
        {
            var index = value.IndexOf(extension, StringComparison.OrdinalIgnoreCase);
            if (index > 0 && (best < 0 || index < best))
            {
                best = index;
                bestLength = extension.Length;
            }
        }

        return best > 0 ? value[..(best + bestLength)] : null;
    }

    private static readonly string[] Executables = [".exe", ".dll", ".cmd", ".bat"];

    /// <summary>Системные запускатели: сами по себе всегда на месте, важно то, что они загружают.</summary>
    private static readonly string[] Launchers =
    [
        "rundll32.exe", "rundll32", "regsvr32.exe", "cmd.exe /c", "cmd /c", "powershell.exe", "wscript.exe", "mshta.exe",
    ];

    /// <summary>
    /// Пункт считается «мёртвым», если программа, которую он запускает, больше не существует. Пункты без
    /// разбираемого пути НЕ трогаем: не смогли понять — значит не наше дело (лучше лишний пункт в меню,
    /// чем сломанное меню).
    /// </summary>
    public static bool IsBroken(string? command, Func<string, bool> fileExists)
    {
        ArgumentNullException.ThrowIfNull(fileExists);

        var path = ExtractExecutablePath(command);
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        // Переменные окружения раскрывать некому в тестах — считаем такие пути живыми и не трогаем.
        if (path.Contains('%'))
        {
            return false;
        }

        return !fileExists(path);
    }

    /// <summary>Понятное человеку имя пункта меню: подпись, если она есть, иначе имя ключа реестра.</summary>
    public static string DisplayName(string keyName, string? label) =>
        string.IsNullOrWhiteSpace(label) ? keyName : label.Trim();
}
