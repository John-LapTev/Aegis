namespace Aegis.Core.Models;

/// <summary>Результат применения исправления к одной находке.</summary>
public sealed record FixOutcome
{
    /// <summary>Исправление применено успешно.</summary>
    public required bool Success { get; init; }

    /// <summary>Идентификатор бэкапа, созданного ПЕРЕД правкой (для отката). Null, если бэкап не делался.</summary>
    public string? BackupId { get; init; }

    /// <summary>Требуется ли перезагрузка, чтобы изменение вступило в силу.</summary>
    public bool RequiresReboot { get; init; }

    /// <summary>Понятное пользователю сообщение (особенно при ошибке) — на русском.</summary>
    public string? Message { get; init; }

    /// <summary>
    /// Успешный результат с ОБРАТИМЫМ бэкапом: <paramref name="backupId"/> должен указывать на реальную запись
    /// в одном из бэкап-хранилищ (экспорт ветки реестра, карантин файла и т.п.), которую умеет восстановить
    /// <c>IRestorePointService.RestoreAsync</c>. Для такой находки в UI появляется кнопка «Вернуть».
    /// </summary>
    public static FixOutcome Ok(string backupId, bool requiresReboot = false) =>
        new() { Success = true, BackupId = backupId, RequiresReboot = requiresReboot };

    /// <summary>
    /// Успех БЕЗ обратимого бэкапа: правку нельзя откатить средствами программы (установка, ремонт SFC/DISM,
    /// удаление навсегда, остановка процесса, перезагрузка). BackupId=null → кнопка «Вернуть» НЕ показывается,
    /// чтобы не обещать откат, которого нет.
    /// </summary>
    public static FixOutcome OkWithoutBackup(bool requiresReboot = false) =>
        new() { Success = true, BackupId = null, RequiresReboot = requiresReboot };

    /// <summary>Результат с ошибкой.</summary>
    public static FixOutcome Failed(string message) =>
        new() { Success = false, Message = message };
}
