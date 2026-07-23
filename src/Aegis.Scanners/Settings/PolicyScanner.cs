using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Internal;
using Aegis.Scanners.Probing;

namespace Aegis.Scanners.Settings;

/// <summary>
/// Сканер чужих ограничений Windows (группа <see cref="ScanGroup.Settings"/>): находит настройки-запреты,
/// оставленные другими «оптимизаторами», активаторами и вирусами — отключённый Защитник, заблокированный
/// диспетчер задач, запрещённые обновления и восстановление системы.
///
/// Человек в такой ситуации видит «Windows сломалась и ничего не помогает»: обычные переключатели не
/// действуют, потому что запрет сильнее их. Починка снимает запрет — обратимо (прежнее значение в бэкапе).
/// </summary>
public sealed class PolicyScanner : IScanner
{
    private readonly IPolicyProbe _probe;

    public PolicyScanner(IPolicyProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    public ScanGroup Group => ScanGroup.Settings;

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var restrictions = await _probe.ReadAsync(cancellationToken).ConfigureAwait(false);
        var findings = new List<Finding>();

        foreach (var restriction in restrictions)
        {
            var rule = PolicyCatalog.Rules.FirstOrDefault(r =>
                string.Equals(r.Hive, restriction.Hive, StringComparison.OrdinalIgnoreCase)
                && string.Equals(r.SubKey, restriction.SubKey, StringComparison.OrdinalIgnoreCase)
                && string.Equals(r.ValueName, restriction.ValueName, StringComparison.OrdinalIgnoreCase));

            if (rule is null)
            {
                continue; // пробник вернул то, чего нет в каталоге — показывать нечего
            }

            findings.Add(new Finding
            {
                Id = $"policy-{restriction.Hive}-{restriction.ValueName}",
                Group = ScanGroup.Settings,
                Severity = rule.Severity,
                Title = rule.Title,
                Detail = "ограничение осталось от другой программы",
                Explain = rule.Explain + " Кнопка «Снять запрет» уберёт только это ограничение; если после " +
                          "исправления что-то понадобится вернуть — нажми «Вернуть» в разделе «Бэкапы».",
                Data = new Dictionary<string, string>
                {
                    [FindingDataKeys.Kind] = FindingKinds.PolicyClear,
                    [FindingDataKeys.Hive] = restriction.Hive,
                    [FindingDataKeys.Subkey] = restriction.SubKey,
                    [FindingDataKeys.Name] = restriction.ValueName,
                    [FindingDataKeys.Section] = "Чужие ограничения Windows",
                },
            });
        }

        return new ScanResult { Group = ScanGroup.Settings, Findings = findings };
    }
}
