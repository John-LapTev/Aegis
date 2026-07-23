using System.Globalization;
using System.Text.RegularExpressions;

namespace Aegis.Scanners.Internal;

/// <summary>
/// Разбор вывода <c>pnputil /enum-drivers</c> — списка пакетов драйверов, которые Windows хранит у себя
/// (хранилище драйверов). Windows НИКОГДА не удаляет старые версии сама: после нескольких обновлений
/// видеодрайвера там накапливаются гигабайты.
///
/// Имена полей в выводе переведены на язык системы, поэтому опираться на них нельзя. Разбираем по форме
/// ЗНАЧЕНИЙ: «oem12.inf» — опубликованное имя, другое «*.inf» — исходное, «31.0.15.3623» — версия,
/// «07/18/1968» — дата. Чистая функция: проверяется тестами на любой ОС.
/// </summary>
public static partial class DriverPackageParser
{
    [GeneratedRegex(@"^oem\d+\.inf$", RegexOptions.IgnoreCase)]
    private static partial Regex PublishedNamePattern();

    [GeneratedRegex(@"\b(\d+\.\d+(?:\.\d+){0,2})\b")]
    private static partial Regex VersionPattern();

    // Разделитель захватываем: по нему различаем «12.05.2024» (день первый) и «05/12/2024» (месяц первый).
    [GeneratedRegex(@"\b(\d{1,2})([./-])(\d{1,2})\2(\d{4})\b")]
    private static partial Regex DatePattern();

    /// <summary>Разобрать весь вывод команды в список пакетов.</summary>
    public static IReadOnlyList<DriverPackage> Parse(string output)
    {
        var packages = new List<DriverPackage>();
        if (string.IsNullOrWhiteSpace(output))
        {
            return packages;
        }

        // Пакеты разделены пустой строкой.
        foreach (var block in output.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            if (ParseBlock(block) is { } package)
            {
                packages.Add(package);
            }
        }

        return packages;
    }

    private static DriverPackage? ParseBlock(string block)
    {
        string? published = null;
        string? original = null;
        string? version = null;
        DateOnly? date = null;

        foreach (var line in block.Split('\n'))
        {
            var separator = line.IndexOf(':');
            if (separator < 0)
            {
                continue;
            }

            var value = line[(separator + 1)..].Trim();
            if (value.Length == 0)
            {
                continue;
            }

            if (PublishedNamePattern().IsMatch(value))
            {
                published = value.ToLowerInvariant();
                continue;
            }

            if (value.EndsWith(".inf", StringComparison.OrdinalIgnoreCase))
            {
                original ??= value.ToLowerInvariant();
                continue;
            }

            // Строка «дата и версия» приходит одним полем: «12.05.2024 31.0.15.3623». Сначала вырезаем дату —
            // иначе в русском формате она сама выглядит как версия и подменяет её. Вырезаем ТОЛЬКО первое
            // осмысленное совпадение: хвост версии («0.15.3623») тоже похож на дату по форме, но датой не является.
            var withoutDate = value;
            foreach (Match candidate in DatePattern().Matches(value))
            {
                if (ParseDate(candidate) is not DateOnly parsed)
                {
                    continue;
                }

                date ??= parsed;
                withoutDate = value.Remove(candidate.Index, candidate.Length);
                break;
            }

            if (VersionPattern().Match(withoutDate) is { Success: true } versionMatch)
            {
                version ??= versionMatch.Value;
            }
        }

        if (published is null)
        {
            return null; // блок без опубликованного имени — не пакет (шапка вывода)
        }

        return new DriverPackage
        {
            PublishedName = published,
            OriginalName = original ?? published,
            Version = version,
            Date = date,
        };
    }

    /// <summary>
    /// Дата в выводе идёт в формате системы. Различаем по разделителю: через точку Windows печатает
    /// «день.месяц.год» (русская и европейская локаль), через косую черту — «месяц/день/год» (США).
    /// Число больше 12 в любом случае может быть только днём.
    /// </summary>
    private static DateOnly? ParseDate(Match match)
    {
        if (!int.TryParse(match.Groups[1].Value, out var first)
            || !int.TryParse(match.Groups[3].Value, out var second)
            || !int.TryParse(match.Groups[4].Value, out var year))
        {
            return null;
        }

        var dayFirst = match.Groups[2].Value != "/";
        var (month, day) = first > 12 ? (second, first)
            : second > 12 ? (first, second)
            : dayFirst ? (second, first)
            : (first, second);
        if (month is < 1 or > 12 || day is < 1 or > 31)
        {
            return null;
        }

        try
        {
            return new DateOnly(year, month, day);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>
    /// Находит устаревшие пакеты: внутри каждой группы (одно исходное имя .inf) оставляем самый новый,
    /// остальные считаем лишними. Пакеты, которыми сейчас пользуется железо, не трогаем никогда.
    /// </summary>
    public static IReadOnlyList<DriverPackage> FindObsolete(
        IReadOnlyList<DriverPackage> packages,
        IReadOnlySet<string> activePublishedNames)
    {
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentNullException.ThrowIfNull(activePublishedNames);

        var obsolete = new List<DriverPackage>();

        foreach (var group in packages.GroupBy(p => p.OriginalName, StringComparer.OrdinalIgnoreCase))
        {
            var ordered = group
                .OrderByDescending(p => ParseVersion(p.Version))
                .ThenByDescending(p => p.Date ?? DateOnly.MinValue)
                .ToList();

            if (ordered.Count < 2)
            {
                continue; // единственная версия — это и есть рабочий драйвер
            }

            // Самый новый оставляем всегда; из остальных — только те, что не привязаны к железу.
            foreach (var package in ordered.Skip(1))
            {
                if (!activePublishedNames.Contains(package.PublishedName))
                {
                    obsolete.Add(package);
                }
            }
        }

        return obsolete;
    }

    /// <summary>Версия в сравнимом виде; отсутствие версии = самая старая.</summary>
    private static Version ParseVersion(string? value) =>
        Version.TryParse(value, out var parsed) ? parsed : new Version(0, 0);

    /// <summary>Человеку — понятная подпись версии и даты.</summary>
    public static string Describe(DriverPackage package)
    {
        var parts = new List<string>();
        if (package.Version is { Length: > 0 })
        {
            parts.Add("версия " + package.Version);
        }

        if (package.Date is DateOnly date)
        {
            parts.Add(date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture));
        }

        return parts.Count > 0 ? string.Join(", ", parts) : package.PublishedName;
    }
}

/// <summary>Пакет драйвера в хранилище Windows.</summary>
public sealed record DriverPackage
{
    /// <summary>Имя в хранилище (например, «oem12.inf») — по нему пакет удаляется.</summary>
    public required string PublishedName { get; init; }

    /// <summary>Исходное имя файла драйвера («nvlt.inf») — по нему группируются версии одного драйвера.</summary>
    public required string OriginalName { get; init; }

    /// <summary>Версия драйвера, если удалось прочитать.</summary>
    public string? Version { get; init; }

    /// <summary>Дата драйвера, если удалось прочитать.</summary>
    public DateOnly? Date { get; init; }
}
