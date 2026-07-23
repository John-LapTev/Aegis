namespace Aegis.Core.Models;

/// <summary>
/// Значения ключа <c>Data["kind"]</c> находки — связывают сканер (кто ставит) и фабрику исправлений
/// (кто по нему выбирает действие). Раньше были «магическими строками» в обоих местах: опечатка тихо
/// ломала подбор починки. Теперь — один источник, защита от опечаток.
/// </summary>
public static class FindingKinds
{
    public const string AppxRemove = "appx-remove";
    public const string AutostartRun = "autostart-run";
    public const string AutostartStartup = "autostart-startup";
    public const string DeviceEnable = "device-enable";
    public const string DismCleanup = "dism-cleanup";
    public const string DriverList = "driver-list";
    public const string DriverSearch = "driver-search";
    public const string DriverWuInstall = "driver-wu-install";
    /// <summary>Удаление старых версий драйверов из хранилища Windows (список пакетов — в <see cref="FindingDataKeys.Items"/>).</summary>
    public const string DriverPackageDelete = "driver-package-delete";
    /// <summary>Включение брандмауэра в перечисленных профилях сети (ключ <see cref="FindingDataKeys.Profiles"/>).</summary>
    public const string FirewallEnable = "firewall-enable";
    public const string DuplicateGroup = "duplicate-group";
    public const string FileDelete = "file-delete";
    public const string FolderDelete = "folder-delete";
    public const string FolderContents = "folder-contents";
    public const string FolderItemsDelete = "folder-items-delete";
    public const string NetworkReset = "network-reset";
    /// <summary>Сжатие (уплотнение) внутренних баз браузера — пути в <see cref="FindingDataKeys.Paths"/>.</summary>
    public const string SqliteVacuum = "sqlite-vacuum";
    /// <summary>Обслуживание дисков средствами Windows: TRIM для SSD, дефрагментация для жёстких дисков.</summary>
    public const string DiskOptimize = "disk-optimize";
    /// <summary>Обновление всех программ через встроенный установщик Windows (winget upgrade --all).</summary>
    public const string ProgramUpgradeAll = "program-upgrade-all";
    /// <summary>Удаление записи из переменной Path (обратимо: прежний список целиком в бэкапе).</summary>
    public const string PathEntryRemove = "path-entry-remove";
    /// <summary>Отключение пункта контекстного меню (обратимо: значение-пометка, ключ не удаляется).</summary>
    public const string ContextMenuDisable = "context-menu-disable";
    /// <summary>Снятие чужого ограничения Windows: удаление значения политики (координаты — в данных находки).</summary>
    public const string PolicyClear = "policy-clear";
    public const string ProcessStop = "process-stop";
    /// <summary>Полное удаление майнера: остановка дерева процессов + снятие автозапуска + карантин файла (обратимо).</summary>
    public const string MinerRemove = "miner-remove";
    public const string Reboot = "reboot";
    public const string RegistryDelete = "registry-delete";
    public const string RegistryToggle = "registry-toggle";
    public const string ServiceDisable = "service-disable";
    public const string SfcDismRepair = "sfc-dism-repair";
    public const string TaskDisable = "task-disable";
    public const string WingetInstall = "winget-install";
}
