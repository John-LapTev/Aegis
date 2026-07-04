using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Probing;

namespace Aegis.Scanners.SystemInfo;

/// <summary>
/// Температуры процессора и видеокарты (группа <see cref="ScanGroup.Health"/>): шкала 🟢/🟡/🔴 с вердиктом
/// простыми словами. Перегрев → компьютер тормозит (троттлинг), шумит и быстрее изнашивается. Только показывает.
/// Пороги для видеокарты чуть ниже, чем для процессора (CPU нормально терпит выше под нагрузкой).
/// </summary>
public sealed class TemperatureScanner : IScanner
{
    private readonly ITemperatureProbe _probe;

    public TemperatureScanner(ITemperatureProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    public ScanGroup Group => ScanGroup.Health;

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var readings = await _probe.ReadAsync(cancellationToken).ConfigureAwait(false);

        var findings = readings.Count == 0
            ? [NoSensorsFinding()]
            : readings.Select((reading, index) => CreateFinding(reading, index)).ToList();

        return new ScanResult { Group = ScanGroup.Health, Findings = findings };
    }

    private static Finding CreateFinding(TemperatureReading reading, int index)
    {
        var (severity, explain) = Classify(reading.Component, reading.Celsius);
        var isGpu = reading.Component.Contains("видео", StringComparison.OrdinalIgnoreCase)
                    || reading.Component.Contains("карт", StringComparison.OrdinalIgnoreCase);

        // Подпись «норма» под плиткой — чтобы человек сравнил свою цифру с эталоном (запрос Ивана).
        var data = new Dictionary<string, string>
        {
            ["hint"] = isGpu
                ? "норма: в покое до 55°C, под нагрузкой до 80°C"
                : "норма: в покое до 60°C, под нагрузкой до 85°C",
        };

        // Модель процессора/видеокарты под заголовком плитки (запрос Ивана 1119).
        if (!string.IsNullOrWhiteSpace(reading.Model))
        {
            data["model"] = reading.Model;
        }

        return new Finding
        {
            Id = $"temp-{index}-{reading.Component}", // индекс — чтобы две одинаково названные детали не давали один Id
            Group = ScanGroup.Health,
            Severity = severity,
            Title = $"Температура: {reading.Component}",
            Detail = reading.Celsius is int c ? $"{c} °C" : "датчик недоступен",
            Explain = explain,
            Data = data,
        };
    }

    private static Finding NoSensorsFinding() => new()
    {
        Id = "temp-none",
        Group = ScanGroup.Health,
        Severity = Severity.Info,
        Title = "Температуры узнать не удалось",
        Detail = "датчики недоступны",
        Explain = "Программа не смогла прочитать температуру процессора и видеокарты — некоторые компьютеры " +
                  "и ноутбуки не отдают эти данные. Это не страшно и не значит, что есть перегрев.",
    };

    /// <summary>Пороги и вердикт по температуре. Чистая логика — проверяется тестами через результат скана.</summary>
    private static (Severity Severity, string Explain) Classify(string component, int? celsius)
    {
        if (celsius is not int t)
        {
            return (Severity.Info, "Датчик этого компонента недоступен — измерить температуру не получилось. Это не страшно.");
        }

        var isGpu = component.Contains("видео", StringComparison.OrdinalIgnoreCase)
                    || component.Contains("карт", StringComparison.OrdinalIgnoreCase);
        var (warn, danger) = isGpu ? (80, 90) : (85, 95);

        if (t >= danger)
        {
            return (Severity.Danger,
                $"Сейчас {t} °C — это горячо. При такой температуре компьютер сбрасывает скорость (тормозит), сильно " +
                "шумит и быстрее изнашивается. Почисти вентиляторы от пыли, проверь, не закрыты ли отверстия охлаждения, " +
                "а ноутбук по возможности поставь на подставку, чтобы снизу был воздух.");
        }

        if (t >= warn)
        {
            return (Severity.Warning,
                $"Сейчас {t} °C — тепловато, но пока терпимо. Под нагрузкой (игры, рендер) стоит последить: если " +
                "температура регулярно выше — почисти компьютер от пыли и улучши охлаждение.");
        }

        return (Severity.Ok, $"Сейчас {t} °C — нормальная температура, беспокоиться не о чем.");
    }
}
