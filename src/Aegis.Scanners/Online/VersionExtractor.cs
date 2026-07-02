using System.Text.RegularExpressions;

namespace Aegis.Scanners.Online;

/// <summary>
/// Извлекает «похожую на версию» строку (например, 610.62 или 6.0.9659.1) из текстов выдачи веб-поиска.
/// Эвристика: берём первый токен вида число.число(.число…), пропуская явные годы (2019–2031) без долей.
/// Точность не гарантируется — результат всегда помечаем как приблизительный.
/// </summary>
public static partial class VersionExtractor
{
    public static string? Extract(IEnumerable<string> texts)
    {
        foreach (var text in texts)
        {
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            foreach (Match match in VersionRegex().Matches(text))
            {
                var candidate = match.Groups[1].Value;
                if (!LooksLikeBareYear(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    /// <summary>Отсекает «2024» как год (две части, первая — 19xx/20xx): это, скорее, дата, а не версия драйвера.</summary>
    private static bool LooksLikeBareYear(string version)
    {
        var parts = version.Split('.');
        return parts.Length == 2
               && parts[0].Length == 4
               && int.TryParse(parts[0], out var year)
               && year is >= 2000 and <= 2099;
    }

    [GeneratedRegex(@"\b(\d{1,5}(?:\.\d{1,5}){1,3})\b")]
    private static partial Regex VersionRegex();
}
