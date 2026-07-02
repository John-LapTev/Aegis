namespace Aegis.Core.Models;

/// <summary>Прогресс применения пакета исправлений (для индикатора в UI).</summary>
public sealed record FixProgress
{
    /// <summary>Находка, которую чиним прямо сейчас.</summary>
    public required string FindingId { get; init; }

    /// <summary>Сколько исправлений уже применено.</summary>
    public required int Completed { get; init; }

    /// <summary>Всего исправлений в пакете.</summary>
    public required int Total { get; init; }
}
