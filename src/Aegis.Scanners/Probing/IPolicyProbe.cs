namespace Aegis.Scanners.Probing;

/// <summary>
/// Чтение системных ограничений Windows (веток <c>Policies</c>) — тех, что мешают человеку и обычно остаются
/// после чужих «оптимизаторов» и активаторов. Только читает. Реализация Windows-специфична.
/// </summary>
public interface IPolicyProbe
{
    /// <summary>Найденные ограничения (пусто — система чистая).</summary>
    Task<IReadOnlyList<PolicyRestriction>> ReadAsync(CancellationToken cancellationToken = default);
}

/// <summary>Обнаруженное ограничение Windows: координаты значения и его текущее состояние.</summary>
public sealed record PolicyRestriction
{
    public required string Hive { get; init; }
    public required string SubKey { get; init; }
    public required string ValueName { get; init; }

    /// <summary>Текущее значение (то, что делает ограничение активным).</summary>
    public required int Value { get; init; }
}
