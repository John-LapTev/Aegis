namespace Aegis.System.Internal;

/// <summary>
/// Защита от удаления «не того»: корней дисков, системных папок Windows и ОБЩИХ папок вендоров (Microsoft, Google…),
/// которыми пользуются много программ. Используется всеми операциями удаления папок (остатки, «Удалить полностью»,
/// чистка по следу установки). Ядро проверки — на чистых строках (без обращения к ОС), поэтому легко тестируется.
/// </summary>
internal static class PathSafety
{
    /// <summary>
    /// «Листовые» имена папок, которые НИКОГДА не удаляем целиком: контейнеры (Program Files, AppData, Roaming…)
    /// и общие папки вендоров/системы, которыми пользуется много программ.
    /// </summary>
    private static readonly HashSet<string> NeverDeleteLeaf = new(StringComparer.OrdinalIgnoreCase)
    {
        // Контейнеры/корни
        "windows", "winnt", "users", "user", "program files", "program files (x86)", "programdata",
        "appdata", "roaming", "local", "locallow", "temp", "tmp", "system32", "syswow64",
        "documents", "desktop", "downloads", "pictures", "music", "videos", "public",
        // Корневые системные папки диска (важны для загрузки/восстановления — целиком не трогаем)
        "$recycle.bin", "recovery", "perflogs", "boot", "config.msi", "system volume information",
        "$winreagent", "$windows.~bt", "$windows.~ws", "$sysreset", "documents and settings",
        "inetpub", "efi", "onedrive", "onedrivetemp",
        // Общие папки-контейнеры первого уровня, куда люди складывают МНОГО программ: удалять целиком нельзя
        // (снесли бы соседние программы). Отдельная папка приложения (C:\Rave) при этом остаётся разрешённой.
        "tools", "games", "portable", "soft", "apps", "programs", "software", "distr", "install", "bin",
        // Общие папки вендоров (под ними — данные многих приложений)
        "common files", "microsoft", "microsoft corporation", "windowsapps", "packages",
        "google", "mozilla", "apple", "apple computer", "nvidia", "nvidia corporation",
        "intel", "amd", "adobe", "steam", "microsoftedge", "comms", "connecteddevicesplatform",
    };

    /// <summary>Общие ветки реестра, которые НИКОГДА не удаляем: системные и вендорские, которыми пользуется много программ.</summary>
    private static readonly HashSet<string> SharedRegistryKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "software", "system", "microsoft", "google", "classes", "policies", "clients", "wow6432node",
        "windows", "microsoft corporation", "intel", "nvidia corporation", "amd", "khronos group",
        "odbc", "registeredapplications", "mozilla", "apple computer", "apple inc.", "valve", "wow6432",
        "currentversion", "run", "runonce", "uninstall",
    };

    /// <summary>Безопасно ли удалять эту ветку реестра (не хайв, не сам SOFTWARE и не общая ветка вендора/системы).</summary>
    public static bool IsSafeRegistryKey(string? regPath)
    {
        if (string.IsNullOrWhiteSpace(regPath))
        {
            return false;
        }

        var segments = regPath.Replace('/', '\\').TrimEnd('\\').Split('\\', StringSplitOptions.RemoveEmptyEntries);

        // Минимум ХАЙВ\SOFTWARE\<имя> — 3 сегмента (не удаляем сам хайв/SOFTWARE), и лист не должен быть общей веткой.
        return segments.Length >= 3 && !SharedRegistryKeys.Contains(segments[^1]);
    }

    /// <summary>Безопасно ли отправлять эту папку в Корзину (не корень, не системная/общая папка).</summary>
    public static bool IsSafeToDeleteFolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        // Тримим весь путь от пробелов (иначе ведущий пробел « C:\Windows\…» обошёл бы проверку папки Windows ниже).
        var normalized = path.Trim().Replace('/', '\\').TrimEnd('\\');

        // Сетевые пути (UNC \\server\share) и относительные с «..» — не удаляем (можно нацелиться на корень шары/уйти
        // вверх из папки программы). Аудит 2026-07-04.
        if (normalized.StartsWith(@"\\", StringComparison.Ordinal) || normalized.Contains(".."))
        {
            return false;
        }

        var segments = normalized.Split('\\', StringSplitOptions.RemoveEmptyEntries);

        // Нужен диск + минимум ОДНА папка («C:\App»): сам корень диска («C:\») целиком не трогаем. Приложения нередко
        // ставятся папкой первого уровня (C:\Rave) — их удалять можно; известные системные/общие папки первого уровня
        // (Windows, Program Files, ProgramData, Recovery, PerfLogs…) отсекаются денилистом ниже.
        if (segments.Length < 2)
        {
            return false;
        }

        // Windows отбрасывает ведущие/хвостовые пробелы и хвостовые точки в имени сегмента — такой путь может указывать
        // на ДРУГУЮ реальную папку («C:\Tools » → C:\Tools). Любой сегмент с такими символами считаем небезопасным.
        foreach (var segment in segments)
        {
            if (segment != segment.Trim().TrimEnd('.'))
            {
                return false;
            }
        }

        // Последняя папка не должна быть контейнером/общей/системной папкой (диск-корень, вендор, папка восстановления).
        if (NeverDeleteLeaf.Contains(segments[^1]))
        {
            return false;
        }

        // Ничего внутри папки Windows (на не-Windows Environment вернёт пусто — проверка пропускается).
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (!string.IsNullOrEmpty(windows)
            && normalized.StartsWith(windows.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}
