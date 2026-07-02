using Aegis.Scanners.Probing;

namespace Aegis.System.Probes;

/// <summary>
/// Температуры процессора и видеокарты через LibreHardwareMonitor (достоверные температуры ядер, а не
/// «средняя по плате» из ACPI). Чего LHM не отдал (не запустился / нет датчика) — добираем из стандартного
/// источника Windows (<see cref="TemperatureProbe"/>: ACPI + nvidia-smi), чтобы плитка не оставалась пустой.
/// </summary>
public sealed class LhmTemperatureProbe : ITemperatureProbe
{
    private readonly IHardwareSensorReader _sensors;
    private readonly ITemperatureProbe _fallback;

    public LhmTemperatureProbe(IHardwareSensorReader sensors, ITemperatureProbe fallback)
    {
        ArgumentNullException.ThrowIfNull(sensors);
        ArgumentNullException.ThrowIfNull(fallback);
        _sensors = sensors;
        _fallback = fallback;
    }

    public async Task<IReadOnlyList<TemperatureReading>> ReadAsync(CancellationToken cancellationToken = default)
    {
        var lhm = _sensors.Read();
        int? cpu = lhm.CpuTempCelsius;
        int? gpu = lhm.GpuTempCelsius;

        // Чего не дал LHM — берём из стандартного датчика Windows (ACPI/nvidia-smi).
        if (cpu is null || gpu is null)
        {
            var fallback = await _fallback.ReadAsync(cancellationToken).ConfigureAwait(false);
            cpu ??= fallback.FirstOrDefault(r => IsCpu(r.Component))?.Celsius;
            gpu ??= fallback.FirstOrDefault(r => IsGpu(r.Component))?.Celsius;
        }

        var readings = new List<TemperatureReading>();
        if (cpu is not null)
        {
            readings.Add(new TemperatureReading { Component = "Процессор", Celsius = cpu });
        }

        if (gpu is not null)
        {
            readings.Add(new TemperatureReading { Component = "Видеокарта", Celsius = gpu });
        }

        return readings;
    }

    private static bool IsCpu(string component) =>
        component.Contains("процессор", StringComparison.OrdinalIgnoreCase);

    private static bool IsGpu(string component) =>
        component.Contains("видео", StringComparison.OrdinalIgnoreCase)
        || component.Contains("карт", StringComparison.OrdinalIgnoreCase);
}
