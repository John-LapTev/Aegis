namespace Aegis.Scanners.Probing;

/// <summary>Откуда программа попадает в автозапуск.</summary>
public enum AutostartLocation
{
    /// <summary>Ветка реестра Run (HKLM/HKCU ...\CurrentVersion\Run).</summary>
    RegistryRun,

    /// <summary>Папка «Автозагрузка» (Startup).</summary>
    StartupFolder,

    /// <summary>Задача в Планировщике заданий.</summary>
    ScheduledTask,

    /// <summary>Служба Windows с автозапуском.</summary>
    Service,
}
