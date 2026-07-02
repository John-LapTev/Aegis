using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Probing;

namespace Aegis.Scanners.SystemInfo;

/// <summary>
/// Здоровье батареи (группа <see cref="ScanGroup.Health"/>): износ в % + вердикт простыми словами
/// (🟢/🟡/🔴). На стационарном ПК батареи нет — пункт не показываем. Только информация, без действий.
/// </summary>
public sealed class BatteryScanner : IScanner
{
    private readonly IBatteryProbe _probe;

    public BatteryScanner(IBatteryProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    public ScanGroup Group => ScanGroup.Health;

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _probe.ReadAsync(cancellationToken).ConfigureAwait(false);
        var findings = new List<Finding>();

        if (!snapshot.HasBattery)
        {
            // Десктоп — батареи нет, это нормально; пункт не нужен.
            return new ScanResult { Group = ScanGroup.Health, Findings = findings };
        }

        if (snapshot.WearPercent is int wear)
        {
            var (severity, verdict) = wear switch
            {
                < 20 => (Severity.Ok, "батарея в хорошем состоянии, почти как новая"),
                < 40 => (Severity.Warning, "батарея заметно изношена — держит меньше, чем раньше"),
                _ => (Severity.Danger, "батарея сильно изношена — держит мало, со временем может потребоваться замена"),
            };

            findings.Add(new Finding
            {
                Id = "health-battery",
                Group = ScanGroup.Health,
                Severity = severity,
                Title = "Здоровье батареи",
                // Износ % показываем плашкой в правом-верхнем углу карточки (правка 762) — в заголовок не дублируем.
                Data = new Dictionary<string, string> { ["wear"] = wear.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                Explain = LiveText(snapshot) +
                          $"Износ батареи — {wear}%: {verdict}. Износ показывает, насколько просела ёмкость " +
                          "относительно заводской: чем больше процент, тем меньше держит заряд. Со временем это " +
                          "естественно; выше 40% — стоит задуматься о замене. Делать ничего не нужно — это информация." +
                          CyclesText(snapshot.CycleCount),
            });
        }
        else
        {
            findings.Add(new Finding
            {
                Id = "health-battery-unknown",
                Group = ScanGroup.Health,
                Severity = Severity.Ok,
                Title = "Батарея обнаружена",
                Detail = "износ измерить не удалось",
                Explain = LiveText(snapshot) +
                          "Батарея есть, но Windows не отдала её ёмкость, поэтому посчитать износ не получилось. " +
                          "Это не страшно — на некоторых ноутбуках так бывает.",
            });
        }

        return new ScanResult { Group = ScanGroup.Health, Findings = findings };
    }

    /// <summary>Хвост про циклы заряда — добавляем, только если датчик их отдал.</summary>
    private static string CyclesText(int? cycleCount) => cycleCount is int cycles
        ? $" Циклов заряда: примерно {cycles} (один цикл — это полная зарядка-разрядка; у ноутбучных батарей " +
          "запас обычно 300–800 циклов)."
        : string.Empty;

    /// <summary>Живое состояние батареи: текущий заряд, заряжается/от батареи, сколько ещё продержится.</summary>
    private static string LiveText(BatterySnapshot snapshot)
    {
        if (snapshot.ChargePercent is not int charge)
        {
            return string.Empty;
        }

        var state = snapshot.IsCharging switch
        {
            true => " Сейчас заряжается от сети.",
            false => " Сейчас работает от батареи.",
            _ => string.Empty,
        };

        var remaining = snapshot is { RemainingMinutes: int minutes, IsCharging: false }
            ? $" Хватит примерно на {HumanMinutes(minutes)}."
            : string.Empty;

        return $"Текущий заряд — {charge}%.{state}{remaining} ";
    }

    /// <summary>Минуты простыми словами: «2 ч 15 мин» / «45 мин».</summary>
    private static string HumanMinutes(int minutes)
    {
        if (minutes >= 60)
        {
            var hours = minutes / 60;
            var rest = minutes % 60;
            return rest > 0 ? $"{hours} ч {rest} мин" : $"{hours} ч";
        }

        return $"{minutes} мин";
    }
}
