using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Probing;

namespace Aegis.Scanners.SystemInfo;

/// <summary>
/// Устройства с ошибками (группа <see cref="ScanGroup.Health"/>): плитка «Устройства» — всё ли железо и
/// драйверы работают. Если Windows жалуется на какое-то устройство (код ошибки в Диспетчере устройств) —
/// показываем понятным списком и советуем переустановить/обновить драйвер. Только показывает.
/// </summary>
public sealed class DeviceErrorScanner : IScanner
{
    private readonly IDeviceErrorProbe _probe;

    public DeviceErrorScanner(IDeviceErrorProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    public ScanGroup Group => ScanGroup.Health;

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var problems = await _probe.ReadAsync(cancellationToken).ConfigureAwait(false);

        var finding = problems.Count == 0
            ? new Finding
            {
                Id = "health-devices",
                Group = ScanGroup.Health,
                Severity = Severity.Ok,
                Title = "Устройства",
                Detail = "все работают",
                Explain = "Всё оборудование компьютера (звук, сеть, видео, порты и т.д.) работает исправно — " +
                          "Windows ни на что не жалуется.",
                Data = new Dictionary<string, string> { [FindingDataKeys.HealthIcon] = "plug", ["metric"] = "все ОК", ["metricLabel"] = "" },
            }
            : new Finding
            {
                Id = "health-devices",
                Group = ScanGroup.Health,
                Severity = Severity.Warning,
                Title = "Устройства с ошибками",
                Detail = string.Join(", ", problems.Take(4)) + (problems.Count > 4 ? $" и ещё {problems.Count - 4}" : string.Empty),
                Explain = $"Windows сообщает, что {problems.Count} устройств(о) работает неправильно: " +
                          $"{string.Join(", ", problems.Take(8))}. Обычно помогает обновить или переустановить драйвер " +
                          "этого устройства (загляни во вкладку «Драйверы»), а иногда — просто перезагрузка. Если " +
                          "устройством ты не пользуешься, можно не обращать внимания.",
                Data = new Dictionary<string, string>
                {
                    [FindingDataKeys.HealthIcon] = "plug",
                    ["metric"] = problems.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["metricLabel"] = "с ошибкой",
                },
            };

        return new ScanResult { Group = ScanGroup.Health, Findings = [finding] };
    }
}
