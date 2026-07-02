using Aegis.Core.Abstractions;
using Aegis.Core.Fixing;
using Aegis.Core.Models;
using Xunit;

namespace Aegis.Core.Tests.Fixing;

public sealed class FixOrchestratorTests
{
    [Fact]
    public async Task ApplyAsync_CreatesRestorePointThenAppliesAllFixes()
    {
        var restore = new FakeRestoreService();
        var orchestrator = new FixOrchestrator(restore);

        var result = await orchestrator.ApplyAsync(
            [new SucceedingFix("a"), new SucceedingFix("b")],
            "Перед починкой автозапуска (2 пункта)");

        Assert.False(result.Aborted);
        Assert.Equal(1, restore.CreateRestorePointCalls);
        Assert.Equal("rp-1", result.RestorePointId);
        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
    }

    [Fact]
    public async Task ApplyAsync_WhenRestorePointThrows_AbortsWithoutApplyingFixes()
    {
        var fix = new SucceedingFix("a");
        var orchestrator = new FixOrchestrator(new FailingRestoreService());

        var result = await orchestrator.ApplyAsync([fix], "Пакет правок");

        Assert.True(result.Aborted);
        Assert.Empty(result.Outcomes);
        Assert.False(fix.WasApplied);
        Assert.NotNull(result.Message);
    }

    [Fact]
    public async Task ApplyAsync_WhenSystemRestoreUnavailable_StillAppliesFixesButReportsNoRestorePoint()
    {
        // System Restore выключена в Windows: точка не создана (Succeeded=false), но не исключение.
        // Пакет должен выполниться (обратимость держится на точечных бэкапах правок), а RestorePointId —
        // честно null, чтобы UI не предлагал откат к несуществующей точке.
        var fix = new SucceedingFix("a");
        var orchestrator = new FixOrchestrator(new UnavailableRestoreService());

        var result = await orchestrator.ApplyAsync([fix], "Пакет правок");

        Assert.False(result.Aborted);
        Assert.True(fix.WasApplied);
        Assert.Equal(1, result.SuccessCount);
        Assert.Null(result.RestorePointId);
    }

    [Fact]
    public async Task ApplyAsync_ContinuesAfterFailingFix_AndCountsBoth()
    {
        var orchestrator = new FixOrchestrator(new FakeRestoreService());

        var result = await orchestrator.ApplyAsync(
            [new ThrowingFix("a"), new SucceedingFix("b")],
            "Пакет правок");

        Assert.Equal(2, result.Outcomes.Count);
        Assert.Equal(1, result.FailureCount);
        Assert.Equal(1, result.SuccessCount);
    }

    [Fact]
    public async Task ApplyAsync_RequiresReboot_WhenAnyFixRequiresIt()
    {
        var orchestrator = new FixOrchestrator(new FakeRestoreService());

        var result = await orchestrator.ApplyAsync(
            [new SucceedingFix("a"), new SucceedingFix("b", requiresReboot: true)],
            "Пакет правок");

        Assert.True(result.RequiresReboot);
    }

    [Fact]
    public async Task ApplyAsync_WithNoFixes_DoesNotCreateRestorePoint()
    {
        var restore = new FakeRestoreService();
        var orchestrator = new FixOrchestrator(restore);

        var result = await orchestrator.ApplyAsync([], "Пустой пакет");

        Assert.False(result.Aborted);
        Assert.Empty(result.Outcomes);
        Assert.Equal(0, restore.CreateRestorePointCalls);
    }

    private sealed class SucceedingFix(string findingId, bool requiresReboot = false) : IFix
    {
        public string FindingId => findingId;

        public ScanGroup Group => ScanGroup.Autostart;

        public bool WasApplied { get; private set; }

        public Task<FixOutcome> ApplyAsync(CancellationToken cancellationToken = default)
        {
            WasApplied = true;
            return Task.FromResult(FixOutcome.Ok($"bk-{findingId}", requiresReboot));
        }
    }

    private sealed class ThrowingFix(string findingId) : IFix
    {
        public string FindingId => findingId;

        public ScanGroup Group => ScanGroup.Autostart;

        public Task<FixOutcome> ApplyAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("сбой правки");
    }

    private sealed class FakeRestoreService : IRestorePointService
    {
        public int CreateRestorePointCalls { get; private set; }

        public Task<BackupRecord> CreateRestorePointAsync(string description, CancellationToken cancellationToken = default)
        {
            CreateRestorePointCalls++;
            return Task.FromResult(NewRecord("rp-1", BackupKind.SystemRestorePoint, description));
        }

        public Task<IReadOnlyList<BackupRecord>> ListBackupsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BackupRecord>>([]);

        public Task RestoreAsync(string backupId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        private static BackupRecord NewRecord(string id, BackupKind kind, string description) => new()
        {
            Id = id,
            Kind = kind,
            Description = description,
            CreatedAt = DateTimeOffset.UnixEpoch,
            AffectedAreas = [],
        };
    }

    private sealed class UnavailableRestoreService : IRestorePointService
    {
        public Task<BackupRecord> CreateRestorePointAsync(string description, CancellationToken cancellationToken = default) =>
            Task.FromResult(new BackupRecord
            {
                Id = "rp-unavailable",
                Kind = BackupKind.SystemRestorePoint,
                Description = description,
                CreatedAt = DateTimeOffset.UnixEpoch,
                AffectedAreas = [],
                Succeeded = false,
            });

        public Task<IReadOnlyList<BackupRecord>> ListBackupsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BackupRecord>>([]);

        public Task RestoreAsync(string backupId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FailingRestoreService : IRestorePointService
    {
        public Task<BackupRecord> CreateRestorePointAsync(string description, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("служба теневого копирования недоступна");

        public Task<IReadOnlyList<BackupRecord>> ListBackupsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BackupRecord>>([]);

        public Task RestoreAsync(string backupId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
