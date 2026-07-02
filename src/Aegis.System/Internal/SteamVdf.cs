using System.Text.RegularExpressions;

namespace Aegis.System.Internal;

/// <summary>
/// Чистые парсеры файлов Steam (без файловой системы — тестируются на любой ОС):
/// пути библиотек из <c>libraryfolders.vdf</c> и AppID из имени <c>appmanifest_&lt;id&gt;.acf</c>.
/// Нужны, чтобы понять, какие игры реально установлены, и отличить остатки удалённых.
/// </summary>
internal static partial class SteamVdf
{
    [GeneratedRegex("\"path\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex PathRegex();

    [GeneratedRegex("appmanifest_(\\d+)\\.acf", RegexOptions.IgnoreCase)]
    private static partial Regex ManifestRegex();

    /// <summary>Пути корневых папок библиотек Steam из libraryfolders.vdf (с разэкранированием <c>\\</c>).</summary>
    public static IReadOnlyList<string> ParseLibraryPaths(string? vdfContent)
    {
        if (string.IsNullOrEmpty(vdfContent))
        {
            return [];
        }

        var paths = new List<string>();
        foreach (Match match in PathRegex().Matches(vdfContent))
        {
            var raw = match.Groups[1].Value.Replace("\\\\", "\\");
            if (raw.Length > 0)
            {
                paths.Add(raw);
            }
        }

        return paths;
    }

    /// <summary>AppID из имени файла манифеста (<c>appmanifest_730.acf</c> → <c>730</c>) или null.</summary>
    public static string? AppIdFromManifest(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return null;
        }

        var match = ManifestRegex().Match(fileName);
        return match.Success ? match.Groups[1].Value : null;
    }
}
