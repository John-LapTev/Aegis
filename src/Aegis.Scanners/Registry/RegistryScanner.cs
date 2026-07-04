using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Probing;

namespace Aegis.Scanners.Registry;

/// <summary>
/// Сканер реестра (блок «А», группа <see cref="ScanGroup.Registry"/>). Превращает проблемы от
/// <see cref="IRegistryProbe"/> в понятные находки. Каждая правка реестра обратима: ветка
/// экспортируется ПЕРЕД изменением (ADR 0002).
/// </summary>
public sealed class RegistryScanner : IScanner
{
    private readonly IRegistryProbe _probe;

    public RegistryScanner(IRegistryProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    public ScanGroup Group => ScanGroup.Registry;

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var issues = await _probe.FindAsync(cancellationToken).ConfigureAwait(false);

        var findings = issues
            .Select(CreateFinding)
            .ToList();

        return new ScanResult { Group = ScanGroup.Registry, Findings = findings };
    }

    private static Finding CreateFinding(RegistryIssue issue)
    {
        var (severity, title, explain) = Classify(issue.Kind);
        var detail = issue.Reference is null ? issue.Path : $"{issue.Path} → {issue.Reference}";

        return new Finding
        {
            // Путь ключа уникален → стабильный и уникальный Id.
            Id = $"registry-{issue.Kind}-{issue.Path}",
            Group = ScanGroup.Registry,
            Severity = severity,
            Title = title,
            Detail = detail,
            Explain = explain,
            Data = new Dictionary<string, string>
            {
                [FindingDataKeys.Kind] = FindingKinds.RegistryDelete,
                ["hive"] = issue.Hive,
                ["subkey"] = issue.Path,
            },
        };
    }

    private static (Severity Severity, string Title, string Explain) Classify(RegistryIssueKind kind) => kind switch
    {
        RegistryIssueKind.OrphanedUninstallEntry => (
            Severity.Warning,
            "Запись об уже удалённой программе",
            "В реестре осталась запись от программы, которой уже нет на компьютере. Она бесполезна и засоряет " +
            "реестр. Уберём её — перед этим экспортируем ветку, чтобы можно было вернуть."),
        RegistryIssueKind.MissingFileReference => (
            Severity.Warning,
            "Ссылка на несуществующий файл",
            "Запись в реестре указывает на файл, которого больше нет. Это «мусорная» ссылка — уберём её. " +
            "Перед правкой сделаем бэкап ветки."),
        RegistryIssueKind.InvalidStartupReference => (
            Severity.Warning,
            "Автозапуск указывает на отсутствующий файл",
            "В автозапуске прописана программа, файла которой уже нет. При включении компьютера система впустую " +
            "пытается её запустить. Уберём запись — обратимо, с бэкапом."),
        RegistryIssueKind.EmptyAutostartKey => (
            Severity.Info,
            "Пустая запись автозапуска",
            "Найдена пустая или повреждённая запись автозапуска. Она ничего не делает — можно безопасно удалить."),
        _ => (
            Severity.Info,
            "Запись реестра требует внимания",
            "Найдена запись реестра, которую стоит проверить."),
    };
}
