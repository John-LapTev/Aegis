using Aegis.Core.Models;
using Aegis.Scanners.Probing;
using Aegis.Scanners.Threats;
using Xunit;

namespace Aegis.Scanners.Tests.Threats;

public sealed class SuspiciousServiceScannerTests
{
    [Fact]
    public async Task ScanAsync_UnsignedFromTemp_IsDanger()
    {
        var scanner = new SuspiciousServiceScanner(new FakeProbe(
        [
            new SuspiciousService
            {
                Name = "xmr", DisplayName = "xmr", BinaryPath = @"C:\Users\u\AppData\Local\Temp\xmr.exe",
                Signed = false, Reason = "запускается из временной папки (Temp)",
            },
        ]));

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);
        Assert.Equal(ScanGroup.Threats, finding.Group);
        Assert.Equal(Severity.Danger, finding.Severity);
        Assert.Contains("Подозрительная служба", finding.Title);
    }

    [Fact]
    public async Task ScanAsync_SignedFromUnusualPath_IsWarning()
    {
        var scanner = new SuspiciousServiceScanner(new FakeProbe(
        [
            new SuspiciousService
            {
                Name = "App", DisplayName = "App Updater", BinaryPath = @"C:\Users\u\AppData\Local\App\svc.exe",
                Signed = true, Reason = "запускается из папки приложений пользователя (AppData)",
            },
        ]));

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);
        Assert.Equal(Severity.Warning, finding.Severity);
    }

    [Fact]
    public async Task ScanAsync_NoSuspiciousServices_IsEmpty() =>
        Assert.Empty((await new SuspiciousServiceScanner(new FakeProbe([])).ScanAsync()).Findings);

    private sealed class FakeProbe(IReadOnlyList<SuspiciousService> services) : ISuspiciousServiceProbe
    {
        public Task<IReadOnlyList<SuspiciousService>> FindAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(services);
    }
}
