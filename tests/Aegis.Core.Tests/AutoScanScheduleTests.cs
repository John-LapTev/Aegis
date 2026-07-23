using Aegis.Core;
using Xunit;

namespace Aegis.Core.Tests;

public sealed class AutoScanScheduleTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Off_NeverRuns()
    {
        Assert.False(AutoScanSchedule.ShouldRun(AutoScanInterval.Off, Now.AddYears(-1), Now));
    }

    [Fact]
    public void Weekly_RunsAfterSevenDays()
    {
        Assert.True(AutoScanSchedule.ShouldRun(AutoScanInterval.Weekly, Now.AddDays(-7), Now));
        Assert.False(AutoScanSchedule.ShouldRun(AutoScanInterval.Weekly, Now.AddDays(-6), Now));
    }

    [Fact]
    public void Monthly_RunsAfterThirtyDays()
    {
        Assert.True(AutoScanSchedule.ShouldRun(AutoScanInterval.Monthly, Now.AddDays(-30), Now));
        Assert.False(AutoScanSchedule.ShouldRun(AutoScanInterval.Monthly, Now.AddDays(-29), Now));
    }

    [Fact]
    public void NoPreviousRun_DoesNotScanImmediately()
    {
        // Иначе программа полезла бы сканировать сразу после установки, пока человек смотрит первый экран.
        Assert.False(AutoScanSchedule.ShouldRun(AutoScanInterval.Weekly, null, Now));
    }

    [Fact]
    public void ClockMovedBackwards_DoesNotLoop()
    {
        // Дата последней проверки «в будущем» (перевели часы) не должна включать бесконечные проверки.
        Assert.False(AutoScanSchedule.ShouldRun(AutoScanInterval.Weekly, Now.AddDays(3), Now));
    }

    [Fact]
    public void DescribeIsHumanReadable()
    {
        Assert.Equal("Проверять раз в неделю", AutoScanSchedule.Describe(AutoScanInterval.Weekly));
        Assert.Equal("Не проверять автоматически", AutoScanSchedule.Describe(AutoScanInterval.Off));
    }
}
