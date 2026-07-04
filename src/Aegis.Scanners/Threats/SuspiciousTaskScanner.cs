using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Internal;
using Aegis.Scanners.Probing;

namespace Aegis.Scanners.Threats;

/// <summary>
/// Подозрительные задачи планировщика (группа <see cref="ScanGroup.Threats"/>). Явный приём злоупотребления в
/// команде (LOLBin/закодированный запуск) — «Опасно»; запуск из Temp/AppData — «Внимание». Задачу можно ОТКЛЮЧИТЬ
/// обратимо (через <c>task-disable</c>), если у неё есть путь. Известные системные задачи сюда не попадают —
/// фильтр реагирует только на подозрительную команду.
/// </summary>
public sealed class SuspiciousTaskScanner : IScanner
{
    private readonly ISuspiciousTaskProbe _probe;

    public SuspiciousTaskScanner(ISuspiciousTaskProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    public ScanGroup Group => ScanGroup.Threats;

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var tasks = await _probe.FindAsync(cancellationToken).ConfigureAwait(false);

        var findings = tasks.Select(Classify).OfType<Finding>().ToList();
        return new ScanResult { Group = ScanGroup.Threats, Findings = findings };
    }

    private static Finding? Classify(SuspiciousTask task)
    {
        var abuse = LolBinHeuristics.DetectAbuse(task.Action);
        var fromTemp = task.Action.Contains(@"\temp\", StringComparison.OrdinalIgnoreCase)
                       || task.Action.Contains("%temp%", StringComparison.OrdinalIgnoreCase);
        var fromAppData = task.Action.Contains(@"\appdata\", StringComparison.OrdinalIgnoreCase);

        if (abuse is null && !fromTemp && !fromAppData)
        {
            return null; // предфильтр был широким — здесь отсеиваем неопасное.
        }

        var canDisable = task.Path.StartsWith('\\');
        var data = canDisable
            ? new Dictionary<string, string> { [FindingDataKeys.Kind] = FindingKinds.TaskDisable, ["task"] = task.Path }
            : null;
        var disableHint = canDisable ? " Можно отключить кнопкой ниже (обратимо — задача просто перестанет запускаться)." : string.Empty;

        // Три уровня: явное злоупотребление = угроза; запуск из Temp = необычно; AppData без злоупотребления =
        // обычное фоновое автообновление (Opera/Zoom/Yandex и пр.) — НЕ пугаем, но даём отключить, если не нужно.
        var (severity, title, explain) = abuse is not null
            ? (Severity.Danger,
               $"Подозрительная задача: {task.Name}",
               $"Задача планировщика «{task.Name}» запускает подозрительную команду ({abuse}). Планировщик — " +
               "частый способ, которым вирусы и майнеры закрепляются (запуск по расписанию или при входе). Проверь " +
               $"компьютер антивирусом (полная проверка Защитником).{disableHint}")
            : fromTemp
                ? (Severity.Warning,
                   $"Необычная задача: {task.Name}",
                   $"Задача «{task.Name}» запускается из временной папки (Temp) — обычные программы так делают редко. " +
                   $"Если ты её не создавал — стоит присмотреться.{disableHint}")
                : (Severity.Info,
                   $"Фоновое автообновление: {task.Name}",
                   $"Эта программа сама проверяет обновления в фоне (задача в папке приложения). Это нормально и не " +
                   $"опасно. Но если хочешь меньше фоновых процессов — можешь отключить.{disableHint} Программа " +
                   "продолжит работать, просто перестанет обновляться автоматически.");

        return new Finding
        {
            Id = $"suspicious-task-{(task.Path.Length > 0 ? task.Path : task.Name)}",
            Group = ScanGroup.Threats,
            Severity = severity,
            Title = title,
            Detail = Truncate(task.Action, 200),
            Explain = explain,
            Data = data,
        };
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";
}
