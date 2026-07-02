namespace Aegis.Scanners.Probing;

/// <summary>Один найденный мусорный объект (папка/набор файлов) с размером и категорией.</summary>
public sealed record JunkCandidate
{
    /// <summary>Путь к папке/файлу.</summary>
    public required string Path { get; init; }

    /// <summary>Сколько занимает в байтах.</summary>
    public required long SizeBytes { get; init; }

    /// <summary>Категория мусора.</summary>
    public required JunkCategory Category { get; init; }
}
