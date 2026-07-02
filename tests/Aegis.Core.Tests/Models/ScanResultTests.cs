using Aegis.Core.Models;
using Xunit;

namespace Aegis.Core.Tests.Models;

public sealed class ScanResultTests
{
    [Fact]
    public void Empty_HasGroupAndNoFindings()
    {
        var result = ScanResult.Empty(ScanGroup.Threats);

        Assert.Equal(ScanGroup.Threats, result.Group);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void FixOutcome_Ok_CarriesBackupId()
    {
        var outcome = FixOutcome.Ok("backup-42", requiresReboot: true);

        Assert.True(outcome.Success);
        Assert.Equal("backup-42", outcome.BackupId);
        Assert.True(outcome.RequiresReboot);
    }

    [Fact]
    public void FixOutcome_Failed_HasMessageAndNoSuccess()
    {
        var outcome = FixOutcome.Failed("не удалось");

        Assert.False(outcome.Success);
        Assert.Equal("не удалось", outcome.Message);
        Assert.Null(outcome.BackupId);
    }
}
