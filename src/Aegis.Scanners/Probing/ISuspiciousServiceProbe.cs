namespace Aegis.Scanners.Probing;

/// <summary>Находит службы Windows, запускающиеся из подозрительных мест (Temp/AppData/папки пользователя).</summary>
public interface ISuspiciousServiceProbe
{
    Task<IReadOnlyList<SuspiciousService>> FindAsync(CancellationToken cancellationToken = default);
}
