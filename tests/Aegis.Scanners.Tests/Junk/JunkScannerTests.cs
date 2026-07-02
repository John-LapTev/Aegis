using Aegis.Core.Models;
using Aegis.Scanners.Junk;
using Aegis.Scanners.Probing;
using Xunit;

namespace Aegis.Scanners.Tests.Junk;

public sealed class JunkScannerTests
{
    // 1.5 ГБ = 1.5 * 1024^3
    private const long OneAndHalfGib = 1_610_612_736L;

    // 700 МБ = 700 * 1024^2
    private const long SevenHundredMib = 734_003_200L;

    [Fact]
    public async Task ScanAsync_GroupsByCategory_SumsSizes_AndOrdersBySizeDescending()
    {
        var probe = new FakeJunkProbe(
        [
            new JunkCandidate { Path = @"C:\Windows\Temp", SizeBytes = 1_000_000_000L, Category = JunkCategory.TempFiles },
            new JunkCandidate { Path = @"C:\Users\Ivan\AppData\Local\Temp", SizeBytes = OneAndHalfGib - 1_000_000_000L, Category = JunkCategory.TempFiles },
            new JunkCandidate { Path = "$Recycle.Bin", SizeBytes = SevenHundredMib, Category = JunkCategory.RecycleBin },
        ]);
        var scanner = new JunkScanner(probe);

        var result = await scanner.ScanAsync();

        Assert.Equal(ScanGroup.Junk, result.Group);
        Assert.Equal(2, result.Findings.Count);

        // Самая большая категория — первой.
        Assert.Equal("junk-TempFiles", result.Findings[0].Id);
        Assert.Contains("1.5 ГБ", result.Findings[0].Title);
        Assert.Equal("2 расположений", result.Findings[0].Detail);

        Assert.Equal("junk-RecycleBin", result.Findings[1].Id);
        Assert.Equal("$Recycle.Bin", result.Findings[1].Detail);
    }

    [Fact]
    public async Task ScanAsync_ExcludesZeroSizeCandidates()
    {
        var probe = new FakeJunkProbe(
        [
            new JunkCandidate { Path = "empty", SizeBytes = 0L, Category = JunkCategory.Cache },
        ]);
        var scanner = new JunkScanner(probe);

        var result = await scanner.ScanAsync();

        Assert.Empty(result.Findings);
    }

    [Fact]
    public async Task ScanAsync_AllFindingsAreInformationalAndExplained()
    {
        var probe = new FakeJunkProbe(
        [
            new JunkCandidate { Path = "cache", SizeBytes = 5_000_000L, Category = JunkCategory.Cache },
        ]);
        var scanner = new JunkScanner(probe);

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings);
        Assert.Equal(Severity.Info, finding.Severity);
        Assert.False(string.IsNullOrWhiteSpace(finding.Explain));
    }

    private sealed class FakeJunkProbe(IReadOnlyList<JunkCandidate> candidates) : IJunkProbe
    {
        public Task<IReadOnlyList<JunkCandidate>> FindAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(candidates);
    }
}
