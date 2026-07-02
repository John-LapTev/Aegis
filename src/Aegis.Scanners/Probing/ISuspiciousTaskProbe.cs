namespace Aegis.Scanners.Probing;

/// <summary>Находит задачи планировщика с подозрительной командой (кандидаты — классифицирует сканер).</summary>
public interface ISuspiciousTaskProbe
{
    Task<IReadOnlyList<SuspiciousTask>> FindAsync(CancellationToken cancellationToken = default);
}
