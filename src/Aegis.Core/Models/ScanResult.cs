namespace Aegis.Core.Models;

/// <summary>Результат работы одного сканера: группа и найденные пункты.</summary>
public sealed record ScanResult
{
    /// <summary>Группа, которую сканировали.</summary>
    public required ScanGroup Group { get; init; }

    /// <summary>Найденные пункты (может быть пусто — значит проблем не найдено).</summary>
    public required IReadOnlyList<Finding> Findings { get; init; }

    /// <summary>Пустой результат для группы (проблем не найдено).</summary>
    public static ScanResult Empty(ScanGroup group) =>
        new() { Group = group, Findings = [] };
}
