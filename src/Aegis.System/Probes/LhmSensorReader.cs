using Aegis.Scanners.Probing;
using LibreHardwareMonitor.Hardware;

namespace Aegis.System.Probes;

/// <summary>
/// Достоверные датчики железа через LibreHardwareMonitor (тот же движок, что у HWMonitor/AIDA): читает
/// температуру ядер процессора, видеокарты, обороты вентиляторов и частоты напрямую с чипов. Открывается
/// один раз (лениво), подгружает свой драйвер (нужны права администратора). Не потокобезопасен — доступ
/// под блокировкой. Если библиотека не запустилась (не Windows / нет прав / нет драйвера) — тихо отдаёт
/// пустые показания, и вызывающий код падает обратно на стандартные датчики Windows.
/// </summary>
public sealed class LhmSensorReader : IHardwareSensorReader, IDisposable
{
    private readonly object _gate = new();
    private Computer? _computer;
    private bool _initFailed;
    private bool _disposed;

    public HardwareReadings Read()
    {
        lock (_gate)
        {
            if (_disposed || !TryEnsureOpen())
            {
                return HardwareReadings.Empty;
            }

            try
            {
                var acc = new Accumulator();
                foreach (var hardware in _computer!.Hardware)
                {
                    hardware.Update();
                    Collect(hardware, acc);
                    foreach (var sub in hardware.SubHardware)
                    {
                        sub.Update();
                        Collect(sub, acc);
                    }
                }

                return new HardwareReadings
                {
                    // Пакет процессора точнее «средней по больнице»; если его нет — берём самое горячее ядро.
                    CpuTempCelsius = acc.CpuPackage ?? acc.CpuMaxCore,
                    GpuTempCelsius = acc.GpuCore,
                    MaxFanRpm = acc.FanPresent ? acc.MaxFan ?? 0 : null,
                    FanPresent = acc.FanPresent,
                    CpuLoadPercent = acc.CpuLoad,
                    MaxCpuClockMhz = acc.MaxCpuClock,
                    GpuLoadPercent = acc.GpuLoad,
                    GpuMemoryUsedMb = acc.GpuMemUsed,
                    GpuMemoryTotalMb = acc.GpuMemTotal,
                    CpuPowerWatts = acc.CpuPower,
                    GpuPowerWatts = acc.GpuPower,
                    StorageMaxTempCelsius = acc.StorageTemp,
                    CpuName = acc.CpuName,
                    GpuName = acc.GpuName,
                };
            }
            catch (Exception)
            {
                return HardwareReadings.Empty;
            }
        }
    }

    private bool TryEnsureOpen()
    {
        if (_computer is not null)
        {
            return true;
        }

        if (_initFailed || !OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            var computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMotherboardEnabled = true, // вентиляторы обычно здесь (SuperIO subhardware)
                IsControllerEnabled = true,  // встроенные контроллеры вентиляторов
                IsStorageEnabled = true,     // температура SSD/HDD
            };
            computer.Open();
            _computer = computer;
            return true;
        }
        catch (Exception)
        {
            _initFailed = true; // драйвер/права/не поддерживается — больше не пытаемся
            return false;
        }
    }

    private static void Collect(IHardware hardware, Accumulator acc)
    {
        var isCpu = hardware.HardwareType == HardwareType.Cpu;
        var isGpu = hardware.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel;
        var isStorage = hardware.HardwareType == HardwareType.Storage;

        // Модель железа (имя от LHM) — для подписи под заголовком плитки «Здоровья».
        if (isCpu && string.IsNullOrEmpty(acc.CpuName))
        {
            acc.CpuName = hardware.Name;
        }
        else if (isGpu && string.IsNullOrEmpty(acc.GpuName))
        {
            acc.GpuName = hardware.Name;
        }

        foreach (var sensor in hardware.Sensors)
        {
            if (sensor.Value is not float raw || float.IsNaN(raw))
            {
                continue;
            }

            var value = (int)Math.Round(raw);
            switch (sensor.SensorType)
            {
                case SensorType.Temperature when isCpu && IsPackage(sensor.Name):
                    acc.CpuPackage = value;
                    break;

                case SensorType.Temperature when isCpu:
                    acc.CpuMaxCore = Math.Max(acc.CpuMaxCore ?? int.MinValue, value);
                    break;

                case SensorType.Temperature when isGpu && sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase):
                    acc.GpuCore = value;
                    break;

                case SensorType.Temperature when isGpu:
                    acc.GpuCore ??= value;
                    break;

                case SensorType.Temperature when isStorage:
                    acc.StorageTemp = Math.Max(acc.StorageTemp ?? 0, value);
                    break;

                case SensorType.Fan:
                    acc.FanPresent = true; // сам факт наличия датчика — чтобы отличить «стоит» (0) от «нет датчика»
                    acc.MaxFan = Math.Max(acc.MaxFan ?? 0, value);
                    break;

                case SensorType.Load when isCpu && sensor.Name.Contains("Total", StringComparison.OrdinalIgnoreCase):
                    acc.CpuLoad = value;
                    break;

                case SensorType.Load when isGpu && sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase):
                    acc.GpuLoad = value;
                    break;

                case SensorType.Clock when isCpu && sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase):
                    acc.MaxCpuClock = Math.Max(acc.MaxCpuClock ?? 0, value);
                    break;

                case SensorType.SmallData when isGpu && sensor.Name.Contains("Memory Used", StringComparison.OrdinalIgnoreCase):
                    acc.GpuMemUsed = value;
                    break;

                case SensorType.SmallData when isGpu && sensor.Name.Contains("Memory Total", StringComparison.OrdinalIgnoreCase):
                    acc.GpuMemTotal = value;
                    break;

                case SensorType.Power when isCpu && IsPackage(sensor.Name):
                    acc.CpuPower = value;
                    break;

                case SensorType.Power when isGpu && sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase):
                    acc.GpuPower = value;
                    break;

                case SensorType.Power when isGpu:
                    acc.GpuPower ??= value;
                    break;
            }
        }
    }

    private static bool IsPackage(string name) =>
        name.Contains("Package", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Tctl", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Tdie", StringComparison.OrdinalIgnoreCase);

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                _computer?.Close();
            }
            catch (Exception)
            {
                // Закрытие best-effort.
            }

            _computer = null;
        }
    }

    private sealed class Accumulator
    {
        public string? CpuName { get; set; }
        public string? GpuName { get; set; }
        public int? CpuPackage { get; set; }
        public int? CpuMaxCore { get; set; }
        public int? GpuCore { get; set; }
        public int? MaxFan { get; set; }
        public bool FanPresent { get; set; }
        public int? CpuLoad { get; set; }
        public int? MaxCpuClock { get; set; }
        public int? GpuLoad { get; set; }
        public int? GpuMemUsed { get; set; }
        public int? GpuMemTotal { get; set; }
        public int? CpuPower { get; set; }
        public int? GpuPower { get; set; }
        public int? StorageTemp { get; set; }
    }
}
