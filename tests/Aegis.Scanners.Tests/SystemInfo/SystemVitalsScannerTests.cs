using Aegis.Core.Models;
using Aegis.Scanners.Probing;
using Aegis.Scanners.SystemInfo;
using Xunit;

namespace Aegis.Scanners.Tests.SystemInfo;

public sealed class SystemVitalsScannerTests
{
    private const long Gb = 1024L * 1024 * 1024;

    [Fact]
    public async Task RamNearlyFull_IsDanger_WithPercentMetric()
    {
        // 16 ГБ всего, свободно 1 ГБ → занято ~94%.
        var finding = await RamAsync(total: 16 * Gb, available: 1 * Gb);

        Assert.Equal(Severity.Danger, finding.Severity);
        Assert.Equal("94%", finding.Data!["metric"]);
        Assert.Equal("memory", finding.Data["healthIcon"]);
    }

    [Fact]
    public async Task RamHalfUsed_IsOk()
    {
        var finding = await RamAsync(total: 16 * Gb, available: 8 * Gb);
        Assert.Equal(Severity.Ok, finding.Severity);
    }

    [Fact]
    public async Task LongUptime_WarnsToReboot_WithPluralizedDays()
    {
        var result = await Scan(new SystemVitals { RamTotalBytes = 8 * Gb, RamAvailableBytes = 4 * Gb, UptimeSeconds = 10 * 86400 });
        var uptime = result.Single(f => f.Id == "health-uptime");

        Assert.Equal(Severity.Warning, uptime.Severity);
        Assert.Equal("10 дней", uptime.Data!["metric"]);
    }

    [Fact]
    public async Task FreshUptime_IsOk_WithPluralizedHours()
    {
        var result = await Scan(new SystemVitals { RamTotalBytes = 8 * Gb, RamAvailableBytes = 4 * Gb, UptimeSeconds = 2 * 3600 });
        var uptime = result.Single(f => f.Id == "health-uptime");

        Assert.Equal(Severity.Ok, uptime.Severity);
        Assert.Equal("2 часа", uptime.Data!["metric"]);
    }

    [Fact]
    public async Task HighCpuLoad_Warns_PointsToProcesses()
    {
        var result = await Scan(new SystemVitals { RamTotalBytes = 8 * Gb, RamAvailableBytes = 4 * Gb, CpuLoadPercent = 92 });
        var cpu = result.Single(f => f.Id == "health-cpuload");

        Assert.Equal(Severity.Warning, cpu.Severity);
        Assert.Equal("92%", cpu.Data!["metric"]);
    }

    [Fact]
    public async Task NoFanSensor_IsInfo_NoMetric()
    {
        var result = await Scan(new SystemVitals { RamTotalBytes = 8 * Gb, RamAvailableBytes = 4 * Gb, FanRpm = null });
        var fan = result.Single(f => f.Id == "health-fan");

        Assert.Equal(Severity.Info, fan.Severity);
        Assert.False(fan.Data!.ContainsKey("metric"));
    }

    [Fact]
    public async Task FanSensorPresent_IsOk_WithRpmMetric()
    {
        var result = await Scan(new SystemVitals { RamTotalBytes = 8 * Gb, RamAvailableBytes = 4 * Gb, FanRpm = 1500 });
        var fan = result.Single(f => f.Id == "health-fan");

        Assert.Equal(Severity.Ok, fan.Severity);
        Assert.Equal("1500 об/мин", fan.Data!["metric"]);
    }

    [Fact]
    public async Task GpuLoad_Present_AddsTile_WithMemoryAndWatts()
    {
        var result = await Scan(new SystemVitals
        {
            RamTotalBytes = 8 * Gb, RamAvailableBytes = 4 * Gb,
            GpuLoadPercent = 95, GpuMemoryUsedMb = 2000, GpuMemoryTotalMb = 8192, GpuPowerWatts = 90,
        });
        var gpu = result.Single(f => f.Id == "health-gpuload");

        Assert.Equal(Severity.Warning, gpu.Severity); // 95% → под нагрузкой
        Assert.Equal("95%", gpu.Data!["metric"]);
        Assert.Contains("2000 из 8192 МБ", gpu.Explain);
        Assert.Contains("90 Вт", gpu.Explain);
    }

    [Fact]
    public async Task NoGpuLoad_NoGpuTile()
    {
        var result = await Scan(new SystemVitals { RamTotalBytes = 8 * Gb, RamAvailableBytes = 4 * Gb });
        Assert.DoesNotContain(result, f => f.Id == "health-gpuload");
    }

    [Fact]
    public async Task FanPresentButStopped_IsOk_MarkedStopped()
    {
        var result = await Scan(new SystemVitals { RamTotalBytes = 8 * Gb, RamAvailableBytes = 4 * Gb, FanRpm = 0, FanPresent = true });
        var fan = result.Single(f => f.Id == "health-fan");

        Assert.Equal(Severity.Ok, fan.Severity);
        Assert.Equal("0 об/мин", fan.Data!["metric"]);
    }

    [Fact]
    public async Task FanPresentButRpmUnreadable_IsInfo_NotStopped()
    {
        // Вентилятор ЕСТЬ, но обороты прочитать не смогли (rpm=null) — «датчик недоступен», а НЕ ложное
        // «0 об/мин, остановлены» (регресс аудита 2026-07-02).
        var result = await Scan(new SystemVitals
        {
            RamTotalBytes = 8 * Gb, RamAvailableBytes = 4 * Gb, FanRpm = null, FanPresent = true,
        });
        var fan = result.Single(f => f.Id == "health-fan");

        Assert.Equal(Severity.Info, fan.Severity);
        Assert.Equal("датчик недоступен", fan.Detail);
        Assert.False(fan.Data!.ContainsKey("metric"));
    }

    [Fact]
    public async Task ZeroRam_SkipsRamCard_ButKeepsOthers()
    {
        var result = await Scan(new SystemVitals { RamTotalBytes = 0, UptimeSeconds = 3600 });

        Assert.DoesNotContain(result, f => f.Id == "health-ram");
        Assert.Contains(result, f => f.Id == "health-cpuload");
    }

    private static async Task<Finding> RamAsync(long total, long available)
    {
        var result = await Scan(new SystemVitals { RamTotalBytes = total, RamAvailableBytes = available });
        return result.Single(f => f.Id == "health-ram");
    }

    private static async Task<IReadOnlyList<Finding>> Scan(SystemVitals vitals)
    {
        var scanner = new SystemVitalsScanner(new FakeProbe(vitals));
        return (await scanner.ScanAsync()).Findings;
    }

    private sealed class FakeProbe(SystemVitals vitals) : ISystemVitalsProbe
    {
        public Task<SystemVitals> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult(vitals);
    }
}
