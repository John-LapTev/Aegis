using Aegis.Core.Models;
using Aegis.Scanners.Maintenance;
using Aegis.Scanners.Probing;
using Xunit;

namespace Aegis.Scanners.Tests.Maintenance;

public sealed class DiskOptimizeScannerTests
{
    [Fact]
    public void RecentRun_NoAttention()
    {
        Assert.False(DiskOptimizeScanner.NeedsAttention(new DiskOptimizeState
        {
            DaysSinceLastRun = 5,
            ScheduleEnabled = true,
        }));
    }

    [Fact]
    public void DisabledSchedule_NeedsAttention()
    {
        // Расписание выключают чужие «оптимизаторы» — человек об этом никогда не узнает сам.
        Assert.True(DiskOptimizeScanner.NeedsAttention(new DiskOptimizeState
        {
            DaysSinceLastRun = 2,
            ScheduleEnabled = false,
        }));
    }

    [Fact]
    public void LongTimeSinceRun_NeedsAttention()
    {
        Assert.True(DiskOptimizeScanner.NeedsAttention(new DiskOptimizeState
        {
            DaysSinceLastRun = DiskOptimizeScanner.StaleDays,
            ScheduleEnabled = true,
        }));
    }

    [Fact]
    public void UnknownLastRun_WithWorkingSchedule_StaysQuiet()
    {
        // Дату узнать не удалось — не поднимаем тревогу на пустом месте.
        Assert.False(DiskOptimizeScanner.NeedsAttention(new DiskOptimizeState { ScheduleEnabled = true }));
    }

    [Fact]
    public async Task SsdSystem_ExplainsTrimNotDefragmentation()
    {
        // Дефрагментировать SSD не нужно и вредно — объяснение обязано отличаться, иначе совет вводит в заблуждение.
        var scanner = new DiskOptimizeScanner(new FakeProbe(new DiskOptimizeState
        {
            ScheduleEnabled = false,
            HasSolidStateDrive = true,
        }));

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);

        Assert.Contains("TRIM", finding.Explain);
        Assert.Equal(FindingKinds.DiskOptimize, finding.Data![FindingDataKeys.Kind]);
    }

    [Fact]
    public async Task HddSystem_ExplainsDefragmentation()
    {
        var scanner = new DiskOptimizeScanner(new FakeProbe(new DiskOptimizeState
        {
            ScheduleEnabled = false,
            HasSolidStateDrive = false,
        }));

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);

        Assert.DoesNotContain("TRIM", finding.Explain);
    }

    [Fact]
    public async Task HealthySystem_NoFindings()
    {
        var scanner = new DiskOptimizeScanner(new FakeProbe(new DiskOptimizeState
        {
            DaysSinceLastRun = 3,
            ScheduleEnabled = true,
        }));

        Assert.Empty((await scanner.ScanAsync()).Findings);
    }

    private sealed class FakeProbe(DiskOptimizeState state) : IDiskOptimizeProbe
    {
        public Task<DiskOptimizeState> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(state);
    }
}
