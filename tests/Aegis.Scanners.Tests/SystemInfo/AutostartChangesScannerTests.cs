using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.SystemInfo;
using Xunit;

namespace Aegis.Scanners.Tests.SystemInfo;

public sealed class AutostartChangesScannerTests
{
    private const string Sep = "\u001F";

    [Fact]
    public async Task FirstScan_NoPrevious_ShowsBaselineAndRemembers()
    {
        var store = new FakeStore(null);
        var scanner = new AutostartChangesScanner(new FakeProbe(Snapshot("HKCU" + Sep + "Steam" + Sep + "steam.exe")), store);

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);
        Assert.Equal("autostart-new-baseline", finding.Id);
        Assert.NotNull(store.Saved);
        Assert.Equal(ScanGroup.Autostart, finding.Group);
    }

    [Fact]
    public async Task NewAutostartEntry_IsReportedAsWarning()
    {
        var store = new FakeStore(Snapshot("HKCU" + Sep + "Steam" + Sep + "steam.exe"));
        var scanner = new AutostartChangesScanner(
            new FakeProbe(Snapshot("HKCU" + Sep + "Steam" + Sep + "steam.exe", "HKCU" + Sep + "Miner" + Sep + "mine.exe")), store);

        var finding = Assert.Single((await scanner.ScanAsync()).Findings, f => f.Id.StartsWith("autostart-new-", System.StringComparison.Ordinal) && f.Severity == Severity.Warning);
        Assert.Contains("Miner", finding.Title);
        Assert.Equal(ScanGroup.Autostart, finding.Group);
    }

    [Fact]
    public async Task NothingNew_ShowsAllClear()
    {
        var same = Snapshot("HKCU" + Sep + "Steam" + Sep + "steam.exe");
        var store = new FakeStore(same);
        var scanner = new AutostartChangesScanner(new FakeProbe(same), store);

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);
        Assert.Equal("autostart-new-none", finding.Id);
        Assert.Equal(Severity.Ok, finding.Severity);
    }

    private static SystemSnapshot Snapshot(params string[] autostart) => new()
    {
        CapturedAt = DateTimeOffset.UnixEpoch,
        Autostart = autostart,
        Programs = [],
        HostsEntries = [],
    };

    private sealed class FakeProbe(SystemSnapshot snapshot) : ISystemSnapshotProbe
    {
        public Task<SystemSnapshot> CaptureAsync(CancellationToken cancellationToken = default) => Task.FromResult(snapshot);
    }

    private sealed class FakeStore(SystemSnapshot? initial) : ISystemSnapshotStore
    {
        private SystemSnapshot? _snapshot = initial;
        public SystemSnapshot? Saved { get; private set; }
        public SystemSnapshot? LoadLatest() => _snapshot;
        public void Save(SystemSnapshot snapshot)
        {
            Saved = snapshot;
            _snapshot = snapshot;
        }
    }
}
