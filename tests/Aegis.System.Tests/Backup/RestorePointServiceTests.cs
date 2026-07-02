using Aegis.Core.Models;
using Aegis.System.Backup;
using Xunit;

namespace Aegis.System.Tests.Backup;

/// <summary>
/// Регресс на скрытый дефект: служба «глотала» отказ System Restore и всегда отдавала «успех»,
/// из-за чего оркестратор не мог отличить реально созданную точку от несозданной. Теперь
/// <see cref="BackupRecord.Succeeded"/> честно отражает результат, а служба не падает, если SR недоступна.
/// </summary>
public sealed class RestorePointServiceTests
{
    private static RestorePointService NewService() =>
        new(new RegistryBackupStore(), new QuarantineStore(), new RegistryKeyBackupStore(),
            new ScheduledTaskBackupStore(), new AppxRemovalBackupStore());

    [Fact]
    public async Task CreateRestorePointAsync_DoesNotThrow_AndReturnsSystemRestoreRecord()
    {
        var service = NewService();

        var record = await service.CreateRestorePointAsync("Тестовая точка");

        Assert.NotNull(record);
        Assert.Equal(BackupKind.SystemRestorePoint, record.Kind);
        Assert.False(string.IsNullOrWhiteSpace(record.Id));
    }

    [Fact]
    public async Task CreateRestorePointAsync_WhenRestoreUnavailable_ReportsNotSucceeded()
    {
        // На не-Windows System Restore недоступна → честный Succeeded=false (а не ложный «успех»).
        // На Windows с включённой защitой точка создастся (Succeeded=true) — тогда тест неактуален.
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var service = NewService();

        var record = await service.CreateRestorePointAsync("Тестовая точка");

        Assert.False(record.Succeeded);
    }
}
