using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Probing;
using Aegis.Scanners.Internal;

namespace Aegis.Scanners.Programs;

/// <summary>
/// «Залежавшееся» в группе <see cref="ScanGroup.Junk"/>: битые ярлыки (ведут в никуда), пустые файлы
/// и давно не тронутые загрузки. Всё удаляется обратимо (в Корзину). Старые загрузки — осторожно (по одному),
/// вдруг ещё нужны; ярлыки и пустышки — безопасно, можно массово.
/// </summary>
public sealed class StaleFileScanner : IScanner
{
    private const string LeftoverSection = "Остатки удалённых программ";
    private const string JunkSection = "Можно безопасно очистить";
    private const string OldSection = "Старые файлы в «Загрузках»";

    private readonly IStaleFileProbe _probe;

    public StaleFileScanner(IStaleFileProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    public ScanGroup Group => ScanGroup.Junk;

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _probe.ReadAsync(cancellationToken).ConfigureAwait(false);
        var findings = new List<Finding>();

        // Пустые (0 байт) файлы сворачиваем в ОДИН пункт — это очевидный мусор, чтобы чистить разом,
        // а не десятками строк (просьба Ивана). Остальное (битые ярлыки, старые загрузки) — по одному.
        var empties = snapshot.Items.Where(static i => i.Kind == StaleFileKind.EmptyFile).ToList();
        if (empties.Count > 0)
        {
            findings.Add(AggregateEmptyFiles(empties));
        }

        findings.AddRange(snapshot.Items.Where(static i => i.Kind != StaleFileKind.EmptyFile).Select(ToFinding));
        return new ScanResult { Group = ScanGroup.Junk, Findings = findings };
    }

    private static Finding AggregateEmptyFiles(IReadOnlyList<StaleFile> empties) => new()
    {
        Id = "junk-empty-all",
        Group = ScanGroup.Junk,
        Severity = Severity.Info,
        Title = $"Пустые файлы (0 байт) — {empties.Count} шт.",
        Detail = "можно очистить разом",
        Explain = $"Это {empties.Count} пустых файлов (0 байт) в «Загрузках», Temp и на Рабочем столе — пустышки, " +
                  "обычно остаются после сбоев записи. Ничего полезного в них нет, безопасно удалить все разом. " +
                  "Удалённое уходит в Корзину.",
        Data = new Dictionary<string, string>
        {
            ["paths"] = string.Join("|", empties.Select(static e => e.Path)),
            [FindingDataKeys.Section] = JunkSection,
        },
    };

    private static Finding ToFinding(StaleFile item)
    {
        var (idPrefix, section, title, explain, batch) = item.Kind switch
        {
            StaleFileKind.BrokenShortcut => (
                "leftover-lnk-", LeftoverSection,
                "Битый ярлык: " + item.Title,
                "Этот ярлык ведёт в никуда — программа или файл, на который он указывал, удалены. " +
                "Сам ярлык теперь просто иконка-пустышка, его можно спокойно убрать.",
                true),
            StaleFileKind.EmptyFile => (
                "junk-empty-", JunkSection,
                "Пустой файл (0 байт): " + item.Title,
                "Файл нулевого размера — пустышка, чаще всего остаток от прерванной записи или сбоя. " +
                "Безопасно удалить, ничего полезного в нём нет.",
                true),
            _ => (
                "old-download-", OldSection,
                "Давно не открывал: " + item.Title,
                "Этот файл в «Загрузках» давно не менялся" + (item.Note is null ? string.Empty : $" ({item.Note})") +
                ". Часто это скачанные установщики и архивы, которые уже не нужны. " +
                "Проверь по кнопке-папке — если не нужен, удали (в Корзину).",
                false),
        };

        var data = new Dictionary<string, string>
        {
            [FindingDataKeys.Kind] = FindingKinds.FileDelete,
            ["path"] = item.Path,
            [FindingDataKeys.Section] = section,
        };
        if (!batch)
        {
            data["noBatch"] = "1";
        }

        return new Finding
        {
            Id = idPrefix + ScanId.ForPath(item.Path),
            Group = ScanGroup.Junk,
            Severity = Severity.Info,
            Title = title,
            Detail = item.Path,
            Explain = explain,
            Data = data,
        };
    }

}
