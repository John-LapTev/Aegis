using Aegis.Core.Models;
using Aegis.Scanners.Probing;
using Aegis.Scanners.SystemInfo;
using Xunit;

namespace Aegis.Scanners.Tests.SystemInfo;

public sealed class BatteryScannerTests
{
    [Theory]
    [InlineData(10, Severity.Ok)]
    [InlineData(28, Severity.Warning)]
    [InlineData(55, Severity.Danger)]
    public async Task ScanAsync_Wear_MapsSeverity_InHealthGroup(int wear, Severity expected)
    {
        var scanner = new BatteryScanner(new FakeBatteryProbe(new BatterySnapshot { HasBattery = true, WearPercent = wear }));

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);

        Assert.Equal(ScanGroup.Health, finding.Group);
        Assert.Equal(expected, finding.Severity);
        // Износ % теперь в Data["wear"] (для плашки в углу), а не в заголовке.
        Assert.Equal(wear.ToString(), finding.Data?.GetValueOrDefault("wear"));
    }

    [Fact]
    public async Task ScanAsync_NoBattery_ReturnsEmpty() =>
        Assert.Empty((await new BatteryScanner(new FakeBatteryProbe(new BatterySnapshot { HasBattery = false })).ScanAsync()).Findings);

    [Fact]
    public async Task ScanAsync_BatteryButUnknownWear_ShowsOkInfo()
    {
        var scanner = new BatteryScanner(new FakeBatteryProbe(new BatterySnapshot { HasBattery = true, WearPercent = null }));

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);
        Assert.Equal(Severity.Ok, finding.Severity);
    }

    private sealed class FakeBatteryProbe(BatterySnapshot snapshot) : IBatteryProbe
    {
        public Task<BatterySnapshot> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(snapshot);
    }
}
