using Aegis.System.Backup;
using Xunit;

namespace Aegis.System.Tests;

public sealed class RebootRollbackSchedulerTests
{
    [Fact]
    public void Schedule_Then_GetPending_RoundTripsViaFile()
    {
        // RunOnce (реестр) на не-Windows не сработает (тихо проглатывается), но памятка-файл пишется и читается.
        var scheduler = new RebootRollbackScheduler();
        scheduler.Clear();

        scheduler.Schedule(["backup-1", "backup-2"], "отключение телеметрии + правки реестра");

        var pending = scheduler.GetPending();
        Assert.NotNull(pending);
        Assert.Equal(new[] { "backup-1", "backup-2" }, pending!.BackupIds);
        Assert.Contains("реестра", pending.Description);

        scheduler.Clear();
        Assert.Null(scheduler.GetPending());
    }
}
