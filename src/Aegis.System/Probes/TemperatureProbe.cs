using System.Globalization;
using System.Management;
using Aegis.Scanners.Probing;
using Aegis.System.Internal;

namespace Aegis.System.Probes;

/// <summary>
/// Реальный пробник температур: процессор — через ACPI-датчик (WMI <c>MSAcpi_ThermalZoneTemperature</c>,
/// значение в десятых долях кельвина), видеокарта NVIDIA — через <c>nvidia-smi</c>. Best-effort: если
/// железо/драйвер не отдаёт данные — компонент просто пропускается. Только читает.
/// </summary>
public sealed class TemperatureProbe : ITemperatureProbe
{
    public async Task<IReadOnlyList<TemperatureReading>> ReadAsync(CancellationToken cancellationToken = default)
    {
        var readings = new List<TemperatureReading>();

        var cpu = ReadCpuCelsius();
        if (cpu is not null)
        {
            readings.Add(new TemperatureReading { Component = "Процессор", Celsius = cpu });
        }

        var gpu = await ReadNvidiaGpuCelsiusAsync(cancellationToken).ConfigureAwait(false);
        if (gpu is not null)
        {
            readings.Add(new TemperatureReading { Component = "Видеокарта", Celsius = gpu });
        }

        return readings;
    }

    private static int? ReadCpuCelsius()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\WMI", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");

            int? hottest = null;
            foreach (var item in searcher.Get())
            {
                using var zone = (ManagementObject)item;
                if (zone["CurrentTemperature"] is null)
                {
                    continue;
                }

                var tenthsKelvin = Convert.ToInt32(zone["CurrentTemperature"], CultureInfo.InvariantCulture);
                var celsius = (int)Math.Round(tenthsKelvin / 10.0 - 273.15);
                if (celsius is > 0 and < 130 && (hottest is null || celsius > hottest))
                {
                    hottest = celsius;
                }
            }

            return hottest;
        }
        catch (Exception)
        {
            // ACPI-датчик недоступен (типично для многих ноутбуков) — нет данных.
            return null;
        }
    }

    private static async Task<int?> ReadNvidiaGpuCelsiusAsync(CancellationToken cancellationToken)
    {
        var output = await ProcessRunner
            .RunForOutputAsync("nvidia-smi", "--query-gpu=temperature.gpu --format=csv,noheader,nounits", cancellationToken)
            .ConfigureAwait(false);

        foreach (var line in output.Split('\n'))
        {
            if (int.TryParse(line.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var celsius)
                && celsius is > 0 and < 130)
            {
                return celsius;
            }
        }

        return null;
    }
}
