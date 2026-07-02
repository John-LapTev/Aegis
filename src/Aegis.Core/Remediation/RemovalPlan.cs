namespace Aegis.Core.Remediation;

/// <summary>Упорядоченный план удаления вредоноса.</summary>
public sealed record RemovalPlan
{
    /// <summary>Шаги по порядку выполнения.</summary>
    public required IReadOnlyList<RemovalStep> Steps { get; init; }

    /// <summary>Требуется ли перезагрузка для завершения удаления.</summary>
    public required bool RequiresReboot { get; init; }
}
