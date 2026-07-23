using System.Globalization;
using System.Management;
using Aegis.Scanners.Probing;
using Aegis.System.Internal;

namespace Aegis.System.Probes;

/// <summary>
/// Реальный пробник обслуживания дисков: состояние и дата последнего запуска встроенной задачи Windows
/// «Оптимизация дисков», а также наличие твердотельного диска (WMI). Только читает.
/// </summary>
public sealed class DiskOptimizeProbe : IDiskOptimizeProbe
{
    private const string TaskName = "ScheduledDefrag";
    private const string TaskPath = @"\Microsoft\Windows\Defrag\";

    public async Task<DiskOptimizeState> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new DiskOptimizeState { ScheduleEnabled = true };
        }

        var (lastRun, enabled) = await ReadScheduleAsync(cancellationToken).ConfigureAwait(false);

        return new DiskOptimizeState
        {
            DaysSinceLastRun = lastRun is DateTime run ? Math.Max(0, (int)(DateTime.Now - run).TotalDays) : null,
            ScheduleEnabled = enabled,
            HasSolidStateDrive = HasSsd(),
        };
    }

    /// <summary>
    /// Дата последнего запуска и состояние задачи. Читаем через PowerShell в машинном формате даты:
    /// у schtasks вывод локализован, и разбирать его пришлось бы по языку системы.
    /// </summary>
    private static async Task<(DateTime? LastRun, bool Enabled)> ReadScheduleAsync(CancellationToken cancellationToken)
    {
        try
        {
            var script =
                $"$t = Get-ScheduledTask -TaskName '{TaskName}' -TaskPath '{TaskPath}' -ErrorAction Stop; " +
                "$i = $t | Get-ScheduledTaskInfo; " +
                "\"$($t.State)|$($i.LastRunTime.ToString('o'))\"";

            var output = await ProcessRunner.RunForOutputAsync(
                ProcessRunner.System(@"WindowsPowerShell\v1.0\powershell.exe"),
                $"-NoProfile -NonInteractive -Command \"{script}\"",
                cancellationToken).ConfigureAwait(false);

            var parts = output.Trim().Split('|');
            if (parts.Length < 2)
            {
                return (null, true);
            }

            // State: Disabled | Ready | Running. Всё, кроме Disabled, считаем рабочим расписанием.
            var enabled = !parts[0].Trim().Equals("Disabled", StringComparison.OrdinalIgnoreCase);
            var lastRun = DateTime.TryParse(parts[1].Trim(), CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var parsed) && parsed.Year > 2000
                ? parsed
                : (DateTime?)null;

            return (lastRun, enabled);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Не удалось прочитать — считаем, что всё в порядке: пугать человека на пустом месте не будем.
            return (null, true);
        }
    }

    /// <summary>Есть ли твердотельный диск (MediaType = 4 в таблице дисков Windows).</summary>
    private static bool HasSsd()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\Microsoft\Windows\Storage", "SELECT MediaType FROM MSFT_PhysicalDisk");

            foreach (var item in searcher.Get())
            {
                using var disk = (ManagementObject)item;
                if (disk["MediaType"] is not null
                    && Convert.ToInt32(disk["MediaType"], CultureInfo.InvariantCulture) == 4)
                {
                    return true;
                }
            }
        }
        catch (Exception)
        {
            // Не определили — объясним нейтрально, без упоминания типа диска.
        }

        return false;
    }
}
