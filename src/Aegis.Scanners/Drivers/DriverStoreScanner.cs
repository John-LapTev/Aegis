using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Internal;
using Aegis.Scanners.Probing;

namespace Aegis.Scanners.Drivers;

/// <summary>
/// Старые версии драйверов в хранилище Windows (группа <see cref="ScanGroup.Drivers"/>). При каждом
/// обновлении драйвера Windows кладёт новую версию рядом со старой и сама их не убирает — за годы там
/// накапливаются гигабайты, особенно от видеокарты и принтеров.
///
/// Предлагаем удалить ТОЛЬКО те версии, у которых есть более новая и которыми сейчас не пользуется ни одно
/// устройство. Действующий драйвер не трогаем никогда.
/// </summary>
public sealed class DriverStoreScanner : IScanner
{
    private readonly IDriverStoreProbe _probe;

    public DriverStoreScanner(IDriverStoreProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    public ScanGroup Group => ScanGroup.Drivers;

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var packages = await _probe.ReadPackagesAsync(cancellationToken).ConfigureAwait(false);
        var active = await _probe.ReadActivePackagesAsync(cancellationToken).ConfigureAwait(false);

        var obsolete = DriverPackageParser.FindObsolete(packages, active);
        var findings = new List<Finding>();

        foreach (var group in obsolete.GroupBy(p => p.OriginalName, StringComparer.OrdinalIgnoreCase))
        {
            var items = group.ToList();
            var names = string.Join(", ", items.Select(p => p.PublishedName));

            findings.Add(new Finding
            {
                Id = "driverstore-" + group.Key,
                Group = ScanGroup.Drivers,
                Severity = Severity.Info,
                Title = $"Старые версии драйвера «{group.Key}»: {items.Count}",
                Detail = string.Join(" · ", items.Select(DriverPackageParser.Describe)),
                Explain = "Когда драйвер обновляется, Windows не выбрасывает старую версию, а складывает её в " +
                          "запас — и делает так каждый раз. Здесь скопились старые версии одного драйвера, которыми " +
                          "сейчас не пользуется ни одно устройство; действующий драйвер мы не трогаем. Удаление " +
                          "освобождает место (у видеокарт это часто гигабайты) и ни на что не влияет. " +
                          "Важно: удаление старой версии необратимо — вернуть её можно будет только скачав заново " +
                          "с сайта производителя. Перед удалением Aegis создаёт точку восстановления Windows.",
                Data = new Dictionary<string, string>
                {
                    [FindingDataKeys.Kind] = FindingKinds.DriverPackageDelete,
                    [FindingDataKeys.Items] = names,
                    [FindingDataKeys.Section] = "Старые версии драйверов",
                },
            });
        }

        return new ScanResult { Group = ScanGroup.Drivers, Findings = findings };
    }
}
