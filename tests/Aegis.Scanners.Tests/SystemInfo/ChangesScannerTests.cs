using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.SystemInfo;
using Xunit;

namespace Aegis.Scanners.Tests.SystemInfo;

public sealed class ChangesScannerTests
{
    [Fact]
    public async Task FirstScan_NoPreviousSnapshot_ShowsBaselineAndRemembers()
    {
        var store = new FakeStore(null);
        var scanner = new ChangesScanner(new FakeProbe(Snapshot(programs: ["Chrome"])), store);

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);

        Assert.Equal("changes-baseline", finding.Id);
        Assert.NotNull(store.Saved); // текущее состояние запомнено как точка отсчёта
    }

    [Fact]
    public async Task NewProgram_SinceLastSnapshot_IsReported()
    {
        var store = new FakeStore(Snapshot(programs: ["Chrome"]));
        var scanner = new ChangesScanner(new FakeProbe(Snapshot(programs: ["Chrome", "SomeToolbar"])), store);

        var findings = (await scanner.ScanAsync()).Findings;

        var finding = Assert.Single(findings);
        Assert.StartsWith("changes-program-", finding.Id);
        Assert.Contains("SomeToolbar", finding.Title);
    }


    [Fact]
    public async Task NothingChanged_ShowsAllClear()
    {
        var same = Snapshot(programs: ["Chrome"], autostart: ["HKCUSteamsteam.exe"]);
        var store = new FakeStore(same);
        var scanner = new ChangesScanner(new FakeProbe(same), store);

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);
        Assert.Equal("changes-none", finding.Id);
        Assert.Equal(Severity.Ok, finding.Severity);
    }

    private static SystemSnapshot Snapshot(
        IReadOnlyList<string>? programs = null,
        IReadOnlyList<string>? autostart = null,
        IReadOnlyList<string>? hosts = null) => new()
    {
        CapturedAt = DateTimeOffset.UnixEpoch,
        Programs = programs ?? [],
        Autostart = autostart ?? [],
        HostsEntries = hosts ?? [],
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
