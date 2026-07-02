namespace Aegis.Core.Remediation;

/// <summary>Шаг плана удаления вредоносного ПО (например, майнера). Все шаги обратимы/безопасны.</summary>
public enum RemovalStep
{
    /// <summary>Бэкап ПЕРЕД действиями: точка восстановления + экспорт затрагиваемого автозапуска.</summary>
    Backup,

    /// <summary>Остановить все процессы вредоноса (кроме критических системных).</summary>
    StopProcesses,

    /// <summary>Снять автозапуск (реестр Run / задача / служба), чтобы не вернулся после ребута.</summary>
    DisableAutostart,

    /// <summary>Перенести файлы в карантин (не безвозвратное удаление).</summary>
    QuarantineFiles,

    /// <summary>Пометить файл на удаление при следующей загрузке Windows (когда процессов уже нет).</summary>
    ScheduleDeleteOnReboot,

    /// <summary>Запросить перезагрузку, чтобы завершить удаление.</summary>
    RequestReboot,
}
