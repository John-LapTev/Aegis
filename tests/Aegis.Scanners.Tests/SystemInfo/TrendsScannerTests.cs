using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Probing;
using Aegis.Scanners.SystemInfo;
using Xunit;

namespace Aegis.Scanners.Tests.SystemInfo;

public sealed class TrendsScannerTests
{
    [Fact]
    public async Task FirstScan_NoHistory_ShowsBaselineAndRemembers()
    {
        var store = new FakeStore();
        var scanner = new TrendsScanner(Probe(Drive("C", realloc: 0)), store);

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);

        Assert.Equal("trends-baseline", finding.Id);
        Assert.Single(store.History); // текущий снимок запомнен как точка отсчёта
    }

    [Fact]
    public async Task ReallocatedSectorsGrew_IsDanger()
    {
        var store = new FakeStore(Snapshot(Point("C", realloc: 4)));
        var scanner = new TrendsScanner(Probe(Drive("C", realloc: 10)), store);

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);

        Assert.Equal("trends-realloc-C", finding.Id);
        Assert.Equal(Severity.Danger, finding.Severity);
        Assert.Contains("начал сыпаться", finding.Title);
    }

    [Fact]
    public async Task ReallocatedSectorsStable_IsWarning()
    {
        var store = new FakeStore(Snapshot(Point("C", realloc: 4)));
        var scanner = new TrendsScanner(Probe(Drive("C", realloc: 4)), store);

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);

        Assert.Equal("trends-realloc-C", finding.Id);
        Assert.Equal(Severity.Warning, finding.Severity);
    }

    [Fact]
    public async Task FillDropped_ReportsFreedSpace()
    {
        var store = new FakeStore(Snapshot(Point("C", fill: 80)));
        var scanner = new TrendsScanner(Probe(Drive("C", fill: 60)), store);

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);

        Assert.Equal("trends-fill-C", finding.Id);
        Assert.Equal(Severity.Ok, finding.Severity);
        Assert.Contains("освободилось", finding.Explain);
    }

    [Fact]
    public async Task FillGrew_ReportsFilling()
    {
        var store = new FakeStore(Snapshot(Point("C", fill: 50)));
        var scanner = new TrendsScanner(Probe(Drive("C", fill: 70)), store);

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);

        Assert.Equal("trends-fill-C", finding.Id);
        Assert.Equal(Severity.Info, finding.Severity);
    }

    [Fact]
    public async Task WearJumped_IsReported()
    {
        var store = new FakeStore(Snapshot(Point("C", wear: 60)));
        var scanner = new TrendsScanner(Probe(Drive("C", wear: 72)), store);

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);

        Assert.Equal("trends-wear-C", finding.Id);
        Assert.Contains("быстро", finding.Title);
    }

    [Fact]
    public async Task NothingNotable_ShowsAllClear()
    {
        var store = new FakeStore(Snapshot(Point("C", realloc: 0, fill: 50, wear: 30, temp: 40)));
        var scanner = new TrendsScanner(Probe(Drive("C", realloc: 0, fill: 51, wear: 30, temp: 41)), store);

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);

        Assert.Equal("trends-none", finding.Id);
        Assert.Equal(Severity.Ok, finding.Severity);
    }

    [Fact]
    public async Task Scan_AppendsCurrentSnapshotToHistory()
    {
        var store = new FakeStore(Snapshot(Point("C", fill: 50)));
        var scanner = new TrendsScanner(Probe(Drive("C", fill: 50)), store);

        await scanner.ScanAsync();

        Assert.Equal(2, store.History.Count); // прошлый + свежий
    }

    private static SmartDriveHealth Drive(string name, int? realloc = null, int? fill = null, int? wear = null, int? temp = null) => new()
    {
        Name = name,
        Level = SmartHealthLevel.Good,
        ReallocatedSectorCount = realloc,
        FillPercent = fill,
        PercentLifeUsed = wear,
        TemperatureCelsius = temp,
    };

    private static DiskTrendPoint Point(string name, int? realloc = null, int? fill = null, int? wear = null, int? temp = null) => new()
    {
        Name = name,
        ReallocatedSectorCount = realloc,
        FillPercent = fill,
        PercentLifeUsed = wear,
        TemperatureCelsius = temp,
    };

    private static HealthTrendSnapshot Snapshot(params DiskTrendPoint[] disks) => new()
    {
        CapturedAt = DateTimeOffset.UnixEpoch,
        Disks = disks,
    };

    private static FakeProbe Probe(params SmartDriveHealth[] drives) => new(drives);

    private sealed class FakeProbe(IReadOnlyList<SmartDriveHealth> drives) : IDiskHealthProbe
    {
        public Task<IReadOnlyList<SmartDriveHealth>> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(drives);
    }

    private sealed class FakeStore : IHealthTrendStore
    {
        public List<HealthTrendSnapshot> History { get; } = [];

        public FakeStore(params HealthTrendSnapshot[] initial) => History.AddRange(initial);

        public IReadOnlyList<HealthTrendSnapshot> LoadHistory() => History;
        public void Append(HealthTrendSnapshot snapshot) => History.Add(snapshot);
    }
}
