using System.Runtime.InteropServices;

namespace Aegis.System.Internal;

/// <summary>
/// Планирует удаление файла при следующей загрузке Windows (MoveFileEx с MOVEFILE_DELAY_UNTIL_REBOOT). Нужно, когда
/// файл вредоноса заперт запущенным процессом и не удаляется прямо сейчас — на следующем старте (процесса уже нет)
/// система его удалит. Список отложенных удалений виден в реестре (PendingFileRenameOperations).
/// </summary>
internal static class PendingDelete
{
    private const int MoveFileDelayUntilReboot = 0x4;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MoveFileEx(string existingFileName, string? newFileName, int flags);

    /// <summary>Пометить файл на удаление при следующей перезагрузке. true — запланировано успешно.</summary>
    public static bool ScheduleDeleteOnReboot(string filePath)
    {
        try
        {
            return MoveFileEx(filePath, null, MoveFileDelayUntilReboot);
        }
        catch (global::System.Exception)
        {
            return false;
        }
    }
}
