namespace Aegis.Scanners.Probing;

/// <summary>
/// Поставщик «залежавшегося»: битые ярлыки (цель удалена), пустые файлы и давно не тронутые загрузки.
/// Только читает. Классификация/тексты — в <see cref="Programs.StaleFileScanner"/>.
/// </summary>
public interface IStaleFileProbe
{
    Task<StaleFileSnapshot> ReadAsync(CancellationToken cancellationToken = default);
}
