using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Core;
using Aegis.Scanners.Probing;

namespace Aegis.Scanners.Files;

/// <summary>
/// Поиск больших и дублирующихся файлов (группа <see cref="ScanGroup.Junk"/>) — помочь освободить место
/// сверх обычного «мусора». Это подсказки (severity Info), а не проблемы: пользователь сам решает, что удалить.
/// Удаление — обратимое (через карантин).
/// </summary>
public sealed class LargeDuplicateScanner : IScanner
{
    /// <summary>Файлы от этого размера считаем «большими» (1 ГБ).</summary>
    private const long LargeFileThreshold = 1024L * 1024 * 1024;

    private readonly IFileInventoryProbe _probe;

    public LargeDuplicateScanner(IFileInventoryProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    public ScanGroup Group => ScanGroup.Junk;

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var files = await _probe.FindAsync(cancellationToken).ConfigureAwait(false);
        var findings = new List<Finding>();

        var duplicateGroups = files
            .Where(static f => !string.IsNullOrEmpty(f.ContentHash))
            .GroupBy(static f => f.ContentHash)
            .Where(static g => g.Count() > 1)
            .ToList();

        // Один и тот же файл не показываем дважды: если он входит в группу дублей, в «больших» его не дублируем.
        var duplicatePaths = duplicateGroups
            .SelectMany(static g => g)
            .Select(static f => f.Path)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        findings.AddRange(files
            .Where(f => f.SizeBytes >= LargeFileThreshold && !duplicatePaths.Contains(f.Path))
            .OrderByDescending(static f => f.SizeBytes)
            .Select(CreateLargeFileFinding));

        findings.AddRange(duplicateGroups.Select(CreateDuplicateFinding));

        return new ScanResult { Group = ScanGroup.Junk, Findings = findings };
    }

    private static Finding CreateLargeFileFinding(FileEntry file) => new()
    {
        Id = $"largefile-{file.Path}",
        Group = ScanGroup.Junk,
        Severity = Severity.Info,
        Title = $"Большой файл — {HumanSize.Format(file.SizeBytes)}",
        Detail = file.Path,
        Explain = "Этот файл занимает много места. Если он больше не нужен — нажми «Очистить»: файл уйдёт в Корзину " +
                  "Windows (можно вернуть оттуда; чтобы освободить место — очисти Корзину). Если нужен — пометь «Безопасно».",
        Data = new Dictionary<string, string>
        {
            [FindingDataKeys.Kind] = FindingKinds.FileDelete,
            ["path"] = file.Path,
            ["bytes"] = file.SizeBytes.ToString(System.Globalization.CultureInfo.InvariantCulture), // для суммы «всего в разделе» (правка 960)
        },
    };

    private static Finding CreateDuplicateFinding(IGrouping<string, FileEntry> group)
    {
        var copies = group.ToList();
        // Консервативно: одинаковые по содержимому файлы должны быть одного размера, берём минимальный.
        var size = copies.Min(static c => c.SizeBytes);
        var reclaimable = size * (copies.Count - 1);

        return new Finding
        {
            Id = $"dupes-{group.Key}",
            Group = ScanGroup.Junk,
            Severity = Severity.Info,
            Title = $"Одинаковые файлы — {copies.Count} копий, можно освободить {HumanSize.Format(reclaimable)}",
            Detail = copies[0].Path,
            Explain = "Несколько одинаковых файлов лежат в разных местах и занимают место зря. Раскрой список " +
                      "ниже, посмотри пути и удали лишние копии, оставив нужную. Удалённое уходит в Корзину Windows.",
            Data = new Dictionary<string, string>
            {
                [FindingDataKeys.Kind] = FindingKinds.DuplicateGroup,
                ["paths"] = string.Join("|", copies.Select(c => c.Path)),
                ["bytes"] = reclaimable.ToString(System.Globalization.CultureInfo.InvariantCulture), // освобождаемое = лишние копии (правка 960)
            },
        };
    }
}
