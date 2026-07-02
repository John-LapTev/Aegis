using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;
using Aegis.Scanners.Probing;

namespace Aegis.System.Probes;

/// <summary>
/// Реальный пробник быстрых показателей здоровья: оперативная память — через <c>GlobalMemoryStatusEx</c>
/// (kernel32, без WMI — быстро и надёжно), время работы — через системный счётчик, загрузка процессора —
/// через WMI <c>Win32_Processor.LoadPercentage</c>, вентиляторы — через <c>Win32_Fan</c> (best-effort, у
/// большинства машин датчик не отдаётся → null). Только читает.
/// </summary>
public sealed partial class SystemVitalsProbe : ISystemVitalsProbe
{
    private readonly IHardwareSensorReader? _sensors;

    /// <summary><paramref name="sensors"/> — достоверные датчики (LHM): дают реальные обороты вентиляторов
    /// и загрузку CPU. Если недоступны — падаем на WMI. null — работаем только на WMI (для тестов).</summary>
    public SystemVitalsProbe(IHardwareSensorReader? sensors = null)
    {
        _sensors = sensors;
    }

    public Task<SystemVitals> ReadAsync(CancellationToken cancellationToken = default)
    {
        var (total, available) = ReadMemory();
        var hardware = _sensors?.Read() ?? HardwareReadings.Empty;

        var vitals = new SystemVitals
        {
            RamTotalBytes = total,
            RamAvailableBytes = available,
            UptimeSeconds = Math.Max(0, Environment.TickCount64 / 1000),
            // Из достоверных датчиков (LHM), иначе — стандартные WMI-датчики Windows.
            CpuLoadPercent = hardware.CpuLoadPercent ?? ReadCpuLoadPercent(),
            FanRpm = hardware.MaxFanRpm ?? ReadFanRpm(),
            FanPresent = hardware.FanPresent,
            GpuLoadPercent = hardware.GpuLoadPercent,
            GpuMemoryUsedMb = hardware.GpuMemoryUsedMb,
            GpuMemoryTotalMb = hardware.GpuMemoryTotalMb,
            CpuPowerWatts = hardware.CpuPowerWatts,
            GpuPowerWatts = hardware.GpuPowerWatts,
            CpuClockMhz = hardware.MaxCpuClockMhz,
        };

        return Task.FromResult(vitals);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    private static (long Total, long Available) ReadMemory()
    {
        try
        {
            var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
            if (GlobalMemoryStatusEx(ref status))
            {
                return ((long)status.TotalPhys, (long)status.AvailPhys);
            }
        }
        catch (Exception)
        {
            // Не удалось прочитать память — вернём 0 (в сканере такой пункт просто не покажется).
        }

        return (0, 0);
    }

    private static int? ReadCpuLoadPercent()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT LoadPercentage FROM Win32_Processor");
            var loads = new List<int>();
            foreach (var item in searcher.Get())
            {
                using var processor = (ManagementObject)item;
                if (processor["LoadPercentage"] is not null)
                {
                    loads.Add(Convert.ToInt32(processor["LoadPercentage"], CultureInfo.InvariantCulture));
                }
            }

            return loads.Count > 0 ? (int)Math.Round(loads.Average()) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static int? ReadFanRpm()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT DesiredSpeed FROM Win32_Fan");
            int? best = null;
            foreach (var item in searcher.Get())
            {
                using var fan = (ManagementObject)item;
                if (fan["DesiredSpeed"] is null)
                {
                    continue;
                }

                var rpm = Convert.ToInt32(fan["DesiredSpeed"], CultureInfo.InvariantCulture);
                if (rpm > 0 && (best is null || rpm > best))
                {
                    best = rpm;
                }
            }

            return best;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
