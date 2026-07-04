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
    public const string DuplicateGroup = "duplicate-group";
    public const string FileDelete = "file-delete";
    public const string FolderDelete = "folder-delete";
    public const string FolderContents = "folder-contents";
    public const string FolderItemsDelete = "folder-items-delete";
    public const string NetworkReset = "network-reset";
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
