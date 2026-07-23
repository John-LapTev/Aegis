using System;

namespace Aegis.Core;

/// <summary>
/// Разбор и сравнение версий для авто-обновления. Тег релиза GitHub («v2.78.0» / «2.78.0») сравнивается с текущей
/// версией сборки по трём числам (Major.Minor.Build), редакция (4-е число) игнорируется. Чистая логика — под тесты.
/// </summary>
public static class UpdateVersion
{
    /// <summary>Разбирает тег релиза («v2.78.0», «V2.78», «2.78.0.1») в версию; null — если не разобрать.</summary>
    public static Version? Parse(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        var text = tag.Trim();
        if (text.StartsWith('v') || text.StartsWith('V'))
        {
            text = text[1..];
        }

        return Version.TryParse(text, out var version) ? Normalize(version) : null;
    }

    /// <summary>Строго ли тег релиза новее текущей версии (сравнение по Major.Minor.Build).</summary>
    public static bool IsNewer(string? latestTag, Version? current)
    {
        var latest = Parse(latestTag);
        if (latest is null || current is null)
        {
            return false;
        }

        return latest > Normalize(current);
    }

    /// <summary>
    /// Достаёт тег релиза из адреса переадресации github.com/releases/latest → «…/releases/tag/vX.Y.Z».
    /// Нужен для запасной проверки обновления без api.github.com. null — тега в адресе нет.
    /// </summary>
    public static string? TagFromReleaseLocation(string? location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return null;
        }

        const string marker = "/releases/tag/";
        var index = location.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return null;
        }

        // Тег может тянуть за собой query/fragment (?..., #...) — обрезаем.
        var tail = location[(index + marker.Length)..].Trim().Trim('/');
        var end = tail.IndexOfAny(['?', '#', '/']);
        if (end >= 0)
        {
            tail = tail[..end];
        }

        return tail.Length > 0 ? tail : null;
    }

    /// <summary>Приводит к трёхчисловому виду (Major.Minor.Build), отбрасывая редакцию и отрицательные компоненты.</summary>
    private static Version Normalize(Version v) =>
        new(Math.Max(0, v.Major), Math.Max(0, v.Minor), Math.Max(0, v.Build));
}
