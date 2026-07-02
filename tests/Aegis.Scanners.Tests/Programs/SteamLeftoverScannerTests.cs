using Aegis.Core.Models;
using Aegis.Scanners.Probing;
using Aegis.Scanners.Programs;
using Xunit;

namespace Aegis.Scanners.Tests.Programs;

public sealed class SteamLeftoverScannerTests
{
    [Fact]
    public async Task ScanAsync_OrphanCache_Info_BatchSelectable_ToRecycleBin()
    {
        var scanner = new SteamLeftoverScanner(new FakeSteamProbe(
            new SteamLeftover { Title = "Кэш удалённой игры (Steam, AppID 730)", Path = @"D:\Steam\steamapps\shadercache\730", Kind = SteamLeftoverKind.OrphanCache }));

        var result = await scanner.ScanAsync();

        Assert.Equal(ScanGroup.Junk, result.Group);
        var finding = Assert.Single(result.Findings, f => f.Id.StartsWith("leftover-steam-cache-", StringComparison.Ordinal));
        Assert.Equal(Severity.Info, finding.Severity);
        Assert.Equal("folder-delete", finding.Data!["kind"]);
        Assert.False(finding.Data!.ContainsKey("noBatch")); // кэши можно чистить массово
    }

    [Fact]
    public async Task ScanAsync_CrackResidue_Info_NotBatchSelectable()
    {
        var scanner = new SteamLeftoverScanner(new FakeSteamProbe(
            new SteamLeftover { Title = "Возможные остатки пиратской игры: Steam\\CODEX", Path = @"C:\Users\u\AppData\Roaming\Steam\CODEX", Kind = SteamLeftoverKind.CrackResidue }));

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings, f => f.Id.StartsWith("leftover-steam-crack-", StringComparison.Ordinal));
        Assert.Equal(Severity.Info, finding.Severity);
        Assert.Equal("1", finding.Data!["noBatch"]); // пиратские следы — только по одному (могут быть сейвы)
    }

    [Fact]
    public async Task ScanAsync_Nothing_ReturnsNoFindings()
    {
        var scanner = new SteamLeftoverScanner(new FakeSteamProbe());

        var result = await scanner.ScanAsync();

        Assert.Empty(result.Findings);
    }

    private sealed class FakeSteamProbe(params SteamLeftover[] items) : ISteamLeftoverProbe
    {
        public Task<SteamLeftoverSnapshot> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new SteamLeftoverSnapshot { Items = items });
    }
}
