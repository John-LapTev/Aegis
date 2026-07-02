using Aegis.Core.Models;
using Aegis.Scanners.Probing;
using Aegis.Scanners.SystemInfo;
using Xunit;

namespace Aegis.Scanners.Tests.SystemInfo;

public sealed class SystemScannerTests
{
    private const long Gb = 1024L * 1024 * 1024;

    [Fact]
    public async Task ScanAsync_WhenRestoreProtectionDisabled_ReportsDanger()
    {
        var scanner = new SystemScanner(new FakeProbe(new SystemHealthSnapshot
        {
            Drives = [HealthyDrive()],
            RestoreProtectionEnabled = false,
            PendingReboot = false,
        }));

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings, f => f.Id == "system-restore-disabled");
        Assert.Equal(Severity.Danger, finding.Severity);
    }

    [Fact]
    public async Task ScanAsync_WhenDiskAlmostFull_ReportsWarning()
    {
        var scanner = new SystemScanner(new FakeProbe(new SystemHealthSnapshot
        {
            Drives = [new DriveSpace { Name = "C:", FreeBytes = 5 * Gb, TotalBytes = 500 * Gb }],
            RestoreProtectionEnabled = true,
            PendingReboot = false,
        }));

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings, f => f.Id == "system-low-disk-C:");
        Assert.Equal(Severity.Warning, finding.Severity);
    }

    [Fact]
    public async Task ScanAsync_WhenHealthy_ReportsRebootNotNeeded()
    {
        var scanner = new SystemScanner(new FakeProbe(new SystemHealthSnapshot
        {
            Drives = [HealthyDrive()],
            RestoreProtectionEnabled = true,
            PendingReboot = false,
        }));

        var result = await scanner.ScanAsync();

        // Здоров → единственная находка: явный зелёный вердикт «перезагрузка не нужна».
        var finding = Assert.Single(result.Findings);
        Assert.Equal("system-pending-reboot", finding.Id);
        Assert.Equal(Severity.Ok, finding.Severity);
    }

    [Fact]
    public async Task ScanAsync_WhenPendingReboot_ReportsWarningWithReason()
    {
        var scanner = new SystemScanner(new FakeProbe(new SystemHealthSnapshot
        {
            Drives = [HealthyDrive()],
            RestoreProtectionEnabled = true,
            PendingReboot = true,
            PendingRebootReason = "обновления Windows",
        }));

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings, f => f.Id == "system-pending-reboot");
        Assert.Equal(Severity.Warning, finding.Severity);
        Assert.Contains("обновления Windows", finding.Detail);
    }

    private static DriveSpace HealthyDrive() =>
        new() { Name = "C:", FreeBytes = 300 * Gb, TotalBytes = 500 * Gb };

    private sealed class FakeProbe(SystemHealthSnapshot snapshot) : ISystemHealthProbe
    {
        public Task<SystemHealthSnapshot> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(snapshot);
    }
}
