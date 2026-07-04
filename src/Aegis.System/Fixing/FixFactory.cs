using Microsoft.Win32;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.System.Backup;
using Aegis.System.Internal;

namespace Aegis.System.Fixing;

/// <summary>
/// Сопоставляет находку с готовым обратимым исправлением: реестровые правки приватности/настроек,
/// очистка мусора, отключение автозапуска (Run-значение или карантин файла из папки автозагрузки).
/// </summary>
public sealed class FixFactory : IFixFactory
{
    private readonly RegistryBackupStore _store;
    private readonly QuarantineStore _quarantine;
    private readonly RegistryKeyBackupStore _keyBackup;
    private readonly ScheduledTaskBackupStore _taskBackup;
    private readonly AppxRemovalBackupStore _appxBackup;

    public FixFactory(
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

    public bool CanFix(Finding finding) => Build(finding, permanentDelete: false) is not null;

    public IFix? CreateFix(Finding finding, bool permanentDelete = false) => Build(finding, permanentDelete);

    private IFix? Build(Finding finding, bool permanentDelete)
    {
        // Включение защиты системы (точек восстановления) — «зонтик» обратимости для всей починки.
        if (finding.Id == "system-restore-disabled")
        {
            return new SystemRestoreEnableFix(finding.Id, _store);
        }

        // Очистка хранилища компонентов Windows (DISM).
        if (finding.Data?.GetValueOrDefault(FindingDataKeys.Kind) == FindingKinds.DismCleanup)
        {
            return new DismComponentCleanupFix(finding.Id);
        }

        // Починка системных файлов (SFC/DISM).
        if (finding.Data?.GetValueOrDefault(FindingDataKeys.Kind) == FindingKinds.SfcDismRepair)
        {
            return new SfcDismRepairFix(finding.Id);
        }

        // Сброс сетевых настроек.
        if (finding.Data?.GetValueOrDefault(FindingDataKeys.Kind) == FindingKinds.NetworkReset)
        {
            return new NetworkResetFix(finding.Id);
        }

        // Поиск/установка драйвера средствами Windows.
        if (finding.Data?.GetValueOrDefault(FindingDataKeys.Kind) == FindingKinds.DriverSearch)
        {
            return new DriverSearchFix(finding.Id);
        }

        // Включение отключённого устройства (микрофон и т.п.).
        if (finding.Data?.GetValueOrDefault(FindingDataKeys.Kind) == FindingKinds.DeviceEnable
            && finding.Data.TryGetValue(FindingDataKeys.DeviceId, out var enableId))
        {
            return new DeviceEnableFix(finding.Id, enableId);
        }

        // Тихая установка фирменной утилиты через встроенный установщик Windows (winget).
        if (finding.Data?.GetValueOrDefault(FindingDataKeys.Kind) == FindingKinds.WingetInstall
            && finding.Data.TryGetValue("winget", out var wingetArgs))
        {
            return new WingetInstallFix(finding.Id, finding.Group, wingetArgs);
        }

        // Удаление файла (большой/дубль/мусор) — в Корзину Windows (освобождает место, восстанавливается из Корзины).
        if (finding.Data?.GetValueOrDefault(FindingDataKeys.Kind) == FindingKinds.FileDelete
            && finding.Data.TryGetValue("path", out var filePath))
        {
            return new RecycleBinFix(finding.Id, finding.Group, filePath, permanentDelete);
        }

        // Удаление ПАПКИ-остатка удалённой программы — в Корзину Windows (обратимо).
        if (finding.Data?.GetValueOrDefault(FindingDataKeys.Kind) == FindingKinds.FolderDelete
            && finding.Data.TryGetValue("path", out var folderPath))
        {
            return new FolderRecycleFix(finding.Id, finding.Group, folderPath, permanentDelete);
        }

        // Удаление выбранных элементов внутри большой папки (файлы/подпапки) — в Корзину или навсегда.
        if (finding.Data?.GetValueOrDefault(FindingDataKeys.Kind) == FindingKinds.FolderItemsDelete
            && finding.Data.TryGetValue("paths", out var itemPaths))
        {
            return new FolderItemsDeleteFix(finding.Id, finding.Group,
                itemPaths.Split('|', StringSplitOptions.RemoveEmptyEntries), permanentDelete);
        }

        // Удаление записи реестра (осиротевшей) — с экспортом ветки.
        if (finding.Data?.GetValueOrDefault(FindingDataKeys.Kind) == FindingKinds.RegistryDelete
            && finding.Data.TryGetValue("hive", out var regHive)
            && finding.Data.TryGetValue("subkey", out var regSubKey))
        {
            return new RegistryKeyDeleteFix(finding.Id, regHive, regSubKey, _keyBackup);
        }

        // Очистка мусора — по списку путей из находки (но НЕ группа дублей: у неё свой раскрывающийся список).
        if (finding.Group == ScanGroup.Junk
            && finding.Data is not null
            && !finding.Data.ContainsKey(FindingDataKeys.Kind)
            && finding.Data.TryGetValue("paths", out var paths))
        {
            return new JunkCleanupFix(finding.Id, paths.Split('|', StringSplitOptions.RemoveEmptyEntries), permanentDelete);
        }

        // Отключение автозапуска — по координатам из находки.
        if (finding.Group == ScanGroup.Autostart
            && finding.Data is not null
            && finding.Data.ContainsKey(FindingDataKeys.Kind))
        {
            return new AutostartDisableFix(finding.Id, finding.Data, _store, _quarantine);
        }

        // Перезагрузка ПК — по кнопке в находке «Нужна перезагрузка».
        if (finding.Data?.GetValueOrDefault(FindingDataKeys.Kind) == FindingKinds.Reboot)
        {
            return new RebootFix(finding.Id);
        }

        // Остановка процесса — по PID из находки. Из вкладки «Процессы» (по группе) либо из «Угроз»
        // (явный kind=process-stop — остановить майнер по сетевому подключению).
        if (finding.Data is not null
            && (finding.Group == ScanGroup.Processes || finding.Data.GetValueOrDefault(FindingDataKeys.Kind) == FindingKinds.ProcessStop)
            && finding.Data.TryGetValue("pid", out var pid)
            && int.TryParse(pid, out var processId))
        {
            return new ProcessStopFix(finding.Id, processId);
        }

        // Отключение лишней задачи планировщика — обратимо (запись для возврата + schtasks /disable).
        if (finding.Data?.GetValueOrDefault(FindingDataKeys.Kind) == FindingKinds.TaskDisable
            && finding.Data.TryGetValue("task", out var taskPath))
        {
            return new ScheduledTaskDisableFix(finding.Id, taskPath, finding.Title, _taskBackup);
        }

        // Удаление встроенного UWP-приложения — обратимо (запись для возврата + Remove-AppxPackage).
        if (finding.Data?.GetValueOrDefault(FindingDataKeys.Kind) == FindingKinds.AppxRemove
            && finding.Data.TryGetValue("package", out var appxPackage))
        {
            var appName = finding.Data.GetValueOrDefault("name") ?? finding.Title;
            return new AppxRemoveFix(finding.Id, appxPackage, appName, _appxBackup);
        }

        // Отключение службы (Start=4) — обратимо через бэкап значения.
        if (finding.Data is not null
            && finding.Data.TryGetValue(FindingDataKeys.Kind, out var kind) && kind == FindingKinds.ServiceDisable
            && finding.Data.TryGetValue("service", out var service))
        {
            return new RegistryValueFix(_store, finding.Id, finding.Group, RegistryHive.LocalMachine,
                $@"SYSTEM\CurrentControlSet\Services\{service}", "Start", 4, RegistryValueKind.DWord,
                "Отключение службы " + service, requiresReboot: true);
        }

        // Переключатель приватности — координаты в находке (обратимо через бэкап значения).
        if (finding.Data?.GetValueOrDefault(FindingDataKeys.Kind) == FindingKinds.RegistryToggle
            && finding.Data.TryGetValue("hive", out var toggleHive)
            && finding.Data.TryGetValue("subkey", out var toggleSubKey)
            && finding.Data.TryGetValue("name", out var toggleName)
            && finding.Data.TryGetValue("value", out var toggleValueText)
            && int.TryParse(toggleValueText, out var toggleValue))
        {
            var hive = RegistryHiveNames.ToHive(toggleHive);
            return new RegistryValueFix(_store, finding.Id, finding.Group, hive, toggleSubKey, toggleName,
                toggleValue, RegistryValueKind.DWord, "Приватность: " + finding.Title);
        }

        return BuildRegistryFix(finding);
    }

    private IFix? BuildRegistryFix(Finding finding) => finding.Id switch
    {
        "privacy-telemetry-full" => Reg(finding, RegistryHive.LocalMachine,
            @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 1,
            "Снижение телеметрии до базового уровня"),
        "settings-firewall-off" => Reg(finding, RegistryHive.LocalMachine,
            @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\StandardProfile", "EnableFirewall", 1,
            "Включение брандмауэра Windows"),
        "settings-uac-off" => Reg(finding, RegistryHive.LocalMachine,
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableLUA", 1,
            "Включение контроля учётных записей (UAC)", requiresReboot: true),
        "settings-updates-off" => Reg(finding, RegistryHive.LocalMachine,
            @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", "NoAutoUpdate", 0,
            "Включение автоматических обновлений Windows"),
        "settings-rdp-on" => Reg(finding, RegistryHive.LocalMachine,
            @"SYSTEM\CurrentControlSet\Control\Terminal Server", "fDenyTSConnections", 1,
            "Отключение удалённого рабочего стола"),
        _ => null,
    };

    private IFix Reg(
        Finding finding,
        RegistryHive hive,
        string subKey,
        string valueName,
        int value,
        string description,
        bool requiresReboot = false) =>
        new RegistryValueFix(_store, finding.Id, finding.Group, hive, subKey, valueName, value,
            RegistryValueKind.DWord, description, requiresReboot);
}
