using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Core;
using Aegis.Scanners.Probing;

namespace Aegis.Scanners.Junk;

/// <summary>
/// Сканер мусора (блок «А», группа <see cref="ScanGroup.Junk"/>). Группирует кандидаты от
/// <see cref="IJunkProbe"/> по категориям и формирует находки с понятным размером и объяснением.
/// Только анализ — удаление выполняется обратимым исправлением (карантин ПЕРЕД удалением).
/// </summary>
public sealed class JunkScanner : IScanner
{
    private readonly IJunkProbe _probe;

    public JunkScanner(IJunkProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    public ScanGroup Group => ScanGroup.Junk;

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var candidates = await _probe.FindAsync(cancellationToken).ConfigureAwait(false);

        var findings = candidates
            .Where(static c => c.SizeBytes > 0)
            .GroupBy(static c => c.Category)
            .Select(group => CreateFinding(
                group.Key,
                group.Sum(static c => c.SizeBytes),
                group.Select(static c => c.Path).ToList()))
            .OrderByDescending(static f => f.Size)
            .Select(static f => f.Finding)
            .ToList();

        return new ScanResult { Group = ScanGroup.Junk, Findings = findings };
    }

    private static (long Size, Finding Finding) CreateFinding(
        JunkCategory category,
        long totalSize,
        IReadOnlyList<string> paths)
    {
        var finding = new Finding
        {
            Id = $"junk-{category}",
            Group = ScanGroup.Junk,
            Severity = Severity.Info,
            Title = $"{DisplayName(category)} — {HumanSize.Format(totalSize)}",
            Detail = paths.Count == 1 ? paths[0] : $"{paths.Count} расположений",
            Explain = ExplainFor(category),
            Data = new Dictionary<string, string>
            {
                ["paths"] = string.Join("|", paths),
                ["bytes"] = totalSize.ToString(System.Globalization.CultureInfo.InvariantCulture), // для суммы «Можно освободить» (правка 946)
            },
        };

        return (totalSize, finding);
    }

    private static string DisplayName(JunkCategory category) => category switch
    {
        JunkCategory.TempFiles => "Временные файлы",
        JunkCategory.RecycleBin => "Корзина",
        JunkCategory.Cache => "Кэш приложений",
        JunkCategory.Logs => "Старые журналы",
        JunkCategory.WindowsUpdateCache => "Кэш обновлений Windows",
        JunkCategory.ThumbnailCache => "Кэш миниатюр",
        _ => "Мусор",
    };

    private static string ExplainFor(JunkCategory category) => category switch
    {
        JunkCategory.TempFiles =>
            "Временные файлы, которые программы оставили и забыли удалить. Их безопасно очистить — " +
            "освободится место на диске.",
        JunkCategory.RecycleBin =>
            "Удалённые файлы всё ещё лежат в Корзине и занимают место. Очистим, если они точно не нужны.",
        JunkCategory.Cache =>
            "Кэш ускоряет работу программ, но со временем разрастается. Очистка освободит место; " +
            "программы пересоздадут нужный кэш сами.",
        JunkCategory.Logs =>
            "Старые журналы работы программ. Нужны только для диагностики — обычно их можно удалить.",
        JunkCategory.WindowsUpdateCache =>
            "Скачанные установочные файлы обновлений Windows, которые уже не нужны. Очистка освободит место.",
        JunkCategory.ThumbnailCache =>
            "Кэш миниатюр (предпросмотр картинок и видео). Windows создаст его заново при необходимости.",
        _ => "Ненужные файлы, которые можно безопасно удалить, чтобы освободить место.",
    };
}
