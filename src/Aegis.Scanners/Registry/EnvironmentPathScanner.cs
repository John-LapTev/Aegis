using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Probing;

namespace Aegis.Scanners.Registry;

/// <summary>
/// Записи переменной <c>Path</c>, ведущие в несуществующие папки (группа <see cref="ScanGroup.Registry"/>).
/// Path — это список мест, где Windows ищет программы; после удаления программ в нём остаются мёртвые
/// записи, и система проверяет их при каждом запуске команды. Удаление записи обратимо.
/// </summary>
public sealed class EnvironmentPathScanner : IScanner
{
    private readonly IEnvironmentPathProbe _probe;

    public EnvironmentPathScanner(IEnvironmentPathProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    public ScanGroup Group => ScanGroup.Registry;

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var broken = await _probe.ReadBrokenAsync(cancellationToken).ConfigureAwait(false);

        var findings = broken.Select(entry => new Finding
        {
            Id = $"envpath-{entry.Hive}-{entry.Directory}",
            Group = ScanGroup.Registry,
            Severity = Severity.Info,
            Title = "В списке путей осталась несуществующая папка",
            Detail = entry.Directory,
            Explain = "Windows держит список папок, в которых ищет программы при запуске команд. Эта папка в " +
                      "списке есть, а на диске её уже нет — след удалённой программы. Система впустую проверяет её " +
                      "при каждом запуске. Удаление записи ни на что не влияет и обратимо: прежний список " +
                      "сохраняется целиком, вернуть его можно в разделе «Бэкапы»." +
                      (entry.Hive == "HKLM"
                          ? " Эта запись общая для всего компьютера — нужны права администратора."
                          : " Эта запись касается только твоей учётной записи."),
            Data = new Dictionary<string, string>
            {
                [FindingDataKeys.Kind] = FindingKinds.PathEntryRemove,
                [FindingDataKeys.Hive] = entry.Hive,
                [FindingDataKeys.Path] = entry.Directory,
                [FindingDataKeys.Section] = "Переменные среды",
            },
        }).ToList();

        return new ScanResult { Group = ScanGroup.Registry, Findings = findings };
    }
}
