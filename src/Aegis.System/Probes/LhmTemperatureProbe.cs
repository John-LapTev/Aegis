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
        // Температура процессора — ТОЛЬКО из LHM (настоящие ядра). ACPI-термозону как запасной вариант НЕ
        // используем: это температура платы/окружения (часто ~28° и не растёт под нагрузкой), а не ядер CPU —
        // показывать её как «температуру процессора» = вводить в заблуждение (правка Ивана). Нет LHM → честно
        // «датчик недоступен», а не фейковое число.
        int? cpu = lhm.CpuTempCelsius;
        int? gpu = lhm.GpuTempCelsius;
        string? gpuModel = lhm.GpuName;

        // Видеокарту можно добрать из nvidia-smi — он даёт НАСТОЯЩУЮ температуру ядра GPU.
        if (gpu is null)
        {
            var fallback = await _fallback.ReadAsync(cancellationToken).ConfigureAwait(false);
            var gpuReading = fallback.FirstOrDefault(r => IsGpu(r.Component));
            gpu = gpuReading?.Celsius;
            gpuModel ??= gpuReading?.Model;
        }

        var readings = new List<TemperatureReading>();
        if (cpu is not null)
        {
            readings.Add(new TemperatureReading { Component = "Процессор", Celsius = cpu, Model = lhm.CpuName });
        }

        if (gpu is not null)
        {
            readings.Add(new TemperatureReading { Component = "Видеокарта", Celsius = gpu, Model = gpuModel });
        }

        return readings;
    }

    private static bool IsGpu(string component) =>
        component.Contains("видео", StringComparison.OrdinalIgnoreCase)
        || component.Contains("карт", StringComparison.OrdinalIgnoreCase);
}
