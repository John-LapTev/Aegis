namespace Aegis.Scanners.Probing;

/// <summary>Чтение температур процессора и видеокарты (best-effort: железо может не отдавать данные).</summary>
public interface ITemperatureProbe
{
    /// <summary>Доступные показания температур. Пустой список — датчики недоступны.</summary>
    Task<IReadOnlyList<TemperatureReading>> ReadAsync(CancellationToken cancellationToken = default);
}
