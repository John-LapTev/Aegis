namespace Aegis.Scanners.Probing;

/// <summary>Одна проблема в реестре (read-only снимок для анализа).</summary>
public sealed record RegistryIssue
{
    /// <summary>Куст реестра ("HKLM"/"HKCU") — для обратимого удаления записи.</summary>
    public string Hive { get; init; } = "HKLM";

    /// <summary>Полный путь ключа/значения (служит и различителем для устойчивого Id).</summary>
    public required string Path { get; init; }

    /// <summary>Тип проблемы.</summary>
    public required RegistryIssueKind Kind { get; init; }

    /// <summary>На что ссылается запись (например, отсутствующий файл), если применимо.</summary>
    public string? Reference { get; init; }
}
