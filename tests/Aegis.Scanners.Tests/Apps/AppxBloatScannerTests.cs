using Aegis.Core.Models;
using Aegis.Scanners.Apps;
using Aegis.Scanners.Probing;
using Xunit;

namespace Aegis.Scanners.Tests.Apps;

public sealed class AppxBloatScannerTests
{
    [Fact]
    public async Task ScanAsync_TurnsBloatAppsIntoRemovableFindings()
    {
        var scanner = new AppxBloatScanner(new FakeProbe(
        [
            new AppxApp { PackageFullName = "king.com.CandyCrushSaga_1_x64__k", Name = "Candy Crush", Category = "промо-игра" },
        ]));

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings);
        Assert.Equal(Severity.Info, finding.Severity);
        Assert.Equal("appx-remove", finding.Data!["kind"]);
        Assert.Equal("king.com.CandyCrushSaga_1_x64__k", finding.Data["package"]);
        Assert.Contains("Candy Crush", finding.Title);
    }

    [Fact]
    public async Task ScanAsync_NoBloat_NoFindings()
    {
        var scanner = new AppxBloatScanner(new FakeProbe([]));

        var result = await scanner.ScanAsync();

        Assert.Empty(result.Findings);
    }

    private sealed class FakeProbe(IReadOnlyList<AppxApp> apps) : IAppxProbe
    {
        public Task<IReadOnlyList<AppxApp>> FindBloatAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(apps);
    }
}
