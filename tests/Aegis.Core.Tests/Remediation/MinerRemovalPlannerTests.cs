using Aegis.Core.Remediation;
using Xunit;

namespace Aegis.Core.Tests.Remediation;

public sealed class MinerRemovalPlannerTests
{
    [Fact]
    public void Plan_CleanCase_StopsDisablesAndQuarantines_NoReboot()
    {
        var plan = MinerRemovalPlanner.Plan(new MinerRemovalContext());

        RemovalStep[] expected =
            [RemovalStep.Backup, RemovalStep.StopProcesses, RemovalStep.DisableAutostart, RemovalStep.QuarantineFiles];

        Assert.False(plan.RequiresReboot);
        Assert.Equal(expected, plan.Steps);
    }

    [Fact]
    public void Plan_FileLocked_DefersDeletionToReboot()
    {
        var plan = MinerRemovalPlanner.Plan(new MinerRemovalContext { FilesLocked = true });

        RemovalStep[] expected =
            [RemovalStep.Backup, RemovalStep.StopProcesses, RemovalStep.DisableAutostart,
             RemovalStep.ScheduleDeleteOnReboot, RemovalStep.RequestReboot];

        Assert.True(plan.RequiresReboot);
        Assert.Equal(expected, plan.Steps);
    }

    [Fact]
    public void Plan_TiedToCriticalProcess_DoesNotStopProcesses_DefersToReboot()
    {
        var plan = MinerRemovalPlanner.Plan(new MinerRemovalContext { TiedToCriticalProcess = true });

        RemovalStep[] expected =
            [RemovalStep.Backup, RemovalStep.DisableAutostart, RemovalStep.ScheduleDeleteOnReboot, RemovalStep.RequestReboot];

        Assert.True(plan.RequiresReboot);
        Assert.DoesNotContain(RemovalStep.StopProcesses, plan.Steps);
        Assert.Equal(expected, plan.Steps);
    }

    [Fact]
    public void Plan_AlwaysBacksUpFirst()
    {
        var contexts = new[]
        {
            new MinerRemovalContext(),
            new MinerRemovalContext { FilesLocked = true },
            new MinerRemovalContext { TiedToCriticalProcess = true },
        };

        foreach (var context in contexts)
        {
            Assert.Equal(RemovalStep.Backup, MinerRemovalPlanner.Plan(context).Steps[0]);
        }
    }
}
