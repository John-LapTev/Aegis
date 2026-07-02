using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.System.Backup;
using Aegis.System.Internal;

namespace Aegis.System.Fixing;

/// <summary>
/// Обратимое отключение лишней задачи планировщика (телеметрия/реклама): сначала сохраняем запись
/// для возврата (<see cref="ScheduledTaskBackupStore"/>), затем <c>schtasks /change /tn … /disable</c>.
/// Вернуть задачу можно в разделе «Бэкапы». Задача не удаляется — только отключается (ADR 0002).
/// </summary>
public sealed class ScheduledTaskDisableFix : IFix
{
    private readonly ScheduledTaskBackupStore _backup;
    private readonly string _taskPath;
    private readonly string _taskName;

    public ScheduledTaskDisableFix(string findingId, string taskPath, string taskName, ScheduledTaskBackupStore backup)
    {
        FindingId = findingId;
        _taskPath = taskPath;
        _taskName = taskName;
        _backup = backup;
    }

    public string FindingId { get; }

    public ScanGroup Group => ScanGroup.Settings;

    public async Task<FixOutcome> ApplyAsync(CancellationToken cancellationToken = default)
    {
        var backupId = _backup.Backup(_taskPath, _taskName, "Отключение задачи: " + _taskName);
        if (backupId is null)
        {
            return FixOutcome.Failed("Не удалось подготовить возврат задачи — изменение отменено.");
        }

        var code = await ProcessRunner
            .RunAsync(ProcessRunner.System("schtasks.exe"), $"/change /tn \"{_taskPath}\" /disable", cancellationToken)
            .ConfigureAwait(false);

        // Часть системных задач телеметрии schtasks не берёт даже под админом — пробуем через PowerShell
        // Disable-ScheduledTask (он разбирает путь на папку+имя и иногда отключает там, где schtasks отказал).
        if (code != 0 && await TryPowerShellDisableAsync(cancellationToken).ConfigureAwait(false))
        {
            code = 0;
        }

        if (code != 0)
        {
            // Откатываем запись бэкапа — задача осталась включённой.
            _backup.Discard(backupId);
            return FixOutcome.Failed("Не удалось отключить задачу — её защищает Windows (нужны особые права " +
                                     "системы) или задача уже отключена. Можно отключить вручную в «Планировщике заданий».");
        }

        return FixOutcome.Ok(backupId);
    }

    private async Task<bool> TryPowerShellDisableAsync(CancellationToken cancellationToken)
    {
        var lastSlash = _taskPath.LastIndexOf('\\');
        var folder = lastSlash > 0 ? _taskPath[..(lastSlash + 1)] : "\\";
        var name = lastSlash >= 0 ? _taskPath[(lastSlash + 1)..] : _taskPath;
        var command = $"Disable-ScheduledTask -TaskPath '{folder}' -TaskName '{name}'";
        var code = await ProcessRunner
            .RunAsync(ProcessRunner.System(@"WindowsPowerShell\v1.0\powershell.exe"),
                $"-NoProfile -NonInteractive -Command \"{command}\"", cancellationToken)
            .ConfigureAwait(false);
        return code == 0;
    }
}
