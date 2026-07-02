using Aegis.Core.Models;
using Aegis.Scanners.Maintenance;
using Aegis.Scanners.Probing;
using Xunit;

namespace Aegis.Scanners.Tests.Maintenance;

public sealed class SystemMaintenanceScannerTests
{
    [Fact]
    public async Task ScanAsync_Tools_AreInfoAndGroupedInMaintenanceSection()
    {
        var result = await new SystemMaintenanceScanner(new FakeHistory(null)).ScanAsync();

        Assert.Equal(2, result.Findings.Count);
        Assert.All(result.Findings, f =>
        {
            Assert.Equal(Severity.Info, f.Severity); // это инструменты, а не проблемы
            Assert.Equal("Инструменты обслуживания — запускать только при проблемах", f.Data!["section"]);
        });
    }

    [Fact]
    public async Task ScanAsync_RecentlyRun_AddsSmallDateNote()
    {
        var when = new DateTimeOffset(2026, 6, 28, 12, 0, 0, TimeSpan.Zero);
        var result = await new SystemMaintenanceScanner(new FakeHistory(when)).ScanAsync();

        var sfc = result.Findings.First(f => f.Id == "maintenance-sfc-dism");
        Assert.Contains("запускали 28.06.2026", sfc.Detail);
    }

    [Fact]
    public async Task ScanAsync_NeverRun_NoDateNote()
    {
        var result = await new SystemMaintenanceScanner(new FakeHistory(null)).ScanAsync();

        var sfc = result.Findings.First(f => f.Id == "maintenance-sfc-dism");
        Assert.DoesNotContain("запускали", sfc.Detail!);
    }

    private sealed class FakeHistory(DateTimeOffset? lastRun) : IMaintenanceHistoryProbe
    {
        public DateTimeOffset? GetLastRun(string toolKey) => lastRun;
    }
}
