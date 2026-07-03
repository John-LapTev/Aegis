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

        var normalized = path.Replace('/', '\\').TrimEnd('\\');
        var segments = normalized.Split('\\', StringSplitOptions.RemoveEmptyEntries);

        // Нужен диск + минимум ДВЕ вложенные папки («C:\A\B»): иначе это корень диска или папка первого уровня
        // (Program Files, ProgramData и т.п.) — такие целиком не удаляем.
        if (segments.Length < 3)
        {
            return false;
        }

        // Последняя папка не должна быть контейнером/общей папкой вендора.
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
