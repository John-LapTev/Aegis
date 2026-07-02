namespace Aegis.Scanners.Probing;

/// <summary>Чтение заполненности дисков и крупнейших папок (анализатор места на диске). Только читает.</summary>
public interface IDiskUsageProbe
{
    Task<DiskUsageSnapshot> ReadAsync(CancellationToken cancellationToken = default);
}
