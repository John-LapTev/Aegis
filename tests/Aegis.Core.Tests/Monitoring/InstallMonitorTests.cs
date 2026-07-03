using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Core.Monitoring;
using Xunit;

namespace Aegis.Core.Tests.Monitoring;

public sealed class InstallMonitorTests
{
    [Fact]
    public void Diff_ReturnsOnlyNewlyAddedPaths()
    {
        var before = new InstallSnapshot { Paths = [@"C:\Program Files\Old"] };
        var after = new InstallSnapshot { Paths = [@"C:\Program Files\Old", @"C:\Program Files\New"] };

        var trace = InstallMonitor.Diff("New", before, after, DateTimeOffset.UnixEpoch);

        Assert.Equal([@"C:\Program Files\New"], trace.AddedPaths);
    }

    [Fact]
    public void Diff_ReturnsOnlyNewlyAddedRegistryKeys()
    {
        var before = new InstallSnapshot { RegistryKeys = [@"HKLM\SOFTWARE\A"] };
        var after = new InstallSnapshot { RegistryKeys = [@"HKLM\SOFTWARE\A", @"HKLM\SOFTWARE\B"] };

        var trace = InstallMonitor.Diff("B", before, after, DateTimeOffset.UnixEpoch);

        Assert.Equal([@"HKLM\SOFTWARE\B"], trace.AddedRegistryKeys);
    }

    [Fact]
    public void Diff_IsCaseInsensitive_NoFalsePositives()
    {
        var before = new InstallSnapshot { Paths = [@"C:\Program Files\App"] };
        var after = new InstallSnapshot { Paths = [@"C:\PROGRAM FILES\APP"] };

        var trace = InstallMonitor.Diff("App", before, after, DateTimeOffset.UnixEpoch);

        Assert.Empty(trace.AddedPaths); // тот же путь в другом регистре — не «новый»
    }

    [Fact]
    public async Task RecordAsync_CapturesAfterAndSavesTrace()
    {
        var baseline = new InstallSnapshot { Paths = [@"C:\Program Files\Old"] };
        var after = new InstallSnapshot { Paths = [@"C:\Program Files\Old", @"C:\Program Files\New"] };
        var store = new FakeStore();
        var monitor = new InstallMonitor(new FakeProbe(after), store);

        var trace = await monitor.RecordAsync("New App", baseline, DateTimeOffset.UnixEpoch);

        Assert.Equal("New App", trace.ProgramName);
        Assert.Equal([@"C:\Program Files\New"], trace.AddedPaths);
        Assert.NotNull(store.Saved);
        Assert.Equal("New App", store.Saved!.ProgramName);
    }

    private sealed class FakeProbe(InstallSnapshot snapshot) : IInstallSnapshotProbe
    {
        public Task<InstallSnapshot> CaptureAsync(CancellationToken cancellationToken = default) => Task.FromResult(snapshot);
    }

    private sealed class FakeStore : IInstallTraceStore
    {
        public InstallTrace? Saved { get; private set; }
        public IReadOnlyList<InstallTrace> LoadAll() => Saved is null ? [] : [Saved];
        public InstallTrace? Find(string programName) => Saved;
        public void Save(InstallTrace trace) => Saved = trace;
        public void Remove(string programName) => Saved = null;
    }
}
