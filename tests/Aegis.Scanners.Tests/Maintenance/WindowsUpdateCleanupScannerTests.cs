using Aegis.Core.Models;
using Aegis.Scanners.Maintenance;
using Xunit;

namespace Aegis.Scanners.Tests.Maintenance;

public sealed class WindowsUpdateCleanupScannerTests
{
    [Fact]
    public async Task ScanAsync_OffersReversibleDismCleanupInJunk()
    {
        var scanner = new WindowsUpdateCleanupScanner();

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings);
        Assert.Equal("junk-windows-update-components", finding.Id);
        Assert.Equal(ScanGroup.Junk, finding.Group);
        Assert.Equal(Severity.Info, finding.Severity);
        Assert.NotNull(finding.Data);
        Assert.Equal("dism-cleanup", finding.Data!["kind"]);
    }
}
