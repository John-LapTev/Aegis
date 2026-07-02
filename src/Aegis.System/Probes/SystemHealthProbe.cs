using Microsoft.Win32;
using Aegis.Scanners.Probing;
using Aegis.System.Internal;

namespace Aegis.System.Probes;

/// <summary>Реальный пробник здоровья системы: диски, защита восстановления, ожидание ребута.</summary>
public sealed class SystemHealthProbe : ISystemHealthProbe
{
    public Task<SystemHealthSnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        var rebootReason = ReadPendingRebootReason();
        var snapshot = new SystemHealthSnapshot
        {
            Drives = ReadDrives(),
            RestoreProtectionEnabled = ReadRestoreProtection(),
            PendingReboot = rebootReason is not null,
            PendingRebootReason = rebootReason,
        };

        return Task.FromResult(snapshot);
    }

    private static IReadOnlyList<DriveSpace> ReadDrives()
    {
        var drives = new List<DriveSpace>();
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (drive is { IsReady: true, DriveType: DriveType.Fixed })
                    {
                        drives.Add(new DriveSpace
                        {
                            Name = drive.Name.TrimEnd('\\'),
                            FreeBytes = drive.AvailableFreeSpace,
                            TotalBytes = drive.TotalSize,
                        });
                    }
                }
                catch (Exception)
                {
                    // Диск недоступен — пропускаем.
                }
            }
        }
        catch (Exception)
        {
            // Не удалось перечислить диски.
        }

        return drives;
    }

    private static bool ReadRestoreProtection()
    {
        // Политика отключения System Restore (если задана). Отсутствие → считаем включённой.
        var disabled = RegistryReader.GetDword(RegistryHive.LocalMachine,
            @"SOFTWARE\Policies\Microsoft\Windows NT\SystemRestore", "DisableSR");
        return disabled != 1;
    }

    /// <summary>Причина ожидающей перезагрузки простыми словами; null — перезагрузка не нужна (все изменения уже применены).</summary>
    private static string? ReadPendingRebootReason()
    {
        if (RegistryReader.KeyExists(RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired"))
        {
            return "обновления Windows";
        }

        if (RegistryReader.KeyExists(RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending"))
        {
            return "установка или удаление компонентов Windows";
        }

        // ВАЖНО: PendingFileRenameOperations НЕ проверяем — это самый ненадёжный сигнал: десятки обычных
        // программ (антивирусы, инсталляторы, чистильщики) оставляют там безобидные «переименовать при загрузке»
        // записи, которые НЕ требуют перезагрузки. Из-за него «Нужна перезагрузка» загоралась сразу после
        // включения ПК на пустом месте (ложное срабатывание). Оставляем только надёжные сигналы выше.
        return null;
    }
}
