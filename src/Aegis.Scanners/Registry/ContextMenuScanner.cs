using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Probing;

namespace Aegis.Scanners.Registry;

/// <summary>
/// Пункты контекстного меню (правый клик) от удалённых программ — группа <see cref="ScanGroup.Registry"/>.
/// Каждый такой пункт проводник пытается загрузить при открытии меню, поэтому правый клик может «думать»
/// секундами. Отключение обратимое: пункт не удаляется, а помечается отключённым штатным способом Windows.
/// </summary>
public sealed class ContextMenuScanner : IScanner
{
    private readonly IContextMenuProbe _probe;

    public ContextMenuScanner(IContextMenuProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    public ScanGroup Group => ScanGroup.Registry;

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var broken = await _probe.ReadBrokenAsync(cancellationToken).ConfigureAwait(false);
        var findings = new List<Finding>();

        foreach (var entry in broken)
        {
            var target = string.IsNullOrWhiteSpace(entry.Target) ? string.Empty : $" Ведёт в «{entry.Target}».";

            findings.Add(new Finding
            {
                Id = $"contextmenu-{entry.Hive}-{entry.SubKey}-{entry.ValueName}",
                Group = ScanGroup.Registry,
                Severity = Severity.Info,
                Title = $"Пункт правого клика «{entry.Name}» ведёт в никуда",
                Detail = $"появляется {entry.Scope}",
                Explain = "Это пункт меню, которое открывается по правому клику мышью. Программа, которая его " +
                          $"добавила, удалена, а пункт остался.{target} Проводник каждый раз пытается его загрузить — " +
                          "из-за таких «мёртвых» пунктов правый клик открывается с задержкой. " +
                          "Отключение обратимое: пункт не удаляется, а помечается выключенным — кнопкой «Вернуть» " +
                          "в разделе «Бэкапы» его можно восстановить.",
                Data = new Dictionary<string, string>
                {
                    [FindingDataKeys.Kind] = FindingKinds.ContextMenuDisable,
                    [FindingDataKeys.Hive] = entry.Hive,
                    [FindingDataKeys.Subkey] = entry.SubKey,
                    [FindingDataKeys.Name] = entry.ValueName,
                    [FindingDataKeys.Section] = "Контекстное меню",
                },
            });
        }

        return new ScanResult { Group = ScanGroup.Registry, Findings = findings };
    }
}
