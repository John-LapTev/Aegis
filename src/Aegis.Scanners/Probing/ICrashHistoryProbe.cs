namespace Aegis.Scanners.Probing;

/// <summary>Недавние синие экраны (BSOD) — по дампам сбоев Windows. Только читает.</summary>
public interface ICrashHistoryProbe
{
    /// <summary>Сколько синих экранов случилось за последние 7 дней (0 — их не было).</summary>
    Task<int> RecentCrashCountAsync(CancellationToken cancellationToken = default);
}
