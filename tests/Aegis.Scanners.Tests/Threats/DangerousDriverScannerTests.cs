using Aegis.Core.Models;
using Aegis.Scanners.Probing;
using Aegis.Scanners.Threats;
using Xunit;

namespace Aegis.Scanners.Tests.Threats;

public sealed class DangerousDriverScannerTests
{
    [Fact]
    public async Task ScanAsync_MaliciousDriver_IsDangerInThreats()
    {
        var scanner = new DangerousDriverScanner(new FakeProbe(
            [new DangerousDriver { Name = "evil.sys", Path = @"C:\x\evil.sys", Malicious = true }]));

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);
        Assert.Equal(ScanGroup.Threats, finding.Group);
        Assert.Equal(Severity.Danger, finding.Severity);
        Assert.Contains("Опасный драйвер", finding.Title);
        Assert.Contains("evil.sys", finding.Title);
    }

    [Fact]
    public async Task ScanAsync_VulnerableDriver_IsWarning()
    {
        var scanner = new DangerousDriverScanner(new FakeProbe(
            [new DangerousDriver { Name = "vuln.sys", Path = @"C:\x\vuln.sys", Malicious = false }]));

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);
        Assert.Equal(Severity.Warning, finding.Severity);
        Assert.Contains("Уязвимый драйвер", finding.Title);
    }

    [Fact]
    public async Task ScanAsync_NoDangerousDrivers_ReturnsEmpty() =>
        Assert.Empty((await new DangerousDriverScanner(new FakeProbe([])).ScanAsync()).Findings);

    private sealed class FakeProbe(IReadOnlyList<DangerousDriver> drivers) : IDangerousDriverProbe
    {
        public Task<IReadOnlyList<DangerousDriver>> FindAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(drivers);
    }
}
