using Aegis.Core.Models;
using Aegis.Scanners.Files;
using Aegis.Scanners.Probing;
using Xunit;

namespace Aegis.Scanners.Tests.Files;

public sealed class LargeDuplicateScannerTests
{
    private const long Gb = 1024L * 1024 * 1024;
    private const long Mb = 1024L * 1024;

    [Fact]
    public async Task ScanAsync_FlagsLargeFiles_NotSmallOnes()
    {
        var scanner = new LargeDuplicateScanner(new FakeProbe(
        [
            File(@"C:\Users\Ivan\Downloads\movie.mkv", 2 * Gb),
            File(@"C:\Users\Ivan\Downloads\note.txt", 100 * Mb),
        ]));

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings);
        Assert.StartsWith("largefile-", finding.Id);
        Assert.Equal(Severity.Info, finding.Severity);
    }

    [Fact]
    public async Task ScanAsync_FlagsDuplicateGroup_WithCopyCount()
    {
        var scanner = new LargeDuplicateScanner(new FakeProbe(
        [
            File(@"C:\a\photo.jpg", 100 * Mb, "hashA"),
            File(@"C:\b\photo.jpg", 100 * Mb, "hashA"),
            File(@"C:\c\photo.jpg", 100 * Mb, "hashA"),
            File(@"C:\unique.bin", 100 * Mb, "hashB"),
        ]));

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings);
        Assert.StartsWith("dupes-", finding.Id);
        Assert.Contains("3 копий", finding.Title);
    }

    [Fact]
    public async Task ScanAsync_NothingBigOrDuplicated_ReturnsEmpty()
    {
        var scanner = new LargeDuplicateScanner(new FakeProbe(
        [
            File(@"C:\a.txt", 10 * Mb, "h1"),
            File(@"C:\b.txt", 20 * Mb, "h2"),
        ]));

        var result = await scanner.ScanAsync();

        Assert.Empty(result.Findings);
    }

    [Fact]
    public async Task ScanAsync_FilesWithoutHash_AreNotTreatedAsDuplicates()
    {
        var scanner = new LargeDuplicateScanner(new FakeProbe(
        [
            File(@"C:\a.bin", 50 * Mb),
            File(@"C:\b.bin", 50 * Mb),
        ]));

        var result = await scanner.ScanAsync();

        Assert.Empty(result.Findings);
    }

    private static FileEntry File(string path, long size, string hash = "") =>
        new() { Path = path, SizeBytes = size, ContentHash = hash };

    private sealed class FakeProbe(IReadOnlyList<FileEntry> files) : IFileInventoryProbe
    {
        public Task<IReadOnlyList<FileEntry>> FindAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(files);
    }
}
