namespace Aegis.System.Internal;

/// <summary>
/// Разворачивает шаблоны путей кэша приложений (подход Winapp2, реализовано clean-room): подстановка
/// переменных (%LocalAppData% и т.п.) и раскрытие подстановочных сегментов (Chrome*\User Data\*\Cache)
/// в реально существующие папки. Подстановка переменных — чистая (тестируется); раскрытие масок — по ФС.
/// </summary>
internal static class AppCachePathExpander
{
    private static readonly (string Token, Environment.SpecialFolder Folder)[] FolderTokens =
    [
        ("%LocalAppData%", Environment.SpecialFolder.LocalApplicationData),
        ("%AppData%", Environment.SpecialFolder.ApplicationData),
        ("%ProgramData%", Environment.SpecialFolder.CommonApplicationData),
        ("%UserProfile%", Environment.SpecialFolder.UserProfile),
        ("%ProgramFiles%", Environment.SpecialFolder.ProgramFiles),
        ("%ProgramFiles(x86)%", Environment.SpecialFolder.ProgramFilesX86),
        ("%SystemRoot%", Environment.SpecialFolder.Windows),
    ];

    /// <summary>Подставить переменные окружения в шаблон (чистая функция). Неизвестные оставляет как есть.</summary>
    public static string ExpandVariables(string pattern)
    {
        var result = pattern;
        foreach (var (token, folder) in FolderTokens)
        {
            if (result.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                result = Replace(result, token, Environment.GetFolderPath(folder));
            }
        }

        if (result.Contains("%LocalLow%", StringComparison.OrdinalIgnoreCase))
        {
            var localLow = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData", "LocalLow");
            result = Replace(result, "%LocalLow%", localLow);
        }

        if (result.Contains("%Temp%", StringComparison.OrdinalIgnoreCase))
        {
            result = Replace(result, "%Temp%", Path.GetTempPath().TrimEnd('\\'));
        }

        if (result.Contains("%Public%", StringComparison.OrdinalIgnoreCase))
        {
            var publicDir = Environment.GetEnvironmentVariable("PUBLIC") ?? @"C:\Users\Public";
            result = Replace(result, "%Public%", publicDir);
        }

        return result;
    }

    /// <summary>Развернуть шаблон (с переменными и масками) в существующие папки. Пусто — ничего не нашлось.</summary>
    public static IReadOnlyList<string> ResolveExistingDirectories(string pattern)
    {
        var expanded = ExpandVariables(pattern);
        if (expanded.Contains('%'))
        {
            return []; // осталась неизвестная переменная — пропускаем (не угадываем)
        }

        var segments = expanded.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return [];
        }

        // Старт — корень диска (например, "C:").
        var current = new List<string> { segments[0] + "\\" };
        for (var i = 1; i < segments.Length; i++)
        {
            var segment = segments[i];
            var next = new List<string>();
            var hasWildcard = segment.Contains('*') || segment.Contains('?');
            foreach (var dir in current)
            {
                try
                {
                    if (hasWildcard)
                    {
                        next.AddRange(Directory.GetDirectories(dir, segment));
                    }
                    else
                    {
                        var combined = Path.Combine(dir, segment);
                        if (Directory.Exists(combined))
                        {
                            next.Add(combined);
                        }
                    }
                }
                catch (Exception)
                {
                    // Недоступно — пропускаем.
                }
            }

            current = next;
            if (current.Count == 0)
            {
                break;
            }
        }

        return current;
    }

    /// <summary>Развернуть шаблон, оканчивающийся ИМЕНЕМ ФАЙЛА (с масками), в существующие файлы (cookie/история).</summary>
    public static IReadOnlyList<string> ResolveExistingFiles(string pattern)
    {
        var expanded = ExpandVariables(pattern);
        if (expanded.Contains('%'))
        {
            return [];
        }

        var lastSep = expanded.LastIndexOf('\\');
        if (lastSep <= 0 || lastSep == expanded.Length - 1)
        {
            return [];
        }

        var filePart = expanded[(lastSep + 1)..];
        var files = new List<string>();
        foreach (var dir in ResolveExistingDirectories(expanded[..lastSep]))
        {
            try
            {
                if (filePart.Contains('*') || filePart.Contains('?'))
                {
                    files.AddRange(Directory.GetFiles(dir, filePart));
                }
                else
                {
                    var full = Path.Combine(dir, filePart);
                    if (File.Exists(full))
                    {
                        files.Add(full);
                    }
                }
            }
            catch (Exception)
            {
                // Недоступно — пропускаем.
            }
        }

        return files;
    }

    private static string Replace(string input, string token, string value)
    {
        var index = input.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        return index < 0 ? input : input[..index] + value + input[(index + token.Length)..];
    }
}
