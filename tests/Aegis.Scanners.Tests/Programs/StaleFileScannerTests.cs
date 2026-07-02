using Aegis.Core.Models;
using Aegis.Scanners.Probing;
using Aegis.Scanners.Programs;
using Xunit;

namespace Aegis.Scanners.Tests.Programs;

public sealed class StaleFileScannerTests
{
    [Fact]
    public async Task ScanAsync_BrokenShortcut_Info_BatchSelectable()
    {
        var scanner = Make(new StaleFile { Title = "Игра", Path = @"C:\Users\u\Desktop\Игра.lnk", Kind = StaleFileKind.BrokenShortcut });

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);

        Assert.StartsWith("leftover-lnk-", finding.Id, StringComparison.Ordinal);
        Assert.Equal(Severity.Info, finding.Severity);
        Assert.Equal("file-delete", finding.Data!["kind"]);
        Assert.False(finding.Data!.ContainsKey("noBatch")); // битые ярлыки — можно массово
    }

    [Fact]
    public async Task ScanAsync_EmptyFiles_CollapsedIntoOneFinding()
    {
        var scanner = Make(
            new StaleFile { Title = "a.tmp", Path = @"C:\Users\u\AppData\Local\Temp\a.tmp", Kind = StaleFileKind.EmptyFile },
            new StaleFile { Title = "b.tmp", Path = @"C:\Users\u\AppData\Local\Temp\b.tmp", Kind = StaleFileKind.EmptyFile });

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);

        Assert.Equal("junk-empty-all", finding.Id);
        Assert.Contains("2", finding.Title); // «… — 2 шт.»
        Assert.False(finding.Data!.ContainsKey("noBatch")); // можно чистить разом
        // Оба пути в одном пункте → очистятся за раз.
        Assert.Contains("a.tmp", finding.Data!["paths"]);
        Assert.Contains("b.tmp", finding.Data!["paths"]);
    }

    [Fact]
    public async Task ScanAsync_OldDownload_NotBatchSelectable()
    {
        var scanner = Make(new StaleFile
        {
            Title = "setup.exe", Path = @"C:\Users\u\Downloads\setup.exe",
            Kind = StaleFileKind.OldDownload, Note = "не менялся 200 дн.",
        });

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);

        Assert.StartsWith("old-download-", finding.Id, StringComparison.Ordinal);
        Assert.Equal("1", finding.Data!["noBatch"]); // старые загрузки — по одному (вдруг нужны)
        Assert.Contains("200", finding.Explain);
    }

    [Fact]
    public async Task ScanAsync_Nothing_ReturnsEmpty() =>
        Assert.Empty((await Make().ScanAsync()).Findings);

    private static StaleFileScanner Make(params StaleFile[] items) => new(new FakeStaleProbe(items));

    private sealed class FakeStaleProbe(params StaleFile[] items) : IStaleFileProbe
    {
        public Task<StaleFileSnapshot> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new StaleFileSnapshot { Items = items });
    }
}
