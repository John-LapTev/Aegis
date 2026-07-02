namespace Aegis.Scanners.Probing;

/// <summary>Находит загруженные драйверы, совпавшие по хэшу с базой опасных драйверов (LOLDrivers).</summary>
public interface IDangerousDriverProbe
{
    Task<IReadOnlyList<DangerousDriver>> FindAsync(CancellationToken cancellationToken = default);
}
