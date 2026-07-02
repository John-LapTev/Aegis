using Aegis.Core.Models;

namespace Aegis.Core.Abstractions;

/// <summary>
/// Создание обратимых бэкапов ПЕРЕД правкой и их хранение для раздела «Бэкапы» (ADR 0002).
/// Реализация — Windows-специфична (System Restore, экспорт реестра) и живёт в слое бэкапа.
/// </summary>
public interface IRestorePointService
{
    /// <summary>Создать точку восстановления Windows перед пакетом изменений.</summary>
    Task<BackupRecord> CreateRestorePointAsync(string description, CancellationToken cancellationToken = default);

    /// <summary>Все доступные бэкапы (для списка в разделе «Бэкапы»), новые сверху.</summary>
    Task<IReadOnlyList<BackupRecord>> ListBackupsAsync(CancellationToken cancellationToken = default);

    /// <summary>Восстановить систему/настройку из выбранного бэкапа.</summary>
    Task RestoreAsync(string backupId, CancellationToken cancellationToken = default);
}
