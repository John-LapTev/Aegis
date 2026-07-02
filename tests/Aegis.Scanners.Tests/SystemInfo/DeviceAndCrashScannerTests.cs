using Aegis.Core.Models;
using Aegis.Scanners.Probing;
using Aegis.Scanners.SystemInfo;
using Xunit;

namespace Aegis.Scanners.Tests.SystemInfo;

public sealed class DeviceAndCrashScannerTests
{
    [Fact]
    public async Task Devices_AllWorking_IsOk()
    {
        var finding = Assert.Single((await new DeviceErrorScanner(new FakeDevices()).ScanAsync()).Findings);

        Assert.Equal("health-devices", finding.Id);
        Assert.Equal(Severity.Ok, finding.Severity);
    }

    [Fact]
    public async Task Devices_WithErrors_Warns_ListsThem()
    {
        var finding = Assert.Single((await new DeviceErrorScanner(new FakeDevices("Realtek Audio", "Wi-Fi адаптер")).ScanAsync()).Findings);

        Assert.Equal(Severity.Warning, finding.Severity);
        Assert.Equal("2", finding.Data!["metric"]);
        Assert.Contains("Realtek Audio", finding.Detail);
    }

    [Fact]
    public async Task Crashes_None_IsOk()
    {
        var finding = Assert.Single((await new CrashHistoryScanner(new FakeCrashes(0)).ScanAsync()).Findings);

        Assert.Equal("health-crashes", finding.Id);
        Assert.Equal(Severity.Ok, finding.Severity);
    }

    [Theory]
    [InlineData(1, Severity.Warning)]
    [InlineData(2, Severity.Warning)]
    [InlineData(3, Severity.Danger)]
    public async Task Crashes_Some_MapsSeverity(int count, Severity expected)
    {
        var finding = Assert.Single((await new CrashHistoryScanner(new FakeCrashes(count)).ScanAsync()).Findings);

        Assert.Equal(expected, finding.Severity);
        Assert.Equal(count.ToString(), finding.Data!["metric"]);
    }

    private sealed class FakeDevices(params string[] problems) : IDeviceErrorProbe
    {
        public Task<IReadOnlyList<string>> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(problems);
    }

    private sealed class FakeCrashes(int count) : ICrashHistoryProbe
    {
        public Task<int> RecentCrashCountAsync(CancellationToken cancellationToken = default) => Task.FromResult(count);
    }
}
