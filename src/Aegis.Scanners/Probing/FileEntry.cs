namespace Aegis.Scanners.Probing;

/// <summary>Файл-кандидат для поиска больших/дублирующихся файлов (read-only).</summary>
public sealed record FileEntry
{
    /// <summary>Путь к файлу.</summary>
    public required string Path { get; init; }

    /// <summary>Размер в байтах.</summary>
    public required long SizeBytes { get; init; }

    /// <summary>Хэш содержимого (для поиска одинаковых файлов). Пустой — если не считался.</summary>
    public required string ContentHash { get; init; }
}
