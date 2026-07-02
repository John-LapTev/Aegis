using Aegis.Scanners.Probing;
using Aegis.System.Internal;

namespace Aegis.System.Probes;

/// <summary>Читает дату последнего запуска инструмента обслуживания из <see cref="MaintenanceHistory"/>.</summary>
public sealed class MaintenanceHistoryProbe : IMaintenanceHistoryProbe
{
    public DateTimeOffset? GetLastRun(string toolKey) => MaintenanceHistory.GetLastRun(toolKey);
}
