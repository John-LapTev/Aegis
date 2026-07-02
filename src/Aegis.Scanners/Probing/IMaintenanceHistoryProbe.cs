namespace Aegis.Scanners.Probing;

/// <summary>Когда последний раз запускали инструмент обслуживания (SFC/DISM, сброс сети) — для пометки «запускали недавно».</summary>
public interface IMaintenanceHistoryProbe
{
    /// <summary>Дата последнего запуска инструмента (<c>sfc-dism</c> / <c>network-reset</c>) или null, если не запускали.</summary>
    DateTimeOffset? GetLastRun(string toolKey);
}
