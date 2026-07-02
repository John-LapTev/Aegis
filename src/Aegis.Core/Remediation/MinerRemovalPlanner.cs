namespace Aegis.Core.Remediation;

/// <summary>
/// Строит безопасный план удаления вредоноса (майнера) по обстоятельствам. Реализует стратегию из
/// docs/security/miner-removal.md: удалить сразу, если можно; иначе снять автозапуск и довести удаление
/// после перезагрузки (когда процессов уже нет). Никогда не убивает критический системный процесс.
/// Чистая логика — без обращения к системе (Windows-исполнение шагов — отдельный слой).
/// </summary>
public static class MinerRemovalPlanner
{
    public static RemovalPlan Plan(MinerRemovalContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Бэкап всегда первым — обратимость (ADR 0002/0004).
        var steps = new List<RemovalStep> { RemovalStep.Backup };

        if (context.TiedToCriticalProcess)
        {
            // Не трогаем критический процесс: глушим автозапуск и доводим удаление после ребута.
            steps.Add(RemovalStep.DisableAutostart);
            steps.Add(RemovalStep.ScheduleDeleteOnReboot);
            steps.Add(RemovalStep.RequestReboot);
            return new RemovalPlan { Steps = steps, RequiresReboot = true };
        }

        steps.Add(RemovalStep.StopProcesses);
        steps.Add(RemovalStep.DisableAutostart);

        if (context.FilesLocked)
        {
            // Файл заперт — удаляем при следующей загрузке, когда процессов уже нет.
            steps.Add(RemovalStep.ScheduleDeleteOnReboot);
            steps.Add(RemovalStep.RequestReboot);
            return new RemovalPlan { Steps = steps, RequiresReboot = true };
        }

        // Чистый случай: останавливаем, снимаем автозапуск, файлы — в карантин.
        steps.Add(RemovalStep.QuarantineFiles);
        return new RemovalPlan { Steps = steps, RequiresReboot = false };
    }
}
