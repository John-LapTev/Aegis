using Aegis.Core.Models;
using Aegis.Scanners.Probing;
using Aegis.Scanners.Threats;
using Xunit;

namespace Aegis.Scanners.Tests.Threats;

public sealed class SuspiciousTaskScannerTests
{
    [Fact]
    public async Task ScanAsync_EncodedCommand_IsDangerAndDisablable()
    {
        var scanner = new SuspiciousTaskScanner(new FakeProbe(
        [
            new SuspiciousTask { Path = @"\Updater", Name = "Updater", Action = "powershell -nop -w hidden -enc SQBFAFgA" },
        ]));

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);
        Assert.Equal(ScanGroup.Threats, finding.Group);
        Assert.Equal(Severity.Danger, finding.Severity);
        // Задача с путём — отключаемая (через task-disable).
        Assert.Equal("task-disable", finding.Data?.GetValueOrDefault("kind"));
        Assert.Equal(@"\Updater", finding.Data?.GetValueOrDefault("task"));
    }

    [Fact]
    public async Task ScanAsync_RunsFromTemp_IsWarning()
    {
        var scanner = new SuspiciousTaskScanner(new FakeProbe(
        [
            new SuspiciousTask { Path = @"\X", Name = "X", Action = @"C:\Users\u\AppData\Local\Temp\run.exe" },
        ]));

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);
        Assert.Equal(Severity.Warning, finding.Severity);
    }

    [Fact]
    public async Task ScanAsync_AppDataUpdater_IsInfoAndDisablable()
    {
        // Фоновое автообновление из AppData (Opera/Zoom/Yandex) — мягкая категория «Совет» (Info), но отключаемая.
        var scanner = new SuspiciousTaskScanner(new FakeProbe(
        [
            new SuspiciousTask
            {
                Path = @"\Opera GX Autoupdate", Name = "Opera GX Autoupdate",
                Action = @"C:\Users\u\AppData\Local\Programs\Opera GX\autoupdate.exe --scheduledtask",
            },
        ]));

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);
        Assert.Equal(Severity.Info, finding.Severity);
        Assert.Contains("Фоновое автообновление", finding.Title);
        Assert.Equal("task-disable", finding.Data?.GetValueOrDefault("kind"));
    }

    [Fact]
    public async Task ScanAsync_BenignCommand_IsSkipped()
    {
        // Кандидат от предфильтра, но без злоупотребления и не из Temp — не находка (без ложных тревог).
        var scanner = new SuspiciousTaskScanner(new FakeProbe(
        [
            new SuspiciousTask { Path = @"\Ok", Name = "Ok", Action = @"C:\Program Files\App\app.exe --update" },
        ]));

        Assert.Empty((await scanner.ScanAsync()).Findings);
    }

    private sealed class FakeProbe(IReadOnlyList<SuspiciousTask> tasks) : ISuspiciousTaskProbe
    {
        public Task<IReadOnlyList<SuspiciousTask>> FindAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(tasks);
    }
}
