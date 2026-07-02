using System.Management;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;

namespace Aegis.System.Backup;

/// <summary>
/// Обратимость правок (ADR 0002): точка восстановления Windows (System Restore, best-effort) как
/// «зонтик» + точные бэкапы значений реестра через <see cref="RegistryBackupStore"/>. Если System
/// Restore недоступна — обратимость всё равно обеспечивает точечный бэкап значения.
/// </summary>
public sealed class RestorePointService : IRestorePointService
{
    private readonly RegistryBackupStore _store;
    private readonly QuarantineStore _quarantine;
    private readonly RegistryKeyBackupStore _keyBackup;
    private readonly ScheduledTaskBackupStore _taskBackup;
    private readonly AppxRemovalBackupStore _appxBackup;

    public RestorePointService(
        RegistryBackupStore store,
        QuarantineStore quarantine,
        RegistryKeyBackupStore keyBackup,
        ScheduledTaskBackupStore taskBackup,
        AppxRemovalBackupStore appxBackup)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(quarantine);
        ArgumentNullException.ThrowIfNull(keyBackup);
        ArgumentNullException.ThrowIfNull(taskBackup);
        ArgumentNullException.ThrowIfNull(appxBackup);
        _store = store;
        _quarantine = quarantine;
        _keyBackup = keyBackup;
        _taskBackup = taskBackup;
        _appxBackup = appxBackup;
    }

    public async Task<BackupRecord> CreateRestorePointAsync(string description, CancellationToken cancellationToken = default)
    {
        var created = false;
        try
        {
            // VSS-точка восстановления может создаваться ДОЛГО или зависать на проблемной системе.
            // Ограничиваем по времени, чтобы не блокировать починку навсегда — иначе кнопки «не работают».
            // Не успели/ошибка — продолжаем: обратимость обеспечивает точечный бэкап (Корзина/экспорт ветки).
            created = await Task.Run(() => TryCreateSystemRestorePoint(description), cancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(25), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            created = false; // таймаут или сбой VSS — не критично
        }

        var record = new BackupRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            Kind = BackupKind.SystemRestorePoint,
            Description = created
                ? description
                : description + " (точка восстановления недоступна — изменения обратимы по бэкапу настроек)",
            CreatedAt = DateTimeOffset.UtcNow,
            AffectedAreas = ["Система"],
            // Честно сообщаем оркестратору, создана ли точка на самом деле. Если нет — пакет всё равно
            // выполняется (каждая правка делает свой точечный бэкап), но «зонтик» не выдаётся за созданный.
            Succeeded = created,
        };

        return record;
    }

    public Task<IReadOnlyList<BackupRecord>> ListBackupsAsync(CancellationToken cancellationToken = default)
    {
        var registry = _store.List()
            .Select(b => new BackupRecord
            {
                Id = b.Id,
                Kind = BackupKind.SettingSnapshot,
                Description = b.Description,
                CreatedAt = b.CreatedAt,
                AffectedAreas = [$@"{b.Hive}\{b.SubKey}"],
            });

        var quarantine = _quarantine.List()
            .Select(q => new BackupRecord
            {
                Id = q.Id,
                Kind = BackupKind.FileQuarantine,
                Description = q.Description,
                CreatedAt = q.CreatedAt,
                AffectedAreas = [q.OriginalPath],
            });

        var keys = _keyBackup.List()
            .Select(k => new BackupRecord
            {
                Id = k.Id,
                Kind = BackupKind.RegistryExport,
                Description = k.Description,
                CreatedAt = k.CreatedAt,
                AffectedAreas = [k.KeyPath],
            });

        var tasks = _taskBackup.List()
            .Select(t => new BackupRecord
            {
                Id = t.Id,
                Kind = BackupKind.SettingSnapshot,
                Description = t.Description,
                CreatedAt = t.CreatedAt,
                AffectedAreas = [t.TaskName],
            });

        var appx = _appxBackup.List()
            .Select(a => new BackupRecord
            {
                Id = a.Id,
                Kind = BackupKind.SettingSnapshot,
                Description = a.Description + " (вернуть из Microsoft Store)",
                CreatedAt = a.CreatedAt,
                AffectedAreas = [a.AppName],
            });

        var records = registry.Concat(quarantine).Concat(keys).Concat(tasks).Concat(appx)
            .OrderByDescending(r => r.CreatedAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<BackupRecord>>(records);
    }

    public Task RestoreAsync(string backupId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupId);

        // Идентификаторы уникальны; каждый стор восстанавливает только «свой» бэкап и возвращает true.
        // Оператор | (не короткое замыкание) — чтобы опросить все сторы. Если бэкап потерян, «свой» стор
        // бросит понятную ошибку. Если id не знает НИКТО — это не тихий успех, а честная ошибка отката.
        var handled =
            _store.Restore(backupId)
            | _quarantine.Restore(backupId)
            | _keyBackup.Restore(backupId)
            | _taskBackup.Restore(backupId)
            | _appxBackup.Restore(backupId);

        if (!handled)
        {
            throw new InvalidOperationException(
                "Для этого изменения не найден бэкап — автоматический откат недоступен. " +
                "Если это была системная правка, можно откатиться через «Восстановление системы» Windows.");
        }

        return Task.CompletedTask;
    }

    private static bool TryCreateSystemRestorePoint(string description)
    {
        try
        {
            using var systemRestore = new ManagementClass(@"\\.\root\default", "SystemRestore", null);
            using var inParams = systemRestore.GetMethodParameters("CreateRestorePoint");
            inParams["Description"] = description;
            inParams["RestorePointType"] = 0;   // APPLICATION_INSTALL
            inParams["EventType"] = 100;        // BEGIN_SYSTEM_CHANGE
            using var outParams = systemRestore.InvokeMethod("CreateRestorePoint", inParams, null);
            return (outParams?["ReturnValue"]?.ToString() ?? string.Empty) == "0";
        }
        catch (Exception)
        {
            // SR выключена / нет прав / лимит частоты — не критично (точечный бэкап обеспечивает откат).
            return false;
        }
    }
}
