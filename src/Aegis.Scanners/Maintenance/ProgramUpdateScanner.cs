using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Probing;

namespace Aegis.Scanners.Maintenance;

/// <summary>
/// Обновления установленных программ (группа <see cref="ScanGroup.System"/>). Старые версии браузеров,
/// проигрывателей и архиваторов — самый частый способ поймать заразу: уязвимость известна всем, а заплатка
/// не поставлена. Обновление идёт через встроенный установщик Windows, с официальных источников программ.
/// </summary>
public sealed class ProgramUpdateScanner : IScanner
{
    /// <summary>Сколько программ перечислять в тексте, чтобы он оставался читаемым.</summary>
    private const int NamesToShow = 6;

    private readonly IProgramUpdateProbe _probe;

    public ProgramUpdateScanner(IProgramUpdateProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    public ScanGroup Group => ScanGroup.System;

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var upgrades = await _probe.ReadAvailableAsync(cancellationToken).ConfigureAwait(false);
        if (upgrades.Count == 0)
        {
            return new ScanResult { Group = ScanGroup.System, Findings = [] };
        }

        var names = upgrades.Take(NamesToShow).Select(u => u.Name).ToList();
        var tail = upgrades.Count > NamesToShow ? $" и ещё {upgrades.Count - NamesToShow}" : string.Empty;

        var finding = new Finding
        {
            Id = "programs-updates-available",
            Group = ScanGroup.System,
            Severity = Severity.Warning,
            Title = $"Для программ есть обновления: {upgrades.Count}",
            Detail = string.Join(", ", names) + tail,
            Explain = "У этих программ вышли новые версии. Обновления — это не только новые возможности: в них " +
                      "закрывают дыры, через которые заражают компьютер (особенно у браузеров, архиваторов и " +
                      "проигрывателей). Кнопка обновит их через встроенный установщик Windows — программы " +
                      "скачиваются с официальных источников, а не с посторонних сайтов. " +
                      "Обновление может занять несколько минут; если какая-то программа сейчас открыта, её " +
                      "лучше закрыть заранее. Отката у этой операции нет: вернуть старую версию можно только " +
                      "установив её вручную.",
            Data = new Dictionary<string, string>
            {
                [FindingDataKeys.Kind] = FindingKinds.ProgramUpgradeAll,
                [FindingDataKeys.Items] = string.Join("|", upgrades.Select(u => $"{u.Name} {u.CurrentVersion} → {u.AvailableVersion}")),
                [FindingDataKeys.Section] = "Обновления программ",
            },
        };

        return new ScanResult { Group = ScanGroup.System, Findings = [finding] };
    }
}
