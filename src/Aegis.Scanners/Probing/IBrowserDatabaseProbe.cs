namespace Aegis.Scanners.Probing;

/// <summary>
/// Поиск «раздутых» баз браузеров — файлов, внутри которых после удаления записей осталось много пустого
/// места. Только читает. Реализация Windows-специфична.
/// </summary>
public interface IBrowserDatabaseProbe
{
    /// <summary>
    /// Базы, которые имеет смысл сжать. Базы запущенных браузеров НЕ возвращаются: сжимать их нельзя.
    /// </summary>
    Task<IReadOnlyList<BloatedDatabase>> FindAsync(CancellationToken cancellationToken = default);
}

/// <summary>База браузера, в которой есть заметное количество пустого места.</summary>
public sealed record BloatedDatabase
{
    /// <summary>Полный путь к файлу базы.</summary>
    public required string Path { get; init; }

    /// <summary>Название браузера (для показа человеку).</summary>
    public required string Browser { get; init; }

    /// <summary>Текущий размер файла, байт.</summary>
    public required long SizeBytes { get; init; }

    /// <summary>Сколько примерно освободится после сжатия, байт.</summary>
    public required long ReclaimableBytes { get; init; }
}
