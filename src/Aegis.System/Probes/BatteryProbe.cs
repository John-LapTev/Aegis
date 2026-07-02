using System.Management;
using Aegis.Scanners.Probing;

namespace Aegis.System.Probes;

/// <summary>
/// Реальный пробник батареи: заводская ёмкость (BatteryStaticData.DesignedCapacity) и текущая полная
/// (BatteryFullChargedCapacity.FullChargedCapacity) из WMI root\WMI → износ. Только читает.
/// </summary>
public sealed class BatteryProbe : IBatteryProbe
{
    public Task<BatterySnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        var designed = QueryFirstInt(@"root\WMI", "SELECT DesignedCapacity FROM BatteryStaticData", "DesignedCapacity");
        var full = QueryFirstInt(@"root\WMI", "SELECT FullChargedCapacity FROM BatteryFullChargedCapacity", "FullChargedCapacity");
        // Циклы заряда — best-effort: многие ноутбуки/контроллеры отдают 0 (не поддерживается) → тогда null.
        var cycles = QueryFirstInt(@"root\WMI", "SELECT CycleCount FROM BatteryCycleCount", "CycleCount");
        if (cycles is <= 0)
        {
            cycles = null;
        }

        var live = ReadLiveState();

        if (designed is > 0 && full is > 0)
        {
            var wear = (int)Math.Round((designed.Value - full.Value) / (double)designed.Value * 100);
            return Task.FromResult(new BatterySnapshot
            {
                HasBattery = true,
                WearPercent = Math.Clamp(wear, 0, 100),
                DesignedCapacity = designed,
                FullChargedCapacity = full,
                CycleCount = cycles,
                ChargePercent = live.Charge,
                IsCharging = live.IsCharging,
                RemainingMinutes = live.RemainingMinutes,
            });
        }

        // Ёмкость не отдалась — но батарея может быть (тогда покажем без процента износа).
        var hasBattery = live.Status is not null;
        return Task.FromResult(new BatterySnapshot
        {
            HasBattery = hasBattery,
            ChargePercent = live.Charge,
            IsCharging = live.IsCharging,
            RemainingMinutes = live.RemainingMinutes,
        });
    }

    /// <summary>Текущее состояние батареи из Win32_Battery: заряд %, статус (заряжается/от батареи), остаток минут.</summary>
    private static (int? Status, int? Charge, bool? IsCharging, int? RemainingMinutes) ReadLiveState()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                new ManagementScope(@"root\CIMV2"),
                new ObjectQuery("SELECT BatteryStatus, EstimatedChargeRemaining, EstimatedRunTime FROM Win32_Battery"));

            foreach (var item in searcher.Get())
            {
                using var battery = (ManagementObject)item;
                var status = ToInt(battery["BatteryStatus"]);
                var charge = ToInt(battery["EstimatedChargeRemaining"]);
                var runtime = ToInt(battery["EstimatedRunTime"]);

                // BatteryStatus: 2 = питание от сети (заряжается/заряжена), 1 = разрядка от батареи.
                bool? isCharging = status is null ? null : status != 1;

                // EstimatedRunTime отдаёт 71582788 (0xFFFFFFFF/60), когда неизвестно/на зарядке — отбрасываем.
                int? remaining = runtime is > 0 and < 6000 ? runtime : null;

                return (status, charge is >= 0 and <= 100 ? charge : null, isCharging, remaining);
            }
        }
        catch (Exception)
        {
            // Нет WMI / не Windows — состояние неизвестно.
        }

        return (null, null, null, null);
    }

    private static int? ToInt(object? value) =>
        value is not null && int.TryParse(value.ToString(), out var parsed) ? parsed : null;

    private static int? QueryFirstInt(string scopePath, string query, string property)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(new ManagementScope(scopePath), new ObjectQuery(query));
            foreach (var item in searcher.Get())
            {
                using var obj = (ManagementObject)item;
                var value = obj[property];
                if (value is not null && int.TryParse(value.ToString(), out var parsed))
                {
                    return parsed;
                }
            }
        }
        catch (Exception)
        {
            // Нет WMI / нет класса (не Windows, нет батареи) — null.
        }

        return null;
    }
}
