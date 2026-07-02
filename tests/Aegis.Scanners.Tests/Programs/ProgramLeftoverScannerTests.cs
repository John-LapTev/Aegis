using Aegis.Core.Models;
using Aegis.Scanners.Probing;
using Aegis.Scanners.Programs;
using Xunit;

namespace Aegis.Scanners.Tests.Programs;

public sealed class ProgramLeftoverScannerTests
{
    [Fact]
    public async Task ScanAsync_EmptyFolderNotInstalled_OffersReversibleCleanup()
    {
        var scanner = new ProgramLeftoverScanner(new FakeLeftoverProbe(
            Folder("OldGame", @"C:\Users\u\AppData\Roaming\OldGame", sizeBytes: 0, isEmpty: true, matchesInstalled: false)));

        var result = await scanner.ScanAsync();

        Assert.Equal(ScanGroup.Junk, result.Group);
        var finding = Assert.Single(result.Findings, f => f.Id.StartsWith("leftover-empty-", StringComparison.Ordinal));
        Assert.Equal(Severity.Info, finding.Severity);
        Assert.Equal("folder-delete", finding.Data!["kind"]);
        Assert.Equal(@"C:\Users\u\AppData\Roaming\OldGame", finding.Data!["path"]);
    }

    [Fact]
    public async Task ScanAsync_NonEmptyFolder_NeverFlagged_AvoidsFalsePositives()
    {
        // Непустую папку НЕ помечаем как остаток — даже если она не в списке установленных (могут быть
        // сейвы/настройки портативной/установленной программы). Надёжность важнее количества.
        var scanner = new ProgramLeftoverScanner(new FakeLeftoverProbe(
            Folder("SomeApp", @"C:\Users\u\AppData\Local\SomeApp", sizeBytes: 200L * 1024 * 1024, isEmpty: false, matchesInstalled: false)));

        var result = await scanner.ScanAsync();

        Assert.DoesNotContain(result.Findings, f => f.Id.StartsWith("leftover-", StringComparison.Ordinal) && f.Id != "leftover-none");
        Assert.Single(result.Findings, f => f.Id == "leftover-none");
    }

    [Fact]
    public async Task ScanAsync_EmptyFolderOfInstalledProgram_NotFlagged()
    {
        var scanner = new ProgramLeftoverScanner(new FakeLeftoverProbe(
            Folder("Spotify", @"C:\Users\u\AppData\Roaming\Spotify", sizeBytes: 0, isEmpty: true, matchesInstalled: true)));

        var result = await scanner.ScanAsync();

        Assert.Single(result.Findings, f => f.Id == "leftover-none");
    }

    [Fact]
    public async Task ScanAsync_NothingToClean_ReportsOk()
    {
        var scanner = new ProgramLeftoverScanner(new FakeLeftoverProbe());

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings);
        Assert.Equal("leftover-none", finding.Id);
        Assert.Equal(Severity.Ok, finding.Severity);
    }

    private static LeftoverFolder Folder(string name, string path, long sizeBytes, bool isEmpty, bool matchesInstalled,
        bool recentlyUsed = false) =>
        new()
        {
            Name = name, Path = path, SizeBytes = sizeBytes, IsEmpty = isEmpty,
            MatchesInstalled = matchesInstalled, RecentlyUsed = recentlyUsed,
        };

    private sealed class FakeLeftoverProbe(params LeftoverFolder[] folders) : ILeftoverProbe
    {
        public Task<LeftoverSnapshot> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new LeftoverSnapshot { Folders = folders });
    }
}
